using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Capa de presentacion (UI Toolkit) de la pantalla de login/registro.
/// No contiene logica de negocio: expone metodos para que SistemaBiometrico actualice lo
/// que se muestra y eventos para que reaccione a las interacciones del usuario.
///
/// Es una fachada: delega el texto de estado, el ambiente y la etiqueta de modo en vistas
/// pequeñas (LoginStatusView, LoginAtmosphereView, LoginModeView) y orquesta las
/// transiciones de entrada y de salida.
/// </summary>
public class UI_Login_Manager : MonoBehaviour
{
    private const string READY_CLASS = "login_listo";
    private const string LEAVING_CLASS = "login_saliendo";
    private const string GLOWS_CLASS = "login_glows";   // enciende los glows; se quita antes de salir

    [Header("Transiciones")]
    [Tooltip("Espera antes de lanzar la animacion de entrada, para que la escena ya este pintada.")]
    [SerializeField] private int entranceDelayMs = 350;
    [Tooltip("Espera extra, tras la entrada de los bloques, para encender los glows.")]
    [SerializeField] private int glowsDelayMs = 900;
    [Tooltip("Duracion de la animacion de salida (debe cubrir las transiciones USS .login_saliendo).")]
    [SerializeField] private int exitDurationMs = 900;

    [Header("Mensajes")]
    [Tooltip("Segundos que un mensaje importante (exito, aviso, error) se queda antes de que los mensajes de fondo lo reemplacen.")]
    [SerializeField] private float importantMessageHoldSeconds = 3f;

    [Header("Sonido (se busca solo si se deja vacio)")]
    [SerializeField] private LoginSfxPlayer sfx;

    UIDocument loginDocument;
    VisualElement container;

    // Reconocimiento facial
    Image facialRecognitionCamera;
    VisualElement faceTrackingFrame;

    // Botones
    Button loginButton;
    Button enterButton;
    Button registerButton;
    Button exitButton;
    Button quitButton;
    Button[] allButtons;

    // Vistas especializadas
    LoginStatusView statusView;
    LoginAtmosphereView atmosphereView;
    LoginModeView modeView;

    private bool isLeaving;

    /// <summary>
    /// Eventos que SistemaBiometrico escucha. Esta clase no sabe que logica de negocio
    /// dispara cada uno, solo que paso la interaccion.
    /// </summary>
    public event Action OnLoginRequested;
    public event Action OnEnterRequested;
    public event Action OnRegisterRequested;
    public event Action OnExitRequested;
    public event Action OnQuitRequested;

    private void OnEnable()
    {
        loginDocument = GetComponent<UIDocument>();
        VisualElement root = loginDocument.rootVisualElement;

        if (sfx == null) sfx = FindFirstObjectByType<LoginSfxPlayer>();

        container = root.Q<VisualElement>("Contenedor");

        facialRecognitionCamera = root.Q<Image>("Reconocimiento_Facial");
        faceTrackingFrame = root.Q<VisualElement>("Marco_Interior_RF");

        loginButton = root.Q<Button>("Btn_IniciarSesion");
        enterButton = root.Q<Button>("Btn_Entrar");
        registerButton = root.Q<Button>("Btn_Registrar");
        exitButton = root.Q<Button>("Btn_CerrarSesion");
        quitButton = root.Q<Button>("Btn_Salir");
        allButtons = new[] { loginButton, enterButton, registerButton, exitButton, quitButton };

        statusView = new LoginStatusView(root, importantMessageHoldSeconds);
        atmosphereView = new LoginAtmosphereView(root);
        modeView = new LoginModeView(root);

        loginButton.clicked += HandleLoginClicked;
        enterButton.clicked += HandleEnterClicked;
        registerButton.clicked += HandleRegisterClicked;
        exitButton.clicked += HandleExitClicked;
        quitButton.clicked += HandleQuitClicked;

        foreach (Button button in allButtons)
        {
            button.RegisterCallback<PointerEnterEvent>(OnButtonHovered);
        }

        isLeaving = false;
        PlayEntranceTransition();
    }

    private void OnDisable()
    {
        statusView?.Dispose();
        atmosphereView?.Dispose();
        container?.UnregisterCallback<GeometryChangedEvent>(OnFirstLayout);

        if (allButtons == null) return;

        loginButton.clicked -= HandleLoginClicked;
        enterButton.clicked -= HandleEnterClicked;
        registerButton.clicked -= HandleRegisterClicked;
        exitButton.clicked -= HandleExitClicked;
        quitButton.clicked -= HandleQuitClicked;

        foreach (Button button in allButtons)
        {
            button.UnregisterCallback<PointerEnterEvent>(OnButtonHovered);
        }
    }

    #region Interaccion con botones (se ignora mientras la pantalla se cierra)

    private void OnButtonHovered(PointerEnterEvent evt) => PlaySfx(LoginSfx.UiHover);

    private void HandleLoginClicked() => Dispatch(OnLoginRequested);
    private void HandleEnterClicked() => Dispatch(OnEnterRequested);
    private void HandleRegisterClicked() => Dispatch(OnRegisterRequested);
    private void HandleExitClicked() => Dispatch(OnExitRequested);
    private void HandleQuitClicked() => Dispatch(OnQuitRequested);

    private void Dispatch(Action handler)
    {
        if (isLeaving) return;

        PlaySfx(LoginSfx.UiClick);
        handler?.Invoke();
    }

    private void PlaySfx(LoginSfx effect)
    {
        if (sfx != null) sfx.Play(effect);
    }

    #endregion

