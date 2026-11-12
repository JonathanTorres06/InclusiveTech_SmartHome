using UnityEngine;

/// <summary>
/// Estado visual del asistente de voz. Lo comparten los HUD y la pantalla de login.
/// </summary>
public enum AssistantVisualState { Idle, Listening, Thinking, Speaking }

/// <summary>
/// Lee el estado real del asistente a partir de los componentes de audio y del
/// orquestador. Solo lee, no controla nada. Prioridad: grabando, procesando, hablando.
///
/// Antes de volver a "reposo" se exige verlo en varias lecturas seguidas, asi no
/// parpadea a reposo en el instante entre "procesando" y "hablando".
/// </summary>
public class AssistantStateReader
{
    private const int IDLE_CONFIRM_READS = 2;

    private readonly AudioRecorder audioRecorder;
    private readonly AudioPlayer audioPlayer;
    private readonly AssistantOrchestrator orchestrator;

    private int idleReads;
    private AssistantVisualState current = AssistantVisualState.Idle;

    public AssistantStateReader(AudioRecorder audioRecorder, AudioPlayer audioPlayer, AssistantOrchestrator orchestrator)
    {
        this.audioRecorder = audioRecorder;
        this.audioPlayer = audioPlayer;
        this.orchestrator = orchestrator;
    }

    /// <summary>
    /// Devuelve el estado estable del asistente. Llamar a intervalos regulares (~100 ms).
    /// </summary>
    public AssistantVisualState Read()
    {
        AssistantVisualState derived = Derive();

        if (derived == AssistantVisualState.Idle)
        {
            if (++idleReads < IDLE_CONFIRM_READS) return current;
        }
        else
        {
            idleReads = 0;
        }

        current = derived;
        return current;
    }

    private AssistantVisualState Derive()
    {
        if (audioRecorder != null && audioRecorder.IsRecording) return AssistantVisualState.Listening;
        if (orchestrator != null && orchestrator.IsProcessingCommand) return AssistantVisualState.Thinking;
        if (audioPlayer != null && audioPlayer.IsAssistantSpeaking) return AssistantVisualState.Speaking;
        return AssistantVisualState.Idle;
    }
}
