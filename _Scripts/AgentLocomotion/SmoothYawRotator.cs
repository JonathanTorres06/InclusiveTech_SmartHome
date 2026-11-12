using System;
using UnityEngine;

/// <summary>
/// Giro suave sobre el eje Y (arranca despacio, acelera y frena al llegar).
///
/// Es una clase serializable y no un MonoBehaviour: GraphAgent la contiene
/// y sus valores se ajustan desde el Inspector del propio agente, sin agregar
/// componentes nuevos a la escena.
///
/// Patron: Strategy ligera (GraphAgent decide HACIA DONDE mirar; esta clase
/// decide COMO girar).
///
/// Analogia: Es el volante con direccion asistida; el conductor solo dice a
/// donde quiere ir y el volante gira sin tirones.
/// </summary>
[Serializable]
public class SmoothYawRotator
{
    [Tooltip("Tiempo aproximado que tarda en completar un giro. Mas alto = giro mas suave y lento")]
    [Range(0.05f, 1.5f)]
    [SerializeField] private float smoothTime = 0.35f;

    [Tooltip("Tope de velocidad de giro en grados por segundo")]
    [Range(30f, 360f)]
    [SerializeField] private float maxTurnSpeed = 150f;

    [Tooltip("Diferencia en grados con la que se considera que el giro ya termino")]
    [Range(0.1f, 5f)]
    [SerializeField] private float toleranceDegrees = 1f;

    // Velocidad angular actual. Se conserva entre frames para que el giro sea continuo
    private float _angularVelocity;

    /// <summary>Angulo (grados, eje Y) hacia el que apunta una direccion en el plano XZ.</summary>
    public static float YawOf(Vector3 direction)
    {
        return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
    }

    /// <summary>Diferencia absoluta en grados entre la orientacion actual y la deseada.</summary>
    public float AngleTo(Transform body, float targetYaw)
    {
        return Mathf.Abs(Mathf.DeltaAngle(body.eulerAngles.y, targetYaw));
    }

    /// <summary>
    /// Avanza un paso del giro. Llamar una vez por frame.
    /// Devuelve true cuando ya quedo mirando al objetivo.
    /// </summary>
    public bool Step(Transform body, float targetYaw)
    {
        Vector3 euler = body.eulerAngles;
        euler.y = Mathf.SmoothDampAngle(euler.y, targetYaw, ref _angularVelocity, smoothTime, maxTurnSpeed);
        body.eulerAngles = euler;

        if (AngleTo(body, targetYaw) > toleranceDegrees) { return false; }

        // Ajuste final exacto y sin velocidad residual
        euler.y = targetYaw;
        body.eulerAngles = euler;
        Reset();
        return true;
    }

    /// <summary>Borra la velocidad acumulada. Llamar antes de empezar un giro nuevo.</summary>
    public void Reset()
    {
        _angularVelocity = 0f;
    }
}
