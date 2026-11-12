// =============================================================
// Reproduce los clips de audio de respuesta del asistente.
//
// Responsabilidad: reproducir AudioClips y notificar cuando la
// reproduccion empieza y termina. Mantiene una cola simple para
// reproducir clips en secuencia sin superponerlos (necesario para
// el saludo matutino que es voz pregrabada + voz dinamica de Mia).
//
// Ahora hay DOS AudioSource, uno para la voz pregrabada de la
// integrante del equipo y otro para la voz dinamica de Mia (TTS),
// para poder darle volumen y canal del mixer distinto a cada una.
// La cola sigue siendo UNA sola, asi que nunca se pisan: cada clip
// solo elige por cual de los dos source sale.
//
// No conoce HTTP, ni wake word, ni de donde viene el audio.
//
// Principio S de SOLID de unica responsabilidad: reproduccion
// de audio en cola.
// =============================================================

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Canal de voz, para elegir el AudioSource correcto.
/// </summary>
public enum VoiceChannel
{
    Prerecorded, // Voz pregrabada de la integrante del equipo
    Assistant    // Voz dinamica de Mia (TTS)
}

/// <summary>
/// Reproduce clips de audio del asistente en una cola secuencial,
/// evitando que se superpongan, con un AudioSource por canal de voz.
/// </summary>
public class AudioPlayer : MonoBehaviour
{
    [Header("Configuracion")]
    [SerializeField] private AssistantConfig assistantConfig;

    [Header("Fuentes de audio")]
    [Tooltip("AudioSource para la voz PREGRABADA. Asignalo con su grupo del mixer.")]
    [SerializeField] private AudioSource prerecordedSource;

    [Tooltip("AudioSource para la voz DINAMICA de Mia. Asignalo con su grupo del mixer.")]
    [SerializeField] private AudioSource assistantSource;

    [Header("Volumen por canal (0 a 1)")]
    [Tooltip("Solo atenua, no amplifica por encima del nivel del archivo. " +
             "Para subir de verdad la pregrabada, usa ganancia en el mixer o normaliza los archivos.")]
    [Range(0f, 1f)]
    [SerializeField] private float prerecordedVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float assistantVolume = 0.85f;

    // Un elemento de la cola: el clip, por cual canal sale, la generacion en la que se pidio
    // y, si alguien lo espera, la tarea que se completa cuando ese clip termina.
    private readonly struct QueueItem
    {
        public readonly AudioClip Clip;
        public readonly VoiceChannel Channel;
        public readonly int Generation;
        public readonly TaskCompletionSource<bool> Completion;

        public QueueItem(AudioClip clip, VoiceChannel channel, int generation, TaskCompletionSource<bool> completion = null)
        {
            Clip = clip;
            Channel = channel;
            Generation = generation;
            Completion = completion;
        }
    }

    private readonly Queue<QueueItem> _playbackQueue = new Queue<QueueItem>();
    private bool _isPlaying = false;

    // Canal del clip que esta sonando ahora mismo
    private VoiceChannel _currentChannel = VoiceChannel.Assistant;

    // Cada interrupcion (PlayInmediate) abre una "generacion" nueva. Un audio
    // que se pide de forma async (la voz de Mia que llega por HTTP) puede
    // encolarse SOLO si su generacion sigue vigente. Asi, si el usuario ya
    // avanzo, ese audio viejo se descarta en vez de sonar fuera de lugar.
    private int _generation = 0;

    // Permite cortar el bucle de reproduccion en seco cuando la aplicacion
    // se cierra o se sale del modo Play. Sin esto, las continuaciones async
    // seguian vivas y el audio se oia colgado despues de cerrar.
    private CancellationTokenSource _cts;
    private bool _isShuttingDown = false;

    // Evento disparado cuando un clip de la cola empieza a reproducirse
    public event Action OnPlaybackStarted;

    // Evento disparado cuando toda la cola termino de reproducirse
    public event Action OnQueueFinished;

    // Eventos por clip CON su canal. El HUD los usa para reaccionar solo a
    // la voz de Mia (Assistant) y no a los audios pregrabados.
    public event Action<VoiceChannel> OnClipStarted;
    public event Action<VoiceChannel> OnClipFinished;

    // True si hay audio reproduciendose en este momento (cualquier canal)
    public bool IsPlaying => _isPlaying;

