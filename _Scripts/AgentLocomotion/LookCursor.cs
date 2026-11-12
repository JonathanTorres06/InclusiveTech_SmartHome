using UnityEngine;

/// <summary>
/// Esconde y bloquea el cursor mientras se gira la camara, y lo devuelve al terminar.
/// Solo toca Unity cuando el estado cambia, para no repetir la orden cada frame.
/// </summary>
public class LookCursor
{
    private bool _captured;

    public void SetCaptured(bool captured)
    {
        if (captured == _captured) { return; }

        _captured = captured;
        Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !captured;
    }
}