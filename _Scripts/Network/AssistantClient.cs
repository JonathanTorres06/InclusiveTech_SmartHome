// =============================================================
// Cliente HTTP para comunicarse con el servidor it_ai_assistant.
//
// Responsabilidad: Encapsular toda la comunicacion HTTP con el
// servidor Python. Envia audio y contexto del hogar, recibe
// audio de respuesta y metadatos de accion. Ninguna otra clase
// de Unity conoce los detalles de UnityWebRequest ni del
// protocolo HTTP usado.
//
// Patron Facade: Esconde la complejidad de UnityWebRequest
// detras de metodos simples como SendVoiceCommandAsync().
//
// Principio S de SOLID, unica responsabilidad: Comunicacion
// HTTP con el servidor. No conoce audio, ni wake word, ni UI.
//
// Principio D de SOLID: Depende de AssistantConfig (abstraccion
// de configuracion), no de valores hardcodeados.
// =============================================================

using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Cliente HTTP para el servidor it_ai_assistant.
/// Contiene y da metodos asincronos para enviar comandos de voz y
/// obtener el saludo matutino.
/// </summary>
public class AssistantClient : MonoBehaviour
{
    [Header("Configuracion")]
    [SerializeField] private AssistantConfig assistantConfig;

    // Headers personalizados que el servidor envia junto al audio
    private const string HEADER_INTENT = "X-Intent";
    private const string HEADER_RESPONSE_TEXT = "X-Response-Text";
    private const string HEADER_ACTION_TYPE = "X-Action-Type";
    private const string HEADER_ACTION_ROOM = "X-Action-Room";
    private const string HEADER_ACTION_STATE = "X-Action-State";
    private const string HEADER_ACTION_VALUE = "X-Action-Value";
    private const string HEADER_ERROR_CODE = "X-Error-Code";

    private const string NULL_HEADER_VALUE = "null";

    // Se dispara cada vez que una llamada al servidor del asistente falla. El orquestador
    // lo reenvia a la UI de la escena actual (casa o login)
    public event Action<AssistantErrorType> OnError;

    /// <summary>
    /// Envia el comando de voz del usuario al pipeline completo del asistente.
    ///
    /// Sube el audio grabado y el contexto actual del hogar al endpoint
    /// /assistant/process. El servidor ejecuta STT, LLM y TTS, y devuelve
    /// el audio de respuesta junto con la accion a ejecutar si aplica.
    /// 
    /// </summary>
    /// <param name="audioData">Bytes del archivo WAV grabado por Unity.</param>
    /// <param name="houseContextJson">Estado actual del hogar como JSON string.</param>
    /// <returns>AssistantResponse con el audio y la accion, o null si fallo.</returns>
    public async Task<AssistantResponse> SendVoiceCommandAsync(byte[] audioData, string houseContextJson, string language = "es", string requestId = null) 
    {
        //string url = $"{assistantConfig.serverUrl}/assistant/process";
        string url = $"{assistantConfig.serverUrl}/assistant/process?language={language}"; // Para que funcione especificamente con espaniol

        if (!string.IsNullOrEmpty(houseContextJson))
        {
            //url += $"?house_context_json={Uri.EscapeDataString(houseContextJson)}";
            url += $"&house_context_json={Uri.EscapeDataString(houseContextJson)}"; // Para que funcione especificamente con espaniol
        }

        var form = new WWWForm();
        form.AddBinaryData("audio_file", audioData, "command.wav", "audio/wav");

        using (var request = UnityWebRequest.Post(url, form)) 
        {
            request.downloadHandler = new DownloadHandlerAudioClip(url, AudioType.MPEG);
            request.timeout = Mathf.RoundToInt(assistantConfig.requestTimeoutSeconds);

            if (!string.IsNullOrEmpty(requestId))
            {
                request.SetRequestHeader("X-Request-Id", requestId);
            }

            await SendRequestAsync(request);

            if (request.result != UnityWebRequest.Result.Success)
            {
                ReportError(ClassifyError(request), "/assistant/process", request.error);
                return null;
            }

            return BuildResponseFromRequest(request);
        }
    }

    /// <summary>
    /// Envia un comando ya transcrito al pipeline LLM - TTS (sin STT).
    /// </summary>
    public async Task<AssistantResponse> SendTextCommandAsync(string text, string houseContextJson, string requestId = null)
    {
        string url = $"{assistantConfig.serverUrl}/assistant/process-text";

        var payload = new TextCommandPayload
        {
            text = text,
            house_context_json = houseContextJson,
            voice = string.IsNullOrEmpty(assistantConfig.ttsVoice) ? null : assistantConfig.ttsVoice,
        };
        string jsonBody = JsonUtility.ToJson(payload);

        using (var request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonBody));
            request.downloadHandler = new DownloadHandlerAudioClip(url, AudioType.MPEG);
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = Mathf.RoundToInt(assistantConfig.requestTimeoutSeconds);

            if (!string.IsNullOrEmpty(requestId))
            {
                request.SetRequestHeader("X-Request-Id", requestId);
            }

