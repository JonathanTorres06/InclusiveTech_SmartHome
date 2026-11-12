using UnityEngine;

[RequireComponent(typeof(Light))]
public class TVAmbientLight : MonoBehaviour
{
    [Tooltip("Render Texture que usa el Video Player")]
    public RenderTexture tvTexture;

    [Tooltip("Qué tan rápido cambia el color de la luz")]
    public float colorSmoothing = 5f;

    [Tooltip("Actualizar 10 veces por segundo es suficiente y ahorra rendimiento")]
    public float updateRate = 0.1f;

    private Light ambientLight;
    private RenderTexture smallRT;
    private Texture2D pixelTexture;
    private float timer;
    private Color targetColor;

    void Start()
    {
        ambientLight = GetComponent<Light>();
        ambientLight.enabled = true;

        // Creamos una textura de 1x1. Al copiar el video aqui, la GPU promedia todos los colores sola
        smallRT = new RenderTexture(1, 1, 0);
        pixelTexture = new Texture2D(1, 1, TextureFormat.RGB24, false);
    }

    void Update()
    {
        if (tvTexture == null) return;

        // Temporizador para no leer la textura cada frame (optimizacion)
        timer += Time.deltaTime;
        if (timer >= updateRate)
        {
            timer = 0;
            CalculateAverageColor();
        }

        // Transicion suave (Fade) entre los colores para que no parpadee bruscamente
        ambientLight.color = Color.Lerp(ambientLight.color, targetColor, Time.deltaTime * colorSmoothing);
    }

    void CalculateAverageColor()
    {
        // Comprime el frame actual del video a 1x1 píxel
        Graphics.Blit(tvTexture, smallRT);

        // Lee ese unico pixel
        RenderTexture.active = smallRT;
        pixelTexture.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
        pixelTexture.Apply();
        RenderTexture.active = null;

        // Asigna el color objetivo
        targetColor = pixelTexture.GetPixel(0, 0);
    }
}