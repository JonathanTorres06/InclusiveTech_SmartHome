using DoorScripting;
using UnityEngine;

/// <summary>
/// Identidad y comandos externos de una puerta. Va en el MISMO objeto que
/// tiene WoodDoor. Se deja WoodDoor intacto a proposito: si el
/// asset se actualiza, no se pierde ningun cambio nuestro.
///
/// Aporta:
///  - deviceKey: el id que usa el contrato MQTT (se elige de la lista).
///  - SetOpen(): abrir o cerrar la puerta virtual desde codigo.
///  - NotifyPhysicalOpen / NotifyPhysicalClose: avisan a la maqueta
///    (a traves de PhysicalDeviceBridge) de que la puerta virtual se abrio o
///    se cerro. Fire and forget: nunca bloquean ni rompen la simulacion.
///
/// DoorDetection es quien decide CUANDO llamarlos (cuando el agente llega y
/// cuando se aleja).
/// </summary>
[RequireComponent(typeof(WoodDoor))]
public class DoorController : MonoBehaviour
{
    [Header("Identificador del dispositivo")]
    [Tooltip("Puerta registrada en el panel de desarrollo. Se elige de la lista.")]
    [DeviceKey(DeviceTypes.Door)]
    public string deviceKey;

    [Header("Maqueta fisica (opcional)")]
    [Tooltip("Puente hacia la maqueta. Si se deja vacio se busca uno en la escena.")]
    [SerializeField] private PhysicalDeviceBridge physicalBridge;

    private WoodDoor _woodDoor;

    public bool IsOpen => _woodDoor != null && _woodDoor.open;

    private void Awake()
    {
        _woodDoor = GetComponent<WoodDoor>();

        if (physicalBridge == null)
        {
            Debug.LogWarning("No has asignado el physicalBridge en algun DoorController");
        }

        if (string.IsNullOrWhiteSpace(deviceKey))
        {
            Debug.LogWarning($"[DoorController] '{name}' no tiene deviceKey asignado: " + "esta puerta no se movera en la maqueta.");
        }
    }

    /// <summary>
    /// Abre o cierra la puerta. Usa OpenDoor() del asset (alterna el estado
    /// y reproduce el sonido) solo si el estado pedido es distinto del actual.
    /// </summary>
    public void SetOpen(bool open)
    {
        if (_woodDoor == null || _woodDoor.open == open) { return; }

        _woodDoor.OpenDoor();
    }

    // Avisa a la maqueta de que esta puerta se esta abriendo.
    public void NotifyPhysicalOpen()
    {
        if (physicalBridge == null || string.IsNullOrWhiteSpace(deviceKey)) { return; }

        physicalBridge.SendDoorOpen(deviceKey);
    }

    // Avisa a la maqueta de que esta puerta se cerro.
    public void NotifyPhysicalClose()
    {
        if (physicalBridge == null || string.IsNullOrWhiteSpace(deviceKey)) { return; }

        physicalBridge.SendDoorClose(deviceKey);
    }
}
