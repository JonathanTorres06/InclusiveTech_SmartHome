using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Controlador del HUD principal de la casa (UI Toolkit).
///
/// Responsabilidades (solo presentacion, nada de logica de negocio):
///  - Reloj 12 h (a.m./p.m.) con segundos y fecha en espanol.
///  - Estado visual del asistente (idle / escuchando / pensando / hablando).
///  - Tarjeta de la habitacion actual.
///  - Boton "Cerrar sesion" (delega en LoginFlowManager.HandleLogout()).
///
/// Patrones: Observer (se suscribe a eventos de audio/orquestador) y
/// Mediator (AssistantOrchestrator coordina; este script solo "pinta").
/// 
/// Analogia: Es el tablero de un auto; muestra lo que pasa en el motor
/// pero no lo controla.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class HudCasaController : MonoBehaviour
{
    private enum AssistantState { Idle, Listening, Thinking, Speaking }

    [Header("Referencias Principales")]
    [Tooltip("Se resuelven solas si se dejan vacias")]
    [SerializeField] private AudioRecorder audioRecorder;
    [SerializeField] private AudioPlayer audioPlayer;
    [SerializeField] private AssistantOrchestrator orchestrator;
    [SerializeField] private LoginFlowManager loginFlowManager;
    [SerializeField] private RoomManager roomManager;

    [Header("Ajustes")]
    [SerializeField] private string assistantName = "Mía";
    [SerializeField] private int waveBarCount = 9;
    [SerializeField] private float roomPollSeconds = 0.5f;

    private static readonly CultureInfo SpanishCulture = new CultureInfo("es-CO");

    private const string STATE_PREFIX = "estado_";
    private const string PULSE_CLASS = "pulso_alto";
    private const string ROOM_CHANGING_CLASS = "habitacion_cambiando";

    [Header("Entrada Animada")]
    [SerializeField] private int entranceDelayMs = 400;
    [SerializeField] private int glowsDelayMs = 900; // espera extra tras la entrada de paneles para los glows de los iconos
    private const string READY_CLASS = "hud_listo";
    private const string GLOWS_CLASS = "hud_glows";

    // Informar posibles errores del asistente desde UI
    private const string ERROR_CLASS = "hud_error";
    private const int ERROR_DURATION_MS = 6000;

    private Label _errorLabel;
    private IVisualElementScheduledItem _errorJob;

    // Elementos UI
    private VisualElement _root;
    private Label _dateLabel, _timeLabel, _stateLabel, _roomLabel;
    private Button _logoutButton;
    private VisualElement _wave, _dots;
    private readonly List<VisualElement> _bars = new List<VisualElement>();
    private readonly List<VisualElement> _dotElements = new List<VisualElement>();
    private const string DATE_PANEL_COLLAPSED = "panel_colapsado";
    private const string TOGGLE_ACTIVE = "toggle_activo";
    private const string PREF_DATE_VISIBLE = "hud_fecha_hora_visible";
    private VisualElement _dateTimePanel;
    private Button _dateToggleButton;

    // Estado
    private AssistantState _state = AssistantState.Idle;
    private string _currentRoom;
    private GraphAgent _graphAgent;
    private bool _pulseHigh;
    private IVisualElementScheduledItem _clockJob, _pulseJob, _waveJob, _roomJob;

    /// <summary>
    /// Variables para manejar con mas precision y exactitud los glows del icono del asistente
    /// dependiendo del estado en el que se encuentra el asistente.
    /// </summary>
    private IVisualElementScheduledItem _stateJob; // Estado del asistente (grabando, procesando, reproduciendo). Se calcula cada 100 ms 

    // Antes de volver a "reposo" se exige verlo en 2 lecturas seguidas (~200 ms).
    // Asi no parpadea a reposo en el instante entre "procesando" y "hablando"
    private const int IDLE_CONFIRM_TICKS = 2;
    private int _idleTicks;

    #region Ciclo de Vida

    private void OnEnable()
    {
        _root = GetComponent<UIDocument>().rootVisualElement;
        QueryElements();
        BuildWaveBars();

        _logoutButton.clicked += OnLogoutClicked;
        _dateToggleButton.clicked += ToggleDateTimePanel;
        SetDateTimePanelVisible(PlayerPrefs.GetInt(PREF_DATE_VISIBLE, 0) == 1);

        ResolveReferences();
        _stateJob = _root.schedule.Execute(SyncState).Every(100);

        if (orchestrator != null) { orchestrator.OnAssistantError += ShowError; }

        StartClock();
        StartPulse();
        StartWave();
        StartRoomPolling();

        SetState(AssistantState.Idle);

        var container = _root.Q("Contenedor");
        container.RemoveFromClassList(READY_CLASS);
        container.RemoveFromClassList(GLOWS_CLASS);
        container.schedule.Execute(() => container.AddToClassList(READY_CLASS)).StartingIn(entranceDelayMs);
        container.schedule.Execute(() => container.AddToClassList(GLOWS_CLASS)).StartingIn(entranceDelayMs + glowsDelayMs);
    }

    private void OnDisable()
    {
        if (_logoutButton != null) { _logoutButton.clicked -= OnLogoutClicked; }
        if (orchestrator != null) { orchestrator.OnAssistantError -= ShowError; }
        _stateJob?.Pause();
        _clockJob?.Pause();
        _pulseJob?.Pause();
        _waveJob?.Pause();
        _roomJob?.Pause();
        _errorJob?.Pause();
    }

    #endregion

    #region Inicializacion

    private void QueryElements()
    {
        _dateLabel = _root.Q<Label>("Fecha_Valor");
        _timeLabel = _root.Q<Label>("Hora_Valor");
        _stateLabel = _root.Q<Label>("Asistente_Estado");
        _roomLabel = _root.Q<Label>("Habitacion_Nombre");
        _logoutButton = _root.Q<Button>("Btn_CerrarSesion");
        _wave = _root.Q<VisualElement>("Asistente_Onda");
        _dots = _root.Q<VisualElement>("Habitacion_Puntos");
        _dateTimePanel = _root.Q<VisualElement>("Panel_FechaHora");
        _dateToggleButton = _root.Q<Button>("Btn_FechaHora");
        _errorLabel = _root.Q<Label>("Asistente_Error");
    }

    private void ResolveReferences()
    {
        if (audioRecorder == null) audioRecorder = FindFirstObjectByType<AudioRecorder>();
        if (audioPlayer == null) audioPlayer = FindFirstObjectByType<AudioPlayer>();
        if (orchestrator == null) orchestrator = FindFirstObjectByType<AssistantOrchestrator>();
        if (loginFlowManager == null) loginFlowManager = FindFirstObjectByType<LoginFlowManager>();
    }

    private void BuildWaveBars()
    {
        _wave.Clear();
        _bars.Clear();
        for (int i = 0; i < waveBarCount; i++)
        {
            var bar = new VisualElement();
            bar.AddToClassList("onda_barra");
            _wave.Add(bar);
            _bars.Add(bar);
        }
    }

    #endregion

    #region Activacion del panel de hora y fecha

    private void ToggleDateTimePanel()
    {
        bool willBeVisible = _dateTimePanel.ClassListContains(DATE_PANEL_COLLAPSED);
        SetDateTimePanelVisible(willBeVisible);
        PlayerPrefs.SetInt(PREF_DATE_VISIBLE, willBeVisible ? 1 : 0);
    }

    private void SetDateTimePanelVisible(bool visible)
    {
        _dateTimePanel.EnableInClassList(DATE_PANEL_COLLAPSED, !visible);
        _dateToggleButton.EnableInClassList(TOGGLE_ACTIVE, visible);
    }

    #endregion

    #region Reloj

    private void StartClock()
    {
        UpdateClock();
        _clockJob = _root.schedule.Execute(UpdateClock).Every(250);
    }

    private void UpdateClock()
    {
        DateTime now = DateTime.Now;
        // "15 de agosto, 2026"
        _dateLabel.text = now.ToString("d 'de' MMMM, yyyy", SpanishCulture);
        // "7:00:05 p.m."
        string suffix = now.Hour < 12 ? "a.m." : "p.m.";
        int hour12 = now.Hour % 12 == 0 ? 12 : now.Hour % 12;
        _timeLabel.text = $"{hour12}:{now:mm}:{now:ss} {suffix}";
    }

    #endregion

    #region Estado del asistente

    private void SetState(AssistantState newState)
    {
        if (newState != AssistantState.Idle) { ClearError(); }

        _state = newState;

        //_root.Q("Contenedor").RemoveFromClassList(STATE_PREFIX + "idle");
        foreach (var s in new[] { "idle", "escuchando", "pensando", "hablando" })
            _root.Q("Contenedor").RemoveFromClassList(STATE_PREFIX + s);

        string cls;
        switch (newState)
        {
            case AssistantState.Listening: cls = "escuchando"; _stateLabel.text = "Te escucho..."; break;
            case AssistantState.Thinking: cls = "pensando"; _stateLabel.text = "Pensando..."; break;
            case AssistantState.Speaking: cls = "hablando"; _stateLabel.text = $"{assistantName} está hablando..."; break;
            default: cls = "idle"; _stateLabel.text = $"{assistantName} está lista"; break;
        }
        _root.Q("Contenedor").AddToClassList(STATE_PREFIX + cls);
    }

    /// <summary>
    /// Lee el estado real del asistente. Prioridad: grabando, procesando, hablando.
    /// </summary>
    private AssistantState DeriveState()
    {
        if (audioRecorder != null && audioRecorder.IsRecording) { return AssistantState.Listening; }
        if (orchestrator != null && orchestrator.IsProcessingCommand) { return AssistantState.Thinking; }
        if (audioPlayer != null && audioPlayer.IsAssistantSpeaking) { return AssistantState.Speaking; }
        return AssistantState.Idle;
    }

    private void SyncState()
    {
        AssistantState derived = DeriveState();

        // Evita parpadeos a reposo en el instante entre "procesando" y "hablando"
        if (derived == AssistantState.Idle)
        {
            if (++_idleTicks < IDLE_CONFIRM_TICKS) { return; }
        }
        else
        {
            _idleTicks = 0;
        }

        if (derived != _state) { SetState(derived); }
    }

    private void StartPulse()
    {
        // Alterna la clase; las transiciones USS suavizan el "latido" del glow.
        _pulseJob = _root.schedule.Execute(() =>
        {
            _pulseHigh = !_pulseHigh;
            var container = _root.Q("Contenedor");
            bool active = _state != AssistantState.Idle;
            if (_pulseHigh && active) container.AddToClassList(PULSE_CLASS);
            else container.RemoveFromClassList(PULSE_CLASS);
        }).Every(600);
    }

    private void StartWave()
    {
        _waveJob = _root.schedule.Execute(() =>
        {
            for (int i = 0; i < _bars.Count; i++)
            {
                float h;
                switch (_state)
                {
                    case AssistantState.Speaking: h = UnityEngine.Random.Range(8f, 34f); break;
                    case AssistantState.Listening: h = UnityEngine.Random.Range(8f, 22f); break;
                    case AssistantState.Thinking: h = 8f + 10f * Mathf.Abs(Mathf.Sin(Time.time * 3f + i * 0.6f)); break;
                    default: h = 8f; break;
                }
                _bars[i].style.height = h;
            }
        }).Every(110);
    }

    #endregion

    #region Errores

    // Muestra el error segun su tipo. El texto vive aqui porque es presentacion.
    public void ShowError(AssistantErrorType errorType)
    {
        ShowError(AssistantErrorMessages.GetText(errorType));
    }

    // Muestra un error en la burbuja de Mia y lo borra al pasar unos segundos.
    public void ShowError(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        _errorLabel.text = message;
        _root.Q("Contenedor").AddToClassList(ERROR_CLASS);

        _errorJob?.Pause();
        _errorJob = _root.schedule.Execute(ClearError).StartingIn(ERROR_DURATION_MS);
    }

    private void ClearError()
    {
        _errorJob?.Pause();
        _root.Q("Contenedor").RemoveFromClassList(ERROR_CLASS);
    }

    // Atajo temporal para probar el aspecto sin servidor. Clic derecho en el componente.
    [ContextMenu("Probar error")]
    private void TestError() => ShowError("No te alcancé a escuchar, repítelo");

    #endregion

    #region Habitacion

    private void StartRoomPolling()
    {
        // Polling barato (2 veces/seg) para no acoplarse al sistema de navegacion.
        // Mejora futura: que GraphAgent exponga un evento OnRoomChanged.
        UpdateRoom();
        _roomJob = _root.schedule.Execute(UpdateRoom).Every((long)(roomPollSeconds * 1000));
    }

    private void UpdateRoom()
    {
        string internalName = GetCurrentInternalRoom();
        if (string.IsNullOrEmpty(internalName) || internalName == _currentRoom) return;
        _currentRoom = internalName;

        _roomLabel.AddToClassList(ROOM_CHANGING_CLASS);
        _roomLabel.schedule.Execute(() =>
        {
            _roomLabel.text = ResolveDisplayName(internalName);
            _roomLabel.RemoveFromClassList(ROOM_CHANGING_CLASS);
        }).StartingIn(250);

        RefreshDots();
    }

    /// <summary>
    /// Punto unico de acoplamiento con navegacion: ajusta aqui si cambia la API.
    /// Solo devuelve nodos tipo Room. Los Waypoint son puntos de paso y se ignoran:
    /// la tarjeta conserva la ultima habitacion hasta llegar a la siguiente.
    /// </summary>
    private string GetCurrentInternalRoom()
    {
        if (_graphAgent == null) { _graphAgent = FindFirstObjectByType<GraphAgent>(); }

        NavigationNode node = _graphAgent != null ? _graphAgent.currentNode : null;
        return node != null && node.IsRoom ? node.InternalName : null;
    }

    /// <summary>Punto unico de acoplamiento con RoomManager. Ajusta si GetDisplayName es de instancia.</summary>
    private string ResolveDisplayName(string internalName)
    {
        string display = roomManager.GetDisplayName(internalName);
        return string.IsNullOrEmpty(display) ? internalName : display;
    }

    private void RefreshDots()
    {
        int total = Mathf.Max(1, roomManager.GetTotalRooms());
        _dots.Clear();
        _dotElements.Clear();
        int active = Mathf.Clamp(IndexOfRoom(_currentRoom), 0, total - 1);
        for (int i = 0; i < total; i++)
        {
            var dot = new VisualElement();
            dot.AddToClassList("punto");
            if (i == active) dot.AddToClassList("punto_activo");
            _dots.Add(dot);
            _dotElements.Add(dot);
        }
    }

    /// <summary>
    /// Posicion de la habitacion actual para los puntos de paginacion.
    /// PENDIENTE con el chat de navegacion: necesitamos una lista ORDENADA de
    /// habitaciones (internalNames). Mientras tanto devuelve 0.
    /// </summary>
    private int IndexOfRoom(string internalName) => 0;

    #endregion

    #region Cerrar sesion

    private void OnLogoutClicked()
    {
        if (loginFlowManager == null)
        {
            Debug.LogError("[HudCasaController] LoginFlowManager no encontrado.");
            return;
        }
        loginFlowManager.HandleLogout();
    }

    #endregion
}