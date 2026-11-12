/*
Responsabilidad: gestionar el ciclo de vida de los servicios externos
(procesos Python) que el proyecto necesita: sistema de navegacion,
asistente de voz Mia y detector de palabra de activacion.

Arranca los servicios que indica ServicesConfig al abrir la aplicacion,
espera a que cada uno responda y los apaga cuando la aplicacion se cierra.

Patron Observer via eventos de ciclo de vida de Unity y un evento propio
para avisar cuando todos los servicios quedaron listos.

Principio S de SOLID: unica responsabilidad, que es gestionar procesos
del sistema operativo. No conoce HTTP de negocio ni audio.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

/// <summary>
/// Gestiona el arranque, la vigilancia y el apagado de los servicios externos.
/// Debe existir en la primera escena (Login) como componente de un GameObject.
/// </summary>
public class ProcessManager : MonoBehaviour
{
    [Header("Configuracion")]
    [Tooltip("Perfil con las rutas y comandos de los servicios. Cambialo segun el computador.")]
    [SerializeField] private ServicesConfig servicesConfig;

    [Header("Opciones de inicio")]
    [Tooltip("Solo aplica dentro del editor. En un build los servicios siempre se arrancan.")]
    [SerializeField] private bool startProcessOnAwake = true;

    // Estado de cada servicio durante la ejecucion
    public enum ServiceRuntimeState { Starting, Ready, Failed }

    /// <summary>Resumen del estado de un servicio, para mostrarlo en pantalla.</summary>
    public readonly struct ServiceStatus
    {
        public readonly string Name;
        public readonly ServiceRuntimeState State;

        public ServiceStatus(string name, ServiceRuntimeState state)
        {
            Name = name;
            State = state;
        }
    }

    private class RunningService
    {
        public ExternalServiceDefinition Definition;
        public Process Process;

        // True si este manager lo arranco. Los que ya estaban corriendo
        // (por ejemplo levantados a mano) no se apagan al cerrar.
        public bool OwnedByManager;

        public ServiceRuntimeState State = ServiceRuntimeState.Starting;
        public float StartTime;

        // Resultado de la comprobacion previa "ya estaba corriendo"
        public bool PreCheckDone;
        public bool AlreadyUp;

        // True cuando el servicio imprimio su texto de listo (modo LogMarker).
        // Se escribe desde otro hilo, por eso es volatile.
        public volatile bool MarkerSeen;
    }

    private readonly List<RunningService> _services = new List<RunningService>();

    /// <summary>True cuando termino la etapa de arranque, hayan quedado bien o mal.</summary>
    public bool StartupFinished { get; private set; }

    /// <summary>Se dispara una vez, cuando todos los servicios quedaron listos.</summary>
    public event Action OnAllServicesReady;

    #region Singleton

    public static ProcessManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // No destruir el GameObject entre escenas para que no se reinicien los procesos
        DontDestroyOnLoad(gameObject);

        if (servicesConfig == null)
        {
            Debug.LogError("[ProcessManager] ServicesConfig no asignado. Asigna el ScriptableObject en el Inspector.");
            StartupFinished = true;
            return;
        }

        bool shouldStart = startProcessOnAwake;
#if !UNITY_EDITOR
        // En un build siempre se arrancan, aunque la casilla este apagada
        shouldStart = true;
#endif

        if (shouldStart)
        {
            StartCoroutine(StartAllServices());
        }
        else
        {
            Debug.Log("[ProcessManager] Arranque desactivado en el editor. Se asume que los servicios corren por separado.");
            StartupFinished = true;
        }
    }

    #endregion

    #region Arranque de servicios

    /// <summary>
    /// Arranca todos los servicios habilitados y espera a que respondan.
    /// Los procesos se lanzan uno tras otro, pero la espera es en paralelo,
    /// asi que el tiempo total es el del servicio mas lento.
    /// </summary>
    private IEnumerator StartAllServices()
    {
        Debug.Log("[ProcessManager] Iniciando servicios externos...");

        // 1. Comprobar en paralelo si algun servicio ya estaba corriendo
        foreach (ExternalServiceDefinition definition in servicesConfig.services)
        {
            if (definition == null || !definition.enabled) { continue; }

            var service = new RunningService { Definition = definition };
            _services.Add(service);
            StartCoroutine(PreCheckAlreadyUp(service));
        }

        yield return new WaitUntil(() => _services.TrueForAll(s => s.PreCheckDone));

        // 2. Arrancar todos los que no estaban corriendo, uno tras otro y sin esperas
        foreach (RunningService service in _services)
        {
            ExternalServiceDefinition definition = service.Definition;

            if (service.AlreadyUp)
            {
                service.State = ServiceRuntimeState.Ready;
                Debug.Log($"[ProcessManager] '{definition.serviceName}' ya estaba corriendo. Se reutiliza y no se apagara al cerrar.");
                continue;
            }

            service.StartTime = Time.realtimeSinceStartup;
            service.Process = StartProcess(service);

            if (service.Process == null)
            {
                service.State = ServiceRuntimeState.Failed;
                continue;
            }

            service.OwnedByManager = true;
        }

        // Esperar a que cada servicio arrancado responda
        bool pending = true;
        while (pending)
        {
            pending = false;

            foreach (RunningService service in _services)
            {
                if (service.State != ServiceRuntimeState.Starting) { continue; }

                ExternalServiceDefinition definition = service.Definition;
                float elapsed = Time.realtimeSinceStartup - service.StartTime;

                if (HasExited(service.Process))
                {
                    service.State = ServiceRuntimeState.Failed;
                    Debug.LogError($"[ProcessManager] '{definition.serviceName}' se cerro durante el arranque " +
                                   $"(codigo {SafeExitCode(service.Process)}). Revisa los mensajes anteriores del servicio.");
                    continue;
                }

                bool ready = false;

                if (definition.healthCheck == ServiceHealthCheck.ProcessAlive)
                {
                    ready = elapsed >= definition.processAliveSeconds;
                }
                else if (definition.healthCheck == ServiceHealthCheck.LogMarker)
                {
                    ready = service.MarkerSeen;
                }
                else
                {
                    yield return CheckHealthOnce(definition, ok => ready = ok);
                }

                if (ready)
                {
                    service.State = ServiceRuntimeState.Ready;
                    Debug.Log($"[ProcessManager] '{definition.serviceName}' listo en {elapsed:F1} s.");
                }
                else if (elapsed > definition.startupTimeoutSeconds)
                {
                    service.State = ServiceRuntimeState.Failed;
                    Debug.LogError($"[ProcessManager] '{definition.serviceName}' no respondio en {definition.startupTimeoutSeconds:F0} s.");
                }
                else
                {
                    pending = true;
                }
            }

            if (pending) { yield return new WaitForSeconds(0.5f); }
        }

        StartupFinished = true;

        if (AllServicesReady)
        {
            Debug.Log("[ProcessManager] Todos los servicios estan listos.");
            OnAllServicesReady?.Invoke();
        }
        else
        {
            Debug.LogWarning("[ProcessManager] El arranque termino con servicios que fallaron. Revisa los errores anteriores.");
        }
    }

    /// <summary>
    /// Inicia un servicio en segundo plano, sin ventana visible.
    /// Devuelve el proceso o null si fallo el arranque.
    /// </summary>
    private Process StartProcess(RunningService service)
    {
        ExternalServiceDefinition definition = service.Definition;
        string label = definition.serviceName;
        string marker = definition.healthCheck == ServiceHealthCheck.LogMarker ? definition.readyLogMarker : null;
        string executable = ResolvePath(definition.executablePath);
        string workingDirectory = ResolvePath(definition.workingDirectory);

        if (string.IsNullOrEmpty(executable) || string.IsNullOrEmpty(workingDirectory))
        {
            Debug.LogError($"[ProcessManager] Rutas no configuradas para '{label}'. Completalas en el ServicesConfig.");
            return null;
        }

        if (!File.Exists(executable))
        {
            Debug.LogError($"[ProcessManager] No se encontro el ejecutable de '{label}': {executable}");
            return null;
        }

        if (!Directory.Exists(workingDirectory))
        {
            Debug.LogError($"[ProcessManager] No se encontro la carpeta de '{label}': {workingDirectory}");
            return null;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = definition.arguments,
                WorkingDirectory = workingDirectory,

                // Sin ventana visible. El proceso corre en segundo plano
                CreateNoWindow = true,
                UseShellExecute = false,

                // Capturar la salida para verla en la consola de Unity y en Player.log
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            // Salida sin buffer y en UTF-8, para ver los logs al instante y evitar
            // errores con tildes como la de Mia cuando no hay consola.
            startInfo.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";
            startInfo.EnvironmentVariables["PYTHONUTF8"] = "1";

            var process = new Process { StartInfo = startInfo };

            process.OutputDataReceived += (sender, args) => HandleOutputLine(service, label, marker, args.Data);

            // Uvicorn y logging de Python escriben en stderr aunque sean mensajes normales
            process.ErrorDataReceived += (sender, args) => HandleOutputLine(service, label, marker, args.Data);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            Debug.Log($"[ProcessManager] '{label}' iniciado con PID {process.Id}.");
            return process;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ProcessManager] Error al iniciar '{label}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Reenvia una linea de salida del servicio a la consola de Unity y
    /// detecta el texto de "listo" cuando el servicio usa el modo LogMarker.
    /// </summary>
    private static void HandleOutputLine(RunningService service, string label, string marker, string line)
    {
        if (string.IsNullOrEmpty(line)) { return; }

        Debug.Log($"[{label}] {line}");

        if (!string.IsNullOrEmpty(marker) && line.Contains(marker))
        {
            service.MarkerSeen = true;
        }
    }

    /// <summary>
    /// Convierte una ruta del ServicesConfig en ruta completa. Las relativas
    /// se calculan desde la carpeta del ejecutable (o la raiz del proyecto en el editor).
    /// </summary>
    private static string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) { return string.Empty; }

        string cleaned = path.Trim().Trim('"');

        if (Path.IsPathRooted(cleaned)) { return Path.GetFullPath(cleaned); }

        string baseDirectory = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(baseDirectory, cleaned));
    }

    #endregion

    #region Comprobacion de salud

    /// <summary>
    /// Comprueba si el servicio ya esta corriendo antes de arrancarlo. Primero mira si el
    /// puerto esta abierto, algo instantaneo cuando esta cerrado, y solo si lo esta hace
    /// la comprobacion completa. Asi no se pierden segundos esperando un servicio que no existe.
    /// </summary>
    private IEnumerator PreCheckAlreadyUp(RunningService service)
    {
        ExternalServiceDefinition definition = service.Definition;
        bool up = false;

        if (TryGetEndpoint(definition, out string host, out int port))
        {
            Task<bool> probe = TcpPortOpenAsync(host, port, 500);
            yield return new WaitUntil(() => probe.IsCompleted);
            up = probe.Status == TaskStatus.RanToCompletion && probe.Result;

            if (up && definition.healthCheck == ServiceHealthCheck.Http)
            {
                bool healthy = false;
                yield return CheckHealthOnce(definition, ok => healthy = ok);
                up = healthy;
            }
        }

        service.AlreadyUp = up;
        service.PreCheckDone = true;
    }

    private static bool TryGetEndpoint(ExternalServiceDefinition definition, out string host, out int port)
    {
        host = null;
        port = 0;

        switch (definition.healthCheck)
        {
            case ServiceHealthCheck.Http:
                if (Uri.TryCreate(definition.healthUrl, UriKind.Absolute, out Uri uri))
                {
                    host = uri.Host;
                    port = uri.Port;
                    return true;
                }
                return false;

            case ServiceHealthCheck.Tcp:
            case ServiceHealthCheck.LogMarker:
                host = definition.tcpHost;
                port = definition.tcpPort;
                return !string.IsNullOrWhiteSpace(host) && port > 0;

            default:
                return false;
        }
    }

    /// <summary>
    /// Hace una sola comprobacion de salud y devuelve el resultado por callback.
    /// </summary>
    private IEnumerator CheckHealthOnce(ExternalServiceDefinition definition, Action<bool> onResult)
    {
        switch (definition.healthCheck)
        {
            case ServiceHealthCheck.Http:
                {
                    if (string.IsNullOrWhiteSpace(definition.healthUrl))
                    {
                        onResult(false);
                        yield break;
                    }

                    using (UnityWebRequest request = UnityWebRequest.Get(definition.healthUrl))
                    {
                        request.timeout = 2;
                        yield return request.SendWebRequest();
                        onResult(request.result == UnityWebRequest.Result.Success);
                    }
                    break;
                }

            case ServiceHealthCheck.Tcp:
                {
                    Task<bool> task = TcpPortOpenAsync(definition.tcpHost, definition.tcpPort, 800);
                    yield return new WaitUntil(() => task.IsCompleted);
                    onResult(task.Status == TaskStatus.RanToCompletion && task.Result);
                    break;
                }

            default:
                onResult(false);
                break;
        }
    }

    private static async Task<bool> TcpPortOpenAsync(string host, int port, int timeoutMs)
    {
        try
        {
            using (var client = new TcpClient())
            {
                Task connect = client.ConnectAsync(host, port);
                Task finished = await Task.WhenAny(connect, Task.Delay(timeoutMs));
                return finished == connect && client.Connected;
            }
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Apagado de procesos

    /// <summary>
    /// Apaga los servicios que arranco este manager al cerrar la aplicacion.
    /// </summary>
    private void OnApplicationQuit()
    {
        bool anyOwned = _services.Exists(s => s.OwnedByManager);
        if (!anyOwned) { return; }

        Debug.Log("[ProcessManager] Apagando servicios externos...");

        foreach (RunningService service in _services)
        {
            if (service.OwnedByManager)
            {
                KillProcessTree(service.Process, service.Definition.serviceName);
            }
        }

        Debug.Log("[ProcessManager] Servicios apagados.");
    }

    /// <summary>
    /// Termina el proceso y todos sus hijos. En Windows el python.exe de un venv
    /// suele lanzar al Python real como proceso hijo, y matar solo al padre
    /// dejaria al hijo vivo ocupando el puerto. Un proceso sin ventana no
    /// responde a CloseMainWindow, por eso el cierre es directo.
    /// </summary>
    private static void KillProcessTree(Process process, string label)
    {
        if (process == null) { return; }

        try
        {
            if (process.HasExited) { return; }

            int pid = process.Id;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var killInfo = new ProcessStartInfo("taskkill", $"/PID {pid} /T /F")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using (Process killer = Process.Start(killInfo))
            {
                killer?.WaitForExit(3000);
            }
#else
            process.Kill();
#endif

            process.WaitForExit(2000);
            Debug.Log($"[ProcessManager] '{label}' (PID {pid}) apagado.");
            process.Dispose();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ProcessManager] Error al apagar '{label}': {ex.Message}");
        }
    }

    #endregion

    #region Utilidades publicas

    /// <summary>True cuando termino el arranque y ningun servicio fallo.</summary>
    public bool AllServicesReady
    {
        get
        {
            if (!StartupFinished) { return false; }
            return !_services.Exists(s => s.State == ServiceRuntimeState.Failed);
        }
    }

    /// <summary>Copia en 'buffer' el estado actual de cada servicio configurado.</summary>
    public void GetServiceStatuses(List<ServiceStatus> buffer)
    {
        buffer.Clear();

        foreach (RunningService service in _services)
        {
            buffer.Add(new ServiceStatus(service.Definition.serviceName, service.State));
        }
    }

    /// <summary>True si el servicio existe y ya respondio.</summary>
    public bool IsServiceReady(string serviceName)
    {
        RunningService service = Find(serviceName);
        return service != null && service.State == ServiceRuntimeState.Ready;
    }

    /// <summary>
    /// Espera a que un servicio quede listo (o falle, o pase el tiempo maximo).
    /// Si el manager no esta arrancando ese servicio, termina de inmediato.
    /// Uso: yield return ProcessManager.Instance.WaitForService("it_nav_system");
    /// </summary>
    public IEnumerator WaitForService(string serviceName, float timeoutSeconds = 30f)
    {
        float start = Time.realtimeSinceStartup;

        while (Time.realtimeSinceStartup - start < timeoutSeconds)
        {
            RunningService service = Find(serviceName);

            if (service != null && service.State != ServiceRuntimeState.Starting) { yield break; }
            if (service == null && StartupFinished) { yield break; }

            yield return null;
        }

        Debug.LogWarning($"[ProcessManager] Se agoto la espera del servicio '{serviceName}'.");
    }

    /// <summary>Compatibilidad: true si it_ai_assistant fue arrancado y sigue vivo.</summary>
    public bool IsAiAssistantRunning() => IsProcessAlive("it_ai_assistant");

    /// <summary>Compatibilidad: true si it_wake_word fue arrancado y sigue vivo.</summary>
    public bool IsWakeWordRunning() => IsProcessAlive("it_wake_word");

    private bool IsProcessAlive(string serviceName)
    {
        RunningService service = Find(serviceName);
        return service != null && service.Process != null && !HasExited(service.Process);
    }

    private RunningService Find(string serviceName)
    {
        return _services.Find(s => s.Definition.serviceName == serviceName);
    }

    private static bool HasExited(Process process)
    {
        try { return process == null || process.HasExited; }
        catch { return true; }
    }

    private static string SafeExitCode(Process process)
    {
        try { return process.ExitCode.ToString(); }
        catch { return "desconocido"; }
    }

    #endregion
}