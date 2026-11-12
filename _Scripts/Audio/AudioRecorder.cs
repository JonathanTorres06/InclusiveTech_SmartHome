// =============================================================
// Graba el comando de voz del usuario usando el microfono.
//
// Responsabilidad: capturar audio del microfono hasta que el
// usuario deja de hablar (o hasta el tiempo maximo configurado)
// y entregarlo codificado en WAV, listo para enviar al servidor
// it_ai_assistant. No sabe de HTTP, ni wake word, ni la logica
// del asistente.
//
// Patron Facade: esconde la complejidad de la clase
// Microphone de Unity, la deteccion de silencio y la
// codificacion WAV detras de un metodo async simple.
//
// Deteccion de silencio: se mide el volumen (RMS) del microfono en
// tramos de 30 ms. Es como un portero que espera a que termines
// la frase: no cierra la puerta mientras hablas, ni durante una
// pausa corta, y la cierra cuando pasa un rato sin que digas nada.
// Si nunca detecta voz (o el volumen es demasiado bajo), se
// comporta como antes y graba el tiempo maximo, asi que fallar
// la deteccion nunca recorta el comando.
//
// Principio S de SOLID de unica responsabilidad: grabacion
// y codificacion de audio.
//
// Principio D de SOLID: depende de AssistantConfig para el
// tiempo maximo de grabacion, no de un valor hardcodeado.
// =============================================================

using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Graba audio del microfono y lo entrega codificado en WAV.
/// </summary>
public class AudioRecorder : MonoBehaviour
{
    [Header("Configuracion")]
    [SerializeField] private AssistantConfig assistantConfig;

    [Header("Deteccion de silencio")]
    [Tooltip("Si esta activo, la grabacion termina cuando el usuario deja de hablar. " +
             "Si no, siempre dura recordingDurationSeconds (comportamiento anterior).")]
    [SerializeField] private bool useSilenceDetection = true;

    [Tooltip("Segundos de silencio continuo, despues de que el usuario hablo, para dar el comando por terminado. " +
             "Subir si corta frases con pausas largas, bajar para responder mas rapido.")]
    [SerializeField] private float silenceSecondsToStop = 1.2f;

    [Tooltip("Segundos acumulados de voz para considerar que el usuario realmente empezo a hablar. " +
             "Evita que un ruido corto active el corte.")]
    [SerializeField] private float minSpeechSeconds = 0.3f;

    [Tooltip("Volumen minimo (RMS) para contar como voz. Bajar si el microfono capta muy bajito.")]
    [SerializeField] private float minVoiceThreshold = 0.012f;

    [Tooltip("Volumen maximo del umbral. Evita que el ruido de fondo suba tanto el umbral que la voz quede por debajo.")]
    [SerializeField] private float maxVoiceThreshold = 0.06f;

    [Tooltip("Cuantas veces mas fuerte que el ruido de fondo debe ser un sonido para contar como voz.")]
    [SerializeField] private float noiseRatio = 3f;

    // Se guardaran los nombres de los microfonos disponibles
    [HideInInspector]
    public string selectedMicrophoneDevice;

    // Whisper necesita audio a 16KHz para procesar sin necesidad
    // de resampleo adicional en el servidor
    private const int SAMPLE_RATE = 16000;

    // Duracion maxima del buffer interno de Microphone.Start
    // Debe ser mayor a la duracion real de grabacion configurada
    private const int MAX_RECORDING_BUFFER_SECONDS = 30;

    // Parametros internos de la deteccion de silencio
    private const float FRAME_SECONDS = 0.03f;          // tamano de cada tramo analizado
    private const float IGNORE_START_SECONDS = 0.2f;    // se ignora el inicio (cola del wake word)
    private const float CALIBRATION_SECONDS = 0.4f;     // ventana para medir el ruido de fondo
    private const int POLL_MS = 30;                     // cada cuanto se revisa el microfono

    private bool _isRecording = false;

    // Evento disparado justo cuando la grabacion del comando inicia
    public event Action OnRecordingStarted;

    // Evento disparado justo cuando la grabacion del comando termina
    public event Action OnRecordingStopped;

    // True si el microfono esta grabando en este momento
    public bool IsRecording => _isRecording;

