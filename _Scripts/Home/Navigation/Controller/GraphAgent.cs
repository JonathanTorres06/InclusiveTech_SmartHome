using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class GraphAgent : MonoBehaviour
{
    #region Variables y configuracion

    [Header("Nodo inicial")]
    [Tooltip("Nodo inicial del grafo desde donde siempre empezara el agente a caminar por la casa")]
    public NavigationNode currentNode;
    private NavMeshAgent navAgent;

    [Header("Ajustes de Rotacion")]
    [Tooltip("Giro suave sobre el eje Y. Se usa al arrancar, en las esquinas, frente a las puertas y al llegar")]
    [SerializeField] private SmoothYawRotator rotator = new SmoothYawRotator();

    [Tooltip("Al llegar a una habitacion, el agente se orienta hacia donde apunta el eje azul (forward) de ese nodo")]
    [SerializeField] private bool alignToRoomOnArrival = true;

    [Tooltip("Velocidad (porcentaje) en un giro cerrado, para que no derrape en las esquinas. Solo movimiento manual")]
    [Range(0.1f, 1f)]
    [SerializeField] private float cornerSpeedFactor = 0.4f;

    [Header("Ajustes de Fluidez")]
    [Tooltip("Distancia a la que el agente pedira el siguiente nodo para no frenar (Solo NavMesh)")]
    [Range(0.5f, 3)] public float waypointTreshold = 1.2f;

    [Header("Movimiento Manual (No NavMesh)")]
    [Tooltip("Usese cuando se vaya a implementar el sistema de path sin usar Nav Mesh")]
    [Range(1, 50)] public float speed = 3.0f; // Velocidad de movimiento del agente
    [Range(0, 2)] public float arrivalDistance = 0.15f; // Umbral de llegada

    // Direcciones mas cortas que esto (1 cm) se ignoran al calcular un giro
    private const float MIN_DIRECTION_SQR = 0.0001f;

    // Desde este angulo de giro el agente avanza solo a cornerSpeedFactor
    private const float SHARP_TURN_ANGLE = 90f;

    // Estado interno
    private List<NavigationNode> currentPath = new List<NavigationNode>();
    private int pathIndex = 0;

    private bool isRotatingBeforeMove = false; // Bloquea el movimiento mientras gira
    private Coroutine rotationRoutine;         // Unica rotacion "por orden" activa (arranque, puerta o llegada)

    #endregion

    #region Estado publico

    /// <summary>
    /// True mientras el agente tiene una ruta en curso o esta girando por una orden
    /// (arranque, puerta u orientacion de llegada). Pasa a false solo cuando ya quedo
    /// quieto y mirando hacia donde apunta la habitacion de destino.
    /// </summary>
    public bool IsBusy => (currentPath != null && currentPath.Count > 0) || rotationRoutine != null;

    #endregion

    #region Unity Mehods

    void Start()
    {
        navAgent = GetComponent<NavMeshAgent>();

        // Si no usamos NavMesh, lo apagamos para que no interfiera
        if (navAgent != null && GraphManager.Instance.noNavMesh)
        {
            navAgent.enabled = false;
        }
    }

    void Update()
    {
        // // No hacer nada si no hay ruta, o si todavia estamos en la fase de rotacion inicial
        if (currentPath == null || currentPath.Count == 0 || pathIndex >= currentPath.Count || isRotatingBeforeMove)
        {
            return;
        }

        if (GraphManager.Instance.noNavMesh)
        {
            HandleManualMovement();
        }
        else
        {
            HandleNavMeshMovement();
        }
    }

    #endregion

    #region Logica de Entrada

    /// <summary>
    /// Metodo principal para enviar al agente a una habitacion
    /// Funciona tanto para NavMesh como para el movimiento manual
    /// </summary>
    public void GoToRoom(string nodeName)
    {
        NavigationNode target = GraphManager.Instance.GetNodeByName(nodeName);
        if (target == null) { return; }

        currentPath = GraphManager.Instance.GetRoute(currentNode, target);
        pathIndex = 0;

        if (currentPath == null || currentPath.Count == 0) { return; }

        Vector3 lookPos = (currentPath.Count > 1) ? currentPath[1].transform.position : currentPath[0].transform.position;
        RunRotation(SequenceNaturalProcess(lookPos));
    }

    #endregion

    #region Secuencia de preparacion del movimiento

    // Secuencia natural de la locomocion del agente al empezar a moverse desde el punto inicial
    IEnumerator SequenceNaturalProcess(Vector3 lookAtRotPosition)
    {
        isRotatingBeforeMove = true;
        ToggleLocomotion(false);

        // Esperar a que termine el giro suave hacia el primer nodo
        yield return RotateToFace(lookAtRotPosition);

        isRotatingBeforeMove = false;
        ToggleLocomotion(true);
    }

    // Controlar el movimiento del agente dependiendo de lo que lo afecte
    public void ModifyAgentLocomotion(Vector3 lookAtRotPosition, bool pauseLocomotion)
    {
        if (pauseLocomotion)
        {
            isRotatingBeforeMove = true;

            // Desactivar el movimiento del player
            ToggleLocomotion(false);

            // Girar (suavemente) hacia el punto indicado, por ejemplo la puerta
            RunRotation(RotateToFace(lookAtRotPosition));
        }
        else
        {
            // Si el giro hacia la puerta no termino, se corta para que no pelee con el movimiento
            StopRotation();

            // Activar el movimiento del agente en su componente nav mesh
            isRotatingBeforeMove = false;
            ToggleLocomotion(true);
        }
    }

    public void ToggleLocomotion(bool active)
    {
        if (GraphManager.Instance.noNavMesh)
        {
            // En manual, se controla si el update puede mover o no
        }
        else if (navAgent != null && navAgent.isActiveAndEnabled)
        {
            navAgent.isStopped = !active;
            navAgent.updateRotation = active;
        }
    }

    #endregion

    #region Rotaciones

    // Reemplaza la rotacion en curso por una nueva. Asi nunca hay dos giros peleando por el mismo transform
    private void RunRotation(IEnumerator routine)
    {
        StopRotation();
        rotationRoutine = StartCoroutine(TrackRotation(routine));
    }

    // Envuelve la rotacion para marcarla como terminada al acabar (lo usa IsBusy)
    private IEnumerator TrackRotation(IEnumerator routine)
    {
        yield return routine;
        rotationRoutine = null;
    }

    private void StopRotation()
    {
        if (rotationRoutine == null) { return; }

        StopCoroutine(rotationRoutine);
        rotationRoutine = null;
    }

    // Gira suavemente para quedar mirando a una posicion del mundo
    private IEnumerator RotateToFace(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < MIN_DIRECTION_SQR) { yield break; }

        yield return RotateToYaw(SmoothYawRotator.YawOf(direction));
    }

    // Gira suavemente hasta un angulo concreto sobre el eje Y
    private IEnumerator RotateToYaw(float targetYaw)
    {
        rotator.Reset();

        while (!rotator.Step(transform, targetYaw))
        {
            yield return null;
        }
    }

    // Al llegar a una habitacion, el agente queda mirando hacia donde apunta el nodo
    private IEnumerator AlignToNode(NavigationNode node)
    {
        Vector3 facing = node.transform.forward;
        facing.y = 0f;

        if (facing.sqrMagnitude < MIN_DIRECTION_SQR) { yield break; }

        // El NavMeshAgent no debe rotar por su cuenta mientras se orienta.
        // Se devuelve el control en el siguiente ToggleLocomotion(true)
        SetNavMeshAutoRotation(false);

        yield return RotateToYaw(SmoothYawRotator.YawOf(facing));
    }

    private void SetNavMeshAutoRotation(bool active)
    {
        if (!GraphManager.Instance.noNavMesh && navAgent != null && navAgent.isActiveAndEnabled)
        {
            navAgent.updateRotation = active;
        }
    }

    // Gira hacia la direccion de marcha y devuelve cuanto puede avanzar (0.4 a 1)
    private float SteerTowards(Vector3 direction)
    {
        if (direction.sqrMagnitude < MIN_DIRECTION_SQR) { return 1f; }

        float targetYaw = SmoothYawRotator.YawOf(direction);
        rotator.Step(transform, targetYaw);

        float sharpness = Mathf.Clamp01(rotator.AngleTo(transform, targetYaw) / SHARP_TURN_ANGLE);
        return Mathf.Lerp(1f, cornerSpeedFactor, sharpness);
    }

    #endregion

    #region Ejecucion de Movimiento

    private void HandleManualMovement()
    {
        Vector3 targetPos = currentPath[pathIndex].transform.position;
        targetPos.y = transform.position.y;

        // Rotacion suave hacia el nodo actual; en giros cerrados avanza mas despacio
        float speedFactor = SteerTowards(targetPos - transform.position);

        // Movimiento
        transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * speedFactor * Time.deltaTime);

        // Verificacion de llegada optimizada con sqrMagnitude
        if ((targetPos - transform.position).sqrMagnitude < arrivalDistance * arrivalDistance)
        {
            CompleteNodeStep();
        }
    }

    private void HandleNavMeshMovement()
    {
        // Es este el ultimo nodo de la ruta?
        bool isLastNode = (pathIndex >= currentPath.Count - 1);

        // Si no es el ultimo nodo, se usa el umbral de anticipacion para mantener la inercia del movimiento
        // Si es el ultimo, se usa el stoppingDistance original del Nav Mesh para una desaceleracion precisa
        float effectiveTreshold = isLastNode ? (navAgent.stoppingDistance + 0.1f) : waypointTreshold;

        if (!navAgent.pathPending && navAgent.remainingDistance <= effectiveTreshold)
        {
            CompleteNodeStep();
        }
    }

    private void MoveToNextNode()
    {
        if (navAgent != null && navAgent.isActiveAndEnabled && pathIndex < currentPath.Count)
        {
            navAgent.SetDestination(currentPath[pathIndex].transform.position);
        }
    }

    private void CompleteNodeStep()
    {
        currentNode = currentPath[pathIndex];

        if (pathIndex >= currentPath.Count - 1)
        {
            Debug.Log($"<color=green>Destino Final:</color> {currentNode.InternalName}");
            currentPath.Clear();
            pathIndex = 0;

            if (alignToRoomOnArrival && currentNode.IsRoom)
            {
                RunRotation(AlignToNode(currentNode));
            }
        }
        else
        {
            pathIndex++;
            if (!GraphManager.Instance.noNavMesh) { MoveToNextNode(); }
        }
    }

    #endregion
}