using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Comportamiento del boton "Panel web" del HUD de la casa (UI Toolkit).
///
/// Al pulsarlo comprueba que el servicio de navegacion responda y abre el
/// panel en el navegador (con la clave de acceso ya puesta si la sesion es de administrador). Mientras tanto el boton muestra su estado:
/// abriendo (ambar y pulso), abierto (verde) o sin conexion (rojo, con un
/// aviso en la burbuja de Mia). Despues vuelve solo a su aspecto normal.
///
/// Va en el mismo objeto que el UIDocument del HUD. No modifica
/// HudCasaController: solo le pide mostrar el aviso de error.
///
/// Patron: Facade ligera sobre WebPanelOpener (la logica) y el boton (la vista).
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class WebPanelButtonController : MonoBehaviour
{
    private enum ButtonState { Idle, Opening, Opened, Failed }

    [Header("Referencias")]
    [Tooltip("Asset NavigationConfig del proyecto. De aqui sale la direccion del panel")]
    [SerializeField] private NavigationConfig config;

    [Tooltip("Se busca solo en este objeto si se deja vacio. Muestra el aviso de error en la burbuja de Mia")]
    [SerializeField] private HudCasaController hud;

    [Header("Tiempos")]
    [Tooltip("Tiempo minimo que se ve 'Abriendo...', para que el cambio se alcance a apreciar")]
    [SerializeField] private float minOpeningSeconds = 0.45f;

    [Tooltip("Tiempo que se queda el resultado (abierto o sin conexion) antes de volver a la normalidad")]
    [SerializeField] private float resultHoldSeconds = 1.4f;

    // Nombres de los elementos del UXML del HUD
    private const string BUTTON_NAME = "Btn_PanelWeb";
    private const string LABEL_NAME = "PanelWeb_Texto";

    // Clases USS de cada estado (definidas en HUD_Casa_USS.uss)
    private const string OPENING_CLASS = "panel_abriendo";
    private const string OPENED_CLASS = "panel_abierto";
    private const string FAILED_CLASS = "panel_error";
    private const string PULSE_CLASS = "panel_pulso";
    private const string TEXT_CHANGING_CLASS = "panel_texto_cambiando";

    private const string LABEL_OPENING = "Abriendo…";
    private const string LABEL_OPENED = "Abierto";
    private const string LABEL_FAILED = "Sin conexión";
    private const string ERROR_MESSAGE = "El panel no responde. Revisa el servicio de navegación.";

    private const long PULSE_INTERVAL_MS = 250;
    private const long TEXT_SWAP_MS = 120;

    private Button _button;
    private Label _label;
    private string _idleLabel;

    private WebPanelOpener _opener;
    private ButtonState _state = ButtonState.Idle;
    private Coroutine _openRoutine;
    private IVisualElementScheduledItem _pulseJob;
    private IVisualElementScheduledItem _resetJob;

    #region Ciclo de vida

    private void OnEnable()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        _button = root.Q<Button>(BUTTON_NAME);
        _label = root.Q<Label>(LABEL_NAME);

        if (_button == null || _label == null)
        {
            Debug.LogError($"[WebPanelButtonController] No se encontro {BUTTON_NAME} o {LABEL_NAME} en el UXML del HUD.");
            return;
        }

        if (config == null)
        {
            Debug.LogError("[WebPanelButtonController] Falta asignar NavigationConfig en el Inspector.");
            return;
        }

        if (hud == null) { hud = GetComponent<HudCasaController>(); }

        _opener = new WebPanelOpener(config, new AdminKeyAccess());
        _idleLabel = _label.text;
        _state = ButtonState.Idle;
        _button.clicked += OnClicked;
    }

    private void OnDisable()
    {
        if (_button != null) { _button.clicked -= OnClicked; }

        if (_openRoutine != null)
        {
            StopCoroutine(_openRoutine);
            _openRoutine = null;
        }

        _pulseJob?.Pause();
        _resetJob?.Pause();
    }

    #endregion

    #region Interaccion

    private void OnClicked()
    {
        // Ignora clics mientras se comprueba o se muestra el resultado
        if (_state != ButtonState.Idle) { return; }

        SetState(ButtonState.Opening);
        _openRoutine = StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        float startedAt = Time.unscaledTime;
        bool reachable = false;

        yield return _opener.CheckReachable(ok => reachable = ok);

        if (reachable) { _opener.OpenInBrowser(); }

        // El estado "Abriendo..." se mantiene un instante para que se vea
        float remaining = minOpeningSeconds - (Time.unscaledTime - startedAt);
        if (remaining > 0f) { yield return new WaitForSecondsRealtime(remaining); }

        if (reachable)
        {
            SetState(ButtonState.Opened);
        }
        else
        {
            SetState(ButtonState.Failed);
            if (hud != null) { hud.ShowError(ERROR_MESSAGE); }
        }

        _resetJob = _button.schedule.Execute(() => SetState(ButtonState.Idle)).StartingIn((long)(resultHoldSeconds * 1000f));
        _openRoutine = null;
    }

    #endregion

    #region Presentacion

    private void SetState(ButtonState newState)
    {
        _state = newState;

        _button.EnableInClassList(OPENING_CLASS, newState == ButtonState.Opening);
        _button.EnableInClassList(OPENED_CLASS, newState == ButtonState.Opened);
        _button.EnableInClassList(FAILED_CLASS, newState == ButtonState.Failed);

        SetPulse(newState == ButtonState.Opening);
        SetLabelText(LabelFor(newState));
    }

    private string LabelFor(ButtonState state)
    {
        switch (state)
        {
            case ButtonState.Opening: return LABEL_OPENING;
            case ButtonState.Opened: return LABEL_OPENED;
            case ButtonState.Failed: return LABEL_FAILED;
            default: return _idleLabel;
        }
    }

    // Alterna la clase; las transiciones USS suavizan el latido del icono
    private void SetPulse(bool active)
    {
        _pulseJob?.Pause();
        _button.RemoveFromClassList(PULSE_CLASS);

        if (!active) { return; }

        _button.AddToClassList(PULSE_CLASS);
        _pulseJob = _button.schedule.Execute(() => _button.ToggleInClassList(PULSE_CLASS)).Every(PULSE_INTERVAL_MS);
    }

    // El texto se desvanece, cambia y vuelve a aparecer
    private void SetLabelText(string text)
    {
        if (_label.text == text) { return; }

        _label.AddToClassList(TEXT_CHANGING_CLASS);
        _label.schedule.Execute(() =>
        {
            _label.text = text;
            _label.RemoveFromClassList(TEXT_CHANGING_CLASS);
        }).StartingIn(TEXT_SWAP_MS);
    }

    #endregion
}