    /// <summary>
    /// Graba un comando de voz y devuelve el audio codificado en WAV.
    /// Termina cuando el usuario deja de hablar o al llegar al tiempo
    /// maximo configurado en AssistantConfig (recordingDurationSeconds).
    /// </summary>
    /// <returns>Bytes del archivo WAV grabado, o null si no hay microfono disponible.</returns>
    public async Task<byte[]> RecordCommandAsync()
    {
        if (!IsMicrophoneAvailable())
        {
            Debug.LogError("[AudioRecorder] No se detecto ningun microfono.");
            return null;
        }

        if (_isRecording)
        {
            Debug.LogWarning("[AudioRecorder] Ya hay una grabacion en curso.");
            return null;
        }

        // INICIA GRABACION DE COMANDO

        _isRecording = true;
        OnRecordingStarted?.Invoke();

        string deviceToUse = selectedMicrophoneDevice;

        if (string.IsNullOrEmpty(deviceToUse) || System.Array.IndexOf(Microphone.devices, deviceToUse) == -1)
        {
            deviceToUse = Microphone.devices[0];
            Debug.LogWarning($"[AudioRecorder] El microfono guardado no esta disponible. Usando: {deviceToUse}");
        }
        else
        {
            Debug.Log($"[AudioRecorder] Iniciando grabacion usando el dispositivo detectado: {deviceToUse}");
        }

        AudioClip recordingClip = Microphone.Start(
            deviceName: deviceToUse, // Microfono detectado para usar
            loop: false,
            lengthSec: MAX_RECORDING_BUFFER_SECONDS,
            frequency: SAMPLE_RATE
        );

        // Tiempo maximo de grabacion (tope de seguridad)
        float maxSeconds = assistantConfig.recordingDurationSeconds;
        string stopReason;

        try
        {
            if (useSilenceDetection)
            {
                stopReason = await WaitForEndOfSpeechAsync(recordingClip, deviceToUse, maxSeconds);
            }
            else
            {
                await Task.Delay(TimeSpan.FromSeconds(maxSeconds));
                stopReason = "duracion fija";
            }
        }
        catch (Exception ex)
        {
            // Si algo falla, se cierra la grabacion con lo que haya para no dejar el microfono abierto
            Debug.LogError($"[AudioRecorder] Error esperando el fin de la voz: {ex.Message}");
            stopReason = "error";
        }

        // Se usa el mismo dispositivo con el que se inicio (antes se pasaba null,
        // que apunta al microfono por defecto y podia ser otro distinto)
        int samplesRecorded = Microphone.GetPosition(deviceToUse);
        Microphone.End(deviceToUse);

        // TERMINA GRABACION DE COMANDO

        _isRecording = false;
        OnRecordingStopped?.Invoke();
        Debug.Log($"[AudioRecorder] Grabacion finalizada ({stopReason}). " +
                  $"{samplesRecorded} muestras, {samplesRecorded / (float)SAMPLE_RATE:F1} s.");

        // PROCESAMIENTO DEL AUDIO

        // Recortar el buffer al numero de muestras realmente grabadas
        AudioClip trimmedClip = TrimClipToRecordedLength(recordingClip, samplesRecorded);

        // Convertir el audio grabado al formato necesario (WAV PCM 16 bits) para enviar al servidor del asistente
        byte[] wavData = WavUtility.FromAudioClip(trimmedClip);

        return wavData;
    }

    #region Deteccion de silencio