    // True SOLO mientras suena la voz dinamica de Mia, no los pregrabados.
    // Es lo que debe mirar el HUD para el estado "hablando".
    public bool IsAssistantSpeaking => _isPlaying && _currentChannel == VoiceChannel.Assistant;

    // Generacion de reproduccion vigente. Capturala ANTES de pedir un audio
    // async y pasala despues a EnqueueIfCurrent para no encolar audio viejo.
    public int Generation => _generation;

    private void Awake()
    {
        // Si no asignaste los source en el Inspector, se crean para no romper
        if (prerecordedSource == null) { prerecordedSource = gameObject.AddComponent<AudioSource>(); }
        if (assistantSource == null) { assistantSource = gameObject.AddComponent<AudioSource>(); }

        prerecordedSource.playOnAwake = false;
        assistantSource.playOnAwake = false;

        _cts = new CancellationTokenSource();

        ApplyVolumeFromConfig();
    }

    // Unity llama a OnApplicationQuit al cerrar el build Y al salir del modo
    // Play en el editor. Es el momento exacto para callar todo el audio.
    private void OnApplicationQuit()
    {
        ShutdownAudio();
    }

    // Por si el objeto se destruye antes del quit (recarga de dominio u otro
    // caso raro). Evita que una continuacion async vuelva a sonar.
    private void OnDestroy()
    {
        ShutdownAudio();
    }

    // Cancela el bucle de reproduccion y para los dos source de inmediato.
    private void ShutdownAudio()
    {
        if (_isShuttingDown) { return; }
        _isShuttingDown = true;

        _generation++;
        _cts?.Cancel();
        StopSources();
        DiscardPending();
        _isPlaying = false;
    }

    /// <summary>
    /// Encola un clip para reproduccion. Si no hay nada sonando, empieza
    /// enseguida; si hay algo sonando, espera su turno. El canal por defecto
    /// es la voz de Mia, asi las llamadas existentes no cambian.
    /// </summary>
    /// <param name="clip">Clip de audio. Se ignora si es null.</param>
    /// <param name="channel">Canal de voz: Prerecorded o Assistant.</param>
    public void Enqueue(AudioClip clip, VoiceChannel channel = VoiceChannel.Assistant)
    {
        if (_isShuttingDown) { return; }

        if (clip == null)
        {
            Debug.LogWarning("[AudioPlayer] Se intento encolar un clip nulo.");
            return;
        }

        _playbackQueue.Enqueue(new QueueItem(clip, channel, _generation));

        if (!_isPlaying)
        {
            _ = ProcessQueueAsync();
        }
    }

    /// <summary>
    /// Encola un clip y espera hasta que ESE clip termine de sonar, aunque tenga otros delante
    /// en la cola. Sirve para secuencias (indicacion, senal, grabar) en las que lo siguiente
    /// no debe empezar mientras el audio anterior todavia se oye.
    /// Devuelve true si el clip sono completo. Devuelve false si no pudo sonar o lo interrumpieron
    /// (PlayInmediate, StopAll o cierre de la aplicacion).
    /// </summary>
    public Task<bool> EnqueueAndWaitAsync(AudioClip clip, VoiceChannel channel = VoiceChannel.Assistant)
    {
        if (_isShuttingDown || clip == null) { return Task.FromResult(false); }

        var completion = new TaskCompletionSource<bool>();
        _playbackQueue.Enqueue(new QueueItem(clip, channel, _generation, completion));

        if (!_isPlaying)
        {
            _ = ProcessQueueAsync();
        }

        return completion.Task;
    }

    /// <summary>
    /// Encola un clip SOLO si la generacion indicada sigue vigente. Para audio
    /// que se pide async (TTS de Mia por HTTP): si mientras llegaba el usuario
    /// interrumpio con otra accion, ese clip ya no corresponde a este momento y
    /// se descarta en vez de sonar fuera de lugar.
    /// </summary>
    public void EnqueueIfCurrent(AudioClip clip, int generation, VoiceChannel channel = VoiceChannel.Assistant)
    {
        if (generation != _generation) { return; }
        Enqueue(clip, channel);
    }

