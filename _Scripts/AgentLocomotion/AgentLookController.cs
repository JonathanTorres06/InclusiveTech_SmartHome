using UnityEngine;

/// <summary>
/// Deja girar la camara con el mouse como en primera persona (izquierda y derecha,
/// arriba y abajo), pero solo cuando el agente ya llego a su destino y quedo orientado.
///
/// Va en la camara, que es hija del agente. El giro se guarda como un desvio LOCAL
/// respecto a la orientacion original de la camara: asi, cuando el agente gira solo,
/// la camara gira con el y el desvio no se mezcla con esa rotacion.
///
/// Reglas de la mirada:
///  - Agente quieto (GraphAgent.IsBusy == false): el mouse mueve el desvio.
///  - Agente ocupado (nuevo destino, giro previo, camino, orientacion final):
///    el desvio vuelve suavemente a 0 y el mouse se ignora.
///
/// Reglas del cursor (se esconde y bloquea solo mientras se esta girando la vista):
///  - requireRightMouseButton activo: se esconde mientras se mantiene el clic derecho.
///  - requireRightMouseButton apagado: se esconde siempre que el agente este quieto.
///    Escape lo devuelve y desactiva la mirada; un clic izquierdo la reactiva.
///
/// Analogia: Es el cuello de una persona sentada en un carro. Puede mirar a los
/// lados, arriba y abajo cuando el carro esta parado; cuando el carro arranca,
/// vuelve a mirar al frente.
/// </summary>
public class AgentLookController : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Si se deja vacio se busca en los objetos padre")]
    [SerializeField] private GraphAgent agent;

    [Header("Mirada libre")]
    [Tooltip("Grados que gira por cada unidad de movimiento del mouse")]
    [Range(0.5f, 10f)]
    [SerializeField] private float sensitivity = 3f;

    [Tooltip("Si se activa, mover el mouse hacia arriba mira hacia abajo (estilo avion)")]
    [SerializeField] private bool invertVertical = false;

    [Tooltip("Si se activa, solo gira (y esconde el cursor) mientras se mantiene el clic derecho. " +
             "Si no, gira siempre; Escape suelta el cursor y un clic izquierdo lo recupera")]
    [SerializeField] private bool requireRightMouseButton = false;

    [Header("Limites de giro")]
    [Tooltip("Giro maximo hacia cada lado respecto al frente del agente")]
    [Range(30f, 180f)]
    [SerializeField] private float maxYawAngle = 180f;

    [Tooltip("Tope al mirar hacia arriba, en grados")]
    [Range(0f, 89f)]
    [SerializeField] private float maxLookUpAngle = 60f;

    [Tooltip("Tope al mirar hacia abajo, en grados")]
    [Range(0f, 89f)]
    [SerializeField] private float maxLookDownAngle = 45f;

    [Header("Suavizado")]
    [Tooltip("Que tan suave sigue la camara al mouse. Mas alto = mas lenta y suave")]
    [Range(0.02f, 0.5f)]
    [SerializeField] private float lookSmoothTime = 0.08f;

    [Tooltip("Tiempo que tarda en volver al frente cuando el agente recibe un nuevo destino")]
    [Range(0.1f, 1f)]
    [SerializeField] private float recenterSmoothTime = 0.35f;

    private ILookInput _input;
    private LookCursor _cursor;
    private bool _freeLookEnabled = true; // Solo se usa con requireRightMouseButton apagado
    private bool _wasLooking;
    private Quaternion _baseLocalRotation; // Orientacion original de la camara

    // Desvios en grados respecto a la orientacion original. Pitch positivo = mirar hacia abajo
    private float _targetYaw, _currentYaw, _yawVelocity;
    private float _targetPitch, _currentPitch, _pitchVelocity;

    private void Awake()
    {
        if (agent == null) { agent = GetComponentInParent<GraphAgent>(); }

        if (agent == null)
        {
            Debug.LogWarning("[AgentLookController] No se encontro un GraphAgent en los padres. La camara no girara.");
        }

        _baseLocalRotation = transform.localRotation;
        _input = new MouseLookInput();
        _cursor = new LookCursor();
    }

    private void OnDisable()
    {
        // Al cerrar sesion o cambiar de escena el cursor debe quedar visible
        _cursor?.SetCaptured(false);
        _wasLooking = false;
    }

    private void LateUpdate()
    {
        UpdateFreeLookToggle();

        bool canLook = agent != null && !agent.IsBusy;
        bool looking = canLook && IsLookRequested();

        _cursor.SetCaptured(looking);

        if (!canLook)
        {
            // Agente en movimiento o girando: la vista vuelve al frente
            _targetYaw = 0f;
            _targetPitch = 0f;
        }
        else if (looking && _wasLooking)
        {
            // El primer frame tras capturar el cursor se descarta: trae un salto falso
            AccumulateLook(_input.ReadDelta());
        }

        _wasLooking = looking;

        float smoothTime = canLook ? lookSmoothTime : recenterSmoothTime;
        _currentYaw = Mathf.SmoothDampAngle(_currentYaw, _targetYaw, ref _yawVelocity, smoothTime);
        _currentPitch = Mathf.SmoothDampAngle(_currentPitch, _targetPitch, ref _pitchVelocity, smoothTime);

        ApplyRotation();
    }

    // Con el clic derecho como requisito, mirar es mantenerlo. Si no, mirar es el modo por defecto
    private bool IsLookRequested()
    {
        return requireRightMouseButton ? _input.IsLookButtonHeld : _freeLookEnabled;
    }

    // Escape suelta la mirada; un clic izquierdo la recupera. Solo aplica sin el requisito del clic derecho
    private void UpdateFreeLookToggle()
    {
        if (requireRightMouseButton) { return; }

        if (_input.ReleaseCursorPressed) { _freeLookEnabled = false; }
        else if (_input.CaptureCursorPressed) { _freeLookEnabled = true; }
    }

    // Suma el movimiento del mouse a los desvios y respeta los topes del Inspector
    private void AccumulateLook(Vector2 mouseDelta)
    {
        float verticalSign = invertVertical ? 1f : -1f; // Mouse arriba = pitch negativo = mirar arriba

        _targetYaw = Mathf.Clamp(_targetYaw + mouseDelta.x * sensitivity, -maxYawAngle, maxYawAngle);
        _targetPitch = Mathf.Clamp(_targetPitch + mouseDelta.y * sensitivity * verticalSign, -maxLookUpAngle, maxLookDownAngle);
    }

    // Yaw sobre el eje vertical del agente y pitch sobre el eje lateral de la camara
    private void ApplyRotation()
    {
        transform.localRotation = Quaternion.AngleAxis(_currentYaw, Vector3.up)
                                  * _baseLocalRotation
                                  * Quaternion.AngleAxis(_currentPitch, Vector3.right);
    }
}