    /// <summary>
    /// Espera a que el usuario termine de hablar analizando el volumen
    /// del microfono en tramos cortos.
    ///
    /// Reglas:
    ///  1. Se ignora el primer instante (la cola del wake word "Mia").
    ///  2. Se mide el ruido de fondo (el tramo mas silencioso de los primeros
    ///     instantes) y se fija un umbral de voz proporcional a ese ruido, siempre
    ///     entre minVoiceThreshold y maxVoiceThreshold.
    ///  3. Cuando se acumulan minSpeechSeconds de voz, se considera que el usuario
    ///     empezo a hablar.
    ///  4. Despues de eso, silenceSecondsToStop de silencio continuo terminan la grabacion.
    ///  5. Si nunca se confirma voz, se graba hasta el tiempo maximo (como antes).
    /// </summary>
    /// <param name="clip">Clip que esta llenando el microfono.</param>
    /// <param name="device">Microfono en uso.</param>
    /// <param name="maxSeconds">Tiempo maximo de grabacion.</param>
    /// <returns>Texto con el motivo del corte y las medidas, para ajustar los parametros.</returns>
    private async Task<string> WaitForEndOfSpeechAsync(AudioClip clip, string device, float maxSeconds)
    {
        int frameSamples = Mathf.RoundToInt(SAMPLE_RATE * FRAME_SECONDS);
        int maxSamples = Mathf.RoundToInt(SAMPLE_RATE * maxSeconds);
        float[] frame = new float[frameSamples];

        int readPos = 0;                    // muestras ya analizadas
        float noiseFloor = float.MaxValue;  // ruido de fondo medido
        float threshold = minVoiceThreshold;
        float voicedSeconds = 0f;
        float silentSeconds = 0f;
        float peakRms = 0f;
        bool speechConfirmed = false;

        float wallStart = Time.realtimeSinceStartup;

        while (true)
        {
            await Task.Delay(POLL_MS);

            // Respaldo: si el microfono dejara de avanzar, no se espera para siempre
            if (Time.realtimeSinceStartup - wallStart > maxSeconds + 1f)
            {
                return "tiempo maximo (respaldo)";
            }

            int writePos = Microphone.GetPosition(device);

            // Se analizan todos los tramos completos que hayan llegado desde la ultima revision
            while (writePos - readPos >= frameSamples)
            {
                if (readPos >= maxSamples)
                {
                    return DescribeMax(speechConfirmed, noiseFloor, threshold, peakRms);
                }

                clip.GetData(frame, readPos);
                float frameStart = readPos / (float)SAMPLE_RATE;
                readPos += frameSamples;

                if (frameStart < IGNORE_START_SECONDS)
                {
                    continue;
                }

                float rms = ComputeRms(frame);
                peakRms = Mathf.Max(peakRms, rms);

                // El ruido de fondo es el tramo mas silencioso de la ventana de calibracion
                if (frameStart < IGNORE_START_SECONDS + CALIBRATION_SECONDS)
                {
                    noiseFloor = Mathf.Min(noiseFloor, rms);
                }

                threshold = noiseFloor == float.MaxValue
                    ? minVoiceThreshold
                    : Mathf.Clamp(noiseFloor * noiseRatio, minVoiceThreshold, maxVoiceThreshold);

                if (rms >= threshold)
                {
                    voicedSeconds += FRAME_SECONDS;
                    silentSeconds = 0f;

                    if (voicedSeconds >= minSpeechSeconds)
                    {
                        speechConfirmed = true;
                    }
                }
                else
                {
                    silentSeconds += FRAME_SECONDS;

                    if (speechConfirmed && silentSeconds >= silenceSecondsToStop)
                    {
                        return $"silencio detectado, voz {voicedSeconds:F1} s, ruido {noiseFloor:F4}, " +
                               $"umbral {threshold:F4}, pico {peakRms:F3}";
                    }
                }
            }

            if (readPos >= maxSamples)
            {
                return DescribeMax(speechConfirmed, noiseFloor, threshold, peakRms);
            }
        }
    }

    /// <summary>
    /// Texto del motivo cuando se llego al tiempo maximo, con las medidas
    /// para saber si el umbral quedo muy alto para este microfono.
    /// </summary>
    private string DescribeMax(bool speechConfirmed, float noiseFloor, float threshold, float peakRms)
    {
        string voice = speechConfirmed ? "con voz" : "SIN voz confirmada";
        float noise = noiseFloor == float.MaxValue ? 0f : noiseFloor;
        return $"tiempo maximo {voice}, ruido {noise:F4}, umbral {threshold:F4}, pico {peakRms:F3}";
    }

    /// <summary>
    /// Volumen medio (RMS) de un tramo de audio, entre 0 (silencio) y 1.
    /// </summary>
    private static float ComputeRms(float[] samples)
    {
        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += samples[i] * samples[i];
        }
        return Mathf.Sqrt(sum / samples.Length);
    }

    #endregion

    /// <summary>
    /// Valida si hay al menos un dispositivo de microfono disponible
    /// en el sistema antes de intentar grabar.
    /// </summary>
    /// <returns>True si existe al menos un microfono conectado.</returns>
    private bool IsMicrophoneAvailable()
    {
        return Microphone.devices.Length > 0;
    }

    /// <summary>
    /// Recorta el AudioClip al numero exacto de muestras grabadas.
    ///
    /// Microphone.Start crea un buffer del tamanio maximo configurado
    /// (MAX_RECORDING_BUFFER_SECONDS), pero solo una parte de ese
    /// buffer contiene audio real. Este metodo extrae solo esa parte.
    /// </summary>
    ///
    /// <param name="sourceClip">Clip original con el buffer completo.</param>
    /// <param name="sampleCount">Numero de muestras realmente grabadas.</param>
    /// <returns>Nuevo AudioClip que contiene solo el audio grabado.</returns>
    private AudioClip TrimClipToRecordedLength(AudioClip sourceClip, int sampleCount)
    {
        if (sampleCount <= 0)
        {
            sampleCount = sourceClip.samples;
        }

        float[] samples = new float[sampleCount * sourceClip.channels];

        sourceClip.GetData(samples, 0);

        AudioClip trimmedClip = AudioClip.Create("RecordedCommand", sampleCount, sourceClip.channels, sourceClip.frequency, false);

        trimmedClip.SetData(samples, 0);

        return trimmedClip;
    }
}