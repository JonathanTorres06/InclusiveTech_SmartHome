using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class RouteFinder 
{
    // Algoritmo A*
    public static List<NavigationNode> FindPath(NavigationNode start, NavigationNode target) 
    {
        // La Open List, seran los nodos por visitar
        // En A*, se empieza con el nodo de inicio
        List<NavigationNode> openList = new List<NavigationNode>();
        openList.Add(start);

        // Diccionarios para guardar los costos (G)
        // gScore: Sera la distancia entre el nodo actual hacia su nodo vecino. Es el costo real de moverse del nodo actual, al nodo siguiente
        Dictionary<NavigationNode, float> gScore = new Dictionary<NavigationNode, float>();
        gScore[start] = 0f;

        // fScore: gScore + Distancia en linea "recta" hacia el destino (Heuristica)
        Dictionary<NavigationNode, float> fScore = new Dictionary<NavigationNode, float>();
        fScore[start] = Vector3.Distance(start.transform.position, target.transform.position);

        // Diccionario para reconstruir el camino (Como migajas de pan en el suelo)
        Dictionary<NavigationNode, NavigationNode> cameFrom = new Dictionary<NavigationNode, NavigationNode>();
        cameFrom[start] = null;

        while (openList.Count > 0)
        {
            // OPTIMIZACION IMPORTANTE: En lugar de sacar el primer nodo,
            // sacamos el que tenga el fScore mas bajo (este valor iria indicando el camino que mas probablemente promete)
            NavigationNode current = openList.OrderBy(node => fScore.ContainsKey(node) ? fScore[node] : float.MaxValue).First();

            if (current == target) { break; } // Se encontro la ruta mas optima

            openList.Remove(current);

            foreach (NavigationNode neighbor in current.neighbors)
            {
                // Se calcula un gScore tentativo para el vecino
                // Distancia acumulada + distancia entre el actual y su vecino
                // f = g + h
                float tentativeGScore = gScore[current] + Vector3.Distance(current.transform.position, neighbor.transform.position);

                // Si no hemos visitado al vecino o encontramos un camino mas corto hacia el
                // Aqui se cumple la funcion de la ClosedList
                if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = gScore[neighbor] + Vector3.Distance(neighbor.transform.position, target.transform.position);

                    if (!openList.Contains(neighbor))
                    {
                        openList.Add(neighbor);
                    }
                }
            }
        }

        // Reconstruccion del camino ya que actualmente esta calculado de fin a inicio
        // y para que empiece bien habra que darle la vuelta al camino para que sea de inicio a fin
        List<NavigationNode> path = new List<NavigationNode>();
        NavigationNode temp = target;

        while (temp != null)
        {
            path.Add(temp);
            temp = cameFrom.ContainsKey(temp) ? cameFrom[temp] : null;
        }

        path.Reverse();
        return path;
    }
}
