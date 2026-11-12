using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Feedback visual de Mia en la pantalla de login (UI Toolkit).
/// Solo presentacion: lee el estado del asistente y pinta el aura del icono, el texto de
/// estado y la onda de sonido. No contiene logica de negocio.
///
/// Las clases de estado se aplican SOLO al bloque de Mia ("Logo_Emma") y nunca al contenedor
/// raiz, para que un cambio de estado no vuelva a calcular estilos del resto de la pantalla.
///
/// Se agrega al mismo GameObject que el UIDocument del login.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class LoginAssistantFeedback : MonoBehaviour
{
    [Header("Referencias (se buscan solas si se dejan vacias)")]
    [SerializeField] private AudioRecorder audioRecorder;
    [SerializeField] private AudioPlayer audioPlayer;
    [SerializeField] private AssistantOrchestrator orchestrator;

    [Header("Textos por estado")]
    [SerializeField] private string idleText = "Lista para escucharte";
    [SerializeField] private string listeningText = "Te escucho...";
    [SerializeField] private string thinkingText = "Pensando...";
    [SerializeField] private string speakingText = "Mía está hablando...";

    [Header("Onda")]
    [SerializeField] private int waveBarCount = 7;

    private const int STATE_POLL_MS = 100;
    private const int PULSE_MS = 600;
    private const int WAVE_MS = 110;

    private const float WAVE_REST_HEIGHT = 4f;
    private const float WAVE_LISTEN_MAX = 12f;
    private const float WAVE_SPEAK_MAX = 16f;
    private const float WAVE_THINK_AMPLITUDE = 7f;
    private const float WAVE_THINK_SPEED = 3f;
    private const float WAVE_THINK_PHASE = 0.6f;

    private const string STATE_PREFIX = "estado_";
    private const string PULSE_CLASS = "pulso_alto";

    private VisualElement block;   // "Logo_Emma": recibe las clases de estado
    private Label stateLabel;
    private VisualElement wave;
    private readonly List<VisualElement> bars = new List<VisualElement>();

    private AssistantStateReader reader;
    private AssistantVisualState state = AssistantVisualState.Idle;
    private bool pulseHigh;

    private IVisualElementScheduledItem stateJob, pulseJob, waveJob;

    private void OnEnable()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;

        block = root.Q<VisualElement>("Logo_Emma");
        stateLabel = root.Q<Label>("Emma_Subtitulo");
        wave = root.Q<VisualElement>("Mia_Onda");

        ResolveReferences();
        reader = new AssistantStateReader(audioRecorder, audioPlayer, orchestrator);

        BuildWaveBars();
        ApplyState(AssistantVisualState.Idle);

        stateJob = root.schedule.Execute(SyncState).Every(STATE_POLL_MS);
        pulseJob = root.schedule.Execute(TogglePulse).Every(PULSE_MS);
        waveJob = root.schedule.Execute(AnimateWave).Every(WAVE_MS);
    }

    private void OnDisable()
    {
        stateJob?.Pause();
        pulseJob?.Pause();
        waveJob?.Pause();
    }

    private void ResolveReferences()
    {
        if (audioRecorder == null) audioRecorder = FindFirstObjectByType<AudioRecorder>();
        if (audioPlayer == null) audioPlayer = FindFirstObjectByType<AudioPlayer>();
        if (orchestrator == null) orchestrator = FindFirstObjectByType<AssistantOrchestrator>();
    }

    private void BuildWaveBars()
    {
        wave.Clear();
        bars.Clear();

        for (int i = 0; i < waveBarCount; i++)
        {
            var bar = new VisualElement();
            bar.AddToClassList("mia_onda_barra");
            wave.Add(bar);
            bars.Add(bar);
        }
    }

    private void SyncState()
    {
        AssistantVisualState derived = reader.Read();
        if (derived != state) ApplyState(derived);
    }

    private void ApplyState(AssistantVisualState newState)
    {
        state = newState;

        foreach (string suffix in new[] { "idle", "escuchando", "pensando", "hablando" })
        {
            block.RemoveFromClassList(STATE_PREFIX + suffix);
        }

        string cls;
        switch (newState)
        {
            case AssistantVisualState.Listening: cls = "escuchando"; stateLabel.text = listeningText; break;
            case AssistantVisualState.Thinking: cls = "pensando"; stateLabel.text = thinkingText; break;
            case AssistantVisualState.Speaking: cls = "hablando"; stateLabel.text = speakingText; break;
            default: cls = "idle"; stateLabel.text = idleText; break;
        }

        block.AddToClassList(STATE_PREFIX + cls);
    }

    private void TogglePulse()
    {
        pulseHigh = !pulseHigh;

        bool active = state != AssistantVisualState.Idle;
        block.EnableInClassList(PULSE_CLASS, pulseHigh && active);
    }

    private void AnimateWave()
    {
        for (int i = 0; i < bars.Count; i++)
        {
            float h;
            switch (state)
            {
                case AssistantVisualState.Speaking: h = Random.Range(WAVE_REST_HEIGHT, WAVE_SPEAK_MAX); break;
                case AssistantVisualState.Listening: h = Random.Range(WAVE_REST_HEIGHT, WAVE_LISTEN_MAX); break;
                case AssistantVisualState.Thinking:
                    h = WAVE_REST_HEIGHT + WAVE_THINK_AMPLITUDE * Mathf.Abs(Mathf.Sin(Time.time * WAVE_THINK_SPEED + i * WAVE_THINK_PHASE));
                    break;
                default: h = WAVE_REST_HEIGHT; break;
            }

            bars[i].style.height = h;
        }
    }
}