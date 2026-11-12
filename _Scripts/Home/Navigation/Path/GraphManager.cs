using System.Collections.Generic;
using UnityEngine;

public class GraphManager : MonoBehaviour
{
    [Header("Without Navmesh")]
    [Tooltip("Usese cuando se vaya a implementar el sistema de path sin usar Nav Mesh")]
    public bool noNavMesh;

    // Instancia estatica (Singleton) para acceder facil desde cualquier otro script
    public static GraphManager Instance { get; private set; }

    // Diccionario para busqueda rapida por nombre tecnico (internalName)
    // La clave siempre esta normalizada en mayusculas
    private Dictionary<string, NavigationNode> nodeDictionary = new Dictionary<string, NavigationNode>();

    private void Awake()
    {
        // Configuracion del singleton
        if (Instance == null)
        {
            Instance = this;
        }
        else 
        {
            Destroy(gameObject);
        }

        RegisterAllNodes();
    }

    private void RegisterAllNodes() 
    {
        // Buscamos todos los nodos en la escena
        NavigationNode[] nodes = FindObjectsByType<NavigationNode>(FindObjectsSortMode.None);

        foreach (var node in nodes)
        {
            string key = node.InternalName;

            if (nodeDictionary.ContainsKey(key))
            {
                Debug.LogWarning("Nombre interno duplicado: " + key + ". El nodo en " +
                    node.transform.position + " sobrescribira al anterior " +
                    "registrado con el mismo nombre. Revisa el Inspector.");
            }

            nodeDictionary[key] = node;
        }
    }

    /// <summary>
    /// Calcula y devuelve la ruta mas corta usando A*
    /// </summary>
    public List<NavigationNode> GetRoute(NavigationNode start, NavigationNode target) 
    {
        if (start == null || target == null) { return null; }

        // Se calcula la ruta mas optima en tiempo real
        return RouteFinder.FindPath(start, target);
    }

    /// <summary>
    /// Busca un nodo por su nombre tecnico (internalName), normalizando
    /// el texto de entrada de la misma forma que lo hace el backend.
    ///
    /// </summary>
    /// <param name="nodeName">Nombre a resolver, tal como llega desde
    /// el comando de voz o desde codigo (no necesita venir normalizado).</param>
    public NavigationNode GetNodeByName(string nodeName) 
    {
        if (string.IsNullOrWhiteSpace(nodeName))
        {
            return null;
        }

        string normalized = nodeName.Trim().ToUpper();

        if (nodeDictionary.TryGetValue(normalized, out NavigationNode node))
        {
            return node;
        }

        Debug.LogWarning("No se encontro ningun nodo con el nombre: " + nodeName + ".");
        return null;
    }

    [ContextMenu("Validar Grafo")]
    public void ValidateGraph()
    {
        NavigationNode[] nodes = FindObjectsByType<NavigationNode>(FindObjectsSortMode.None);
        int warnings = 0;

        foreach (var node in nodes)
        {
            if (node.neighbors.Count == 0)
            {
                Debug.Log("El Nodo en " + node.transform.position + " esta aislado. No tiene conexiones. " + node);
                warnings++;
            }
        }

        Debug.Log("<color=cyan>Validacion completa:</color>" + warnings + " problemas encontrados.");
    }
}
