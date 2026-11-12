// Contrato para los comandos de accion del hogar.
//
// Responsabilidad: Definir la interfaz que cualquier accion
// del hogar (luz, temperatura, movimiento) debe implementar
// para ser ejecutable de forma polimorfica por HomeActionHandler.
//
// Patron Command: Cada accion del hogar es un objeto que
// encapsula su propia logica de ejecucion.
//
// Principio O de SOLID: Agregar un nuevo tipo de accion
// (persianas, electrodomesticos) es crear una nueva clase,
// sin modificar HomeActionHandler ni las acciones existentes.
// =============================================================

/// <summary>
/// Contrato para una accion ejecutable sobre el estado del hogar.
/// </summary>
public interface IHomeActionCommand
{
    /// <summary>
    /// Ejecuta la accion sobre HouseState con los parametros
    /// extraidos de la respuesta del LLM.
    /// </summary>
    /// <param name="room">Habitacion objetivo. Puede ser null segun la accion.</param>
    /// <param name="state">Estado objetivo (ON/OFF). Puede ser null segun la accion.</param>
    /// <param name="value">Valor numerico. Puede ser null segun la accion.</param>
    void Execute(string room, string state, float? value);
}
