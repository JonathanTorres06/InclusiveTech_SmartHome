using UnityEngine;

/// <summary>
/// Herramienta temporal de QA para el Paso de prueba de microservicio de navegacion:
/// Backend -> Panel Web -> Unity.
/// Simula un comando de voz ya transcrito ("lo que el usuario dijo") sin depender del
/// microservicio de asistente del hogar (STT/LLM), que aun esta en desarrollo
/// por separado. Ejercita la misma cadena que usara el asistente
/// real: RoomManager.ResolveNodeName -> GraphAgent.GoToRoom.
/// </summary>
public class Paso9EndToEndTester : MonoBehaviour
{
    [Header("Referencias")]
    public RoomManager roomManager;
    public GraphAgent graphAgent;

    [Header("Prueba")]
    [Tooltip("Texto que simula lo que el usuario 'dijo'. Prueba con " +
             "el display_name o un alias configurado desde el panel web.")]
    public string testSpokenText;

    [ContextMenu("Probar Comando de Voz (Simulado)")]
    public void RunTest() 
    {
        if (roomManager == null || graphAgent == null)
        {
            Debug.LogError("[Paso9EndToEndTester] Falta asignar RoomManager o GraphAgent en el inspector");
            return;
        }

        Debug.Log($"<color=cyan>[Prueba]</color> Texto simulado: \"{testSpokenText}\"");

        string internalName = roomManager.ResolveNodeName(testSpokenText);

        if (internalName == null)
        {
            Debug.LogError($"[Prueba] No se reconocio ninguna habitacion para: \"{testSpokenText}\". " +
                $"Verifica el display_name/alias en el panel web.");
            return;
        }

        Debug.Log($"<color=green>[Prueba] Resuelto correctamente</color> -> internal_name: {internalName}");
        graphAgent.GoToRoom(internalName);
    }
}
