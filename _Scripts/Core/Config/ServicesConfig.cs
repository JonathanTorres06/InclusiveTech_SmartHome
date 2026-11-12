/*
Responsabilidad: guardar en un solo lugar como se arranca cada servicio
externo del proyecto (navegacion, asistente Mia y palabra de activacion).
Se edita desde el Inspector, sin recompilar.

Se pueden tener varios perfiles, por ejemplo uno con las rutas del
computador de desarrollo y otro con las rutas del portatil de la
sustentacion. ProcessManager usa el que tenga asignado.
*/

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Forma de comprobar que un servicio ya esta listo para recibir peticiones.
/// </summary>
public enum ServiceHealthCheck
{
    // Hace GET a healthUrl y espera una respuesta 2xx.
    Http,

    // Intenta abrir una conexion TCP a tcpHost:tcpPort.
    Tcp,

    // Solo comprueba que el proceso siga vivo despues de processAliveSeconds.
    ProcessAlive,

    // Espera a que el servicio imprima un texto concreto (readyLogMarker) en su salida.
    // No hace ninguna conexion al servicio, asi que no deja errores en sus logs.
    LogMarker
}

/// <summary>
/// Datos para arrancar y vigilar un servicio externo.
/// </summary>
[Serializable]
public class ExternalServiceDefinition
{
    [Tooltip("Nombre corto del servicio. Se usa en los logs y para consultar su estado.")]
    public string serviceName;

    [Tooltip("Si esta apagado, el servicio se ignora por completo.")]
    public bool enabled = true;

    [Header("Arranque")]
    [Tooltip("Ruta al ejecutable, normalmente el python.exe del venv del servicio. " +
             "Puede ser absoluta o relativa a la carpeta del ejecutable de Unity.")]
    public string executablePath;

    [Tooltip("Carpeta desde la que se ejecuta el comando. Es la misma carpeta donde haces cd " +
             "antes de levantar el servicio a mano. Absoluta o relativa a la carpeta del ejecutable.")]
    public string workingDirectory;

    [Tooltip("Argumentos del ejecutable. Por ejemplo: main.py o -m app.main")]
    public string arguments = "main.py";

    [Header("Comprobacion de que esta listo")]
    public ServiceHealthCheck healthCheck = ServiceHealthCheck.Http;

    [Tooltip("Solo para Http. Direccion que debe responder 2xx cuando el servicio este listo.")]
    public string healthUrl;

    [Tooltip("Para Tcp. Con LogMarker es opcional: sirve para detectar si el servicio ya estaba corriendo.")]
    public string tcpHost = "127.0.0.1";

    [Tooltip("Para Tcp. Con LogMarker es opcional: sirve para detectar si el servicio ya estaba corriendo.")]
    public int tcpPort;

    [Tooltip("Solo para LogMarker. Texto que el servicio imprime cuando ya esta listo.")]
    public string readyLogMarker;

    [Tooltip("Solo para ProcessAlive. Segundos que debe seguir vivo el proceso para darlo por listo.")]
    public float processAliveSeconds = 3f;

    [Tooltip("Segundos maximos que Unity espera a que el servicio quede listo.")]
    public float startupTimeoutSeconds = 60f;
}

[CreateAssetMenu(fileName = "ServicesConfig", menuName = "Inclusive Tech/Services Config")]
public class ServicesConfig : ScriptableObject
{
    [Tooltip("Servicios que ProcessManager arranca al abrir la aplicacion, en este orden.")]
    public List<ExternalServiceDefinition> services = new List<ExternalServiceDefinition>
    {
        new ExternalServiceDefinition
        {
            serviceName = "it_nav_system",
            arguments = "-m app.main",
            healthCheck = ServiceHealthCheck.Http,
            healthUrl = "http://127.0.0.1:8001/health",
            startupTimeoutSeconds = 30f
        },
        new ExternalServiceDefinition
        {
            serviceName = "it_ai_assistant",
            arguments = "main.py",
            healthCheck = ServiceHealthCheck.Http,
            healthUrl = "http://127.0.0.1:8000/",
            startupTimeoutSeconds = 120f
        },
        new ExternalServiceDefinition
        {
            serviceName = "it_wake_word",
            arguments = "main.py",
            healthCheck = ServiceHealthCheck.LogMarker,
            readyLogMarker = "Servidor WebSocket activo",
            tcpHost = "127.0.0.1",
            tcpPort = 9000,
            startupTimeoutSeconds = 90f
        }
    };
}