    /// <summary>
    /// Reproduce un clip inmediatamente, descartando la cola actual. Util para
    /// respuestas a botones o comandos, que no deben esperar turno. Abre una
    /// generacion nueva: invalida cualquier audio async pendiente anterior.
    /// </summary>
    public void PlayInmediate(AudioClip clip, VoiceChannel channel = VoiceChannel.Assistant)
    {
        if (_isShuttingDown) { return; }

        if (clip == null)
        {
            return;
        }

        // Interrumpir = nueva generacion. Lo encolado se descarta y un audio
        // async de una generacion previa quedara invalidado al intentar entrar.
        _generation++;

        DiscardPending();
        StopSources();
        _playbackQueue.Enqueue(new QueueItem(clip, channel, _generation));

        if (!_isPlaying)
        {
            _ = ProcessQueueAsync();
        }
    }

    // Detiene toda reproduccion y vacia la cola pendiente
    public void StopAll()
    {
        _generation++; // quien estaba esperando un clip recibe false, y el audio async pendiente se descarta
        DiscardPending();
        StopSources();
        _isPlaying = false;
    }

    // Vacia la cola avisando a quien esperaba esos clips de que no van a sonar
    private void DiscardPending()
    {
        while (_playbackQueue.Count > 0)
        {
            _playbackQueue.Dequeue().Completion?.TrySetResult(false);
        }
    }

    private void StopSources()
    {
        if (prerecordedSource != null) { prerecordedSource.Stop(); }
        if (assistantSource != null) { assistantSource.Stop(); }
    }

    // Procesa la cola de forma secuencial, esperando a que cada clip
    // termine antes de pasar al siguiente.
    private async Task ProcessQueueAsync()
    {
        _isPlaying = true;

        try
        {
            while (_playbackQueue.Count > 0 && !_isShuttingDown)
            {
                QueueItem next = _playbackQueue.Dequeue();
                await PlayClipAndWaitAsync(next, _cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Esperado: la aplicacion se esta cerrando y cortamos la cola.
        }

        _isPlaying = false;

        // Si estamos apagando, no avisamos de "cola terminada": no termino,
        // la cortamos nosotros.
        if (!_isShuttingDown) { OnQueueFinished?.Invoke(); }
    }

    /// <summary>
    /// Reproduce un clip en el AudioSource de su canal y espera de forma
    /// asincrona hasta que termine antes de continuar.
    /// </summary>
    private async Task PlayClipAndWaitAsync(QueueItem item, CancellationToken token)
    {
        try
        {
            await PlayClipCoreAsync(item, token);
        }
        finally
        {
            // Completa a quien espera este clip: true si sono completo, false si lo interrumpieron
            item.Completion?.TrySetResult(item.Generation == _generation);
        }
    }

    private async Task PlayClipCoreAsync(QueueItem item, CancellationToken token)
    {
        AudioSource source = item.Channel == VoiceChannel.Prerecorded ? prerecordedSource : assistantSource;

        if (source == null)
        {
            Debug.LogWarning($"[AudioPlayer] No hay AudioSource para el canal {item.Channel}.");
            return;
        }

        _currentChannel = item.Channel;

        source.clip = item.Clip;
        source.Play();

        OnPlaybackStarted?.Invoke();
        OnClipStarted?.Invoke(item.Channel);

        Debug.Log($"[AudioPlayer] Reproduciendo clip de {item.Clip.length:F1}s por el canal {item.Channel}.");

        // Esperar a que el audio TERMINE de verdad, no un tiempo estimado.
        // Con Task.Delay(clip.length) el siguiente clip arrancaba un instante
        // antes y cortaba la cola final del clip actual. Esto se notaba entre
        // dos pregrabados que comparten el mismo AudioSource (bienvenida + tutorial).
        // El token hace que estos Delay lancen OperationCanceledException en
        // cuanto se cierra la aplicacion, cortando la espera sin seguir sonando.
        await Task.Delay(60, token); // margen minimo para asegurar que isPlaying ya sea true

        while (source.isPlaying && source.clip == item.Clip)
        {
            await Task.Delay(50, token);
        }

        OnClipFinished?.Invoke(item.Channel);
    }

    /// <summary>
    /// Aplica el volumen de cada canal a su AudioSource. El audioVolume de
    /// AssistantConfig sigue actuando como volumen maestro sobre los dos.
    /// Llamalo de nuevo si cambias algun volumen en tiempo de ejecucion.
    /// </summary>
    public void ApplyVolumeFromConfig()
    {
        float master = assistantConfig != null ? assistantConfig.audioVolume : 1f;

        if (prerecordedSource != null) { prerecordedSource.volume = prerecordedVolume * master; }
        if (assistantSource != null) { assistantSource.volume = assistantVolume * master; }
    }
}