// =============================================================
// Punto de entrada para ejecutar acciones del hogar recibidas
// del servidor it_ai_assistant.
//
// Responsabilidad: Recibir un AssistantResponse con datos de
// accion, pedirle al Factory el Command correcto y ejecutarlo.
// No conoce los detalles de cada accion individual ya que eso vive
// en cada Command concreto.
//
// Principio S de SOLID de unica responsabilidad: Orquestar la
// ejecucion de la accion correcta. No conoce HTTP ni audio.
//
// Principio D de SOLID: Depende de IHomeActionCommand
// (abstraccion), nunca de las clases concretas directamente.
// =============================================================

using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ejecuta acciones del hogar a partir de una respuesta del asistente.
/// </summary>
public class HomeActionHandler : MonoBehaviour
{
    [Header("Dependencias de escena")]
    [Tooltip("Agente que se mueve por la casa en primera persona")]
    private GraphAgent _graphAgent;

    private HomeActionFactory _actionFactory;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        RefreshFactory();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    /*private void Awake()
    {
        _actionFactory = new HomeActionFactory(_graphAgent);
    }*/

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) 
    {
        RefreshFactory();
    }

    private void RefreshFactory() 
    {
        _graphAgent = FindFirstObjectByType<GraphAgent>();
        _actionFactory = new HomeActionFactory(_graphAgent);
    }

    /// <summary>
    /// Ejecuta la accion del hogar contenida en una respuesta del asistente.
    /// No hace nada si la respuesta no contiene una accion (HasAction es false).
    /// </summary>
    /// <param name="response">Respuesta del asistente con los datos de la accion.</param>
    public void HandleResponse(AssistantResponse response) 
    {
        if (response == null || !response.HasAction)
        {
            return;
        }

        // Si estamos en la escena de login, HouseState.Instance es null,
        // no hay casa que controlar todavia. Fallara de forma segura
        if (HouseState.Instance == null)
        {
            Debug.LogWarning("[HomeActionHandler] Se recibio una accion del hogar pero " +
                "la escena de la casa aun no esta cargada. Se ignorara la orden.");
            return;
        }

        IHomeActionCommand command = _actionFactory.CreateCommand(response.ActionType);

        if (command == null)
        {
            Debug.LogWarning("[HomeActionHandler] No se pudo construir un Command para " +
                "el tipo de accion " + response.ActionType + ".");
            return;
        }

        command.Execute(response.ActionRoom, response.ActionState, response.ActionValue);
    }
}
