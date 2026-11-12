// TestAudioPlayer.cs — verificacion temporal, borrar despues
// =============================================================
// Carga un .wav existente en disco y lo encola dos veces en
// AudioPlayer para verificar que la cola secuencial funciona
// sin superponer reproducciones.
// =============================================================

using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class TestAudioPlayer : MonoBehaviour
{
    [SerializeField] private AudioPlayer audioPlayer;

    private async void Start()
    {
        string path = Path.Combine(Application.persistentDataPath, "test_recording.wav");

        if (!File.Exists(path))
        {
            Debug.LogError($"[TestAudioPlayer] No se encontro el archivo: {path}");
            Debug.LogError("Ejecuta primero el TestAudioRecorder para generar el .wav.");
            return;
        }

        AudioClip clip = await LoadWavFromDiskAsync(path);

        if (clip == null)
        {
            Debug.LogError("[TestAudioPlayer] No se pudo cargar el AudioClip.");
            return;
        }

        audioPlayer.OnPlaybackStarted += () => Debug.Log("[TestAudioPlayer] Empezo a reproducir un clip.");
        audioPlayer.OnQueueFinished += () => Debug.Log("[TestAudioPlayer] La cola completa termino de reproducirse.");

        Debug.Log("[TestAudioPlayer] Encolando el mismo clip DOS VECES...");
        Debug.Log("[TestAudioPlayer] Deberian sonar uno despues del otro, sin superponerse.");

        audioPlayer.Enqueue(clip);
        audioPlayer.Enqueue(clip);
    }

    /// <summary>
    /// Carga un archivo WAV desde disco como AudioClip usando
    /// UnityWebRequestMultimedia, que soporta rutas locales con
    /// el prefijo file://.
    /// </summary>
    /// <param name="filePath">Ruta absoluta al archivo .wav.</param>
    /// <returns>AudioClip cargado, o null si fallo la carga.</returns>
    private async System.Threading.Tasks.Task<AudioClip> LoadWavFromDiskAsync(string filePath)
    {
        string url = "file://" + filePath;

        using (var request = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
        {
            var operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                await System.Threading.Tasks.Task.Yield();
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[TestAudioPlayer] Error al cargar WAV: {request.error}");
                return null;
            }

            return DownloadHandlerAudioClip.GetContent(request);
        }
    }
}