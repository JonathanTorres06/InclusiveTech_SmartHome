using System.Collections;
using UnityEngine;

/// <summary>
/// Punto de union que conecta RoomManager con la fuente real
/// de nombres, y mantiene esa conexion sincronizada mientras que la
/// escena corre (polling periodico), reflejando cambios que el
/// usuario haga desde el panel web sin necesidad de reiniciar Unity.
/// 
/// Debe colocarse en un GameObject de la escena junto (o antes) del
/// GameObject que tiene el componente RoomManager.
/// </summary>
public class RoomNameBootstrap : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("RoomManager de la escena al que se le va a inyectar el proveedor real.")]
    public RoomManager roomManager;

    [Tooltip("Asset de configuracion del microservicio de navegacion")]
    public NavigationConfig navigationConfig;

    [Header("Sincronizacion en tiempo real")]
    [Tooltip("Segundo entre cada consulta al backend para detectar cambios.")]
    [Range(3f, 60f)]
    public float pollingIntervalSeconds = 8f;

    [Tooltip("Si esta activo, sigue consultando el backend periodicamente " +
             "despues de la carga inicial. Si esta desactivado, se comporta " +
             "como el Paso 9 original. Es decir solo carga una vez al iniciar.")]
    public bool enablePolling = true;

    private NavigationServiceRoomNameProvider _backendProvider;

    private IEnumerator Start()
    {
        if (roomManager == null)
        {
            Debug.LogError("[RoomNameBootstrap] No se asigno ninguna referencia a RoomManager en el Inspector.");
            yield break;
        }

        if (navigationConfig == null)
        {
            Debug.LogError("[RoomNameBootstrap] No se asigno ninguna referencia a navigationConfig en el Inspector.");
            yield break;
        }

        // Si ProcessManager esta arrancando el servicio de navegacion, esperar a que
        // responda antes de la primera carga. Si no lo arranca, continua de inmediato.
        if (ProcessManager.Instance != null)
        {
            yield return ProcessManager.Instance.WaitForService("it_nav_system", 30f);
        }

        _backendProvider = new NavigationServiceRoomNameProvider(navigationConfig);

        // Primera carga de los nodos almacenados en el backend
        yield return LoadOnce();

        // A partir de aqui, el ciclo periodico de consultar cambios sobre los nodos cada cierto tiempo, toma el control
        if (enablePolling)
        {
            StartCoroutine(PollingLoop());
        }
    }

    /// <summary>
    /// Ejecuta una carga desde el backend y, si hubo cambios reales
    /// respecto a la anterior (segun la huella calculada en
    /// NavigationServiceRoomNameProvider), actualiza RoomManager.
    /// </summary>
    private IEnumerator LoadOnce()
    {
        bool fetchSucceeded = false;
        yield return _backendProvider.FetchFromBackend(success => fetchSucceeded = success);

        if (!fetchSucceeded)
        {
            Debug.LogWarning("[RoomNameBootstrap] No se pudo conectar con el backend. " +
                              "Se mantiene la ultima configuracion de nombres disponible.");
            yield break;
        }

        if (_backendProvider.LastFetchHadChanges)
        {
            roomManager.SetNameProvider(_backendProvider);
            Debug.Log($"<color=green>[RoomNameBootstrap]</color> Nombres actualizados desde el backend. " +
                      $"Total de habitaciones reconocibles: {roomManager.GetTotalRooms()}");
        }

        // Si no hubo cambios, no hace falta llamar SetNameProvider de nuevo.
        // RoomManager ya esta usando esta misma instande de _backendProvider,
        // y su cache interna solo se reemplaza cuando SI hay contenido nuevo.
    }

    /// <summary>
    /// Ciclo que repite la consulta al backend cada pollingIntervalSeconds,
    /// mientras el GameObject este activo. Se detiene automaticamente si
    /// el objeto se desactiva o la escena termina.
    /// </summary>
    private IEnumerator PollingLoop()
    {
        var wait = new WaitForSeconds(pollingIntervalSeconds);

        while (true)
        {
            yield return wait;
            yield return LoadOnce();
        }
    }
}