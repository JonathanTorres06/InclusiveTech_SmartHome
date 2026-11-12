/// <summary>
/// Tipos de falla que el asistente del hogar puede reportar a la interfaz.
/// El servidor los etiqueta con X-Error-Code; Unity agrega ServerUnreachable
/// cuando no hay respuesta HTTP. La UI decide el texto de cada uno.
/// </summary>
public enum AssistantErrorType
{
    None,
    ServerUnreachable,
    NoInternet,
    RateLimited,
    LlmError,
    TtsError,
    EmptyTranscription,
    Unknown
}
