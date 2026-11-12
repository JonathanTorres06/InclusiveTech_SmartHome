// =============================================================
// Implementacion de respaldo: usa el nombre del enum RoomID
// como internal_name Y como display_name por defecto, sin
// aliases adicionales, mientras el microservicio de navegacion
// (panel web + base de datos) todavia no esta disponible.
// =============================================================

using System.Collections.Generic;
using UnityEngine;

public class DefaultRoomNameProvider : IRoomNameProvider
{
    public Dictionary<string, RoomNameData> GetAllRoomNames()
    {
        var roomNames = new Dictionary<string, RoomNameData>();

        NavigationNode[] sceneNodes = Object.FindObjectsByType<NavigationNode>(FindObjectsSortMode.None);

        foreach (var node in sceneNodes)
        {
            // Los waypoints son puntos de paso internos, nunca
            // deben ser reconocibles por comandos de voz.
            if (node.nodeCategory != NodeCategory.Room)
            {
                continue;
            }

            string key = node.InternalName;

            roomNames[key] = new RoomNameData
            {
                InternalName = key,
                DisplayName = key,
                Aliases = new List<string>(),
            };
        }

        return roomNames;
    }
}
