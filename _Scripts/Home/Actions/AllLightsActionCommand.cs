// =============================================================
// Comando para encender o apagar todas las luces de la casa.
// =============================================================

using UnityEngine;

public class AllLightsActionCommand : IHomeActionCommand
{
    public void Execute(string room, string state, float? value)
    {
        bool turnOn = state == "ON";
        HouseState.Instance.lightManager.AllLights(turnOn);
        Debug.Log("[AllLightsActionCommand] Todas las luces -> " + state);
    }
}
