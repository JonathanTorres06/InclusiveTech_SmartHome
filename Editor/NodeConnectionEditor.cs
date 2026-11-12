#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NavigationNode))]
[CanEditMultipleObjects]
public class NodeConnectionEditor : Editor
{
    // Variable estatica para que el estado se mantenga de forma global en el editor
    private static bool snapToFloor = false;
    private static string floorTag = "Floor";
    private static float raycastDistance = 100f;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        NavigationNode currentNode = (NavigationNode)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Configuracion de Edicion", EditorStyles.boldLabel);

        // Saber si pegar o no pegar los nodos
        snapToFloor = EditorGUILayout.Toggle("Pegar al suelo (Snap)", snapToFloor);
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Herramientas de Conexion", EditorStyles.boldLabel);

        // Boton para conectar nodos seleccionados
        if (GUILayout.Button("Conectar con Seleccionados (Bidireccional)"))
        {
            ConnectSelectedNodes(currentNode);
        }

        if (GUILayout.Button("Limpiar Conexiones Nulas"))
        {
            currentNode.neighbors.RemoveAll(item => item == null);
            EditorUtility.SetDirty(currentNode);
        }

        if (GUILayout.Button("Extender Nodo (Duplicar y Conectar)"))
        {
            ExtendNode(currentNode);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Backend - Microservicio de Navegacion", EditorStyles.boldLabel);

        if (currentNode.IsRegisteredInBackend)
        {
            EditorGUILayout.HelpBox(
                $"Este nodo ya esta registrado en el backend. ID: {currentNode.backendNodeId}",
                MessageType.Info
            );

            if (GUILayout.Button("Eliminar del Backend"))
            {
                int idToDelete = currentNode.backendNodeId;

                NodeRegistrationClient.DeleteNode(
                    idToDelete,
                    onSuccess: () =>
                    {
                        Debug.Log($"<color=orange>Nodo eliminado del backend.</color> ID: {idToDelete}");

                        Undo.RecordObject(currentNode, "Unregister Node");
                        currentNode.backendNodeId = -1;
                        EditorUtility.SetDirty(currentNode);
                    },
                    onError: (message) =>
                    {
                        Debug.LogError($"[NodeConnectionEditor] Error al eliminar el nodo: {message}");
                    }
                );
            }
        }
        else if (string.IsNullOrWhiteSpace(currentNode.internalName))
        {
            EditorGUILayout.HelpBox(
                "Este nodo no tiene un internalName asignado. Asignalo antes de registrarlo en el backend.",
                MessageType.Warning
            );
        }
        else if (GUILayout.Button("Registrar Nodo en Backend"))
        {
            NavigationNode nodeToRegister = currentNode;

            NodeRegistrationClient.RegisterNode(
                nodeToRegister,
                onSuccess: (id) =>
                {
                    Debug.Log($"<color=green>Nodo registrado correctamente.</color> " +
                              $"ID backend: {id}, internal_name: {nodeToRegister.InternalName}");

                    Undo.RecordObject(nodeToRegister, "Register Node");
                    nodeToRegister.backendNodeId = id;
                    EditorUtility.SetDirty(nodeToRegister);
                },
                onError: (message) =>
                {
                    Debug.LogError($"[NodeConnectionEditor] Error al registrar el nodo: {message}");
                }
            );
        }
    }

    // Esta funcion se ejecuta mientras se manipulan objetos en la vista de la Escena
    private void OnSceneGUI()
    {
        if (!snapToFloor)
        {
            return;
        }

        NavigationNode node = (NavigationNode)target;

        // Detectar si el nodo se esta moviendo en la escena
        if (Event.current.type == EventType.MouseDrag || Event.current.type == EventType.MouseMove)
        {
            SnapNodeToFloor(node);
        }
    }

    private void ConnectSelectedNodes(NavigationNode mainNode) 
    {
        foreach (GameObject obj in Selection.gameObjects)
        {
            NavigationNode otherNode = obj.GetComponent<NavigationNode>();

            if (otherNode != null && otherNode != mainNode)
            {
                // Registro de la operacion para poder usar Ctrl+Z
                Undo.RecordObject(mainNode, "Connect Nodes");
                Undo.RecordObject(otherNode, "Connect Nodes");

                mainNode.AddNeighbor(otherNode);
                otherNode.AddNeighbor(mainNode);
            }
        }
    }

