using System.Collections;
using DoorScripting;
using UnityEngine;
using UnityEngine.AI;

public class DoorDetection : MonoBehaviour
{
    [SerializeField] WoodDoor doorMovement;
    [SerializeField] float cooldownAfterExit = 2.0f; // Tiempo para poder volver a detectar al agente

    private bool isProcessing = false; // Bloquear multiples entrada simultaneas
    private bool canTrigger = true; // Contola el cooldown despues de salir

    // Puerta de la maqueta asociada (vive en el mismo objeto que WoodDoor). Puede no existir.
    private DoorController doorController;

    private void Awake()
    {
        if (doorMovement != null)
        {
            doorController = doorMovement.GetComponent<DoorController>();
        }

        if (doorController == null)
        {
            Debug.LogWarning($"[DoorDetection] '{name}': la puerta no tiene DoorController. " + "Funciona en Unity, pero no se movera en la maqueta.");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player") && !isProcessing && canTrigger)
        {
            // Si la puerta ya esta abierta, no detenemos al agente. Esto evita giros raros
            if (doorMovement.open) { return; }

            GraphAgent graphAgent = other.gameObject.GetComponent<GraphAgent>();

            if (graphAgent == null)
            {
                Debug.LogWarning("Error al capturar la referencia de GraphAgent");
                return;
            }

            StartCoroutine(OpenDoorRoutine(graphAgent));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            StartCoroutine(CloseDoorRoutine());
        }
    }

    IEnumerator OpenDoorRoutine(GraphAgent graphAgent) 
    {
        isProcessing = true;

        // Detenemos y rotamos hacia la puerta
        graphAgent.ModifyAgentLocomotion(this.transform.position, true);

        yield return new WaitForSeconds(0.5f);

        doorMovement.open = true;

        // Avisa a la maqueta (sin esperar respuesta): la puerta fisica tambien se abre
        if (doorController != null) { doorController.NotifyPhysicalOpen(); }

        // Esperar hasta que la puerta termine de abrirse
        yield return new WaitForSeconds(doorMovement.smooth + .5f);

        // Reanudamos el movimiento
        graphAgent.ModifyAgentLocomotion(Vector3.zero, false);

        isProcessing = false;
    }

    IEnumerator CloseDoorRoutine() 
    {
        // Al salir, bloqueamos el trigger por un momento
        canTrigger = false;

        yield return new WaitForSeconds(1f);

        bool wasOpen = doorMovement.open;

        doorMovement.open = false;

        if (wasOpen && doorController != null) { doorController.NotifyPhysicalClose(); }

        // Esperamos el tiempo de cooldown para reactivar la deteccion
        yield return new WaitForSeconds(cooldownAfterExit);

        canTrigger = true;
    }
}
