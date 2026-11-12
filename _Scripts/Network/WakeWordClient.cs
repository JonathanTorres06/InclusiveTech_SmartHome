// =============================================================
// Cliente WebSocket que escucha al proceso it_wake_word.
//
// Responsabilidad: Mantener una conexion WebSocket persistente
// con el servidor de deteccion de wake word y notificar al resto
// de Unity mediante un evento cuando se detecta la palabra clave de
// activacion. No conoce audio, ni HTTP, ni logica de negocio.
//
// Patron Observer: Expone un evento al que otros scripts se
// suscriben sin que este cliente conozca quienes son ni que
// haran con la notificacion.
//
// Principio S de SOLID de unica responsabilidad: comunicacion
// WebSocket con it_wake_word.
//
// Principio D de SOLID: depende de AssistantConfig, no de
// valores de host/puerto hardcodeados.
// =============================================================

using System;
using System.Threading.Tasks;
using UnityEngine;
using NativeWebSocket;

/// <summary>
/// Cliente WebSocket para el proceso it_wake_word.
/// Dispara OnWakeWordDetected cuando la palabra de activacion
/// es detectada por el modelo de deteccion en Python.
/// </summary>
public class WakeWordClient : MonoBehaviour
{
    [Header("Configuracion")]
    [SerializeField] private AssistantConfig assistantConfig;

    [Header("Reconexion")]
    [Tooltip("Segundo de espera antes de reintentar la conexion")]
    [SerializeField] private float reconnectDelaySeconds = 3f;

    // Mensaje exacto que envia it_wake_word al detectar la palabra clave
    private const string WAKE_WORD_DETECTED_MESSAGE = "WAKE_WORD_DETECTED";

    private WebSocket _webSocket;
    private bool _isConnecting = false;
    private bool _shouldReconnect = false;
    private bool _reconnectedScheduled = false;

    // Evento disparado cuando it_wake_word detecta la palabra de activacion
    // Los scripts interesados (como AssistantOrchestrator) se suscriben aqui
    public event Action OnWakeWordDetected;

    // Evento disparado cuando la conexion con it_wake_word se establece
    public event Action OnConnected;

    // Evento disparado cuando la conexion con it_wake_word se pierde
    public event Action OnDisconnected;

    // True si la conexion WebSocket esta activa en este momento
    public bool IsConnected => _webSocket != null && _webSocket.State == WebSocketState.Open;

    private async void Start() 
    {
        // No conectar hasta que el ProcessManager haya terminado de arrancar
        // los servicios. Asi it_wake_word ya existe en el primer intento.
        if (ProcessManager.Instance != null)
        {
            float waited = 0f;
            
            while (!ProcessManager.Instance.StartupFinished && waited < 120f)
            {
                await Task.Delay(250);
                waited += 0.25f;
            }
        }

        await ConnectAsync();
    }

    private void Update()
    {
        // NativeWebSocket requiere DispatchMessageQueue en builds de WebGL,
        // pero es seguro llamarlo siempre para mantener compatibilidad
        // entre plataformas sin necesidad de condicionales de compilacion.
#if !UNITY_WEBGL || UNITY_EDITOR
        _webSocket?.DispatchMessageQueue();
#endif
    }

    #region Metodos para permitir conexion o desconexion al servidor

    /// <summary>
    /// Establece la conexion WebSocket con it_wake_word.
    /// Si la conexion falla, programa un reintento automatico.
    /// </summary>
    public async Task ConnectAsync() 
    {
        if (_isConnecting || IsConnected)
        {
            return;
        }

        _shouldReconnect = true; // habilita la reconexion automatica al servidor de la palabra clave
        _isConnecting = true;

        // URL del websocket
        string url = $"ws://{assistantConfig.wakeWordHost}:{assistantConfig.wakeWordPort}";

        Debug.Log("[WakeWordClient] Conectando a: " + url + "...");

        _webSocket = new WebSocket(url);

        _webSocket.OnOpen += HandleOpen;
        _webSocket.OnMessage += HandleMessage;
        _webSocket.OnError += HandleError;
        _webSocket.OnClose += HandleClose;

        await _webSocket.Connect();
    }

    /// <summary>
    /// Cierra la conexion WebSocket de forma ordenada.
    /// Detiene tambien los reintentos automaticos de reconexion.
    /// </summary>
    public async Task DisconnectAsync() 
    {
        _shouldReconnect = false;

        if (_webSocket != null && _webSocket.State == WebSocketState.Open)
        {
            await _webSocket.Close();
        }
    }

    #endregion

    #region Manejadores de eventos del WebSocket

    // =========================================================
    // Manejadores de eventos del WebSocket
    // =========================================================

    private void HandleOpen() 
    {
        _isConnecting = false;
        Debug.Log("[WakeWordClient] Conectado a it_wake_word server.");
        OnConnected?.Invoke();
    }

    private void HandleMessage(byte[] data) 
    {
        string message = System.Text.Encoding.UTF8.GetString(data);

        if (message == WAKE_WORD_DETECTED_MESSAGE)
        {
            Debug.Log("[WakeWordClient] Wake word detectado.");
            OnWakeWordDetected?.Invoke();
        }
        else 
        {
            Debug.Log($"[WakeWordClient] Mensaje no reconocido: '{message}'");
        }
    }

    private void HandleError(string errorMessage) 
    {
        Debug.LogWarning($"[WakeWordClient] Error de conexion: '{errorMessage}'");
        _isConnecting = false;
        ScheduledReconnectIfNeeded();
    }

    private async void HandleClose(WebSocketCloseCode closeCode) 
    {
        _isConnecting = false;
        Debug.LogWarning($"[WakeWordClient] Conexion cerrada: {closeCode}");
        OnDisconnected?.Invoke();
        ScheduledReconnectIfNeeded();
    }

    /// <summary>
    /// Programa un intento de reconexion, evitando duplicar el ciclo
    /// si OnError y OnClose se disparan casi al mismo tiempo para el
    /// mismo fallo de conexion (comportamiento observado al intentar
    /// conectar contra un servidor que nunca llego a estar disponible).
    /// </summary>
    private async void ScheduledReconnectIfNeeded()
    {
        if (!_shouldReconnect || _reconnectedScheduled)
        {
            return;
        }

        _reconnectedScheduled = true;
        await AttemptReconnectAsync();
        _reconnectedScheduled = false;
    }

    /// <summary>
    /// Espera el tiempo configurado y reintenta la conexion.
    /// Se ejecuta automaticamente tras cualquier desconexion
    /// inesperada, ya sea porque it_wake_word aun no arranco
    /// o porque el proceso se reinicio.
    /// </summary>
    private async Task AttemptReconnectAsync() 
    {
        Debug.Log("[WakeWordClient] Reintentando conexion en " + reconnectDelaySeconds + "s...");

        await Task.Delay(TimeSpan.FromSeconds(reconnectDelaySeconds));

        if (_shouldReconnect)
        {
            await ConnectAsync();
        }
    }

    private async void OnApplicationQuit()
    {
        await DisconnectAsync();
    }

    #endregion
}