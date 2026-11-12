/// <summary>
/// Controla la rutina matutina del asistente: Bienvenida
/// pregrabada (primera vez o repetida) seguida del saludo
/// dinamico de Mia con el clima del dia.
///
/// Responsabilidad: Decidir CUANDO se dispara la rutina (la
/// bienvenida pregrabada en cada apertura, el saludo dinamico
/// una vez por dia) y coordinar la secuencia de reproduccion.
/// No graba audio, no hace HTTP directamente, no reproduce
/// directamente sino que delega en AssistantClient y AudioPlayer,
/// ya construidos en pasos anteriores.
///
/// Principio S de SOLID de unica responsabilidad: Logica de
/// disparo y secuenciacion de la rutina matutina.
///
/// Principio D de SOLID: Depende de AssistantClient y
/// AudioPlayer (sus contratos publicos), no de sus detalles
/// internos de implementacion.
/// </summary>

using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controla el disparo y la secuencia de la rutina matutina
/// del asistente Mia.
/// </summary>
public class MorningGreetingManager : MonoBehaviour
{
    [Header("Configuracion")]
    [SerializeField] private AssistantConfig assistantConfig;

    [Header("Componentes del pipeline")]
    [SerializeField] private AssistantClient assistantClient;
    [SerializeField] private AudioPlayer audioPlayer;

    [Header("Audios pregrabados")]
    [Tooltip("Se reproduce SOLO la primera vez que se abre la aplicacion")]
    [SerializeField] private AudioClip firstTimeWelcomeClip;

    [Space(10)]
    [Header("Tutorial de primera vez")]
    [SerializeField] private AudioClip tutorialIntroClip;
    [SerializeField] private AudioClip tutorialMoveClip;
    [SerializeField] private AudioClip tutorialLightsClip;
    [SerializeField] private AudioClip tutorialTemperatureClip;
    [SerializeField] private AudioClip tutorialWakeWordClip;
    [SerializeField] private AudioClip tutorialHelpClip;
    [SerializeField] private AudioClip tutorialOutroClip;

    [Tooltip("Se reproduce cada dia posterior, antes del saludo dinamico de Mia")]
    [SerializeField] private AudioClip dailyWelcomeClip;

    // Claves de PlayerPrefs para persistir el estado entre sesiones
    private const string PREFS_KEY_LAST_GREETING_DATE = "Mia_LastGreetingDate";
    private const string PREFS_KEY_FIRST_TIME_DONE = "Mia_FirstTimeGreetingDone";

    // Formato de fecha usado para realizar la comparacion el dia guardado vs el nuevo dia a guardar
    private const string DATE_FORMAT = "yyyy-MM-dd";

