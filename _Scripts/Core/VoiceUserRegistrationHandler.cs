// =============================================================
// Maneja el flujo de voz para capturar y confirmar el nombre de
// un usuario recien registrado por reconocimiento facial.
//
// Responsabilidad: escuchar el evento de registro facial completado,
// guiar al usuario por voz para decir y confirmar su nombre, y
// entregar el resultado a GestorUsuarios.AsignarNombre(). No conoce
// nada de biometria ni de base de datos, solo el ID que recibe
// y el nombre que confirma.
//
// Patron State Machine simple: Sera via enum, para modelar los estados
// mutuamente excluyentes de la conversacion (pidiendo nombre,
// confirmando, reintentando) sin depender de banderas booleanas
// sueltas que podrian contradecirse entre si.
//
// Principio S de SOLID de unica responsabilidad: Orquestar la
// conversacion de registro de nombre. Delega grabacion, transcripcion,
// reproduccion y normalizacion fonetica a los componentes ya existentes.
//
// Principio D de SOLID: Depende de AssistantClient, AudioRecorder y
// AudioPlayer por sus contratos publicos, no por su implementacion.
// =============================================================

using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

// Estados posibles del flujo de registro de nombre por voz.
public enum RegistrationState
{
    Idle,
    AskingName,
    ConfirmingName,
    RetryingName,
}

/// <summary>
/// Orquesta la captura y confirmacion por voz del nombre de un
/// usuario recien registrado por reconocimiento facial.
/// </summary>
public class VoiceUserRegistrationHandler : MonoBehaviour
{
    // Constantes
    private const string NAME_CAPTURE_HINT = "El usuario va a decir su nombre propio. Ejemplos de nombres: " +
    "Angie, Isabella, Jonathan, Michael, Kevin, Jennifer, Ashley, " +
    "Andrés, Pedro, Natalia, Nicolás, Aurora, Ian, Yanet, Alisson, " +
    "Jonathan.";

    // Pausa tras la senal de escucha para que su cola no entre a la grabacion
    private const int LISTEN_CUE_TAIL_MS = 150;

    [Header("Componentes del pipeline")]
    [SerializeField] private AssistantClient assistantClient;
    [SerializeField] private AudioRecorder audioRecorder;
    [SerializeField] private AudioPlayer audioPlayer;

    [Header("Dependencia del modulo de reconocimiento facial")]
    [Tooltip("Referencia al GestorUsuarios del modulo de reconocimiento facial")]
    [SerializeField] private SistemaBiometrico sistemaBiometrico;

    [Header("Audios pregrabados (voz de la integrante del equipo)")]
    [Tooltip("Pide al usuario que diga su nombre")]
    [SerializeField] private AudioClip askNameClip;

    [Tooltip("Transicion: Indica que a continuacion Mia confirmara el nombre")]
    [SerializeField] private AudioClip transitionToMiaClip;

    [Tooltip("Se reproduce cuando el usuario confirma que el nombre es correcto")]
    [SerializeField] private AudioClip successClip;

    [Tooltip("Se reproduce cuando el usuario indica que el nombre NO esta bien")]
    [SerializeField] private AudioClip retryClip;

    [Header("Senal de escucha")]
    [Tooltip("Efecto corto (tipo bip) que indica 'ahora habla'. Suena al terminar la indicacion y " +
             "Mia solo empieza a escuchar cuando la senal termino. Si se deja vacio, no hay senal.")]
    [SerializeField] private AudioClip listenCueClip;

    [Header("Configuracion")]
    [Tooltip("Numero maximo de intentos antes de abandonar el registro")]
    [SerializeField] private int maxRetries = 3;

    // Palabras clave para detectar confirmacion positiva/negativa
    private static readonly string[] YES_KEYWORDS = { "si", "sí", "se", "correcto", "exacto", "afirmativo", "justamente", "bien" };
    private static readonly string[] NO_KEYWORDS = { "no", "incorrecto", "negativo", "mal" };

