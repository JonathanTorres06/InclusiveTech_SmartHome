using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Sabe como llegar al panel web de la casa: comprueba que el servicio de
/// navegacion responda y abre el navegador del sistema en su direccion.
///
/// Usa NavigationConfig.serverUrl, la misma direccion con la que Unity habla con
/// el servicio de navegacion.
///
/// Si hay sesion de administrador, la clave viaja en el fragmento de la URL
/// (lo que va despues del '#'), que el navegador nunca envia a ningun servidor.
/// El panel la guarda y limpia la direccion.
///
/// No conoce la interfaz. Solo el boton (WebPanelButtonController) decide
/// cuando usarla y que mostrar al usuario.
/// </summary>
public class WebPanelOpener
{
    // Plazo corto: el servicio corre en el mismo portatil, si no responde es que esta apagado
    private const int HEALTH_TIMEOUT_SECONDS = 2;

    private readonly NavigationConfig _config;
    private readonly AdminKeyAccess _keyAccess;

    public WebPanelOpener(NavigationConfig config, AdminKeyAccess keyAccess)
    {
        _config = config;
        _keyAccess = keyAccess;
    }

    private string PanelUrl => _config.serverUrl.TrimEnd('/') + "/";

    /// <summary>Consulta GET /health. Debe ejecutarse como Coroutine.</summary>
    public IEnumerator CheckReachable(Action<bool> onCompleted)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(PanelUrl + "health"))
        {
            request.timeout = HEALTH_TIMEOUT_SECONDS;

            yield return request.SendWebRequest();

            onCompleted?.Invoke(request.result == UnityWebRequest.Result.Success);
        }
    }

    /// <summary>Abre el panel. Con sesion de administrador, ya lleva la clave de acceso.</summary>
    public void OpenInBrowser()
    {
        string url = PanelUrl;

        if (_keyAccess.TryGetKey(out string key))
        {
            url += "#key=" + Uri.EscapeDataString(key);
        }

        Application.OpenURL(url);
    }
}