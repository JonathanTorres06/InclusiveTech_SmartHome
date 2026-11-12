using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Boton "Clave de acceso" y tarjeta del HUD de la casa. Solo existe para la sesion de
/// administrador: el resto de las personas no ve el boton y, aunque lo vieran, AdminKeyAccess
/// vuelve a comprobar la sesion cada vez que se pide la clave.
///
/// La tarjeta muestra la clave oculta. "Mostrar" la deja visible 10 s y "Copiar" la manda
/// al portapapeles, que se borra a los 30 s si todavia contiene esa clave. La clave nunca
/// se guarda en un campo ni se imprime en la consola.
///
/// Va en el mismo objeto que el UIDocument del HUD. No modifica HudCasaController.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class AdminKeyCardController : MonoBehaviour
{
    [Header("Tiempos")]
    [Tooltip("Segundos que la clave se ve despues de pulsar Mostrar")]
    [SerializeField] private float revealSeconds = 10f;

    [Tooltip("Segundos que la clave se queda en el portapapeles antes de borrarse")]
    [SerializeField] private float clipboardSeconds = 30f;

    // Nombres de los elementos del UXML del HUD
    private const string BUTTON_NAME = "Btn_Clave";
    private const string CARD_NAME = "Panel_Clave";
    private const string VALUE_NAME = "Clave_Valor";
    private const string SHOW_NAME = "Clave_Mostrar";
    private const string COPY_NAME = "Clave_Copiar";
    private const string CLOSE_NAME = "Clave_Cerrar";
    private const string STATUS_NAME = "Clave_Estado";

    // Clases USS (definidas en HUD_Casa_USS.uss)
    private const string CARD_OPEN_CLASS = "clave_abierto";
    private const string BUTTON_ACTIVE_CLASS = "clave_btn_activa";
    private const string STATUS_ERROR_CLASS = "clave_estado_error";

    private const long CARD_ANIMATION_MS = 260;
    private const long CARD_FADE_IN_DELAY_MS = 10; // deja que el elemento se dibuje antes de animarlo

    private const string SHOW_TEXT = "Mostrar";
    private const string HIDE_TEXT = "Ocultar";
    private static readonly string MASK = new string('•', 32);

    private Button _button, _showButton, _copyButton, _closeButton;
    private VisualElement _card;
    private Label _valueLabel, _statusLabel;

    private AdminKeyAccess _access;
    private bool _isOpen;
    private bool _revealed;
    private string _copiedKey; // solo para borrar el portapapeles si sigue teniendo esta clave

    private IVisualElementScheduledItem _revealJob, _clipboardJob, _hideCardJob;

    #region Ciclo de vida

    private void OnEnable()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;

        _button = root.Q<Button>(BUTTON_NAME);
        _card = root.Q<VisualElement>(CARD_NAME);
        _valueLabel = root.Q<Label>(VALUE_NAME);
        _showButton = root.Q<Button>(SHOW_NAME);
        _copyButton = root.Q<Button>(COPY_NAME);
        _closeButton = root.Q<Button>(CLOSE_NAME);
        _statusLabel = root.Q<Label>(STATUS_NAME);

        if (_button == null || _card == null || _valueLabel == null || _showButton == null
            || _copyButton == null || _closeButton == null || _statusLabel == null)
        {
            Debug.LogError("[AdminKeyCardController] Faltan elementos del UXML del HUD (Btn_Clave y Panel_Clave).");
            return;
        }

        _access = new AdminKeyAccess();

        _button.clicked += OnButtonClicked;
        _showButton.clicked += OnShowClicked;
        _copyButton.clicked += OnCopyClicked;
        _closeButton.clicked += OnCloseClicked;

        _valueLabel.text = MASK;
        RefreshAvailability();
    }

    private void OnDisable()
    {
        if (_button != null) { _button.clicked -= OnButtonClicked; }
        if (_showButton != null) { _showButton.clicked -= OnShowClicked; }
        if (_copyButton != null) { _copyButton.clicked -= OnCopyClicked; }
        if (_closeButton != null) { _closeButton.clicked -= OnCloseClicked; }

        _revealJob?.Pause();
        _hideCardJob?.Pause();

        // Al cerrar sesion o cambiar de escena no debe quedar la clave en el portapapeles
        ClearClipboardIfCopied();
    }

    #endregion

    #region Disponibilidad

    // El boton solo se ve con sesion de administrador
    private void RefreshAvailability()
    {
        bool available = _access.IsAvailable;
        _button.style.display = available ? DisplayStyle.Flex : DisplayStyle.None;

        if (!available) { CloseCard(immediate: true); }
    }

    private void DenyAccess()
    {
        SetStatus("Solo un administrador con sesión activa puede ver la clave.", isError: true);
        RefreshAvailability();
    }

    #endregion

    #region Tarjeta

    private void OnButtonClicked()
    {
        if (!_access.IsAvailable)
        {
            RefreshAvailability();
            return;
        }

        if (_isOpen) { CloseCard(); }
        else { OpenCard(); }
    }

    private void OnCloseClicked() => CloseCard();

    private void OpenCard()
    {
        _isOpen = true;
        _hideCardJob?.Pause();
        SetStatus(string.Empty);

        _card.style.display = DisplayStyle.Flex;
        _card.schedule.Execute(() => _card.AddToClassList(CARD_OPEN_CLASS)).StartingIn(CARD_FADE_IN_DELAY_MS);
        _button.AddToClassList(BUTTON_ACTIVE_CLASS);
    }

    private void CloseCard(bool immediate = false)
    {
        _isOpen = false;
        HideKey();

        _card.RemoveFromClassList(CARD_OPEN_CLASS);
        _button.RemoveFromClassList(BUTTON_ACTIVE_CLASS);

        _hideCardJob?.Pause();

        if (immediate)
        {
            _card.style.display = DisplayStyle.None;
            return;
        }

        // Espera a que termine el desvanecido para quitarla del layout
        _hideCardJob = _card.schedule.Execute(() => _card.style.display = DisplayStyle.None).StartingIn(CARD_ANIMATION_MS);
    }

    #endregion

    #region Mostrar y copiar

    private void OnShowClicked()
    {
        if (_revealed)
        {
            HideKey();
            return;
        }

        if (!_access.TryGetKey(out string key))
        {
            DenyAccess();
            return;
        }

        _revealed = true;
        _valueLabel.text = key;
        _showButton.text = HIDE_TEXT;

        _revealJob?.Pause();
        _revealJob = _card.schedule.Execute(HideKey).StartingIn((long)(revealSeconds * 1000f));
        SetStatus($"Se oculta sola en {Mathf.RoundToInt(revealSeconds)} s.");
    }

    private void HideKey()
    {
        _revealJob?.Pause();
        _revealed = false;
        _valueLabel.text = MASK;
        _showButton.text = SHOW_TEXT;
    }

    private void OnCopyClicked()
    {
        if (!_access.TryGetKey(out string key))
        {
            DenyAccess();
            return;
        }

        ClearClipboardIfCopied(); // si habia una copia anterior, se limpia antes

        GUIUtility.systemCopyBuffer = key;
        _copiedKey = key;

        _clipboardJob = _card.schedule.Execute(OnClipboardTimeout).StartingIn((long)(clipboardSeconds * 1000f));
        SetStatus($"Clave copiada. Pégala en la pestaña Usuarios. Se borra del portapapeles en {Mathf.RoundToInt(clipboardSeconds)} s.");
    }

    private void OnClipboardTimeout()
    {
        ClearClipboardIfCopied();
        SetStatus("Portapapeles limpio.");
    }

    // Solo borra el portapapeles si todavia tiene la clave: si la persona copio otra cosa, no se toca
    private void ClearClipboardIfCopied()
    {
        _clipboardJob?.Pause();

        if (_copiedKey == null) { return; }

        if (GUIUtility.systemCopyBuffer == _copiedKey) { GUIUtility.systemCopyBuffer = string.Empty; }
        _copiedKey = null;
    }

    #endregion

    private void SetStatus(string text, bool isError = false)
    {
        _statusLabel.text = text;
        _statusLabel.EnableInClassList(STATUS_ERROR_CLASS, isError);
    }
}
