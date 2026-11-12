/// <summary>
/// Puerta unica por la que la interfaz obtiene la clave de acceso del servicio de usuarios.
/// Solo la entrega si hay una sesion de administrador activa EN ESE MOMENTO.
/// Comprobar al pedirla (y no solo al mostrar el boton) evita que un boton visible por error
/// o una sesion que ya se cerro dejen ver la clave.
///
/// Analogia: Es el guardia de la caja fuerte. Pide la credencial cada vez, aunque ya
/// te haya visto entrar hace un rato.
/// </summary>
public class AdminKeyAccess
{
    /// <summary>True si hay una sesion activa y es de administrador.</summary>
    public bool IsAvailable
    {
        get
        {
            GestorSesion session = GestorSesion.Instance;
            return session != null && session.HaySesionActiva() && session.EsAdministradorActual();
        }
    }

    /// <summary>Devuelve la clave solo con sesion de administrador. Si no, false y key queda en null.</summary>
    public bool TryGetKey(out string key)
    {
        key = null;
        return IsAvailable && AdminApiKeyFile.TryRead(out key);
    }
}
