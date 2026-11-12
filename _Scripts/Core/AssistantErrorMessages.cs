/// <summary>
/// Textos de pantalla para cada tipo de error del asistente del hogar. Un solo
/// lugar para que el HUD de la casa y el login digan lo mismo.
/// </summary>
public static class AssistantErrorMessages
{
    public static string GetText(AssistantErrorType errorType)
    {
        switch (errorType)
        {
            case AssistantErrorType.ServerUnreachable: return "No me puedo conectar con mi sistema";
            case AssistantErrorType.NoInternet: return "Sin conexión a internet";
            case AssistantErrorType.RateLimited: return "Muchas solicitudes, espera un momento";
            case AssistantErrorType.LlmError: return "Tuve un problema, intenta de nuevo";
            case AssistantErrorType.TtsError: return "No pude generar mi voz, intenta de nuevo";
            case AssistantErrorType.EmptyTranscription: return "No te alcancé a escuchar, repítelo";
            default: return "Algo salió mal, intenta de nuevo";
        }
    }
}