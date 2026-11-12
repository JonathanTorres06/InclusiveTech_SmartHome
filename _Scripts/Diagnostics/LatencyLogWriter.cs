// =============================================================
// Escribe el registro temporal de latencia medido en Unity.
//
// Responsabilidad: guardar una fila por comando de voz en un CSV
// dentro de Application.persistentDataPath/logs, para cruzarla con
// el log del servidor (latency_log.csv) usando el request_id.
// No mide nada ni decide nada: solo escribe lo que le entregan.
//
// Principio S de SOLID: unica responsabilidad, persistir filas CSV.
// Nunca lanza excepciones hacia el flujo del asistente: si el disco
// falla, avisa en consola y el comando de voz sigue normal.
//
// Privacidad: solo tiempos y estados, nunca el texto del usuario.
// =============================================================

using System;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// Escritor de CSV para el log de latencia del lado de Unity.
/// </summary>
public sealed class LatencyLogWriter
{
    private const string HEADER =
        "timestamp,request_id,scene,recording_ms,precheck_stt_ms,process_ms,total_ms,response_audio_s,status";

    private readonly string _path;

    // Ruta completa del CSV, util para mostrarla en consola
    public string FilePath => _path;

    /// <param name="fileName">Nombre del archivo, por ejemplo unity_latency.csv.</param>
    public LatencyLogWriter(string fileName)
    {
        string folder = Path.Combine(Application.persistentDataPath, "logs");

        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LatencyLogWriter] No se pudo crear la carpeta de logs: {ex.Message}");
        }

        _path = Path.Combine(folder, fileName);
    }

    // Agrega una fila al CSV (crea el archivo y el encabezado la primera vez)
    public void Append(
        string requestId, string scene,
        long recordingMs, long precheckMs, long processMs, long totalMs,
        float responseAudioSeconds, string status)
    {
        try
        {
            bool isNew = !File.Exists(_path) || new FileInfo(_path).Length == 0;

            string line = string.Join(",",
                DateTime.Now.ToString("s", CultureInfo.InvariantCulture),
                requestId,
                scene,
                recordingMs,
                precheckMs,
                processMs,
                totalMs,
                responseAudioSeconds.ToString("F2", CultureInfo.InvariantCulture),
                status);

            using (var writer = new StreamWriter(_path, append: true))
            {
                if (isNew) writer.WriteLine(HEADER);
                writer.WriteLine(line);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LatencyLogWriter] No se pudo escribir el log: {ex.Message}");
        }
    }
}