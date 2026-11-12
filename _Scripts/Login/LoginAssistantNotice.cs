using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Muestra en la pantalla de login los errores del asistente (servidor
/// apagado, fallo de voz, etc.) en un aviso temporal.
///
/// Patron Observer: Se suscribe a AssistantOrchestrator.OnAssistantError
/// y solo "pinta"; no sabe que fallo ni por que.
/// 
/// Principio S de SOLID: Solo presentacion del aviso. Vive aparte de
/// UI_Login_Manager para no mezclarse con el modulo de reconocimiento facial.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class LoginAssistantNotice : MonoBehaviour
{
    private const string NOTICE_NAME = "Aviso_Asistente";
    private const string TEXT_NAME = "Aviso_Texto";
    private const string VISIBLE_CLASS = "aviso_visible";

    private AssistantOrchestrator orchestrator;

    [Header("Ajustes")]
    [SerializeField] private int visibleMilliseconds = 6000;

    private VisualElement _notice;
    private Label _text;
    private IVisualElementScheduledItem _hideJob;
    private bool _subscribed;

    private void OnEnable()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        _notice = root.Q<VisualElement>(NOTICE_NAME);
        _text = root.Q<Label>(TEXT_NAME);
        TrySubscribe();
    }

    // Por si el orquestador (persistente) todavia no existia en OnEnable
    private void Start()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        _hideJob?.Pause();
    }

    private void TrySubscribe()
    {
        if (_subscribed) { return; }
        if (orchestrator == null) { orchestrator = FindFirstObjectByType<AssistantOrchestrator>(); }
        if (orchestrator == null) { return; }

        orchestrator.OnAssistantError += ShowError;
        orchestrator.OnCommandProcessingStarted += HideNotice; // el usuario volvio a hablar
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || orchestrator == null) { return; }

        orchestrator.OnAssistantError -= ShowError;
        orchestrator.OnCommandProcessingStarted -= HideNotice;
        _subscribed = false;
    }

    private void ShowError(AssistantErrorType errorType)
    {
        if (_notice == null) { return; }

        _text.text = AssistantErrorMessages.GetText(errorType);
        _notice.AddToClassList(VISIBLE_CLASS);

        _hideJob?.Pause();
        _hideJob = _notice.schedule.Execute(HideNotice).StartingIn(visibleMilliseconds);
    }

    private void HideNotice()
    {
        _hideJob?.Pause();
        _notice?.RemoveFromClassList(VISIBLE_CLASS);
    }
}