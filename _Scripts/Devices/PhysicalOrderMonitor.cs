using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Vigila si la maqueta cumple las ordenes: cada pocos segundos le pregunta al
/// backend por las ordenes RECIENTES que no se cumplieron (el dispositivo no
/// respondio en el plazo del contrato, o quedo en un estado distinto al pedido)
/// y avisa de cada una UNA sola vez.
///
/// Responsabilidad unica: detectar y anunciar. NO decide como mostrarlo: eso
/// lo hace quien se suscriba a OnOrderProblem (un aviso en pantalla, un sonido,
/// una frase de Mia...). Por defecto deja un aviso en la consola.
///
/// Reglas de oro (igual que PhysicalDeviceBridge):
///  - Si el backend esta apagado, se queda en silencio: es un estado normal.
///  - Nunca bloquea ni rompe la simulacion.
///  - Se apaga con NavigationConfig.sendOrdersToPhysicalDevices.
///
/// Va en el mismo objeto que PhysicalDeviceBridge.
/// Mejora futura: cambiar el sondeo (polling) por un WebSocket.
/// </summary>
public class PhysicalOrderMonitor : MonoBehaviour
{
    private const string ProblemsPath = "/user/devices/orders/problems";
    private const int PollTimeoutSeconds = 2;
    private const string OutcomeNoResponse = "no_response";
    private const string OutcomeMismatch = "mismatch";

    [Header("Configuracion")]
    [Tooltip("La misma NavigationConfig que usa el resto del proyecto.")]
    [SerializeField] private NavigationConfig navigationConfig;

    [Tooltip("Cada cuantos segundos se consulta al backend.")]
    [SerializeField, Min(0.5f)] private float pollIntervalSeconds = 2f;

    /// <summary>Se dispara una vez por cada orden que la maqueta no cumplio.</summary>
    public event Action<PhysicalOrderProblem> OnOrderProblem;

    // Los nombres de campo deben ser IGUALES a los del JSON del backend
    [Serializable]
    public class PhysicalOrderProblem
    {
        public int order_id;
        public string device_key;
        public string requested;     // on, off, open, close
        public string outcome;       // no_response, mismatch
        public string reported_state;
        public string detail;

        /// <summary>Frase corta en espanol lista para mostrar o decir.</summary>
        public string FriendlyMessage()
        {
            if (outcome == OutcomeMismatch)
            {
                return $"El dispositivo '{device_key}' quedo en '{reported_state}' en lugar de cumplir la orden '{requested}'.";
            }

            return $"El dispositivo '{device_key}' no respondio a la orden '{requested}'.";
        }
    }

    // JsonUtility no lee arreglos en la raiz, por eso se envuelven en "items"
    [Serializable] private class ProblemList { public PhysicalOrderProblem[] items; }

    private readonly HashSet<int> _alertedOrderIds = new HashSet<int>();
    private Coroutine _pollLoop;

    private void OnEnable()
    {
        _pollLoop = StartCoroutine(PollLoop());
    }

    private void OnDisable()
    {
        if (_pollLoop != null)
        {
            StopCoroutine(_pollLoop);
            _pollLoop = null;
        }
    }

    private IEnumerator PollLoop()
    {
        var wait = new WaitForSecondsRealtime(pollIntervalSeconds);

        while (true)
        {
            if (navigationConfig != null && navigationConfig.sendOrdersToPhysicalDevices)
            {
                yield return PollOnce();
            }

            yield return wait;
        }
    }

    private IEnumerator PollOnce()
    {
        string url = navigationConfig.serverUrl.TrimEnd('/') + ProblemsPath;

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = PollTimeoutSeconds;
            yield return request.SendWebRequest();

            // Backend apagado o sin red: es normal, no se avisa
            if (request.result != UnityWebRequest.Result.Success) { yield break; }

            ProblemList list;

            try
            {
                list = JsonUtility.FromJson<ProblemList>("{\"items\":" + request.downloadHandler.text + "}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PhysicalOrderMonitor] Respuesta inesperada del backend: {ex.Message}");
                yield break;
            }

            if (list == null || list.items == null) { yield break; }

            HandleProblems(list.items);
        }
    }

    private void HandleProblems(PhysicalOrderProblem[] problems)
    {
        var currentIds = new HashSet<int>();

        foreach (PhysicalOrderProblem problem in problems)
        {
            currentIds.Add(problem.order_id);

            // Add devuelve false si ya se aviso de esta orden
            if (!_alertedOrderIds.Add(problem.order_id)) { continue; }

            Debug.LogWarning($"[PhysicalOrderMonitor] {problem.FriendlyMessage()}");
            OnOrderProblem?.Invoke(problem);
        }

        // Un problema que ya no aparece no puede volver: se olvida para no crecer sin limite
        _alertedOrderIds.RemoveWhere(id => !currentIds.Contains(id));
    }
}
