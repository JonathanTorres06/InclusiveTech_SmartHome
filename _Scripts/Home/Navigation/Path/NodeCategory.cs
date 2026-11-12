/// <summary>
/// Categoria estructural del nodo: Diferencia un nodo si es un destino con
/// nombre visible para el usuario (Room) o un punto de paso interno
/// del grafo sin relevancia para comandos de voz (Waypoint).
///
/// A diferencia del anterior RoomID, esta categoria NUNCA necesita
/// crecer. El nombre real del nodo vive en internalName (string) y
/// en el backend, no aqui.
/// </summary>
public enum NodeCategory 
{
    Room, // Nodo tipo habitacion de la casa
    Waypoint, // Nodo intermedio de paso que conecta otros nodos
}
