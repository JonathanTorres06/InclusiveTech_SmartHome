using System.IO;
using UnityEngine;

public class TestAudioRecorder : MonoBehaviour
{
    [SerializeField] private AudioRecorder audioRecorder;
    [SerializeField] private AssistantClient assistantClient;

    private async void Start()
    {
        foreach (string device in Microphone.devices)
        {
            Debug.Log($"[Diagnostico] Microfono disponible: {device}");
        }

        await System.Threading.Tasks.Task.Delay(3000);

        Debug.Log("Grabando en 2 segundos... preparate para hablar.");
        await System.Threading.Tasks.Task.Delay(2000);

        // Fragmento de prueba temporal
        byte[] audioData = await audioRecorder.RecordCommandAsync();
        string transcribedText = await assistantClient.TranscribeAudioAsync(audioData);
        Debug.Log($"Texto transcrito: '{transcribedText}'");

        // Diagnostico temporal — agregar dentro de Start(), antes de guardar el archivo
        /*AudioClip[] _ = null; // placeholder, ignorar

        byte[] wavData = await audioRecorder.RecordCommandAsync();

        if (wavData != null)
        {
            // Leer los bytes PCM (saltando los 44 bytes de cabecera)
            // y calcular la amplitud maxima para diagnosticar silencio
            int maxAmplitude = 0;
            for (int i = 44; i < wavData.Length - 1; i += 2)
            {
                short sample = System.BitConverter.ToInt16(wavData, i);
                int absSample = System.Math.Abs(sample);
                if (absSample > maxAmplitude) maxAmplitude = absSample;
            }
            Debug.Log($"[Diagnostico] Amplitud maxima detectada: {maxAmplitude} (de 32767 posibles)");

            string path = Path.Combine(Application.persistentDataPath, "test_recording.wav");
            File.WriteAllBytes(path, wavData);
            Debug.Log($"Audio guardado en: {path}");
        }*/
    }
}
