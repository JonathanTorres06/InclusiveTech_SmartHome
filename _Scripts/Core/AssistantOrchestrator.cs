/// <summary>
/// Coordina el flujo completo del asistente de voz: wake word ->
/// grabacion -> envio al servidor -> reproduccion de respuesta ->
/// ejecucion de accion sobre el hogar.
///
/// Responsabilidad: Mediar entre componentes independientes que
/// no se conocen entre si (WakeWordClient, AudioRecorder,
/// AssistantClient, AudioPlayer, HomeActionHandler). Ninguno de
/// esos componentes tiene referencia a los demas.
///
/// Patron Mediator: Centraliza la comunicacion entre
/// componentes independientes. Distinto de Facade (usado en
/// AssistantClient/AudioRecorder para esconder UNA API compleja),
/// aqui se coordinan VARIOS componentes ya simples entre si.
///
/// Principio S de SOLID unica responsabilidad: Orquestar la
/// secuencia del flujo. No graba audio, no hace HTTP, no ejecuta
/// acciones directamente sino que delega cada paso al componente correcto.
/// 
/// Principio D de SOLID: Depende de las abstracciones de cada
/// componente (sus metodos publicos), nunca de sus detalles
/// internos de implementacion.
/// </summary> 

using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Orquesta el flujo completo del asistente de voz Mia, desde
/// la deteccion del wake word hasta la ejecucion de la accion
/// del hogar resultante.
/// </summary>
public class AssistantOrchestrator : MonoBehaviour
{
    [Header("Configuracion del asistente")]
    [SerializeField] private AssistantConfig assistantConfig;

    [Header("Componentes del pipeline")]
    [SerializeField] private WakeWordClient wakeWordClient; // Wake word
    [SerializeField] private AudioRecorder audioRecorder; // Grabacion
    [SerializeField] private AssistantClient assistantClient; // Envio al servidor
    [SerializeField] private AudioPlayer audioPlayer; // Reproduccion de la respuesta
    [SerializeField] private HomeActionHandler homeActionHandler; // Ejecucion de accion en el hogar

    [Header("Referencias en la escena")]
    private GraphAgent _graphAgent; // Referencia al agente que se mueve por la casa

    // Evita que un nuevo wake word interrumpa un nuevo comando que 
    // todavia esta siendo procesado
    private bool _isProcessingCommand = false;

    //EVENTOS

    // Evento disparado cuando arranca el procesamiento de un comando
    public event Action OnCommandProcessingStarted;

    // Evento disparado cuando termina de procesarse un comando sea exitoso o sea un error
    public event Action OnCommandProcessingFinished;
    // Evento disparado cuando el pipeline falla. La UI decide como mostrarlo.
    public event Action<AssistantErrorType> OnAssistantError;


    // True mientras el orquestador esta procesando un comando de voz
    public bool IsProcessingCommand => _isProcessingCommand;

    // Constantes de palabras clave para comando de cerrar sesion (sin LLM)
    private static readonly string[] LOGOUT_VERBS = { "cerrar", "cierra", "cierre", "salir", "terminar", "finalizar" };

    // Se incluye "sección/seccion" porque Whisper a veces transcribe asi "sesión"
    private static readonly string[] SESSION_WORDS = { "sesión", "sesion", "sección", "seccion" };
    private static readonly string[] DISCONNECT_WORDS = { "desconectar", "desconectame", "desconéctame" };

    
    [Header("Flujo de login")]
    [SerializeField] private LoginFlowManager loginFlowManager;


    [Header("Diagnostico")]
    [Tooltip("Guarda un CSV con los tiempos de cada comando. Desactivar en la version final.")]
    [SerializeField] private bool logLatency = true;

    private LatencyLogWriter _latencyLog;

    private void OnEnable()
    {
        wakeWordClient.OnWakeWordDetected += HandleWakeWordDetected;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        assistantClient.OnError += RaiseError;

        // Por si el componente se habilita ya estando en la escena de la casa
        RefreshSceneDependentReferences();

        if (logLatency && _latencyLog == null)
        {
            _latencyLog = new LatencyLogWriter("unity_latency.csv");
            Debug.Log($"[AssistantOrchestrator] Log de latencia en: {_latencyLog.FilePath}");
        }
    }

    private void OnDisable()
    {
        wakeWordClient.OnWakeWordDetected -= HandleWakeWordDetected;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        assistantClient.OnError -= RaiseError;
    }

    #region Persistencia de AssistantOrchestrator entre escenas

