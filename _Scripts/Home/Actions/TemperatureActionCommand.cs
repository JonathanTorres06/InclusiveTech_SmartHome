// =============================================================
// Comando para modificar la temperatura del termostato.
// =============================================================

using UnityEngine;

/// <summary>
/// Ejecuta el ajuste de temperatura del termostato.
/// </summary>
public class TemperatureActionCommand : IHomeActionCommand
{
    public void Execute(string room, string state, float? value)
    {
        if (!value.HasValue)
        {
            Debug.LogWarning("[TemperatureActionCommand] No se especifico un valor.");
        }

        HouseState.Instance.tempManager.ModifyTemp(value.Value);
        Debug.Log("[TemperatureActionCommand] Temperatura modificada en: " + value.Value);
    }
}
