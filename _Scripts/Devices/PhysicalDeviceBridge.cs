using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Puente entre la simulacion y la maqueta fisica: despues de que Unity
/// ejecuta una accion sobre las luces o las puertas, este componente manda la misma orden
/// al backend de navegacion (it_nav_system), que la publica por MQTT a la ESP32.
///
/// Reglas de oro:
///  - FIRE AND FORGET: nunca se espera la respuesta. La simulacion no depende
///    de la maqueta ni del backend; si estan apagados, Unity funciona igual.
///  - Un error aqui solo deja un aviso en consola. Jamas lanza excepciones
///    hacia el flujo de voz.
///  - Se puede apagar del todo con NavigationConfig.sendOrdersToPhysicalDevices.
///
/// Responsabilidad unica: hablar HTTP con el backend. No sabe de luces de la
/// escena ni de voz (eso lo decide LightManager).
///
/// Va en el MISMO objeto que LightManager (o se arrastra en su campo).
/// </summary>
public class PhysicalDeviceBridge : MonoBehaviour
{
    private const string LightsPath = "/user/devices/lights";
    private const string DoorsPath = "/user/devices/doors";
    private const string ActionOpen = "open";
    private const string ActionClose = "close";
    private const string JsonContentType = "application/json";
    private const int OrderTimeoutSeconds = 3; // Corto a proposito: la maqueta es opcional
    private const string StateOn = "on";
    private const string StateOff = "off";

    [Header("Configuracion")]
    [Tooltip("La misma NavigationConfig que usa el resto del proyecto.")]
    [SerializeField] private NavigationConfig navigationConfig;

    [Header("Diagnostico")]
    [Tooltip("Muestra en consola cada orden enviada y su resultado.")]
    [SerializeField] private bool logOrders = false;

    // Los nombres de campo deben ser IGUALES a los del JSON que espera el backend
    [Serializable] private class LightOrder { public string state; }
    [Serializable] private class AllLightsOrder { public int house_id; public string state; }

    // Dos clases a proposito: JsonUtility siempre escribe los campos int (hold_ms = 0) y el
    // contrato NO admite hold_ms en la orden de cerrar.
    [Serializable] private class DoorOpenOrder { public string action; public int hold_ms; }
    [Serializable] private class DoorCloseOrder { public string action; }

    /// <summary>Pide encender o apagar UNA luz de la maqueta por su deviceKey.</summary>
    public void SendLight(string deviceKey, bool turnOn)
    {
        if (!CanSend() || string.IsNullOrWhiteSpace(deviceKey)) { return; }

        string key = UnityWebRequest.EscapeURL(deviceKey.Trim().ToLowerInvariant());
        string url = $"{BaseUrl()}{LightsPath}/{key}";
        string json = JsonUtility.ToJson(new LightOrder { state = ToState(turnOn) });

        Post(url, json, deviceKey);
    }

    /// <summary>Pide encender o apagar TODAS las luces de la casa activa con UNA sola orden.</summary>
    public void SendAllLights(bool turnOn)
    {
        if (!CanSend()) { return; }

        string url = $"{BaseUrl()}{LightsPath}";
        string json = JsonUtility.ToJson(new AllLightsOrder
        {
            house_id = navigationConfig.activeHouseId,
            state = ToState(turnOn)
        });

        Post(url, json, "todas las luces");
    }

    /// <summary>
    /// Pide abrir una puerta de la maqueta (escenario A: sin robot) con un cierre de
    /// seguridad (hold_ms) tomado de NavigationConfig.
    /// </summary>
    public void SendDoorOpen(string deviceKey)
    {
        if (!CanSendDoors() || string.IsNullOrWhiteSpace(deviceKey)) { return; }

        string json = JsonUtility.ToJson(new DoorOpenOrder
        {
            action = ActionOpen,
            hold_ms = navigationConfig.doorSafetyHoldMs
        });

        Post(DoorUrl(deviceKey), json, deviceKey);
    }

    /// <summary>Pide cerrar de inmediato una puerta de la maqueta (escenario A).</summary>
    public void SendDoorClose(string deviceKey)
    {
        if (!CanSendDoors() || string.IsNullOrWhiteSpace(deviceKey)) { return; }

        string json = JsonUtility.ToJson(new DoorCloseOrder { action = ActionClose });

        Post(DoorUrl(deviceKey), json, deviceKey);
    }

    private string DoorUrl(string deviceKey)
    {
        string key = UnityWebRequest.EscapeURL(deviceKey.Trim().ToLowerInvariant());
        return $"{BaseUrl()}{DoorsPath}/{key}";
    }

    // Las puertas solo reciben ordenes de Unity en el escenario A. En el B se abren solas con su sensor.
    private bool CanSendDoors()
    {
        if (!CanSend()) { return false; }

        if (navigationConfig.doorScenario == PhysicalDoorScenario.WithRobot)
        {
            if (logOrders)
            {
                Debug.Log("[PhysicalDeviceBridge] Escenario con robot: no se mandan ordenes de puertas.");
            }
            return false;
        }

        return true;
    }

    private bool CanSend()
    {
        if (navigationConfig == null)
        {
            Debug.LogWarning("[PhysicalDeviceBridge] Falta asignar NavigationConfig en el Inspector. " +
                "No se enviaran ordenes a la maqueta.");
            return false;
        }

        return navigationConfig.sendOrdersToPhysicalDevices;
    }

    private string BaseUrl()
    {
        return navigationConfig.serverUrl.TrimEnd('/');
    }

    private static string ToState(bool turnOn)
    {
        return turnOn ? StateOn : StateOff;
    }

    /// <summary>Envia el POST sin bloquear y deja el resultado para el callback.</summary>
    private void Post(string url, string json, string label)
    {
        var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", JsonContentType);
        request.timeout = OrderTimeoutSeconds;

        bool log = logOrders;
        if (log) { Debug.Log($"[PhysicalDeviceBridge] Enviando orden ({label}): {json}"); }

        UnityWebRequestAsyncOperation operation = request.SendWebRequest();
        operation.completed += _ => OnOrderFinished(request, label, log);
    }

    private static void OnOrderFinished(UnityWebRequest request, string label, bool log)
    {
        try
        {
            if (request.result == UnityWebRequest.Result.Success)
            {
                if (log) { Debug.Log($"[PhysicalDeviceBridge] Orden aceptada ({label}): HTTP {request.responseCode}."); }
                return;
            }

            // La simulacion NO se ve afectada: solo se avisa de que la maqueta no recibio la orden
            Debug.LogWarning($"[PhysicalDeviceBridge] La maqueta no recibio la orden ({label}). " +
                $"HTTP {request.responseCode}: {request.error}. La simulacion sigue normal.");
        }
        finally
        {
            request.Dispose();
        }
    }
}