    private RegistrationState _currentState = RegistrationState.Idle;
    private int _attemptCount = 0;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryBindToSistemaBiometrico();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnbindFromSistemaBiometrico();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Si salimos de la escena de login, la instancia anterior de
        // SistemaBiometrico ya fue destruida por Unity al descargar
        // esa escena hay que soltarla para no quedarnos con una
        // referencia que ya no existe.
        UnbindFromSistemaBiometrico();
        TryBindToSistemaBiometrico();
    }

    private void TryBindToSistemaBiometrico()
    {
        sistemaBiometrico = FindFirstObjectByType<SistemaBiometrico>();

        if (sistemaBiometrico != null)
        {
            sistemaBiometrico.OnRegistroFacialCompletado.AddListener(HandleFacialRegistrationCompleted);
            Debug.Log("[VoiceUserRegistrationHandler] Suscrito a SistemaBiometrico");
        }
        // Es normal que llegue a ser null estando en la escena de la casa. No es un error.
    }

    private void UnbindFromSistemaBiometrico()
    {
        if (sistemaBiometrico != null)
        {
            sistemaBiometrico.OnRegistroFacialCompletado.RemoveListener(HandleFacialRegistrationCompleted);
            sistemaBiometrico = null;
        }
    }

    /// <summary>
    /// Punto de entrada. Se dispara cuando el modulo de reconocimiento
    /// facial termina de capturar el rostro de un usuario nuevo.
    /// </summary>
    /// <param name="idUsuario">ID del usuario recien registrado.</param>
    private async void HandleFacialRegistrationCompleted(int idUsuario)
    {
        if (_currentState != RegistrationState.Idle)
        {
            Debug.LogWarning("[VoiceUserRegistrationHandler] Ya hay un registro en curso. " +
                "Se ignora la nueva solicitud.");
            return;
        }

        _attemptCount = 0;

        try
        {
            await RunRegistrationFlowAsync(idUsuario);
        }
        catch (OperationCanceledException)
        {
            // Otro audio interrumpio la secuencia (boton, comando, cierre): no se sigue grabando encima
            Debug.LogWarning("[VoiceUserRegistrationHandler] Registro de nombre interrumpido por otro audio.");
            _currentState = RegistrationState.Idle;
        }
    }

    /// <summary>
    /// Ejecuta el ciclo completo de captura y confirmacion del nombre,
    /// reintentando hasta maxRetries veces si el usuario no confirma.
    /// </summary>
    /// <param name="idUsuario">ID del usuario a registrar.</param>
    private async Task RunRegistrationFlowAsync(int idUsuario)
    {
        while (_attemptCount < maxRetries)
        {
            _attemptCount++;

            string capturedName = await CaptureNameAsync();

            if (string.IsNullOrWhiteSpace(capturedName))
            {
                Debug.LogWarning($"[VoiceUserRegistrationHandler] Intento {_attemptCount}: " +
                    $"no se pudo capturar nigun nombre."
                    );

                _currentState = RegistrationState.RetryingName;
                EnqueuePregrabado(retryClip);
                continue;
            }

            bool confirmed = await ConfirmNameAsync(capturedName);

            if (confirmed)
            {
                bool saved = sistemaBiometrico.AsignarNombreUsuario(idUsuario, capturedName);

                if (saved)
                {
                    Debug.Log($"[VoiceUserRegistrationHandler] Nombre {capturedName} " +
                        $"asignado correctamente al usuario {idUsuario}."
                        );
                    EnqueuePregrabado(successClip);
                }
                else
                {
                    Debug.LogError($"[VoiceUserRegistrationHandler] GestorUsuarios.AsignarNombre " +
                        $"devolvio false para el usuario {idUsuario}"
                        );
                }

                _currentState = RegistrationState.Idle;
                return;
            }

            EnqueuePregrabado(retryClip);
        }

        Debug.LogWarning($"[VoiceUserRegistrationHandler] Se agotaron los {maxRetries} intentos " +
            $"para el usuario {idUsuario}. Registro de nombre abandonado."
            );

        _currentState = RegistrationState.Idle;
    }

    /// <summary>
    /// Reproduce la indicacion pregrabada, graba la respuesta del
    /// usuario y la transcribe para obtener el nombre dicho.
    /// </summary>
    /// <returns>Texto transcrito del nombre, o string vacio si fallo.</returns>
    private async Task<string> CaptureNameAsync()
    {
        _currentState = RegistrationState.AskingName;

        await PlayPrerecordedAndWaitAsync(askNameClip);
        await SignalListeningAsync();

        Debug.Log("DI TU NOMBRE...");

        byte[] audioData = await audioRecorder.RecordCommandAsync();

        if (audioData == null)
        {
            return "";
        }

        string transcribed = await assistantClient.TranscribeAudioAsync(
            audioData,
            language: "es",
            contextHint: NAME_CAPTURE_HINT
            );
        return transcribed?.Trim() ?? "";
    }

    /// <summary>
    /// Reproduce la transicion, hace que Mia repita el nombre por
    /// TTS para confirmar, graba la respuesta y determina si fue
    /// afirmativa o negativa segun palabras clave.
    /// </summary>
    /// <param name="capturedName">Nombre capturado a confirmar.</param>
    /// <returns>True si el usuario confirmo el nombre como correcto.</returns>
    private async Task<bool> ConfirmNameAsync(string capturedName)
    {
        _currentState = RegistrationState.ConfirmingName;

        await PlayPrerecordedAndWaitAsync(transitionToMiaClip);

        string confirmationQuestion = $"Tu nombre es {capturedName}";

        //Debug.Log("Valor en el diccionario PhoneticMap del nombre: " + NameNormalizer.namePronunciationPhonetic);
        Debug.Log("TU NOMBRE ES: " + capturedName);

        AssistantResponse ttsResponse = await assistantClient.GetSimpleSpeechAsync(confirmationQuestion);

        if (ttsResponse != null && ttsResponse.AudioClip != null)
        {
            if (!await audioPlayer.EnqueueAndWaitAsync(ttsResponse.AudioClip))
            {
                throw new OperationCanceledException();
            }
        }

        await SignalListeningAsync();

        byte[] confirmationAudio = await audioRecorder.RecordCommandAsync();

        if (confirmationAudio == null)
        {
            return false;
        }

        string confirmationText = await assistantClient.TranscribeAudioAsync(confirmationAudio);
        Debug.Log("TU RESPUESTA A CONFIRMACION FUE: " + confirmationText);
        return ContainsKeyword(confirmationText, YES_KEYWORDS) && !ContainsKeyword(confirmationText, NO_KEYWORDS);
    }

    /// <summary>
    /// Verifica si alguna palabra COMPLETA del texto transcrito coincide
    /// con las palabras clave dadas. Se compara por palabra completa (no
    /// por substring) para evitar falsos positivos, por ejemplo que "se"
    /// (variante fonetica frecuente de "si" en transcripciones de Whisper
    /// para respuestas cortas) no dispare coincidencias dentro de otras
    /// palabras que lo contengan.
    /// </summary>
    private bool ContainsKeyword(string text, string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string normalized = text.ToLowerInvariant().Trim(' ', '.', ',', '!', '?');
        string[] words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (string word in words)
        {
            string cleanWord = word.Trim('.', ',', '!', '?');

            foreach (string keyword in keywords)
            {
                if (cleanWord == keyword)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void EnqueuePregrabado(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("[VoiceUserRegistrationHandler] Un audio pregrabado no esta " +
                "asignado en el Inspector. Se omite ese paso de la secuencia.");
            return;
        }
        audioPlayer.Enqueue(clip, VoiceChannel.Prerecorded);
    }

    /// <summary>
    /// Reproduce un clip pregrabado y espera a que ESE clip termine de sonar de verdad, aunque
    /// haya otros audios delante en la cola. Asi la grabacion nunca empieza mientras todavia se oye.
    /// Si el clip no esta asignado se omite el paso. Si lo interrumpen, cancela la secuencia.
    /// </summary>
    private async Task PlayPrerecordedAndWaitAsync(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("[VoiceUserRegistrationHandler] Un audio pregrabado no esta " +
                "asignado en el Inspector. Se omite ese paso de la secuencia.");
            return;
        }

        if (!await audioPlayer.EnqueueAndWaitAsync(clip, VoiceChannel.Prerecorded))
        {
            throw new OperationCanceledException();
        }
    }

    /// <summary>
    /// Suena la senal "ahora habla", espera a que termine y deja una pausa corta. Solo despues
    /// de esto se abre el microfono, para que la grabacion no capte ni la indicacion ni la senal.
    /// </summary>
    private async Task SignalListeningAsync()
    {
        if (listenCueClip == null)
        {
            return;
        }

        await PlayPrerecordedAndWaitAsync(listenCueClip);
        await Task.Delay(LISTEN_CUE_TAIL_MS);
    }
}