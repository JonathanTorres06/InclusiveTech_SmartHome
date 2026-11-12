// =============================================================
// Utilidad para convertir un AudioClip de Unity a bytes WAV.
//
// Responsabilidad: codificar las muestras de audio de un
// AudioClip en el formato de archivo WAV (PCM 16 bits) que
// el servidor it_ai_assistant espera recibir.
//
// Principio S de SOLID de unica responsabilidad: Codificacion
// de audio a WAV. No graba del microfono ni envia HTTP.
// =============================================================

using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Convierte AudioClips de Unity al formato de archivo WAV.
/// </summary>
public class WavUtility : MonoBehaviour
{
    private const int BITS_PER_SAMPLE = 16;
    private const int HEADER_SIZE_BYTES = 44;

    /// <summary>
    /// Convierte un AudioClip a un array de bytes en formato WAV PCM 16 bits.
    /// </summary>
    /// <param name="clip">AudioClip grabado a convertir.</param>
    /// <returns>Bytes del archivo WAV completo, listo para enviar o guardar.</returns>
    public static byte[] FromAudioClip(AudioClip clip) 
    {
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        byte[] pcmData = ConvertFloatSamplesToPcm16(samples);

        using (var memoryStream = new MemoryStream()) 
        {
            WriteWavHeader(memoryStream, clip.channels, clip.frequency, pcmData.Length);
            memoryStream.Write(pcmData, 0, pcmData.Length);
            return memoryStream.ToArray();
        }
    }

    /// <summary>
    /// Convierte muestras de audio en float (-1.0 a 1.0, formato de Unity)
    /// a enteros de 16 bits con signo (formato PCM estandar de WAV).
    /// </summary>
    /// <param name="samples">Muestras en formato float de Unity.</param>
    /// <returns>Bytes PCM 16 bits little-endian.</returns>
    private static byte[] ConvertFloatSamplesToPcm16(float[] samples) 
    {
        byte[] pcmData = new byte[samples.Length * 2];

        for (int i = 0; i < samples.Length; i++)
        {
            float clampedSample = Mathf.Clamp(samples[i], -1f, 1f);
            short pcmSample = (short)(clampedSample * short.MaxValue);

            byte[] sampleBytes = BitConverter.GetBytes(pcmSample);
            pcmData[i * 2] = sampleBytes[0];
            pcmData[i * 2 + 1] = sampleBytes[1];
        }

        return pcmData;
    }

    /// <summary>
    /// Escribe la cabecera estandar de archivo WAV en el stream.
    /// Sigue la especificacion RIFF/WAVE con formato PCM.
    /// </summary>
    /// <param name="stream">Stream donde escribir la cabecera.</param>
    /// <param name="channels">Numero de canales de audio (1 = mono).</param>
    /// <param name="sampleRate">Frecuencia de muestreo en Hz.</param>
    /// <param name="dataLength">Tamanio en bytes de los datos PCM que siguen.</param>
    private static void WriteWavHeader(Stream stream, int channels, int sampleRate, int dataLength) 
    {
        int byteRate = sampleRate * channels * (BITS_PER_SAMPLE / 8);
        int blockAlign = channels * (BITS_PER_SAMPLE / 8);

        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) 
        {
            // Chunk RIFF
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(HEADER_SIZE_BYTES - 8 + dataLength);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

            // Sub-chunk fmt
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16); // Tamanio del sub-chunk fmt
            writer.Write((short)1); // Formato PCM
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((short)blockAlign);
            writer.Write((short)BITS_PER_SAMPLE);

            // Sub-chunk data
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataLength);
        }
    }
}