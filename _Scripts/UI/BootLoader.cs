// =====================================================================
// BootLoader.cs
// Inclusive Tech - Pantalla de arranque con UI Toolkit (UXML + USS).
//
// Vive en la escena _BootScene (indice 0 del Build Settings) junto al
// objeto CORE que lleva el ProcessManager. Muestra el estado de cada
// servicio y NO carga el Login hasta que todos esten listos. Si alguno
// falla, avisa y continua despues de unos segundos para no dejar la
// aplicacion bloqueada.
//
// Reparto de animaciones:
//   - USS se encarga de los cambios de estado (entrada, salida, colores,
//     barra de progreso), porque USS solo sabe hacer transiciones.
//   - Este script se encarga de los bucles continuos (respiracion del
//     logo, anillos que se expanden, spinner que gira), porque USS no
//     tiene @keyframes.
//
// Las imagenes y los sonidos son opcionales: si dejas un hueco vacio,
// la pantalla simplemente omite esa parte sin romperse.
// =====================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class BootLoader : MonoBehaviour
{
    [Header("Escenas")]
    [Tooltip("Escena que se carga cuando los servicios estan listos.")]
    [SerializeField] private string nextSceneName = "_LoginScene";

    [Header("Tiempos")]
    [Tooltip("Tiempo minimo que se muestra la pantalla, para que no parpadee.")]
    [SerializeField] private float minimumDisplaySeconds = 3f;

    [Tooltip("Si algun servicio falla, segundos antes de continuar igual. 0 = nunca continuar solo.")]
    [SerializeField] private float continueAfterFailureSeconds = 10f;

    [Tooltip("Tope de espera total. Pasado este tiempo se continua aunque falten servicios.")]
    [SerializeField] private float maxTotalWaitSeconds = 150f;

    [Tooltip("Duracion de la animacion de salida antes de cargar el Login.")]
    [SerializeField] private float exitAnimationSeconds = 0.7f;

    [Header("Imagenes (todas opcionales)")]
    [Tooltip("Logo principal de Mia / Inclusive Tech. PNG transparente cuadrado.")]
    [SerializeField] private Texture2D logoTexture;

    [Tooltip("Imagen de fondo. Si la dejas vacia se usa solo el color de fondo.")]
    [SerializeField] private Texture2D backgroundTexture;

    [Tooltip("Logo institucional del pie de pagina. PNG transparente horizontal.")]
    [SerializeField] private Texture2D universityLogoTexture;

    [Space(6)]
    [Tooltip("Icono del servicio de navegacion. PNG blanco monocromatico.")]
    [SerializeField] private Texture2D navigationIcon;

    [Tooltip("Icono del asistente Mia. PNG blanco monocromatico.")]
    [SerializeField] private Texture2D assistantIcon;

    [Tooltip("Icono de la palabra de activacion. PNG blanco monocromatico.")]
    [SerializeField] private Texture2D wakeWordIcon;

    [Header("Sonidos (todos opcionales)")]
    [Tooltip("Efecto corto al aparecer la pantalla. Suena una vez.")]
    [SerializeField] private AudioClip bootStartClip;

    [Tooltip("Suena cada vez que un servicio queda listo. Se sube el tono en cada uno.")]
    [SerializeField] private AudioClip serviceReadyClip;

    [Tooltip("Suena cuando TODOS los servicios quedaron listos.")]
    [SerializeField] private AudioClip allReadyClip;

    [Tooltip("Suena si algun servicio no arranco.")]
    [SerializeField] private AudioClip errorClip;

    [Tooltip("Suena junto con la animacion de salida hacia el Login.")]
    [SerializeField] private AudioClip exitWhooshClip;

    [Tooltip("Ambiente de fondo en bucle mientras carga. Debe ser un audio loopeable.")]
    [SerializeField] private AudioClip ambientLoopClip;

    [Space(6)]
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 0.7f;

    [Range(0f, 1f)]
    [SerializeField] private float ambientVolume = 0.22f;

    [Header("Audio Source")]
    [SerializeField] private AudioSource _sfxSource;     // boot start, error, whoosh
    [SerializeField] private AudioSource _ambientSource; // ambiente en bucle
    [SerializeField] private AudioSource _blipSource;    // blip de servicio listo (tono variable)
    [SerializeField] private AudioSource _allSystemsReady;     // all ready

    [Header("Apariencia")]
    [Tooltip("Color de fondo de la camara, para que coincida con el del USS.")]
    [SerializeField] private Color cameraBackgroundColor = new Color(0.071f, 0.051f, 0.118f, 1f);

    [Header("Pruebas")]
    [Tooltip("SOLO para ver el UI. Si esta activo NO levanta ni espera los servicios: " +
             "simula que cargan para probar animaciones y sonidos, y al terminar reinicia esta misma " +
             "escena para que lo veas en bucle. Dejar en FALSE para el flujo real.")]
    [SerializeField] private bool uiPreviewMode = false;

    [Tooltip("Modo vista previa: segundos que tarda en 'cargar' cada servicio simulado.")]
    [SerializeField] private float previewSecondsPerService = 1.1f;

    // ---------- Elementos de la UI ----------
    private VisualElement _background;
    private VisualElement _glowA;
    private VisualElement _glowB;
    private VisualElement _content;
    private VisualElement _logoStage;
    private VisualElement _logo;
    private VisualElement _logoFallback;
    private VisualElement _ringOuter;
    private VisualElement _ringInner;
    private VisualElement _spinner;
    private VisualElement _card;
    private VisualElement _serviceList;
    private VisualElement _progressFill;
    private VisualElement _footer;
    private VisualElement _footerLogo;
    private VisualElement _curtain;
    private Label _title;
    private Label _subtitle;
    private Label _statusMessage;
    private Label _progressCount;
    private Label _notice;

    // ---------- Estado interno ----------
    private class ServiceRow
    {
        public VisualElement Root;
        public Label State;
        public ProcessManager.ServiceRuntimeState Current = ProcessManager.ServiceRuntimeState.Starting;
    }

    private readonly Dictionary<string, ServiceRow> _rows = new Dictionary<string, ServiceRow>();
    private readonly List<ProcessManager.ServiceStatus> _statusBuffer = new List<ProcessManager.ServiceStatus>();

    private float _elapsed;
    private float _spinnerAngle;
    private int _readyCount;
    private bool _animateDots = true;
    private bool _loopsActive = true;
    private string _baseStatusMessage = "Iniciando servicios";

    #region Ciclo de vida

    private void Awake()
    {
        EnsureCamera();

        var document = GetComponent<UIDocument>();
        VisualElement root = document != null ? document.rootVisualElement : null;

        if (root == null)
        {
            Debug.LogError("[BootLoader] El UIDocument no tiene raiz. Asigna BootScreen.uxml en el componente UIDocument.");
            return;
        }

        QueryElements(root);
        ApplyTextures();
    }

    private IEnumerator Start()
    {
        if (_content == null)
        {
            // Sin UI no tiene sentido mostrar nada: se continua directo
            Debug.LogWarning("[BootLoader] Sin UI disponible. Se carga la siguiente escena sin pantalla de arranque.");
            yield return LoadNextScene();
            yield break;
        }

        if (uiPreviewMode)
        {
            Debug.LogWarning("[BootLoader] MODO VISTA PREVIA activo: NO se esperan los servicios reales. " +
                             "Ponlo en false para el flujo normal.");
            yield return RunPreviewSequence();
            yield break;
        }

        yield return RunBootSequence();
    }

    private void Update()
    {
        _elapsed += Time.unscaledDeltaTime;

        if (_loopsActive)
        {
            AnimateLogoBreathing();
            AnimateRing(_ringOuter, 0f);
            AnimateRing(_ringInner, 0.5f);
            AnimateSpinner();
            AnimateGlows();
        }

        if (_animateDots && _statusMessage != null)
        {
            int dots = (int)(_elapsed * 2.2f) % 4;
            _statusMessage.text = _baseStatusMessage + new string('.', dots);
        }
    }

    #endregion

    #region Secuencia de arranque

    private IEnumerator RunBootSequence()
    {
        float startTime = Time.realtimeSinceStartup;

        StartCoroutine(PlayEntrance());

        // Esperar un momento a que exista el ProcessManager
        float waitManager = 0f;
        while (ProcessManager.Instance == null && waitManager < 1.5f)
        {
            waitManager += Time.unscaledDeltaTime;
            yield return null;
        }

        ProcessManager manager = ProcessManager.Instance;

        if (manager == null)
        {
            Debug.LogWarning("[BootLoader] No hay ProcessManager en la escena. Se continua sin esperar servicios.");
            SetStatusMessage("Continuando", false);
            yield return new WaitForSecondsRealtime(1f);
            yield return PlayExitAndLoad();
            yield break;
        }

        // Esperar a que terminen de arrancar, refrescando la UI en cada frame
        while (!manager.StartupFinished)
        {
            SyncRows(manager);

            if (Time.realtimeSinceStartup - startTime > maxTotalWaitSeconds)
            {
                Debug.LogWarning("[BootLoader] Se agoto el tiempo de espera total.");
                break;
            }

            yield return null;
        }

        SyncRows(manager);

        // Tiempo minimo en pantalla, para que la animacion se alcance a ver
        while (Time.realtimeSinceStartup - startTime < minimumDisplaySeconds)
        {
            yield return null;
        }

        if (manager.AllServicesReady)
        {
            yield return HandleSuccess();
        }
        else
        {
            yield return HandleFailure();
        }
    }

    /// <summary>
    /// Secuencia SOLO para ver el UI sin levantar los servicios. Crea filas
    /// simuladas, las va marcando como listas con sus sonidos y animaciones,
    /// y al final reinicia esta misma escena para verlo en bucle.
    /// </summary>
    private IEnumerator RunPreviewSequence()
    {
        StartCoroutine(PlayEntrance());

        string[] names = { "it_nav_system", "it_ai_assistant", "it_wake_word" };
        var previewRows = new List<ServiceRow>();

        for (int i = 0; i < names.Length; i++)
        {
            ServiceRow row = CreateRow(names[i]);
            _rows[names[i]] = row;
            _serviceList.Add(row.Root);
            previewRows.Add(row);
            StartCoroutine(AnimateRowIn(row, i * 0.09f));
        }

        UpdateProgress(0, 0, names.Length);

        yield return new WaitForSecondsRealtime(1.2f);

        int ready = 0;
        foreach (ServiceRow row in previewRows)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.3f, previewSecondsPerService));
            ApplyRowState(row, ProcessManager.ServiceRuntimeState.Ready);
            row.Current = ProcessManager.ServiceRuntimeState.Ready;
            ready++;
            UpdateProgress(ready, 0, names.Length);
        }

        yield return new WaitForSecondsRealtime(0.5f);
        yield return HandleSuccess();
    }

    private IEnumerator HandleSuccess()
    {
        SetStatusMessage("Todo listo", false);
        PlayAllReadyService(allReadyClip);
        HideSpinner();

        yield return new WaitForSecondsRealtime(2.7f);
        yield return PlayExitAndLoad();
    }

    private IEnumerator HandleFailure()
    {
        Debug.LogWarning("[BootLoader] Hay servicios que no iniciaron. Se muestra el aviso antes de continuar.");

        PlaySfx(errorClip);
        HideSpinner();
        SetStatusMessage("Arranque incompleto", false);

        if (_progressFill != null) { _progressFill.AddToClassList("progress-fill--failed"); }

        if (_notice != null)
        {
            _notice.text = "Algunos servicios no iniciaron. La aplicación puede funcionar de forma limitada.";
            _notice.AddToClassList("is-in");
        }

        if (continueAfterFailureSeconds <= 0f)
        {
            // Nunca continua solo: la persona decide cerrar la aplicacion
            yield break;
        }

        float remaining = continueAfterFailureSeconds;
        while (remaining > 0f)
        {
            SetStatusMessage($"Continuando en {Mathf.CeilToInt(remaining)} s", false);
            remaining -= Time.unscaledDeltaTime;
            yield return null;
        }

        yield return PlayExitAndLoad();
    }

    #endregion

    #region Animaciones de entrada y salida

    /// <summary>
    /// Hace aparecer los bloques de la pantalla de forma escalonada.
    /// Cada clase 'is-in' dispara la transicion definida en el USS.
    /// </summary>
    private IEnumerator PlayEntrance()
    {
        // Un frame para que el layout quede resuelto antes de animar
        yield return null;

        PlaySfx(bootStartClip);
        StartAmbient();

        AddClass(_logoStage, "is-in");
        yield return new WaitForSecondsRealtime(0.20f);

        AddClass(_title, "is-in");
        yield return new WaitForSecondsRealtime(0.10f);

        AddClass(_subtitle, "is-in");
        yield return new WaitForSecondsRealtime(0.12f);

        AddClass(_card, "is-in");
        yield return new WaitForSecondsRealtime(0.18f);

        AddClass(_footer, "is-in");
    }

    private IEnumerator PlayExitAndLoad()
    {
        _animateDots = false;
        PlaySfx(exitWhooshClip);
        StartCoroutine(FadeOutAmbient(exitAnimationSeconds));

        AddClass(_content, "is-out");
        RemoveClass(_footer, "is-in");
        AddClass(_curtain, "is-on");

        yield return new WaitForSecondsRealtime(exitAnimationSeconds);

        // Los bucles ya no hacen falta y la pantalla esta cubierta
        _loopsActive = false;

        yield return LoadNextScene();
    }

    private IEnumerator LoadNextScene()
    {
        // En vista previa se recarga esta misma escena para ver el UI en bucle.
        string target = uiPreviewMode ? SceneManager.GetActiveScene().name : nextSceneName;
        AsyncOperation operation = SceneManager.LoadSceneAsync(target);

        if (operation == null)
        {
            Debug.LogError($"[BootLoader] No se pudo cargar la escena '{target}'. Revisa el Build Settings.");

            if (_notice != null)
            {
                _notice.text = $"No se encontró la escena {target}.";
                _notice.AddToClassList("is-in");
            }

            RemoveClass(_curtain, "is-on");
            yield break;
        }

        while (!operation.isDone)
        {
            yield return null;
        }
    }

    #endregion

    #region Bucles continuos (lo que USS no puede hacer)

    private void AnimateLogoBreathing()
    {
        if (_logo == null) { return; }

        float breathe = 1f + Mathf.Sin(_elapsed * 1.9f) * 0.04f;
        _logo.style.scale = new Scale(new Vector3(breathe, breathe, 1f));
    }

    /// <summary>
    /// Da vida a las dos luces de fondo: cada una deriva lentamente en una
    /// trayectoria distinta y respira de tamano, con frecuencias diferentes
    /// para que no se sientan puestas ni sincronizadas.
    /// </summary>
    private void AnimateGlows()
    {
        if (_glowA != null)
        {
            float x = Mathf.Sin(_elapsed * 0.25f) * 70f;
            float y = Mathf.Cos(_elapsed * 0.19f) * 55f;
            _glowA.style.translate = new Translate(x, y, 0f);

            float s = 1f + Mathf.Sin(_elapsed * 0.5f) * 0.09f;
            _glowA.style.scale = new Scale(new Vector3(s, s, 1f));
        }

        if (_glowB != null)
        {
            float x = Mathf.Cos(_elapsed * 0.21f) * 80f;
            float y = Mathf.Sin(_elapsed * 0.27f) * 60f;
            _glowB.style.translate = new Translate(x, y, 0f);

            float s = 1f + Mathf.Cos(_elapsed * 0.43f) * 0.10f;
            _glowB.style.scale = new Scale(new Vector3(s, s, 1f));
        }
    }

    /// <summary>
    /// Expande el anillo y lo apaga, en bucle. El desfase permite tener
    /// dos anillos encadenados para que el pulso se sienta continuo.
    /// </summary>
    private void AnimateRing(VisualElement ring, float phase)
    {
        if (ring == null) { return; }

        float t = Mathf.Repeat(_elapsed * 0.45f + phase, 1f);
        float scale = Mathf.Lerp(0.95f, 1.5f, t);

        ring.style.scale = new Scale(new Vector3(scale, scale, 1f));
        ring.style.opacity = Mathf.Lerp(0.5f, 0f, t);
    }

    private void AnimateSpinner()
    {
        if (_spinner == null) { return; }

        _spinnerAngle = Mathf.Repeat(_spinnerAngle + Time.unscaledDeltaTime * 150f, 360f);
        _spinner.style.rotate = new Rotate(new Angle(_spinnerAngle, AngleUnit.Degree));
    }

    private void HideSpinner()
    {
        if (_spinner == null) { return; }

        _spinner.AddToClassList("is-faded");
    }

    #endregion

    #region Filas de servicios

    /// <summary>
    /// Crea las filas que falten y actualiza el estado de las que ya existen.
    /// Se llama en cada frame mientras se espera a los servicios.
    /// </summary>
    private void SyncRows(ProcessManager manager)
    {
        if (_serviceList == null) { return; }

        manager.GetServiceStatuses(_statusBuffer);

        int ready = 0;
        int failed = 0;

        foreach (ProcessManager.ServiceStatus status in _statusBuffer)
        {
            if (!_rows.TryGetValue(status.Name, out ServiceRow row))
            {
                row = CreateRow(status.Name);
                _rows.Add(status.Name, row);
                _serviceList.Add(row.Root);
                StartCoroutine(AnimateRowIn(row, (_rows.Count - 1) * 0.09f));
            }

            if (row.Current != status.State)
            {
                ApplyRowState(row, status.State);
                row.Current = status.State;
            }

            if (status.State == ProcessManager.ServiceRuntimeState.Ready) { ready++; }
            else if (status.State == ProcessManager.ServiceRuntimeState.Failed) { failed++; }
        }

        UpdateProgress(ready, failed, _statusBuffer.Count);
    }

    private ServiceRow CreateRow(string serviceName)
    {
        var root = new VisualElement();
        root.AddToClassList("service-row");

        var badge = new VisualElement();
        badge.AddToClassList("service-badge");

        Texture2D icon = IconFor(serviceName);

        if (icon != null)
        {
            badge.style.backgroundImage = new StyleBackground(icon);
        }
        else
        {
            var dot = new VisualElement();
            dot.AddToClassList("service-dot");
            badge.Add(dot);
        }

        root.Add(badge);

        var name = new Label(FriendlyName(serviceName));
        name.AddToClassList("service-name");
        root.Add(name);

        var state = new Label("Iniciando");
        state.AddToClassList("service-state");
        root.Add(state);

        return new ServiceRow { Root = root, State = state };
    }

    private IEnumerator AnimateRowIn(ServiceRow row, float delay)
    {
        if (delay > 0f) { yield return new WaitForSecondsRealtime(delay); }

        // Un frame para que el elemento quede en el arbol antes de animar
        yield return null;

        row.Root.AddToClassList("is-in");
    }

    private void ApplyRowState(ServiceRow row, ProcessManager.ServiceRuntimeState state)
    {
        row.Root.RemoveFromClassList("is-ready");
        row.Root.RemoveFromClassList("is-failed");

        switch (state)
        {
            case ProcessManager.ServiceRuntimeState.Ready:
                row.Root.AddToClassList("is-ready");
                row.State.text = "Listo";

                // Cada servicio que queda listo suena un poco mas agudo
                _readyCount++;
                PlayBlip(serviceReadyClip, 1f + (_readyCount - 1) * 0.09f);
                break;

            case ProcessManager.ServiceRuntimeState.Failed:
                row.Root.AddToClassList("is-failed");
                row.State.text = "Error";
                break;

            default:
                row.State.text = "Iniciando";
                break;
        }
    }

    private void UpdateProgress(int ready, int failed, int total)
    {
        if (_progressFill != null && total > 0)
        {
            float percent = (float)(ready + failed) / total * 100f;
            _progressFill.style.width = Length.Percent(percent);
        }

        if (_progressCount != null)
        {
            _progressCount.text = total > 0 ? $"{ready} de {total}" : string.Empty;
        }
    }

    private static string FriendlyName(string serviceName)
    {
        switch (serviceName)
        {
            case "it_nav_system": return "Sistema de navegación";
            case "it_ai_assistant": return "Asistente Mía";
            case "it_wake_word": return "Palabra de activación";
            default: return serviceName;
        }
    }

    private Texture2D IconFor(string serviceName)
    {
        switch (serviceName)
        {
            case "it_nav_system": return navigationIcon;
            case "it_ai_assistant": return assistantIcon;
            case "it_wake_word": return wakeWordIcon;
            default: return null;
        }
    }

    #endregion

    #region Preparacion de la UI

    private void QueryElements(VisualElement root)
    {
        _background = root.Q<VisualElement>("background");
        _glowA = root.Q<VisualElement>("glow-a");
        _glowB = root.Q<VisualElement>("glow-b");
        _content = root.Q<VisualElement>("content");
        _logoStage = root.Q<VisualElement>("logo-stage");
        _logo = root.Q<VisualElement>("logo");
        _logoFallback = root.Q<VisualElement>("logo-fallback");
        _ringOuter = root.Q<VisualElement>("ring-outer");
        _ringInner = root.Q<VisualElement>("ring-inner");
        _spinner = root.Q<VisualElement>("spinner");
        _card = root.Q<VisualElement>("card");
        _serviceList = root.Q<VisualElement>("service-list");
        _progressFill = root.Q<VisualElement>("progress-fill");
        _footer = root.Q<VisualElement>("footer");
        _footerLogo = root.Q<VisualElement>("footer-logo");
        _curtain = root.Q<VisualElement>("curtain");

        _title = root.Q<Label>("title");
        _subtitle = root.Q<Label>("subtitle");
        _statusMessage = root.Q<Label>("status-message");
        _progressCount = root.Q<Label>("progress-count");
        _notice = root.Q<Label>("notice");
    }

    /// <summary>
    /// Asigna las imagenes del Inspector. Lo que quede sin asignar se oculta,
    /// asi la pantalla se ve bien aunque falten recursos.
    /// </summary>
    private void ApplyTextures()
    {
        if (_background != null)
        {
            if (backgroundTexture != null)
            {
                _background.style.backgroundImage = new StyleBackground(backgroundTexture);
            }
            else
            {
                _background.style.display = DisplayStyle.None;
            }
        }

        if (_logo != null && logoTexture != null)
        {
            _logo.style.backgroundImage = new StyleBackground(logoTexture);

            // Con logo real, el marcador de texto ya no hace falta
            if (_logoFallback != null) { _logoFallback.style.display = DisplayStyle.None; }
        }

        if (_footerLogo != null)
        {
            if (universityLogoTexture != null)
            {
                _footerLogo.style.backgroundImage = new StyleBackground(universityLogoTexture);
            }
            else
            {
                _footerLogo.style.display = DisplayStyle.None;
            }
        }
    }

    private void SetStatusMessage(string message, bool withDots = true)
    {
        _baseStatusMessage = message;
        _animateDots = withDots;

        if (_statusMessage != null && !withDots)
        {
            _statusMessage.text = message;
        }
    }

    private static void AddClass(VisualElement element, string className)
    {
        if (element != null) { element.AddToClassList(className); }
    }

    private static void RemoveClass(VisualElement element, string className)
    {
        if (element != null) { element.RemoveFromClassList(className); }
    }

    /// <summary>
    /// Crea una camara de color solido si la escena no tiene ninguna, para
    /// evitar el aviso "No cameras rendering", y un AudioListener para que
    /// los sonidos de esta pantalla se escuchen.
    /// </summary>
    private void EnsureCamera()
    {
        if (Camera.main == null && FindAnyObjectByType<Camera>() == null)
        {
            var cameraObject = new GameObject("BootCamera");
            var cam = cameraObject.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = cameraBackgroundColor;
            cam.cullingMask = 0;
        }

        if (FindAnyObjectByType<AudioListener>() == null)
        {
            gameObject.AddComponent<AudioListener>();
        }
    }

    #endregion

    #region Sonido

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null || _sfxSource == null) { return; }

        // Siempre a tono normal. El tono variable vive en PlayBlip, en otro source.
        _sfxSource.pitch = 1f;
        _sfxSource.PlayOneShot(clip, sfxVolume);
    }

    /// <summary>
    /// Reproduce el blip de "servicio listo" con un tono que sube en cada
    /// servicio. Usa su propio source para no alterar el tono de otros sonidos.
    /// </summary>
    private void PlayBlip(AudioClip clip, float pitch)
    {
        if (clip == null || _blipSource == null) { return; }

        _blipSource.pitch = pitch;
        _blipSource.PlayOneShot(clip, sfxVolume);
    }

    private void PlayAllReadyService(AudioClip clip) 
    {
        if (clip == null || _allSystemsReady == null) { return; }

        _allSystemsReady.pitch = 1f;
        _allSystemsReady.PlayOneShot(clip, sfxVolume);
    }

    private void StartAmbient()
    {
        if (ambientLoopClip == null || _ambientSource == null) { return; }

        _ambientSource.clip = ambientLoopClip;
        _ambientSource.loop = true;
        _ambientSource.volume = ambientVolume;
        _ambientSource.Play();
    }

    private IEnumerator FadeOutAmbient(float duration)
    {
        if (_ambientSource == null || !_ambientSource.isPlaying) { yield break; }

        float start = _ambientSource.volume;
        float time = 0f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            _ambientSource.volume = Mathf.Lerp(start, 0f, time / duration);
            yield return null;
        }

        _ambientSource.Stop();
    }

    #endregion
}