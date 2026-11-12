using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Centralizara todos los light Controllers de la casa
/// </summary>
public class LightManager : MonoBehaviour
{
    public List<LightController> luces = new List<LightController>();

    [Header("Maqueta fisica (opcional)")]
    [Tooltip("Envia cada orden de luz tambien a la maqueta. Si se deja vacio se busca en este mismo objeto.")]
    [SerializeField] private PhysicalDeviceBridge physicalBridge;

    // Al iniciar avisa de luces sin clave o con clave repetida
    private void Awake()
    {
        if (physicalBridge == null)
        {
            physicalBridge = GetComponent<PhysicalDeviceBridge>();
        }

        var seen = new HashSet<string>();

        foreach (var luz in luces)
        {
            if (luz == null) { continue; }

            if (string.IsNullOrWhiteSpace(luz.deviceKey))
            {
                Debug.LogWarning($"[LightManager] '{luz.name}' no tiene deviceKey asignado.");
            }
            else if (!seen.Add(luz.deviceKey.Trim().ToLowerInvariant())) 
            {
                Debug.LogWarning($"[LightManager] deviceKey repetido: '{luz.deviceKey}'.");
            }
        }
    }

    public void SetLight(string internalName, bool state) 
    {
        if (string.IsNullOrWhiteSpace(internalName)) { return; }

        string normalizedInternalName = internalName.Trim().ToUpperInvariant();

        foreach (var luz in luces)
        {
            if (luz == null || string.IsNullOrWhiteSpace(luz.internalName)) { continue; }

            if (luz.internalName.Trim().ToUpperInvariant() == normalizedInternalName)
            {
                luz.ToggleLights(state);
                NotifyPhysicalLight(luz, state);
            }
        }
    }

    public void AllLights(bool state) 
    {
        foreach (var luz in luces)
        {
            luz.ToggleLights(state);
        }

        // UNA sola orden para toda la casa (no una por luz)
        if (physicalBridge != null)
        {
            physicalBridge.SendAllLights(state);
        }
    }

    // Busca por clave de dispositivo (la que usara MQTT). Devuelve false si no existe.
    public bool SetLightByKey(string deviceKey, bool state) 
    {
        if (string.IsNullOrWhiteSpace(deviceKey)) { return false; }

        string normalizeKey = deviceKey.Trim().ToLowerInvariant();

        var luz = luces.Find(l => l != null && !string.IsNullOrWhiteSpace(l.deviceKey) && l.deviceKey.Trim().ToLowerInvariant() == normalizeKey);

        if (luz == null)
        {
            Debug.LogWarning($"[LightManager] No hay ninguna luz con deviceKey '{deviceKey}'.");
            return false;
        }

        luz.ToggleLights(state);
        NotifyPhysicalLight(luz, state);
        return true;
    }

    // Manda la orden de una luz a la maqueta (fire and forget). No afecta a la simulacion.
    private void NotifyPhysicalLight(LightController luz, bool state)
    {
        if (physicalBridge == null || string.IsNullOrWhiteSpace(luz.deviceKey)) { return; }

        physicalBridge.SendLight(luz.deviceKey, state);
    }

    // Numero de luces actualmente encendidas. Usado por el
    // AssistantOrchestrator para construir el HouseContext
    // que se envia al servidor it_ai_assistant.
    public int GetLightsOnCount() 
    {
        return luces.Sum(l => l.lightsSource != null ? l.lightsSource.Count(light => light != null && light.enabled) : 0);
    }

    // Numero total de luces registradas en la casa. Valor sin ser string
    public int GetTotalLightsCount() 
    {
        return luces.Sum(l => l.lightsSource != null ? l.lightsSource.Count(light => light != null) : 0);
    }

    public string GetSummary() 
    {
        return "Luces encendidas: " + GetLightsOnCount() + ".";
    }
}
