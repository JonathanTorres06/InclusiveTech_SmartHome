// =============================================================
// Factory que construye el Command correcto segun el tipo
// de accion recibido del servidor it_ai_assistant.
//
// Responsabilidad: Decidir que implementacion de
// IHomeActionCommand instanciar segun ActionType. Es el
// unico punto del sistema que conoce todos los tipos de
// accion disponibles.
//
// Patron Factory Method
//
// Principio O de SOLID: Agregar un nuevo tipo de accion es
// agregar un nuevo case aqui y una clase nueva, el resto
// del sistema (HomeActionHandler) no se modifica.
// =============================================================

using UnityEngine;

/// <summary>
/// Construye instancias de IHomeActionCommand segun el tipo de accion.
/// </summary>
public class HomeActionFactory
{
    private readonly GraphAgent _graphAgent;

    public HomeActionFactory(GraphAgent agent) 
    {
        _graphAgent = agent;
    }

    /// <summary>
    /// Crea el Command correspondiente al tipo de accion recibido.
    /// </summary>
    /// <param name="actionType">Tipo de accion: LUZ, TODAS_LUCES, TEMP, MOVER.</param>
    /// <returns>Instancia del Command correspondiente, o null si el tipo no se reconoce.</returns>
    public IHomeActionCommand CreateCommand(string actionType) 
    {
        switch (actionType)
        {
            case "LUZ":
                return new LightActionCommand();

            case "TODAS_LUCES":
                return new AllLightsActionCommand();

            case "TEMP":
                return new TemperatureActionCommand();

            case "MOVER":
                return new MoveActionCommand(_graphAgent);

            default:
                Debug.LogWarning("[HomeActionFactory] Tipo de accion no reconocido: " + actionType);
                return null;
        }

    }
}
