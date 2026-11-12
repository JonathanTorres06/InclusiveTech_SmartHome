// =============================================================
// Comando para mover al agente (usuario en primera persona)
// a una habitacion especifica de la casa.
// =============================================================

using UnityEngine;

/// <summary>
/// Ejecuta el desplazamiento del agente hacia una habitacion.
/// </summary>
public class MoveActionCommand : IHomeActionCommand
{
    private readonly GraphAgent _graphAgent;

    public MoveActionCommand(GraphAgent agent) 
    {
        _graphAgent = agent;
    }

    public void Execute(string room, string state, float? value)
    {
        if (string.IsNullOrEmpty(room))
        {
            Debug.LogWarning("[MoveActionCommand] No se especifico habitacion de destino.");
            return;
        }

        string internalNodeName = HouseState.Instance.roomManager.ResolveNodeName(room);

        if (internalNodeName == null)
        {
            Debug.LogWarning("[MoveActionCommand] No se pudo resolver la habitacion: " + room);
            return;
        }

        _graphAgent.GoToRoom(internalNodeName);
        Debug.Log("[MoveActionCommand] Moviendo agente a " + room + " (RoomID: " + internalNodeName + ")");
    }
}
