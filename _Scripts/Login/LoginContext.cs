/// <summary>
/// Lo que esta pasando AHORA en la pantalla de login. Cada contexto define el color
/// del indicador, el aspecto de la camara, el ambiente y si el menu se bloquea.
/// </summary>
public enum LoginContext
{
    Searching,      // buscando rostro
    FaceDetected,   // rostro visible, listo para actuar
    Capturing,      // tomando las fotos del registro
    Processing,     // entrenando el modelo o esperando el nombre por voz
    Success,        // algo salio bien
    Warning,        // no se pudo, el usuario puede reintentar
    Denied,         // acceso denegado o error
    EnteringHouse,  // transicion hacia la casa
    Closing         // cerrando la aplicacion
}

/// <summary>
/// En que "modo" esta la pantalla. Se muestra como etiqueta sobre el texto central.
/// </summary>
public enum LoginMode { SignIn, FirstSetup, Registration, SessionActive }

/// <summary>
/// Paso actual del registro de un usuario nuevo.
/// </summary>
public enum RegistrationStep { None, Face, Name, Done }

/// <summary>
/// Datos de presentacion de un contexto. Son nombres de clases USS, la UI no sabe de colores.
/// </summary>
public readonly struct ContextStyle
{
    public readonly string StatusClass;   // color del indicador del texto central
    public readonly string CameraClass;   // aspecto del recuadro de la camara
    public readonly string AmbientClass;  // tinte suave sobre toda la pantalla
    public readonly bool HoldsMessage;    // el mensaje se queda unos segundos antes de que lo pise otro de fondo
    public readonly bool LocksMenu;       // bloquea los botones mientras dura
    public readonly bool Scans;           // barra de escaneo sobre la camara
    public readonly bool Pulses;          // latido de la camara

    public ContextStyle(string statusClass, string cameraClass, string ambientClass,
                        bool holdsMessage = false, bool locksMenu = false, bool scans = false, bool pulses = false)
    {
        StatusClass = statusClass;
        CameraClass = cameraClass;
        AmbientClass = ambientClass;
        HoldsMessage = holdsMessage;
        LocksMenu = locksMenu;
        Scans = scans;
        Pulses = pulses;
    }
}

/// <summary>
/// Tabla unica contexto -> estilo. Para cambiar el look de un estado se toca solo aqui.
/// </summary>
public static class LoginContextStyles
{
    public static ContextStyle Of(LoginContext context)
    {
        switch (context)
        {
            case LoginContext.FaceDetected:  return new ContextStyle("estado_exito",   "cam_detectado",  "amb_neutro");
            case LoginContext.Capturing:     return new ContextStyle("estado_ocupado", "cam_capturando", "amb_registro", locksMenu: true, scans: true, pulses: true);
            case LoginContext.Processing:    return new ContextStyle("estado_ocupado", "cam_procesando", "amb_proceso",  locksMenu: true, scans: true);
            case LoginContext.Success:       return new ContextStyle("estado_exito",   "cam_exito",      "amb_exito",    holdsMessage: true);
            case LoginContext.Warning:       return new ContextStyle("estado_aviso",   "cam_alerta",     "amb_alerta",   holdsMessage: true);
            case LoginContext.Denied:        return new ContextStyle("estado_error",   "cam_denegado",   "amb_error",    holdsMessage: true);
            case LoginContext.EnteringHouse: return new ContextStyle("estado_ocupado", "cam_exito",      "amb_exito",    locksMenu: true);
            case LoginContext.Closing:       return new ContextStyle("estado_info",    "cam_buscando",   "amb_neutro",   locksMenu: true);
            default:                         return new ContextStyle("estado_info",    "cam_buscando",   "amb_neutro");
        }
    }
}
