using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Implementacion real de IRoomNameProvider: Obtiene display_name y
/// aliases de cada habitacion consultando el backend del microservicio
/// de navegacion (GET /user/nodes), que ya filtra por la casa activa
/// configurada en el servidor (ACTIVE_HOUSE_ID).
///
/// Decision de disenio: GetAllRoomNames() de la interfaz es sincrona,
/// pero una peticion HTTP es asincrona por naturaleza. Por eso este
/// proveedor separa dos responsabilidades:
///   - FetchFromBackend(): asincrono (Coroutine), actualiza la cache.
///   - GetAllRoomNames(): sincrono, cumple el contrato de la interfaz
///     devolviendo la ultima cache disponible.
///
/// Quien use este proveedor debe llamar a FetchFromBackend() antes de
/// que RoomManager necesite resolver el primer comando de voz.
/// </summary>
public class NavigationServiceRoomNameProvider : IRoomNameProvider
{
    private readonly NavigationConfig _config;
    private Dictionary<string, RoomNameData> _cache = new Dictionary<string, RoomNameData>();

    // True una vez que al menos una carga desde el backend se completo
    // exitosamente. El script de arranque la usa para decidir
    // si hace fallback al DefaultRoomNameProvider.
    public bool HasLoadedSuccessfully { get; private set; } = false;

    private string _lastContentHash = null;

    // True si la ultima llamada a FetchFromBackend detecto cambios
    // reales respecto a la carga anterior. False si el contenido
    // era identico (o si es la primera carga exitosa, que siempre
    // cuenta como "cambio" ya que no habia nada antes).
    public bool LastFetchHadChanges { get; private set; } = false;

    #region Constructor

    /// <summary>
    /// Recibe la configuracion por constructor (Dependency Injection),
    /// en vez de leer una constante estatica. Permite que la URL del
    /// backend se edite desde el Inspector sin recompilar.
    /// </summary>
    public NavigationServiceRoomNameProvider(NavigationConfig config) 
    {
        _config = config;
    }

    #endregion

    #region DTOs

    // DTOs que reflejan el schema NodeResponse/AliasResponse del
    // backend, definidos en presentation/schemas.py
    private class NodeDto 
    {
        public int id;
        public string node_type;
        public string internal_name;
        public string display_name;
        public List<AliasDto> aliases;
    }

    private class AliasDto 
    {
        public string alias;
    }

    public Dictionary<string, RoomNameData> GetAllRoomNames()
    {
        return _cache;
    }

    #endregion

    /// <summary>
    /// Consulta GET /user/nodes en el backend y reconstruye la cache
    /// de nombres. Debe ejecutarse como Coroutine desde un MonoBehaviour,
    /// ya que esta clase no es un MonoBehaviour y no puede iniciar
    /// corutinas por si misma.
    /// </summary>
    /// <param name="onComplete">Se invoca al terminar, indicando exito o fallo.</param>
    public IEnumerator FetchFromBackend(Action<bool> onComplete) 
    {
        string url = $"{_config.serverUrl}/user/nodes";

        using (UnityWebRequest request = UnityWebRequest.Get(url)) 
        {
            request.timeout = Mathf.CeilToInt(_config.requestTimeoutSeconds);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[NavigationServiceRoomNameProvider] Error al consultar: {url}: {request.error}");
                onComplete?.Invoke(false);
                yield break;
            }

            try
            {
                List<NodeDto> nodes = JsonConvert.DeserializeObject<List<NodeDto>>(request.downloadHandler.text);
                RebuildCache(nodes);
                HasLoadedSuccessfully = true;
                onComplete?.Invoke(true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NavigationServiceRoomNameProvider] Error al parsear la respuesta: {ex.Message}");
                onComplete?.Invoke(false);
            }
        }
    }

    /// <summary>
    /// Reconstruye la cache desde cero a partir de los nodos recibidos,
    /// y calcula una huella del contenido para detectar si hubo cambios
    /// reales respecto a la carga anterior de nodos usando polling en
    /// RoomNameBooststrap para evitar reconstrucciones innecesarias.
    /// </summary>
    private void RebuildCache(List<NodeDto> nodes) 
    {
        var newCache = new Dictionary<string, RoomNameData>();
        var hasBuilder = new System.Text.StringBuilder();

        if (nodes != null)
        {
            // Orden estable por internal_name para que la huella no cambie
            // solo porque el backend devolvio los nodos en otro orden.
            var orderedNodes = nodes
                .Where(n => n.node_type == "room")
                .OrderBy(n => n.internal_name);

            foreach (var node in orderedNodes)
            {
                //if (node.node_type != "room") { continue; }

                string key = node.internal_name.Trim().ToUpperInvariant();
                var aliases = node.aliases?.Select(a => a.alias).ToList() ?? new List<string>();

                newCache[key] = new RoomNameData
                {
                    InternalName = key,
                    DisplayName = node.display_name,
                    Aliases = aliases,
                };

                // HUELLA: Nombre tecnico + nombre visible + alias ordenados,
                // concatenados. No necesita ser criptografica, solo
                // suficientemente unica para detectar diferencias de contenido.
                hasBuilder.Append(key).Append('|');
                hasBuilder.Append(node.display_name).Append('|');
                hasBuilder.Append(string.Join(",", aliases.OrderBy(a => a)));
                hasBuilder.Append(";");
            }
        }

        string newHash = hasBuilder.ToString();
        LastFetchHadChanges = (_lastContentHash != null) && (newHash != _lastContentHash);

        // La primera carga exitosa no cuenta como "cambio detectado" en el
        // sentido de "algo se modifico mientras el juego corria", pero si
        // se considera la primera vez que hay datos disponibles
        bool isFirstLoad = _lastContentHash == null;

        _lastContentHash = newHash;
        _cache = newCache;

        if (isFirstLoad)
        {
            LastFetchHadChanges = true; // primera carga = no hay datos nuevos que usar
        }
    }
}
