using UnityEngine;

/// <summary>
/// Fuente de la mirada del usuario. AgentLookController solo conoce esta
/// interfaz, asi que el dispositivo se puede cambiar sin tocar la logica.
///
/// Patron: Strategy.
/// </summary>
public interface ILookInput
{
    /// <summary>
    /// Cuanto se movio la mirada desde el ultimo frame.
    /// X: derecha es positivo. Y: arriba es positivo.
    /// </summary>
    Vector2 ReadDelta();

    /// <summary>True mientras se mantiene presionado el boton de mirar (clic derecho).</summary>
    bool IsLookButtonHeld { get; }

    /// <summary>True solo en el frame en que se pidio soltar el cursor (tecla Escape).</summary>
    bool ReleaseCursorPressed { get; }

    /// <summary>True solo en el frame en que se pidio recuperar la mirada (clic izquierdo).</summary>
    bool CaptureCursorPressed { get; }
}