    private void ExtendNode(NavigationNode currentNode) 
    {
        // Duplicar el objeto actual
        GameObject newObj = Instantiate(currentNode.gameObject, currentNode.transform.parent);
        newObj.name = "NavigationNode_Link";

        NavigationNode newNode = newObj.GetComponent<NavigationNode>();

        // Limpiar vecinos del nuevo para que no herede los del anterior
        newNode.neighbors.Clear();

        // Un nodo extendido siempre nace como waypoint: Es un punto de paso,
        // no un destino con nombre. Su internalName queda vacio a proposito
        //para forzar que se asigne manualmente antes de registralo.
        newNode.nodeCategory = NodeCategory.Waypoint; // Por defecto nodo de paso
        newNode.internalName = "";

        // Colocar conexion bidireccional
        currentNode.AddNeighbor(newNode);
        newNode.AddNeighbor(currentNode);

        // Si el snap esta activo, el nodo se pegara al suelo inmediatamente despues de ser creado
        if (snapToFloor)
        {
            SnapNodeToFloor(newNode);
        }

        // Seleccionar el nuevo para seguir extendiendo
        Selection.activeGameObject = newObj;
        Undo.RegisterCreatedObjectUndo(newObj, "Extend Node");
    }

    private void SnapNodeToFloor(NavigationNode node) 
    {
        // Lanzamos un rayo hacia abajo desde un poco mas arriba del nodo
        Ray ray = new Ray(node.transform.position + Vector3.up * 2.0f, Vector3.down);
        RaycastHit hit;

        // Solo detectara objetos que tengan un collider (el suelo)
        if (Physics.Raycast(ray, out hit, raycastDistance))
        {
            // Verificar esi el objeto impactado tiene el Tag que se le indica
            if (string.IsNullOrEmpty(floorTag) || hit.collider.CompareTag(floorTag))
            {
                // Registramos el cambio para poder deshacer con Ctrl+Z
                Undo.RecordObject(node.transform, "Snap to Floor");
                
                // Posicion con un pequenio offset para que no quede exactamente enterrado en el poligono
                node.transform.position = hit.point + Vector3.up * 0.05f;
            }
        }
    }

    // Atajo de teclado: Si presionas Ctrl+L teniendo nodos seleccionados, estos se conectan
    [MenuItem("Tools/Navegación/Conectar Nodos Seleccionados %l")] // Ctrl + L para conectar dos o mas nodos
    private static void LinkNodesShortcut() 
    {
        if (Selection.gameObjects.Length < 2) { return; }

        foreach (var outerGo in Selection.gameObjects)
        {
            var outerNode = outerGo.GetComponent<NavigationNode>();
            if (outerNode == null) { continue; }

            foreach (var innerGo in Selection.gameObjects)
            {
                var innerNode = innerGo.GetComponent<NavigationNode>();

                if (innerNode != null && innerNode != outerNode)
                {
                    Undo.RecordObject(outerNode, "Quick Link");
                    outerNode.AddNeighbor(innerNode);
                }
            }
        }
    }

    // Atajo de teclado: Si presionas Ctrl+G, se crea un GameOject nuevo de tipo nodo
    [MenuItem("Tools/Navegación/Crear Nodo Nuevo %g")] // Ctrl + G para crear
    public static void CreateNewNode() 
    {
        GameObject newNode = new GameObject("New_Navigation_Node");
        NavigationNode nodeComponent = newNode.AddComponent<NavigationNode>();

        // Igual que en ExtendNode, por defecto el nodo se crea desde 0 como waypooint,
        // ya que es el caso mas comun al crear nodos rapidamente
        // mientras se traza un camino en la escena
        nodeComponent.nodeCategory = NodeCategory.Waypoint;

        // Posicionar el nodo frente a la camara de la escena para que no aparezca en el origen (0,0,0)
        if (SceneView.lastActiveSceneView != null)
        {
            Transform camTransform = SceneView.lastActiveSceneView.camera.transform;
            newNode.transform.position = camTransform.position + camTransform.forward * 2.0f;
        }

        Selection.activeGameObject = newNode;
        Undo.RegisterCreatedObjectUndo(newNode, "Create Node");
    }
}
#endif
