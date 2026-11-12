// =============================================================
// Comando para encender o apagar una luz especifica.
// Estara en el case "LUZ" del switch del HomeActionHandler,
// Estara como una clase independiente.
// =============================================================

using UnityEngine;

/// <summary>
/// Ejecuta el encendido/apagado de una luz especifica de una habitacion.
/// </summary>
public class LightActionCommand : IHomeActionCommand
{
    public void Execute(string room, string state, float? value)
    {
        if (string.IsNullOrEmpty(room))
        {
            Debug.LogWarning("[LightActionCommand] No se especifico habitacion");
            return;
        }

        string internalNodeName = HouseState.Instance.roomManager.ResolveNodeName(room);

        if (internalNodeName == null)
        {
            Debug.LogWarning($"[LightActionCommand] No se pudo resolver la habitacion: {room}");
            return;
        }

        bool turnOn = state == "ON";

        HouseState.Instance.lightManager.SetLight(internalNodeName, turnOn);
        Debug.Log($"[LightActionCommand] Luz de '{room}' -> {state}");
    }
}
