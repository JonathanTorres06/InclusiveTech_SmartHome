// =============================================================
// Controla la secuencia de bienvenida en la escena de login y
// dirige al usuario hacia el registro (primera vez / admin) o
// la validacion facial (usuarios ya existentes). Tambien maneja
// el cierre de sesion desde la escena de la casa.
//
// Responsabilidad: Orquestar la conversacion de la pantalla de
// login. No conoce biometria en detalle ni el pipeline STT/LLM/TTS
// completo, eso delega en AssistantClient, AudioPlayer y el
// SistemaBiometrico ya resuelto dinamicamente.
// =============================================================

using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoginFlowManager : MonoBehaviour
{
    [Header("Componentes")]
    [SerializeField] private AssistantConfig assistantConfig;
    [SerializeField] private AssistantClient assistantClient;
    [SerializeField] private AudioPlayer audioPlayer;

    [Header("Audios pregrabados: Bienvenida inicial")]
    [Tooltip("Saludo amigable + contexto de la pantalla + nota de seguridad neutra")]
    [SerializeField] private AudioClip welcomeAndContextClip;

    [Tooltip("Indica que el proceso esta por comenzar")]
    [SerializeField] private AudioClip startingProcessClip;

    [Tooltip("Audio pregrabados - respuestas a comandos")]
    [SerializeField] private AudioClip registerAcceptedClip;
    [SerializeField] private AudioClip validateAcceptedClip;
    [SerializeField] private AudioClip enterAcceptedClip;
    [SerializeField] private AudioClip logoutClip;

    [Tooltip("Despedida al cerrar la aplicacion. Si se deja vacio se usa el audio de cierre de sesion.")]
    [SerializeField] private AudioClip quitClip;

    // No reproducir los audios de inicio despues de que el usuario ya haya pasado por el Login al menos una vez
    // Vive mientras la aplicacion esta abierta, aunque se recargue la escena del login
    private static bool s_welcomePlayer;

    // Por si en el Editor esta desactivado "Reload Domain", reinicia la bandera al darle Play
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetWelcomeFlag() => s_welcomePlayer = false;

    [Header("Mensajes de cierre de sesion en el UI")]
    [SerializeField]
    private string[] LogoutTips =
    {
        "Cerrando tu sesión...",
        "Asegurando tu hogar...",
        "Hasta pronto..."
    };

    private SistemaBiometrico _sistemaBiometrico;
    private UI_Login_Manager _loginUi;

    // Constantes de operacion en Login
    private static readonly string[] REGISTER_KEYWORDS = { "registrarme", "registro", "registrar", "registrame" };
    private static readonly string[] VALIDATE_KEYWORDS = { "validar", "validarme", "valídame", "iniciar", "validame", "inicia" };
    private static readonly string[] ENTER_KEYWORDS = { "entrar", "acceder", "ingresar", "ingresa" };
    // "Cerrar sesion" = LOGOUT. "Salir", "cerrar", "salir de la aplicacion", "cerrar mi casa" = cerrar la aplicacion.
    // Si el comando trae ademas la palabra "sesion", se interpreta como cerrar sesion.
    private static readonly string[] LOGOUT_KEYWORDS = { "desconectar", "desconéctame", "desconectame" };
    private static readonly string[] SESSION_KEYWORDS = { "sesión", "sesion" };
    private static readonly string[] QUIT_KEYWORDS = { "salir", "cerrar" };

    // Constantes del sistema biometrico
    private const int SISTEMA_BIOMETRICO_TIMEOUT_MS = 5000;
    private const int SISTEMA_BIOMETRICO_POLL_MS = 100;

    // Espera a que Mia termine un audio pregrabado (introduccion de la validacion, despedida)
    private const int ASSISTANT_POLL_MS = 100;
    private const int CLIP_END_MARGIN_MS = 300;    // holgura sobre la duracion del clip
    private const int CLIP_START_GRACE_MS = 1500;  // tiempo extra permitido si el AudioPlayer tarda en empezar a sonar
    private const int VALIDATION_INTRO_MAX_WAIT_MS = 8000;
    private const int FAREWELL_MAX_WAIT_MS = 4000;

    #region Singleton

    public static LoginFlowManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    #endregion

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryBindSistemaBiometrico();
        TryBindLoginUi();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnbindLoginUi();
    }


    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryBindSistemaBiometrico();
        TryBindLoginUi();
    }

    private void TryBindSistemaBiometrico()
    {
        _sistemaBiometrico = FindAnyObjectByType<SistemaBiometrico>();

        // Paso previo a validar el rostro: Mia explica lo que va a pasar (lo usan el boton y la voz)
        if (_sistemaBiometrico != null) { _sistemaBiometrico.PreValidacionAsync = PlayValidationIntroAsync; }
    }

    #region Botones de la UI de login

    /// <summary>
    /// Se suscribe a los botones de la pantalla de login para que Mia responda con el mismo audio
    /// pregrabado que cuando el comando se dice por voz. Los comandos de voz no pasan por estos
    /// eventos (llaman directo a SistemaBiometrico), asi que ningun audio suena dos veces.
    /// </summary>
    private void TryBindLoginUi()
    {
        UnbindLoginUi();

        _loginUi = FindAnyObjectByType<UI_Login_Manager>();
        if (_loginUi == null) return;

        _loginUi.OnRegisterRequested += HandleRegisterButton;
        _loginUi.OnEnterRequested += HandleEnterButton;
        _loginUi.OnExitRequested += HandleLogoutButton;
        _loginUi.OnQuitRequested += HandleQuitButton;
    }

    private void UnbindLoginUi()
    {
        if (_loginUi == null) return;

        _loginUi.OnRegisterRequested -= HandleRegisterButton;
        _loginUi.OnEnterRequested -= HandleEnterButton;
        _loginUi.OnExitRequested -= HandleLogoutButton;
        _loginUi.OnQuitRequested -= HandleQuitButton;
        _loginUi = null;
    }

    private void HandleRegisterButton() => PlayActionResponse(registerAcceptedClip);
    private void HandleEnterButton() => PlayActionResponse(enterAcceptedClip);
    private void HandleLogoutButton() => PlayActionResponse(logoutClip);
    private void HandleQuitButton() => PlayActionResponse(quitClip != null ? quitClip : logoutClip);

    #endregion

    /// <summary>
    /// Reproduce la secuencia de bienvenida al entrar a la escena de
    /// login. Debe llamarse una vez, al iniciar esa escena (por
    /// ejemplo desde el Start de un controlador de la UI de login).
    /// </summary>
    public async Task PlayWelcomeSequenceAsync()
    {
        // Ya se dio la bienvenida en esta sesion de la app (por ejemplo, al volver de cerrar sesion desde la casa)
        if (s_welcomePlayer)
        {
            Debug.Log("[LoginFlowManager] Se omite la bienvenida ya que esta ya sono en esta ejecucion.");
            return;
        }
        s_welcomePlayer = true;

        // Generacion al arrancar la bienvenida. Si el usuario interrumpe con un
        // boton o comando, la pregunta de Mia que llega tarde por HTTP ya no
        // correspondera a esta generacion y se descarta.
        int gen = audioPlayer.Generation;

        EnqueueIfAssigned(welcomeAndContextClip);
        EnqueueIfAssigned(startingProcessClip);

        if (!await WaitForSistemaBiometricoAsync())
        {
            Debug.LogError("[LoginFlowManager] SistemaBiometrico no estuvo listo a tiempo. Se omite la pregunta dinámica.");
            return;
        }

        // Mia toma la palabra y pregunta segun corresponda
        string question = BuildDynamicPrompt();
        AssistantResponse response = await assistantClient.GetSimpleSpeechAsync(question);
        if (response?.AudioClip != null) { audioPlayer.EnqueueIfCurrent(response.AudioClip, gen); }
    }

    private async Task<bool> WaitForSistemaBiometricoAsync()
    {
        int waited = 0;

        while (waited < SISTEMA_BIOMETRICO_TIMEOUT_MS)
        {
            if (_sistemaBiometrico == null) { TryBindSistemaBiometrico(); }
            if (_sistemaBiometrico != null && _sistemaBiometrico.IsReady) { return true; }

            await Task.Delay(SISTEMA_BIOMETRICO_POLL_MS);
            waited += SISTEMA_BIOMETRICO_POLL_MS;
        }
        return false;
    }

    /// <summary>
    /// Construye la pregunta dinamica de Mia segun si ya existe
    /// un administrador registrado o no.
    /// </summary>
    private string BuildDynamicPrompt()
    {
        bool esPrimeraVez = _sistemaBiometrico.RequiereConfigInicial();

        if (esPrimeraVez)
        {
            return $"Hola, soy {assistantConfig.assistantName} y seré tu asistente del hogar. Veo que aún no hay ningún usuario registrado. " +
                   "Puedes decir 'registrarme' o usar el botón para comenzar tu registro " +
                   "como administrador del sistema.";
        }
        else
        {
            return $"Hola, soy {assistantConfig.assistantName}, tu asistente del hogar. Recuerda que puedes decir ' {assistantConfig.assistantName}, iniciar sesión' o usar el botón para iniciar " +
                   "sesión. Si eres nuevo, necesitarás que un administrador apruebe tu ingreso.";
        }
    }

    /// <summary>
    /// Procesa el texto transcrito de un comando de voz dicho en la
    /// escena de login, y ejecuta la accion correspondiente.
    /// </summary>
    public async Task HandleVoiceCommandAsync(string transcribedText)
    {
        if (ContainsKeyword(transcribedText, REGISTER_KEYWORDS))
        {
            TriggerRegisterFlow();
            PlayActionResponse(registerAcceptedClip);
        }
        else if (ContainsKeyword(transcribedText, VALIDATE_KEYWORDS))
        {
            await TriggerValidateFlowAsync();
        }
        else if (ContainsKeyword(transcribedText, ENTER_KEYWORDS))
        {
            TriggerEnterFlow();
            PlayActionResponse(enterAcceptedClip);
        }
        else if (IsLogoutCommand(transcribedText))
        {
            TriggerLogoutFlow();
        }
        else if (ContainsKeyword(transcribedText, QUIT_KEYWORDS))
        {
            await TriggerQuitFlowAsync();
        }
        else
        {
            Debug.Log("[LoginFlowManager] Comando no reconocido: " + transcribedText);

            // Generacion antes de pedir el audio: si el usuario hace otra accion
            // mientras llega, este "no entendí" ya no corresponde y se descarta.
            int gen = audioPlayer.Generation;

            string fallback = "No entendí. Puedes decir 'registrarme', 'iniciar sesión', 'cerrar sesión' o 'salir' para cerrar la aplicación. Si ya iniciaste sesión, puedes decir 'entrar a la casa' para acceder a tu hogar. " +
                "Si prefieres, puedes darle también al botón de la opción de tu preferencia.";
            AssistantResponse response = await assistantClient.GetSimpleSpeechAsync(fallback);

            if (response?.AudioClip != null)
            {
                audioPlayer.EnqueueIfCurrent(response.AudioClip, gen);
            }
        }
    }

    /// <summary>
    /// Cierra la sesion desde la escena de la casa y regresa al menu
    /// de reconocimiento facial. Llama primero a CerrarSesion() (el
    /// mismo metodo que usa el boton de la UI, para no duplicar su
    /// logica interna), y solo fuerza el cambio de escena si
    /// CerrarSesion() no lo hizo por su cuenta, asi funciona
    /// correctamente sin importar si esa logica vive dentro del
    /// metodo o se maneja aparte en el boton de la UI.
    /// </summary>
    /// OPTIMIZAR ESTO DE CERRAR SESION PARA QUE LA MAYORIA DE LA LOGICA QUEDE EN UN PUNTO CENTRAL
    public void HandleLogout()
    {
        PlayActionResponse(logoutClip);

        if (_sistemaBiometrico != null)
        {
            // Estamos en el login, se cierra la sesion y se refrescan los botones
            _sistemaBiometrico.CerrarSesion();
            return;
        }

        // Estamos en la casa, cierra la sesion en la fuente de verdad
        // y deja que el login se pinte solo al cargar
        GestorSesion.Instance.CerrarSesion();

        var loading = FindFirstObjectByType<LoadingScreenController>();

        if (loading != null)
        {
            loading.LoadScene(assistantConfig.loginSceneName, "Hasta pronto", LogoutTips);
        }
        else
        {
            SceneManager.LoadScene(assistantConfig.loginSceneName);
        }
    }

    private void TriggerRegisterFlow()
    {
        // Punto de integracion. Llamar al metodo publico que
        // SistemaBiometrico exponga para iniciar la captura facial
        // de un nuevo usuario (equivalente a presionar el boton
        // "Registrar" de la UI).
        _sistemaBiometrico.RegistrarUsuario();
    }

    private Task TriggerValidateFlowAsync()
    {
        // Punto de integracion equivalente a presionar el boton "Iniciar sesion" de la UI.
        // El audio de introduccion lo reproduce PreValidacionAsync, igual que con el boton.
        return _sistemaBiometrico.SolicitarValidacionAsync();
    }

    /// <summary>
    /// Mia explica que va a confirmar quien es el usuario y espera a terminar antes de que
    /// SistemaBiometrico valide el rostro. Asi el audio coincide con el momento en que se mira a la camara.
    /// </summary>
    private async Task PlayValidationIntroAsync()
    {
        Debug.Log("[LoginFlowManager] Introduccion de validacion: inicia");

        PlayActionResponse(validateAcceptedClip);
        await WaitForClipAsync(validateAcceptedClip, VALIDATION_INTRO_MAX_WAIT_MS);

        Debug.Log("[LoginFlowManager] Introduccion de validacion: termina, se valida el rostro");
    }

    /// <summary>
    /// Espera a que termine un audio pregrabado que se acaba de encolar. No depende solo de
    /// AudioPlayer.IsAssistantSpeaking (puede tardar en pasar a true): tambien exige que pase
    /// la duracion del propio clip. Tiene un tope para no esperar para siempre.
    /// </summary>
    private async Task WaitForClipAsync(AudioClip clip, int maxWaitMs)
    {
        int minimumMs = clip != null ? Mathf.CeilToInt(clip.length * 1000f) + CLIP_END_MARGIN_MS : 0;
        int elapsedMs = 0;
        bool started = false;

        while (elapsedMs < maxWaitMs)
        {
            await Task.Delay(ASSISTANT_POLL_MS);
            elapsedMs += ASSISTANT_POLL_MS;

            bool speaking = audioPlayer != null && audioPlayer.IsAssistantSpeaking;
            if (speaking) { started = true; }

            // Termino: ya sono, dejo de sonar y paso al menos la duracion del clip
            if (started && !speaking && elapsedMs >= minimumMs) { break; }

            // Respaldo: nunca se detecto sonido, se espera la duracion del clip mas una holgura
            if (!started && elapsedMs >= minimumMs + CLIP_START_GRACE_MS) { break; }
        }
    }

    private void TriggerEnterFlow()
    {
        // Punto de integracion equivalente a presionar el boton
        // "Entrar a la casa" de la UI
        _sistemaBiometrico.EntrarALaCasa();
    }

    private void TriggerLogoutFlow()
    {
        // Punto de integracion equivalente a presionar el boton
        // "Cerrar Sesion" de la UI
        _sistemaBiometrico.CerrarSesion();
    }

    private void TriggerQuitFlow()
    {
        // Punto de integracion equivalente a presionar el boton
        // "Salir de la aplicacion" de la UI
        _sistemaBiometrico.SalirDeLaAplicacion();
    }

    /// <summary>
    /// Se despide y cierra la aplicacion. Primero deja que Mia termine la despedida
    /// (con un tope de espera) para que el audio no se corte al salir.
    /// </summary>
    private async Task TriggerQuitFlowAsync()
    {
        AudioClip farewell = quitClip != null ? quitClip : logoutClip;
        PlayActionResponse(farewell);
        await WaitForClipAsync(farewell, FAREWELL_MAX_WAIT_MS);

        if (_sistemaBiometrico != null) { TriggerQuitFlow(); }
    }

    /// <summary>
    /// "Cerrar sesion" (o "salir/cerrar" junto a la palabra "sesion") y "desconectar".
    /// </summary>
    private bool IsLogoutCommand(string text)
    {
        return ContainsKeyword(text, LOGOUT_KEYWORDS)
            || (ContainsKeyword(text, QUIT_KEYWORDS) && ContainsKeyword(text, SESSION_KEYWORDS));
    }

    private bool ContainsKeyword(string text, string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        string normalized = text.ToLowerInvariant().Trim(' ', '.', ',', '!', '?');
        foreach (string word in normalized.Split(' ', System.StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (string keyword in keywords)
            {
                if (word.Trim('.', ',', '!', '?') == keyword) return true;
            }
        }
        return false;
    }

    private void EnqueueIfAssigned(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("[LoginFlowManager] Un audio pregrabado no esta asignado.");
            return;
        }
        audioPlayer.Enqueue(clip, VoiceChannel.Prerecorded);
    }

    /// <summary>
    /// Respuesta a un boton o comando: interrumpe lo que este sonando (y la cola)
    /// y suena de una vez. Asi el audio del momento es el correcto aunque el
    /// usuario no haya dejado terminar la bienvenida.
    /// </summary>
    private void PlayActionResponse(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("[LoginFlowManager] Un audio de respuesta no esta asignado.");
            return;
        }
        audioPlayer.PlayInmediate(clip, VoiceChannel.Prerecorded);
    }
}