    /// <summary>
    /// Se ejecuta cada vez que Unity termina de cargar una escena nueva.
    /// Vuelve a buscar las referencias que solo existen en la escena de
    /// la casa (GraphAgent), ya que este componente persiste desde la
    /// escena de login via DontDestroyOnLoad y su referencia original
    /// quedaria en null al cambiar de escena.
    /// </summary>
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) 
    {
        RefreshSceneDependentReferences();
    }

    private void RefreshSceneDependentReferences() 
    {
        _graphAgent = FindFirstObjectByType<GraphAgent>();

        if (_graphAgent == null)
        {
            Debug.Log("[AssistantOrchestrator] GraphAgent no encontrado en esta escena " +
                "(normal si estamos en la escena de login)");
        }
    }

    #endregion

    #region Punto de entrada del flujo - Reacciona a la deteccion

    /// <summary>
    /// Reacciona a la deteccion del wake word iniciando el
    /// procesamiento completo del comando. Ignora la deteccion
    /// si ya hay un comando en curso.
    /// </summary>
    private async void HandleWakeWordDetected() 
    {
        if (_isProcessingCommand)
        {
            Debug.LogWarning("[AssistantOrchestrator] Wake word detectado pero ya hay " +
                "un comando en proceso. Se ignorara esta deteccion.");
            return;
        }

        if (audioPlayer.IsPlaying)
        {
            Debug.Log("[AssistantOrchestrator] Wake word ignorado: Mia esta hablando.");
            return;
        }

        _isProcessingCommand = true;
        OnCommandProcessingStarted?.Invoke();

        try
        {
            var recordingWatch = System.Diagnostics.Stopwatch.StartNew();
            byte[] audioData = await audioRecorder.RecordCommandAsync();
            recordingWatch.Stop();

            if (audioData == null)
            {
                Debug.LogError("[AssistantOrchestrator] La grabacion fallo. " +
                    "Se cancela el procesamiento del comando.");
                return;
            }

            string activeScene = SceneManager.GetActiveScene().name;

            if (activeScene == assistantConfig.loginSceneName)
            {
                await HandleLoginSceneCommandAsync(audioData);
            }
            else 
            {
                await HandleHouseSceneCommandAsync(audioData, recordingWatch.ElapsedMilliseconds);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AssistantOrchestrator] Error inesperado en los pasos para enviar " +
                $"una peticion al servidor del asistente y recibir una respuesta de este: {ex.Message}");
            RaiseError(AssistantErrorType.Unknown);
        }
        finally
        {
            _isProcessingCommand = false;
            OnCommandProcessingFinished?.Invoke();
        }

        //await ProcessVoiceCommandAsync();
    }

    /// <summary>
    /// Maneja comandos de voz mientras el usuario esta en la escena de
    /// login. No usa el LLM, son opciones de menu fijas y discretas
    /// (registrarme / validar), no lenguaje natural que interpretar.
    /// </summary>
    private async Task HandleLoginSceneCommandAsync(byte[] audioData) 
    {
        string transcribed = await assistantClient.TranscribeAudioAsync(audioData);

        // null: Siginifica que el servidor fallo y el cliente ya aviso. No tiene sentido seguir
        if (transcribed == null) { return; }

        await loginFlowManager.HandleVoiceCommandAsync(transcribed);
    }

    /// <summary>
    /// Maneja comandos de voz en la escena de la casa. Primero verifica
    /// localmente si es "cerrar sesion" (sin LLM, es una resolucion inmediata).
    /// Si no lo es, reenvia el MISMO audio ya grabado al pipeline
    /// completo normal, sin volver a grabar.
    /// </summary>
    private async Task HandleHouseSceneCommandAsync(byte[] audioData, long recordingMs) 
    {
        var totalWatch = System.Diagnostics.Stopwatch.StartNew();
        var stageWatch = System.Diagnostics.Stopwatch.StartNew();

        string transcribed = await assistantClient.TranscribeAudioAsync(audioData);
        long precheckMs = stageWatch.ElapsedMilliseconds;

        if (transcribed == null) { return; }

        if (string.IsNullOrWhiteSpace(transcribed))
        {
            RaiseError(AssistantErrorType.EmptyTranscription);
            return;
        }

        if (IsLogoutCommand(transcribed))
        {
            if (loginFlowManager == null)
            {
                Debug.LogError("[AssistantOrchestrator] loginFlowManager sin asignar en el Inspector.");
                return;
            }

            loginFlowManager.HandleLogout();
            return;
        }

        // No fue logout. Continua el flujo normal ya existente
        string houseContextJson = BuildHouseContextJson();
        string requestId = Guid.NewGuid().ToString("N").Substring(0, 8);

        stageWatch.Restart();

        // Reenvia el texto
        AssistantResponse response = await assistantClient.SendTextCommandAsync(transcribed, houseContextJson, requestId);
        long processMs = stageWatch.ElapsedMilliseconds;

        if (response == null) 
        {
            WriteLatencyLog(requestId, recordingMs, precheckMs, processMs, totalWatch.ElapsedMilliseconds, 0f, "error");
            return; 
        } 

        HandleAssistantResponse(response);

        float clipSeconds = response.AudioClip != null ? response.AudioClip.length : 0f;
        WriteLatencyLog(requestId, recordingMs, precheckMs, processMs, totalWatch.ElapsedMilliseconds, clipSeconds, "ok");
    }

    // Reutiliza la misma logica de comparacion por palabra completa
    // que ya construimos en VoiceUserRegistrationHandler
    private bool ContainsKeyword(string text, string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        string normalized = text.ToLowerInvariant().Trim(' ', '.', ',', '!', '?');
        string[] words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (string word in words)
        {
            string cleanWord = word.Trim('.', ',', '!', '?');
            foreach (string keyword in keywords)
            {
                if (cleanWord == keyword) return true;
            }
        }
        return false;
    }

    #endregion

    #region Procesamiento del comando de cerrar sesion en la casa

    private bool IsLogoutCommand(string text)
    {
        return ContainsKeyword(text, DISCONNECT_WORDS) || (ContainsKeyword(text, LOGOUT_VERBS) && ContainsKeyword(text, SESSION_WORDS));
    }

    #endregion

    #region Flujo Principal

    /// <summary>
    /// Reproduce el audio de la respuesta y ejecuta la accion del
    /// hogar si la respuesta contiene una.
    /// </summary>
    /// <param name="response">Respuesta completa del servidor it_ai_assistant.</param>
    private void HandleAssistantResponse(AssistantResponse response) 
    {
        audioPlayer.Enqueue(response.AudioClip);

        if (response.HasAction)
        {
            homeActionHandler.HandleResponse(response);
        }
        else 
        {
            Debug.Log($"[AssistantOrchestrator] Intent '{response.Intent}' sin accion asociada.");
        }
    }

    private void RaiseError(AssistantErrorType errorType)
    {
        Debug.LogWarning($"[AssistantOrchestrator] Error del asistente: {errorType}");
        OnAssistantError?.Invoke(errorType);
    }

    #endregion

    #region Construccion del contexto del hogar

    /// <summary>
    /// Arma el JSON del estado actual del hogar que se envia al
    /// servidor junto con el audio del comando, para que el LLM
    /// tenga contexto real de la casa al momento de responder.
    /// </summary>
    /// <returns>JSON string con el HouseContext actual.</returns>
    private string BuildHouseContextJson() 
    {
        var payload = new HouseContextPayload
        {
            lights_on_count = HouseState.Instance.lightManager.GetLightsOnCount(),
            lights_total = HouseState.Instance.lightManager.GetTotalLightsCount(),
            temperature_celsius = HouseState.Instance.tempManager.temperaturaActual,
            current_room = GetCurrentRoomDisplayName(),
            available_rooms = HouseState.Instance.roomManager.GetAllSearchableNames(),
            rooms_total = HouseState.Instance.roomManager.GetTotalRooms(),
            room_display_names = HouseState.Instance.roomManager.GetAllDisplayNames(),
            user_name = GestorSesion.Instance?.UsuarioActual?.NombreUsuario,
        };

        return JsonUtility.ToJson(payload);
    }

    /// <summary>
    /// Obtiene el display_name de la habitacion donde se encuentra
    /// actualmente el agente, para incluirlo en el HouseContext.
    /// </summary>
    /// <returns>Nombre visible de la habitacion actual, o un valor por defecto si no hay nodo.</returns>
    private string GetCurrentRoomDisplayName() 
    {
        if (_graphAgent == null ||  _graphAgent.currentNode == null || HouseState.Instance == null)
        {
            return "Ruta Desconocida";
        }

        string internalName = _graphAgent.currentNode.InternalName;
        return HouseState.Instance.roomManager.GetDisplayName(internalName);
    }

    /// <summary>
    /// DTO interno para serializar el HouseContext a JSON con
    /// JsonUtility. Los nombres de campo coinciden exactamente
    /// con el modelo Pydantic HouseContext del servidor Python.
    /// </summary>
    [Serializable]
    private class HouseContextPayload 
    {
        public int lights_on_count;
        public int lights_total;
        public float temperature_celsius;
        public string current_room;
        public List<string> available_rooms;
        public int rooms_total;
        public List<string> room_display_names;
        public string user_name;
    }

    #endregion

    #region Logs de pruebas de funcionalidad

    private void WriteLatencyLog(string requestId, long recordingMs, long precheckMs,
    long processMs, long totalMs, float responseAudioSeconds, string status)
    {
        if (_latencyLog == null) return;

        _latencyLog.Append(requestId, SceneManager.GetActiveScene().name,
            recordingMs, precheckMs, processMs, totalMs, responseAudioSeconds, status);
    }

    #endregion
}
