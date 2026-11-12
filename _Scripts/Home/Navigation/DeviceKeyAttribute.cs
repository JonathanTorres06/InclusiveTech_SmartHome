using UnityEngine;

/// <summary>
/// Tipos de dispositivo del backend. Deben coincidir con los valores
/// que acepta POST /dev/devices ("light" y "door").
/// </summary>
public static class DeviceTypes
{
    public const string Light = "light";
    public const string Door = "door";
}

/// <summary>
/// Marca un campo string como "clave de dispositivo". En el Inspector deja
/// de ser una caja de texto y pasa a ser un desplegable con los dispositivos
/// registrados en el panel de desarrollo (sin escribir nada a mano).
///
/// Este archivo va FUERA de la carpeta Editor porque los scripts
/// LightController y DoorController lo usan. El dibujo real vive en
/// DeviceKeyDrawer (carpeta Editor).
/// </summary>
public class DeviceKeyAttribute : PropertyAttribute
{
    /// <summary>Tipo de dispositivo a listar ("light" o "door").</summary>
    public readonly string DeviceType;

    /// <summary>
    /// Opcional: Nombre de otro campo string del mismo script que se rellena
    /// solo con el InternalName del nodo al que pertenece el dispositivo.
    /// </summary>
    public readonly string NodeNameField;

    public DeviceKeyAttribute(string deviceType, string nodeNameField = null)
    {
        DeviceType = deviceType;
        NodeNameField = nodeNameField;
    }
}
