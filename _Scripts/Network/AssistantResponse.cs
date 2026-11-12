// =============================================================
// DTO que representa la respuesta completa del endpoint
// /assistant/process del servidor it_ai_assistant.
//
// Responsabilidad: Unir el audio recibido en el body y
// los metadatos de la accion recibidos en los headers HTTP
// en un solo objeto que el resto de Unity pueda consumir.
//
// Patron: DTO (Data Transfer Object).
// =============================================================

using UnityEngine;

/// <summary>
/// Representa la respuesta completa del asistente del hogar tras procesar
/// un comando de voz, el audio a reproducir y la accion a ejecutar.
/// </summary>
public class AssistantResponse
{
    // Clip de audio con la respuesta de voz del asistente del hogar
    public AudioClip AudioClip { get; set; }

    // Intencion clasificada: general_question, home_question, home_action
    public string Intent { get; set; }

    // Texto que el asistente esta diciendo en el audio
    public string ResponseText { get; set; }

    // Tipo de accion del hogar: LUZ, TODAS_LUCES, TEMP, MOVER. Null si no aplica
    public string ActionType { get; set; }

    // Habitacion objetivo de la accion. Null si no aplica
    public string ActionRoom { get; set; }

    // Estado objetivo para las luces: ON u OFF. Null si no aplica
    public string ActionState { get; set; }

    // Valor numerico para temperatura. Null si no aplica
    public float? ActionValue { get; set; }

    // True si la intencion fue una accion del hogar
    public bool HasAction => Intent == "home_action" && !string.IsNullOrEmpty(ActionType);
}
