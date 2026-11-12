/*
 * Scriptable Object de configuracion del microservicio de navegacion
 *
 * Responsabilidad: Centralizar los parametros configurables de la
 * conexion con el backend de navegacion (FastAPI), editables desde
 * el Inspector sin necesidad de recompilar.
 *
 * Sigue la misma convencion que AssistantConfig, para mantener
 * consistencia entre los distintos microservicios del proyecto.
 */

using UnityEngine;

/// <summary>
/// Quien abre las puertas de la maqueta fisica (contrato de comunicacion v0.3).
/// </summary>
public enum PhysicalDoorScenario
{
    // Escenario A: sin robot. Unity abre la puerta virtual y el backend manda la orden a la maqueta.
    WithoutRobot,

    // Escenario B: con robot. La puerta de la maqueta se abre sola con su sensor de distancia;
    // Unity NO manda ordenes de puertas.
    WithRobot
}

[CreateAssetMenu(fileName = "NavigationConfig", menuName = "Inclusive Tech/Navigation Config")]
public class NavigationConfig : ScriptableObject
{
    [Header("Servidor Python - Microservicio de Navegacion")]
    [Space(5)]

    [Tooltip("URL base del backend FastAPI de navegacion (it_nav_system)")]
    public string serverUrl = "http://127.0.0.1:8001"; // OJO CON EL PUERTO QUE SEA DISTINTO A LOS DE LOS DEMAS MICROSERVICIOS

    [Tooltip("ID de la casa activa. Debe coincidir con ACTIVE_HOUSE_ID en config.py")]
    public int activeHouseId = 1;

    [Tooltip("Timeout en segundos para las peticiones HTTP")]
    public float requestTimeoutSeconds = 15f;

    [Header("Maqueta fisica")]
    [Space(5)]

    [Tooltip("Si esta activo, cada orden de luces tambien se envia a la maqueta a traves del backend. " +
             "Si esta apagado, o la maqueta no esta conectada, Unity funciona exactamente igual.")]
    public bool sendOrdersToPhysicalDevices = true;

    [Tooltip("Quien abre las puertas de la maqueta. Sin robot (A): Unity manda las ordenes. " +
             "Con robot (B): las puertas se abren solas con su sensor y Unity no manda nada.")]
    public PhysicalDoorScenario doorScenario = PhysicalDoorScenario.WithoutRobot;

    [Tooltip("Cierre de seguridad de la puerta fisica (hold_ms, en milisegundos). Si la orden de cerrar nunca llega " +
             "(por ejemplo, se cae el Wi-Fi), la puerta se cierra sola a este tiempo. Debe ser MAYOR que lo que " +
             "tarda el agente en cruzar. El backend acepta entre 1000 y 60000.")]
    public int doorSafetyHoldMs = 15000;
}