            await SendRequestAsync(request);

            if (request.result != UnityWebRequest.Result.Success)
            {
                ReportError(ClassifyError(request), "/assistant/process-text", request.error);
                return null;
            }

            return BuildResponseFromRequest(request);
        }
    }

    /// <summary>
    /// Solicita el saludo matutino dinamico de NOVA con el clima del dia.
    /// </summary>
    /// <param name="userName">Nombre del usuario autenticado, o null para saludo generico.</param>
    /// <returns>AssistantResponse con el audio del saludo, o null si fallo.</returns>
    public async Task<AssistantResponse> GetMorningGreetingAsync(string userName) 
    {
        string url = $"{assistantConfig.serverUrl}/assistant/morning-greeting";

        var payload = new MorningGreetingPayload
        {
            user_name = userName,
            voice = string.IsNullOrEmpty(assistantConfig.ttsVoice) ? null : assistantConfig.ttsVoice,
        };

        string jsonBody = JsonUtility.ToJson(payload);

        using (var request = new UnityWebRequest(url, "POST")) 
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerAudioClip(url, AudioType.MPEG);
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = Mathf.RoundToInt(assistantConfig.requestTimeoutSeconds);

            await SendRequestAsync(request);

            if (request.result != UnityWebRequest.Result.Success)
            {
                ReportError(ClassifyError(request), "/assistant/morning-greeting", request.error);
                return null;
            }

            var response = new AssistantResponse
            {
                AudioClip = DownloadHandlerAudioClip.GetContent(request),
                ResponseText = Uri.UnescapeDataString(request.GetResponseHeader(HEADER_RESPONSE_TEXT) ?? ""),
            };
            return response;
        }
    }

    /// <summary>
    /// Transcribe un audio a texto usando unicamente el subsistema STT
    /// del servidor, sin pasar por el LLM ni el TTS. Pensado para flujos
    /// que solo necesitan el texto crudo de lo que dijo el usuario, como
    /// la captura del nombre durante el registro de un nuevo usuario.
    /// </summary>
    /// <param name="audioData">Bytes del archivo WAV grabado por Unity.</param>
    /// <returns>Texto transcrito, o null si la transcripcion fallo.</returns>
    public async Task<string> TranscribeAudioAsync(byte[] audioData, string language = "es", string contextHint = null) 
    {

        string url = $"{assistantConfig.serverUrl}/stt/transcribe?language={language}";

        if (!string.IsNullOrEmpty(contextHint))
        {
            url += $"&context_hint={Uri.EscapeDataString(contextHint)}";
        }

        var form = new WWWForm();
        form.AddBinaryData("audio_file", audioData, "command.wav", "audio/wav");

        using (var request = UnityWebRequest.Post(url, form)) 
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = Mathf.RoundToInt(assistantConfig.requestTimeoutSeconds);


            await SendRequestAsync(request);

            if (request.result != UnityWebRequest.Result.Success)
            {
                ReportError(ClassifyError(request), "/stt/transcribe", request.error);
                return null;
            }

            return ExtractTranscribedText(request.downloadHandler.text);
        }
    }

    /// <summary>
    /// Sintetiza un texto arbitrario a voz usando el TTS del servidor,
    /// sin pasar por el LLM. Pensado para frases que Unity ya construyo
    /// (como confirmaciones dinamicas) y solo necesitan convertirse en audio.
    /// </summary>
    /// <param name="text">Texto a sintetizar.</param>
    /// <returns>AssistantResponse con el audio generado, o null si fallo.</returns>
    public async Task<AssistantResponse> GetSimpleSpeechAsync(string text) 
    {
        string url = $"{assistantConfig.serverUrl}/tts/speak";

        var payload = new SimpleSpeechPayload { text = text };
        string jsonBody = JsonUtility.ToJson(payload);

        using (var request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerAudioClip(url, AudioType.MPEG);
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = Mathf.RoundToInt(assistantConfig.requestTimeoutSeconds);

            await SendRequestAsync(request);

            if (request.result != UnityWebRequest.Result.Success)
            {
                ReportError(ClassifyError(request), "/tts/speak", request.error);
                return null;
            }

            return new AssistantResponse { AudioClip = DownloadHandlerAudioClip.GetContent(request) };
        }
    }

    [Serializable]
    private class SimpleSpeechPayload 
    {
        public string text;
    }

    [Serializable]
    private class TextCommandPayload
    {
        public string text;
        public string house_context_json;
        public string voice;
    }

    /// <summary>
    /// Verifica que el servidor este disponible llamando a /assistant/health.
    /// </summary>
    /// <returns>True si el servidor responde correctamente.</returns>
    public async Task<bool> CheckServerHealthAsync() 
    {
        string url = $"{assistantConfig.serverUrl}/assistant/health";

        using (var request = UnityWebRequest.Get(url)) 
        {
            request.timeout = 5;
            await SendRequestAsync(request);
            return request.result == UnityWebRequest.Result.Success;
        }
    }

    // =========================================================
    // Metodos privados de soporte
    // =========================================================

    #region Metodos privados de soporte

    /// <summary>
    /// Envia la peticion UnityWebRequest de forma asincrona compatible
    /// con async/await, ya que UnityWebRequest usa Coroutines por defecto.
    /// </summary>
    /// <param name="request">Peticion ya configurada a enviar.</param>
    private Task SendRequestAsync(UnityWebRequest request) 
    {
        var tcs = new TaskCompletionSource<bool>();
        var operation = request.SendWebRequest();

        operation.completed += _ => tcs.SetResult(true);

        return tcs.Task;
    }

    /// <summary>
    /// Construye un AssistantResponse leyendo el audio del body
    /// y la accion del hogar de los headers HTTP.
    /// </summary>
    /// <param name="request">Peticion completada exitosamente.</param>
    /// <returns>AssistantResponse con todos los datos extraidos.</returns>
    private AssistantResponse BuildResponseFromRequest(UnityWebRequest request) 
    {
        var response = new AssistantResponse
        {
            AudioClip = DownloadHandlerAudioClip.GetContent(request),
            Intent = request.GetResponseHeader(HEADER_INTENT),
            ResponseText = Uri.UnescapeDataString(request.GetResponseHeader(HEADER_RESPONSE_TEXT) ?? ""),
            ActionType = NormalizeNullableHeader(request.GetResponseHeader(HEADER_ACTION_TYPE)),
            ActionRoom = NormalizeNullableHeader(request.GetResponseHeader(HEADER_ACTION_ROOM)),
            ActionState = NormalizeNullableHeader(request.GetResponseHeader(HEADER_ACTION_STATE)),
        };

        string rawValue = NormalizeNullableHeader(request.GetResponseHeader(HEADER_ACTION_VALUE));

        if (rawValue != null && float.TryParse(rawValue, out float parsedValue))
        {
            response.ActionValue = parsedValue;
        }

        return response;
    }

    /// <summary>
    /// Extrae el campo "text" del JSON que devuelve /stt/transcribe
    /// (el STTResult serializado por el servidor).
    /// </summary>
    /// <param name="rawJson">Cuerpo JSON completo de la respuesta.</param>
    /// <returns>Texto transcrito, o string vacio si no se pudo parsear.</returns>
    private string ExtractTranscribedText(string rawJson) 
    {
        try
        {
            var result = JsonUtility.FromJson<STTResultPayload>(rawJson);
            return result?.text ?? "";
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AssistantClient] Error al parsear STTResult: {ex.Message}");
            return "";
        }
    }

    /// <summary>
    /// Traduce una peticion fallida a un AssistantErrorType.
    /// Sin respuesta HTTP el servidor no esta disponible; con respuesta,
    /// el header X-Error-Code dice que subsistema fallo.
    /// </summary>
    private AssistantErrorType ClassifyError(UnityWebRequest request)
    {
        if (request.result == UnityWebRequest.Result.ConnectionError)
            return AssistantErrorType.ServerUnreachable;

        switch (request.GetResponseHeader(HEADER_ERROR_CODE))
        {
            case "no_internet": return AssistantErrorType.NoInternet;
            case "rate_limited": return AssistantErrorType.RateLimited;
            case "llm_error": return AssistantErrorType.LlmError;
            case "tts_error": return AssistantErrorType.TtsError;
            case "empty_transcription": return AssistantErrorType.EmptyTranscription;
            default: return AssistantErrorType.Unknown;
        }
    }

    /// <summary>
    /// Convierte el string "null" literal que envia el servidor
    /// en un null real de C#, ya que los headers HTTP no soportan
    /// el valor null nativamente.
    /// </summary>
    /// <param name="headerValue">Valor crudo del header.</param>
    /// <returns>El valor original o null si era el string "null".</returns>
    private string NormalizeNullableHeader(string headerValue) 
    {
        if (string.IsNullOrEmpty(headerValue) || headerValue == NULL_HEADER_VALUE)
        {
            return null;
        }
        return Uri.UnescapeDataString(headerValue);
    }

    /// <summary>
    /// DTO interno para serializar el body JSON del saludo matutino.
    /// JsonUtility de Unity requiere una clase serializable simple.
    /// </summary>
    [Serializable]
    private class MorningGreetingPayload 
    {
        public string user_name;
        public string voice;
    }

    /// <summary>
    /// DTO interno para deserializar el STTResult del servidor con
    /// JsonUtility. Los campos coinciden con el modelo Pydantic STTResult.
    /// </summary>
    [Serializable]
    private class STTResultPayload 
    {
        public string text;
        public string language;
        public float confidence;
        public float processing_time_ms;
    }
    #endregion

    // ===========================================================================
    // Reporte de errores con relacion a la conexion con el servidor del asistente
    // ===========================================================================

    #region Reporte de Errores 

    private void ReportError(AssistantErrorType errorType, string endpoint, string detail)
    {
        Debug.LogError($"[AssistantClient] Error en {endpoint} ({errorType}): {detail}");
        OnError?.Invoke(errorType);
    }

    #endregion
}
