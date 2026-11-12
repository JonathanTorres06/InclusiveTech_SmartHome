/*
 * Scriptable Object de configuracion del asistente de voz
 * 
 * Responsabilidad: Centralizar todos los parametros configurables
 * del asistente que pueden editarse desde el Inspecto de Unity
 * sin necesidad de modificar codigo.
 * 
 * Patron Singleton de configuracion: Una sola instancia del
 * asset se comparte por todos los sistemas del proyecto.
 * 
 * Principio S de SOLID: Unica responsabilidad de almacenar
 * y centralizar configuracion. No contiene logica de negocio
*/

using UnityEngine;

/// <summary>
/// Configuracion centralizada del asistente de voz del hogar.
/// Se crea desde: Assets > Create > Inclusive Tech > Assistant Config
/// </summary>
[CreateAssetMenu(fileName = "AssistantConfig", menuName = "Inclusive Tech/Assistant Config")]
public class AssistantConfig : ScriptableObject
{
    [Header("Servidor Python")]

    [Tooltip("URL base del servidor it_ai_assistant")]
    public string serverUrl = "http://localhost:8000";

    [Tooltip("Timeout en segundos para las peticiones HTTP")]
    public float requestTimeoutSeconds = 30f;

    [Header("Es Primera Vez")]
    public bool playFirstTimeTutorial = true;

    [Header("Wake Word")]

    [Tooltip("IP donde corre el proceso it_wake_word")]
    public string wakeWordHost = "127.0.0.1";

    [Tooltip("Puerto del servidor WebSocket de it_wake_word")]
    public int wakeWordPort = 9000;

    [Tooltip("Segundos que Unity graba despues de detectar el wake word")]
    public float recordingDurationSeconds = 6f;

    [Header("Voz del asistente")]

    [Tooltip("Voz de Edge TTS. Dejar vacio para usar la del servidor")]
    public string ttsVoice = "";

    [Tooltip("Volumen de reproduccion del audio de Nova (0 a 1)")]
    [Range(0, 1)]
    public float audioVolume = 1f;

    [Header("Asistente")]

    [Tooltip("Nombre del asistente que se mostrara en la UI")]
    public string assistantName = "Mia";

    [Tooltip("Reproducir saludo matutino al abrir la aplicacion")]
    public bool playMorningGreeting = true;

    [Header("Escenas")]
    [Tooltip("Nombre exacto de la escena de login/reconocimiento facial")]
    public string loginSceneName = "_LoginScene";

    [Tooltip("Nombre exacto de la escena de la casa")]
    public string houseSceneName = "_InclusiveTech_House";
}