    #region Transiciones

    private void PlayEntranceTransition()
    {
        // Todo arranca apagado: sin bloques visibles ni glows
        container.RemoveFromClassList(LEAVING_CLASS);
        container.RemoveFromClassList(READY_CLASS);
        container.RemoveFromClassList(GLOWS_CLASS);

        // UI Toolkit solo anima un cambio de clase si el estado inicial ya se resolvio y se dibujo.
        // Si la clase se agrega antes del primer layout, la pantalla nace directamente en su estado
        // final y no hay transicion. Por eso se espera al primer layout antes de programar la entrada.
        if (HasLayout(container))
        {
            ScheduleEntrance();
        }
        else
        {
            container.RegisterCallback<GeometryChangedEvent>(OnFirstLayout);
        }
    }

    private static bool HasLayout(VisualElement element)
    {
        return !float.IsNaN(element.layout.width) && element.layout.width > 0f;
    }

    private void OnFirstLayout(GeometryChangedEvent evt)
    {
        if (!HasLayout(container)) return;

        container.UnregisterCallback<GeometryChangedEvent>(OnFirstLayout);
        ScheduleEntrance();
    }

    private void ScheduleEntrance()
    {
        container.schedule.Execute(() =>
        {
            container.AddToClassList(READY_CLASS);
            PlaySfx(LoginSfx.ScreenIn);
        }).StartingIn(entranceDelayMs);

        // Los glows se encienden cuando los bloques ya llegaron, salvo que la pantalla ya se este cerrando
        container.schedule.Execute(() =>
        {
            if (!isLeaving) container.AddToClassList(GLOWS_CLASS);
        }).StartingIn(entranceDelayMs + glowsDelayMs);
    }

    /// <summary>
    /// Reproduce la animacion de salida (bloques que se retiran y fundido a negro) y, al
    /// terminar, ejecuta la accion indicada (cambiar de escena, cerrar la aplicacion).
    /// Solo se dispara una vez; los clics posteriores se ignoran.
    /// </summary>
    public void PlayExitTransition(Action onCompleted)
    {
        if (isLeaving) return;

        isLeaving = true;
        container.RemoveFromClassList(GLOWS_CLASS); // los glows se apagan primero
        container.RemoveFromClassList(READY_CLASS);
        container.AddToClassList(LEAVING_CLASS);
        container.schedule.Execute(() => onCompleted?.Invoke()).StartingIn(exitDurationMs);
    }

    #endregion

    #region Metodos publicos usados por SistemaBiometrico

    public float CameraWidth => facialRecognitionCamera.resolvedStyle.width;
    public float CameraHeight => facialRecognitionCamera.resolvedStyle.height;

    public void SetCameraTexture(Texture textura)
    {
        facialRecognitionCamera.image = textura;
    }

    /// <summary>
    /// Accion explicita: muestra el mensaje, cambia el ambiente y, si es un mensaje importante,
    /// lo mantiene unos segundos aunque el bucle de deteccion siga corriendo.
    /// </summary>
    /// <param name="animateText">false para contadores (ej. 7/20) que cambian muy seguido.</param>
    public void SetContext(LoginContext context, string text, bool animateText = true)
    {
        statusView.Show(context, text, animateText);
        atmosphereView.Apply(context, replay: true);
    }

    /// <summary>
    /// Mensaje de fondo (ej. "Mira la camara..."). No pisa un mensaje importante en su tiempo de lectura.
    /// </summary>
    public void SetAmbient(LoginContext context, string text)
    {
        if (statusView.IsHolding) return;

        statusView.Show(context, text, animateText: true);
        atmosphereView.Apply(context, replay: false);
    }

    /// <summary>
    /// Bloquea o libera los botones del menu y el icono de salir (ej. mientras se valida el rostro).
    /// </summary>
    public void SetInteractionLocked(bool locked) => atmosphereView.SetInteractionLocked(locked);

    public void SetMode(LoginMode mode) => modeView.SetMode(mode);

    public void SetRegistrationStep(RegistrationStep step) => modeView.SetStep(step);

    public void SetCaptureProgress(int current, int total) => statusView.SetProgress(current, total);

    public void HideCaptureProgress() => statusView.HideProgress();

    /// <summary>
    /// Posiciona y dimensiona el cuadro de seguimiento del rostro.
    /// Las coordenadas ya deben venir calculadas en el espacio de UI Toolkit.
    /// </summary>
    public void UpdateFaceTrackingFrame(Vector2 posicion, Vector2 tamanio, bool visible)
    {
        faceTrackingFrame.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        if (!visible)
        {
            return;
        }

        faceTrackingFrame.style.width = tamanio.x;
        faceTrackingFrame.style.height = tamanio.y;
        faceTrackingFrame.style.translate = new Translate(new Length(posicion.x), new Length(posicion.y));
    }

    public void SetButtonsVisibility(bool mostrarRegistrar, bool mostrarValidar, bool mostrarEntrar, bool mostrarCerrarSesion)
    {
        registerButton.style.display = mostrarRegistrar ? DisplayStyle.Flex : DisplayStyle.None;
        loginButton.style.display = mostrarValidar ? DisplayStyle.Flex : DisplayStyle.None;
        enterButton.style.display = mostrarEntrar ? DisplayStyle.Flex : DisplayStyle.None;
        exitButton.style.display = mostrarCerrarSesion ? DisplayStyle.Flex : DisplayStyle.None;
        // El icono "Salir de la aplicacion" siempre esta visible: no depende de la sesion
    }
    #endregion
}