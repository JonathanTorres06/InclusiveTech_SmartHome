using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Entrada de mirada con el Input System nuevo: mouse para mirar y
/// teclado (Escape) para soltar el cursor.
///
/// Requiere el paquete Input System y que Project Settings, Player,
/// Active Input Handling sea "Input System Package" o "Both".
/// </summary>
public class MouseLookInput : ILookInput
{
    // El Input System entrega pixeles. Este factor los lleva a una escala comoda
    // para que la sensibilidad del Inspector quede en valores cercanos a 1 a 5
    private const float PIXELS_TO_LOOK_UNITS = 0.1f;

    public Vector2 ReadDelta()
    {
        Mouse mouse = Mouse.current;
        return mouse != null ? mouse.delta.ReadValue() * PIXELS_TO_LOOK_UNITS : Vector2.zero;
    }

    public bool IsLookButtonHeld
    {
        get { return Mouse.current != null && Mouse.current.rightButton.isPressed; }
    }

    public bool ReleaseCursorPressed
    {
        get { return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame; }
    }

    public bool CaptureCursorPressed
    {
        get { return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame; }
    }
}