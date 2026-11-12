using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Efectos de sonido de la pantalla de login. Cada valor es un "espacio" que se llena
/// en el inspector arrastrando uno o varios AudioClip.
/// </summary>
public enum LoginSfx
{
    UiHover,          // pasar el mouse por un boton
    UiClick,          // pulsar un boton
    ScreenIn,         // entrada de la pantalla
    FaceDetected,     // aparece un rostro frente a la camara
    CaptureStart,     // empieza la captura del registro
    CaptureShot,      // cada foto tomada (el tono sube con el avance)
    CaptureComplete,  // termina la captura y empieza el procesado
    RegisterSuccess,  // usuario registrado por completo
    LoginSuccess,     // acceso concedido
    AccessDenied,     // acceso denegado o error
    Warning,          // aviso (rostro repetido, sin rostro...)
    EnterHouse,       // transicion hacia la casa
    SessionClosed,    // sesion cerrada
    QuitApp           // cierre de la aplicacion
}

[Serializable]
public class LoginSfxSlot
{
    public LoginSfx sfx;
    [Tooltip("Uno o varios clips. Si hay varios se elige uno al azar para que no suene repetitivo.")]
    public AudioClip[] variations;
    [Range(0f, 1f)] public float volume = 1f;

    public AudioClip Pick()
    {
        if (variations == null || variations.Length == 0) return null;
        return variations[UnityEngine.Random.Range(0, variations.Length)];
    }
}

/// <summary>
/// Reproduce los efectos de la pantalla de login con un pequeño grupo de voces, para que
/// varios sonidos cortos se puedan solapar y cada uno tenga su propio tono (pitch).
/// No toca el AudioPlayer de Mia, que es el canal de la voz.
/// </summary>
public class LoginSfxPlayer : MonoBehaviour
{
    [Range(0f, 1f)]
    [SerializeField] private float masterVolume = 0.7f;
    [Min(1)]
    [SerializeField] private int voiceCount = 4;
    [SerializeField] private LoginSfxSlot[] slots;

    private readonly Dictionary<LoginSfx, LoginSfxSlot> lookup = new Dictionary<LoginSfx, LoginSfxSlot>();
    private AudioSource[] voices;
    private int nextVoice;
    private bool initialized;

    /// <summary>Reproduce el efecto indicado. Si su espacio esta vacio no hace nada.</summary>
    public void Play(LoginSfx sfx, float pitch = 1f)
    {
        EnsureInitialized();

        if (!lookup.TryGetValue(sfx, out LoginSfxSlot slot)) return;

        AudioClip clip = slot.Pick();
        if (clip == null) return;

        AudioSource voice = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;

        voice.pitch = pitch;
        voice.PlayOneShot(clip, slot.volume * masterVolume);
    }

    private void EnsureInitialized()
    {
        if (initialized) return;

        lookup.Clear();
        if (slots != null)
        {
            foreach (LoginSfxSlot slot in slots) lookup[slot.sfx] = slot;
        }

        voices = new AudioSource[voiceCount];
        for (int i = 0; i < voiceCount; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // sonido 2D de interfaz
            voices[i] = source;
        }

        initialized = true;
    }

#if UNITY_EDITOR
    // Mantiene un espacio por cada efecto del enum, sin perder los clips ya asignados
    private void OnValidate()
    {
        var current = new Dictionary<LoginSfx, LoginSfxSlot>();
        if (slots != null)
        {
            foreach (LoginSfxSlot slot in slots) current[slot.sfx] = slot;
        }

        var synced = new List<LoginSfxSlot>();
        foreach (LoginSfx sfx in Enum.GetValues(typeof(LoginSfx)))
        {
            synced.Add(current.TryGetValue(sfx, out LoginSfxSlot existing) ? existing : new LoginSfxSlot { sfx = sfx });
        }

        slots = synced.ToArray();
    }
#endif
}
