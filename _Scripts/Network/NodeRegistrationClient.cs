#if UNITY_EDITOR
using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Cliente HTTP exclusivo del Editor: Registra nodos contra el
/// endpoint /dev/nodes del backend. Separado de NodeConnectionEditor
/// a proposito (Single Responsibility). Es decir que este archivo solo sabe
/// hablar HTTP, NodeConnectionEditor solo sabe dibujar el Inspector.
/// </summary>
public class NodeRegistrationClient
{
    private static NavigationConfig _cachedConfig;

    [Serializable]
    private class NodeCreatePayload 
    {
        public int house_id;
        public string node_type;
        public string internal_name;
        public string display_name;
        public float unity_x;
        public float unity_y;
        public float unity_z;
    }

    [Serializable]
    private class NodeCreateResponse 
    {
        public int id;
    }

    [Serializable]
    private class ErrorDetail 
    {
        public string detail;
    }

    /// <summary>
    /// Busca y cachea el asset NavigationConfig existente en el proyecto.
    /// Al ser un script de Editor, no puede recibirlo por Inspector como
    /// un MonoBehaviour, asi que lo localiza directamente en Assets.
    /// </summary>
    public static NavigationConfig GetConfig() 
    {
        if (_cachedConfig != null) { return _cachedConfig; }

        string[] guids = AssetDatabase.FindAssets("t:NavigationConfig");

        if (guids.Length == 0)
        {
            Debug.LogError("[NodeRegistrationClient] No se encontro ningun asset NavigationConfig " +
                "en el proyecto. Crea uno desde Assets > Create > Inclusive Tech > Navigation Config.");
            return null;
        }

        if (guids.Length > 1)
        {
            Debug.LogWarning("[NodeRegistrationClient] Se encontro mas de un NavigationConfig. " +
                "Se usara el primero encontrado; considera dejar solo uno en el proyecto.");
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        _cachedConfig = AssetDatabase.LoadAssetAtPath<NavigationConfig>(path);
        return _cachedConfig;
    }

    /// <summary>
    /// Envia una peticion POST /dev/nodes con los datos actuales del
    /// NavigationNode (nodo nuevo) indicado. Es asincronico via sondeo con
    /// EditorApplication.update, ya que el Editor no ejecuta
    /// corutinas de MonoBehaviour fuera del modo Play.
    /// </summary>
    /// <param name="node">Nodo de la escena a registrar.</param>
    /// <param name="onSuccess">Callback si el backend confirma la creacion, recibe el ID asignado.</param>
    /// <param name="onError">Callback con el mensaje de error si falla.</param>
    public static void RegisterNode(NavigationNode node, Action<int> onSuccess, Action<string>onError) 
    {
        var config = GetConfig();
        if (config == null) 
        {
            onError?.Invoke("No se encontro NavigationConfig en el proyecto");
            return;
        }

        var payload = new NodeCreatePayload
        {
            house_id = config.activeHouseId,
            node_type = node.BackendNodeType,
            internal_name = node.InternalName,
            display_name = string.IsNullOrEmpty(node.initialDisplayName) ?
                node.InternalName : node.initialDisplayName.Trim(),
            unity_x = node.transform.position.x,
            unity_y = node.transform.position.y,
            unity_z = node.transform.position.z,
        };

        string json = JsonUtility.ToJson(payload);
        byte[] bodyBytes = Encoding.UTF8.GetBytes(json);

        var request = new UnityWebRequest($"{config.serverUrl}/dev/nodes", "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyBytes);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = Mathf.CeilToInt(config.requestTimeoutSeconds);

        var operation = request.SendWebRequest();

        EditorApplication.CallbackFunction poll = null;

        poll = () =>
        {
            if (operation == null || !operation.isDone) { return; }

            EditorApplication.update -= poll;

            try
            {
                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<NodeCreateResponse>(request.downloadHandler.text);
                    onSuccess?.Invoke(response.id);
                }
                else
                {
                    string detail = ExtractErrorDetail(request.downloadHandler.text);
                    onError?.Invoke(string.IsNullOrEmpty(detail) ? request.error : detail);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NodeRegistrationClient] Error inesperado procesando la respuesta: {ex.Message}");
            }
            finally 
            {
                request.Dispose();
            }
        };

        EditorApplication.update += poll;
    }

    /// <summary>
    /// Envia una peticion DELETE /dev/nodes/{id} para eliminar del
    /// backend el nodo asociado. Sigue el mismo patron de sondeo que
    /// RegisterNode, con la misma garantia de desuscripcion segura.
    /// </summary>
    /// <param name="backendNodeId">ID que el backend asigno a este nodo.</param>
    /// <param name="onSuccess">Callback si el backend confirma la eliminacion.</param>
    /// <param name="onError">Callback con el mensaje de error si falla.</param>
    public static void DeleteNode(int backendNodeId, Action onSuccess, Action<string> onError) 
    {
        var config = GetConfig();

        if (config == null) 
        { 
            onError?.Invoke("No se encontro NavigationConfig en el proyecto.");
            return;
        }

        var request = UnityWebRequest.Delete($"{config.serverUrl}/dev/nodes/{backendNodeId}");
        request.downloadHandler = new DownloadHandlerBuffer();
        request.timeout = Mathf.CeilToInt(config.requestTimeoutSeconds);

        var operation = request.SendWebRequest();

        EditorApplication.CallbackFunction poll = null;
        poll = () =>
        {
            if (operation == null || !operation.isDone) { return; }

            EditorApplication.update -= poll;

            try
            {
                if (request.result == UnityWebRequest.Result.Success)
                {
                    onSuccess?.Invoke();
                }
                else 
                {
                    string detail = ExtractErrorDetail(request.downloadHandler.text);
                    onError?.Invoke(string.IsNullOrEmpty(detail) ? request.error : detail);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NodeRegistrationClient] Error inesperado eliminando el nodo: {ex.Message}");
            }
            finally 
            {
                request.Dispose();
            }
        };

        EditorApplication.update += poll;
    }

    /// <summary>
    /// Extrae el campo "detail" del cuerpo de error que devuelve
    /// FastAPI (HTTPException), el mismo formato que ya maneja
    /// handleResponse() en el panel web (api.js).
    /// </summary>
    public static string ExtractErrorDetail(string responseBody) 
    {
        if (string.IsNullOrWhiteSpace(responseBody)) { return null; }

        try
        {
            var error = JsonUtility.FromJson<ErrorDetail>(responseBody);
            return error?.detail;
        }
        catch
        {
            return null;
        }
    }
}
#endif