    // Nombre del usuario autenticado. Sera asignado externamente por el microservicio
    // de reconocimiento facial cuando este sea integrado. Mientras tanto queda null,
    // lo que reproduce un saludo generico (decision tomada en el disenio del microservicio)
    // es decir tener un fallback seguro, hasta tener el flujo de registro por voz.
    private string _currentUserName = null;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private async void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != assistantConfig.houseSceneName) { return; }

        if (!assistantConfig.playMorningGreeting)
        {
            Debug.Log("[MorningGreetingManager] Rutina matutina desactivada en AssistantConfig");
            return;
        }

        // Ya no se corta aqui si el saludo de Mia se dio hoy. La bienvenida
        // pregrabada debe sonar en cada apertura; es PlayMorningRoutineAsync
        // quien decide si ademas toca el saludo dinamico de Mia.
        try
        {
            await PlayMorningRoutineAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MorningGreetingManager] Error en la rutina matutina: {ex.Message}");
        }

    }

    #region Punto de integracion con el reconocimiento facial para el nombre

    /// <summary>
    /// Nombre del usuario para el saludo. Lo toma de la base de datos
    /// de usuarios.db y lo toma de la sesión activa.
    /// </summary>
    private string ResolveUserName()
    {
        string nombre = GestorSesion.Instance?.UsuarioActual?.NombreUsuario;
        return string.IsNullOrWhiteSpace(nombre) ? null : nombre;
    }

    #endregion

    #region Logica de disparo

    /// <summary>
    /// Determina si el saludo DINAMICO de Mia debe reproducirse hoy,
    /// comparando la fecha actual contra la ultima fecha guardada.
    /// </summary>
    /// <returns>True si todavia no se ha dado el saludo dinamico en el dia de hoy.</returns>
    private bool ShouldGreetToday()
    {
        string today = DateTime.Now.ToString(DATE_FORMAT);
        string lastGretingDate = PlayerPrefs.GetString(PREFS_KEY_LAST_GREETING_DATE, "");

        return today != lastGretingDate;
    }

    // Marca en PlayerPrefs que el saludo dinamico ya se dio hoy
    private void MarkGreetedToday()
    {
        string today = DateTime.Now.ToString(DATE_FORMAT);
        PlayerPrefs.SetString(PREFS_KEY_LAST_GREETING_DATE, today);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Determina si esta es la primera vez que se abre la
    /// aplicacion en esta instalacion.
    /// </summary>
    /// <returns>True si nunca se ha completado la bienvenida especial.</returns>
    private bool IsFirstTimeEver()
    {
        return PlayerPrefs.GetInt(PREFS_KEY_FIRST_TIME_DONE, 0) == 0;
    }

    // Marca en PlayerPrefs que la bienvenida especial de
    // primera vez, ya fue reproducida
    private void MarkFirstTimeDone()
    {
        PlayerPrefs.SetInt(PREFS_KEY_FIRST_TIME_DONE, 1);
        PlayerPrefs.Save();
    }

    #endregion

    #region Secuencia de reproduccion

    /// <summary>
    /// Ejecuta la secuencia matutina. La bienvenida pregrabada suena en
    /// CADA apertura (primera vez con tutorial, o la diaria el resto de
    /// veces). El saludo dinamico de Mia solo se agrega una vez por dia.
    /// </summary>
    private async Task PlayMorningRoutineAsync()
    {
        bool isFirstTime = IsFirstTimeEver();
        bool withTutorial = isFirstTime && assistantConfig.playFirstTimeTutorial;
        bool alreadyGreetedToday = !ShouldGreetToday();

        // 1. Bienvenida pregrabada: suena siempre que se entra a la casa.
        AudioClip welcomeClip = isFirstTime ? firstTimeWelcomeClip : dailyWelcomeClip;
        EnqueuePregrabadoClip(welcomeClip, isFirstTime);

        // 2. Tutorial: solo la primera vez.
        if (withTutorial)
        {
            foreach (var clip in new[] { tutorialIntroClip, tutorialMoveClip, tutorialLightsClip, tutorialTemperatureClip, tutorialWakeWordClip, tutorialHelpClip, tutorialOutroClip })
            {
                EnqueueIfAssigned(clip);
            }
        }

        // 3. Saludo dinamico de Mia: solo si aun no ha saludado hoy.
        //    Se encola despues del pregrabado; AudioPlayer reproduce en orden
        //    de llegada, asi que aunque el HTTP tarde, el pregrabado suena primero.
        if (!alreadyGreetedToday)
        {
            AssistantResponse greetingResponse = await assistantClient.GetMorningGreetingAsync(ResolveUserName());

            if (greetingResponse == null || greetingResponse.AudioClip == null)
            {
                Debug.LogWarning("[MorningGreetingManager] No se pudo obtener el saludo dinamico. " +
                    "Se omite esa parte de la rutina.");
            }
            else
            {
                audioPlayer.Enqueue(greetingResponse.AudioClip);
                Debug.Log($"[MorningGreetingManager] Saludo dinamico encolado: '{greetingResponse.ResponseText}'");
            }

            // Se marca el dia solo cuando efectivamente tocaba el saludo dinamico
            MarkGreetedToday();
        }
        else
        {
            Debug.Log("[MorningGreetingManager] El saludo dinamico de Mia ya se dio hoy. " +
                "Solo suena la bienvenida pregrabada.");
        }

        // 4. Marcar que la bienvenida de primera vez ya quedo hecha.
        if (isFirstTime) { MarkFirstTimeDone(); }
    }

    private void EnqueueIfAssigned(AudioClip clip)
    {
        if (clip != null)
        {
            audioPlayer.Enqueue(clip, VoiceChannel.Prerecorded);
        }
    }

    /// <summary>
    /// Encola el audio pregrabado correspondiente, validando que
    /// este asignado en el Inspector antes de intentar reproducirlo.
    /// </summary>
    /// <param name="clip">Clip pregrabado a reproducir.</param>
    /// <param name="isFirstTime">Indica si corresponde a la bienvenida especial.</param>
    private void EnqueuePregrabadoClip(AudioClip clip, bool isFirstTime)
    {
        if (clip == null)
        {
            string clipType = isFirstTime ? "de primera vez" : "diario";
            Debug.LogWarning($"[MorningGreetingManager] El clip pregrabado {clipType} no esta " +
                $"asignado en el Inspector. Se omite esa parte de la rutina.");
            return;
        }

        audioPlayer.Enqueue(clip, VoiceChannel.Prerecorded);
    }

    #endregion

    #region Utilidad para pruebas durante el desarrollo

    [ContextMenu("Resetear estado de rutina matutina (solo pruebas)")]
    public void ResetGreetingStateForTesting()
    {
        PlayerPrefs.DeleteKey(PREFS_KEY_LAST_GREETING_DATE);
        PlayerPrefs.DeleteKey(PREFS_KEY_FIRST_TIME_DONE);
        PlayerPrefs.Save();
        Debug.Log("[MorningGreetingManager] Estado de rutina matutina resetado.");
    }

    #endregion
}