#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Cliente HTTP exclusivo del Editor: lee del backend la lista de
/// dispositivos registrados (GET /dev/devices) y el nombre interno de
/// cada habitacion (GET /user/nodes) para alimentar el desplegable.
/// Solo sabe hablar HTTP; el dibujo lo hace DeviceKeyDrawer.
/// Reutiliza NavigationConfig a traves de NodeRegistrationClient.
/// </summary>
public static class DeviceDirectoryClient
{
    private const string DevicesPath = "/dev/devices";
    private const string NodesPath = "/user/nodes";

    // Los nombres de campo en snake_case deben ser IGUALES a los del JSON
    // del backend porque JsonUtility los empareja por nombre exacto.
    [Serializable]
    public class DeviceEntry
    {
        public int id;
        public int node_id;
        public string device_type;
        public string device_key;
        public string display_name;

        // Se rellena despues cruzando con /user/nodes (no viene en el JSON)
        [NonSerialized] public string NodeInternalName;
    }

    // JsonUtility no lee arreglos en la raiz, por eso se envuelven en "items"
    [Serializable] private class DeviceList { public DeviceEntry[] items; }
    [Serializable] private class NodeEntry { public int id; public string internal_name; }
    [Serializable] private class NodeList { public NodeEntry[] items; }

    /// <summary>
    /// Obtiene los dispositivos de un tipo en la casa configurada, con el
    /// InternalName de su habitacion ya resuelto.
    /// </summary>
    public static void FetchDevices(string deviceType, Action<List<DeviceEntry>> onSuccess, Action<string> onError)
    {
        var config = NodeRegistrationClient.GetConfig();

        if (config == null)
        {
            onError?.Invoke("No se encontro NavigationConfig en el proyecto.");
            return;
        }

        string devicesUrl = $"{config.serverUrl}{DevicesPath}" + $"?house_id={config.activeHouseId}&device_type={UnityWebRequest.EscapeURL(deviceType)}";
        string nodesUrl = $"{config.serverUrl}{NodesPath}";
        int timeout = Mathf.CeilToInt(config.requestTimeoutSeconds);

        // Paso 1: dispositivos. Paso 2 (dentro): nombres de habitaciones.
        Get(devicesUrl, timeout, devicesJson =>
        {
            var devices = new List<DeviceEntry>(
                JsonUtility.FromJson<DeviceList>(Wrap(devicesJson)).items);

            Get(nodesUrl, timeout, nodesJson =>
            {
                var nodes = JsonUtility.FromJson<NodeList>(Wrap(nodesJson)).items;
                var nameById = new Dictionary<int, string>();
                foreach (var node in nodes) { nameById[node.id] = node.internal_name; }

                foreach (var device in devices)
                {
                    nameById.TryGetValue(device.node_id, out device.NodeInternalName);
                }

                onSuccess?.Invoke(devices);
            }, onError);
        }, onError);
    }

    /// <summary>Envuelve un arreglo JSON raiz en {"items": ...}.</summary>
    private static string Wrap(string jsonArray)
    {
        return "{\"items\":" + jsonArray + "}";
    }

    /// <summary>
    /// GET asincrono con el mismo patron de sondeo de NodeRegistrationClient
    /// (el Editor no ejecuta corutinas fuera del modo Play).
    /// </summary>
    private static void Get(string url, int timeoutSeconds, Action<string> onOk, Action<string> onError)
    {
        var request = UnityWebRequest.Get(url);
        request.timeout = timeoutSeconds;
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
                    onOk?.Invoke(request.downloadHandler.text);
                }
                else
                {
                    onError?.Invoke(request.error);
                }
            }
            catch (Exception ex)
            {
                // Cubre JSON inesperado o fallos dentro de los callbacks
                onError?.Invoke($"Respuesta inesperada: {ex.Message}");
            }
            finally
            {
                request.Dispose();
            }
        };

        EditorApplication.update += poll;
    }
}
#endif