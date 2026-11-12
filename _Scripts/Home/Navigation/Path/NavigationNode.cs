using UnityEngine;
using System.Collections.Generic;

public class NavigationNode : MonoBehaviour
{
    [Header("Configuracion del Nodo")]
    public NodeCategory nodeCategory; // Ej: Waypoint o Room

    [Header("Estado de Registro (no editar manualmente)")]
    [Tooltip("ID asignado por el backend una vez registrado como nuevo nodo. Vacio o -1 " +
         "significa que este nodo aun no existe en el backend.")]
    public int backendNodeId = -1;

    // Dice si este nodo ya fue registrado exitosamente en el backend
    public bool IsRegisteredInBackend => backendNodeId > 0;

    [Header("Identificador Tecnico")]
    [Tooltip("Nombre unico del nodo. Debe coincidir exactamente con " +
        "el internal_name registrado para este nodo en el backend. " +
        "Se normaliza automaticamente a mayusculas al usarse.")]
    public string internalName;

    [Header("Registro Inicial (Solo Editor)")]
    [Tooltip("Nombre visible sugerido al registrar este nodo por primera " +
         "vez en el backend. Despues del registro, el nombre real " +
         "(display_name) se edita desde el panel web, este campo ya " +
         "no tiene efecto.")]
    public string initialDisplayName;

    // Lista de nodos a los que este nodo puede ir directamente
    public List<NavigationNode> neighbors = new List<NavigationNode>();

    #region Identidad del nodo para el backend

    /// <summary>
    /// Nombre tecnico normalizado (internal_name) que este nodo debe
    /// tener en el backend. La normalizacion (mayusculas, sin espacios
    /// extra) espeja exactamente lo que hace Node.create() en el
    /// backend, para que nunca haya un desfase entre ambos.
    /// </summary>
    public string InternalName 
    {
        get 
        {
            if (!string.IsNullOrWhiteSpace(internalName))
            {
                return internalName.Trim().ToUpper();
            }

            // Fallback de seguridad: nunca debe usarse en un nodo bien
            // configurado. Si aparece, significa que falta asignar
            // internalName manualmente en el inspector.
            return $"SIN_NOMBRE_{gameObject.GetInstanceID()}";
        }
    }

    /// <summary>
    /// Tipo de nodo tal como lo espera el backend ("room" o "waypoint").
    /// </summary>
    public string BackendNodeType => nodeCategory == NodeCategory.Room ? "room" : "waypoint";
    
    /// <summary>True si el nodo es una habitacion (destino con nombre), false si es un punto de paso.</summary>
    public bool IsRoom => nodeCategory == NodeCategory.Room;

    #endregion

    #region Gizmos

    // Para visualizar en el editor
    private void OnDrawGizmos()
    {
        // Si es un nodo de paso (None), es mas pequenio y de otro color
        if (nodeCategory == NodeCategory.Waypoint)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawSphere(transform.position, 0.08f); // Nodo mas pequenio
        }
        else 
        {
            Gizmos.color = Color.hotPink;
            Gizmos.DrawSphere(transform.position, 0.15f); // Nodo mas grande. Indica un destino final
        }

        // Dibujar las conexiones
        foreach (var neighbor in neighbors)
        {
            if (neighbor != null)
            {
                // Purpura si es ida y vuelta, Rojo si es un solo sentido
                Gizmos.color = neighbor.neighbors.Contains(this) ? Color.purple : Color.red;
                Gizmos.DrawLine(transform.position, neighbor.transform.position);
            }
        }
    }

    #endregion

    // Metodo encapsulado para agregar vecinos y evitar duplicados
    public void AddNeighbor(NavigationNode neighbor) 
    {
        if (neighbor != null && neighbor != this && !neighbors.Contains(neighbor))
        {
            neighbors.Add(neighbor);
        }
    }

    // Metodo para eliminar una conexion entre nodos
    public void RemoveNeighbor(NavigationNode neighbor) 
    {
        if (neighbors.Contains(neighbor))
        {
            neighbors.Remove(neighbor);
        }
    }
}
