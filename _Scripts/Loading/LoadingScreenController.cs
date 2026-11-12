using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Pantalla de carga animada (UI Toolkit). Se monta como overlay sobre el
/// UIDocument del login, carga la escena destino de forma asincrona y
/// anima anillos, particulas, frases y barra de progreso real.
/// Solo presenta la carga mas no conoce reglas de negocio ni sesiones.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class LoadingScreenController : MonoBehaviour
{
    // Valores Constantes
    private const float SCENE_READY_PROGRESS = 0.9f;
    private const float PROGRESS_SMOOTHING_SPEED = 3f;
    private const float DISPLAY_COMPLETE_THRESHOLD = 0.99f;
    private const float FADE_IN_SECONDS = 0.5f;
    private const float HOLD_AT_FULL_SECONDS = 0.35f;

    private const float OUTER_RING_DEGREES_PER_SECOND = 40f;
    private const float MIDDLE_RING_DEGREES_PER_SECOND = -70f;
    private const float INNER_RING_DEGREES_PER_SECOND = 120f;
    private const float LOGO_PULSE_SPEED = 2.2f;
    private const float LOGO_PULSE_AMPLITUDE = 0.06f;

    private const int PARTICLE_COUNT = 18;
    private const float PARTICLE_MIN_SPEED = 4f;    // % de alto por segundo
    private const float PARTICLE_MAX_SPEED = 14f;
    private const float PARTICLE_MIN_SIZE = 3f;
    private const float PARTICLE_MAX_SIZE = 8f;

    private const int OVERLAY_FADE_DELAY_MS = 30;
    private const int TIP_FADE_MS = 350;

    private const string CLASS_LOADING = "loading";
    private const string CLASS_LOADING_VISIBLE = "loading--visible";
    private const string CLASS_PARTICLE = "particle";
    private const string CLASS_TIP_HIDDEN = "loading__tip--hidden";

    private const float LOGIN_FADE_OUT_SECONDS = 0.3f; // debe coincidir con transition-duration de .login--hiding
    private const string CLASS_LOGIN_HIDING = "login--hiding";

    [Header("Assets de UI")]
    [SerializeField] private VisualTreeAsset loadingTemplate;
    [SerializeField] private StyleSheet loadingStyleSheet;
    [SerializeField] private Texture2D logoTexture;
    [SerializeField] private Texture2D glowTexture;

    [Header("Comportamiento")]
    [Tooltip("Tiempo minimo en pantalla, para que la animacion no se un parpadeo si la escena carga muy rapido.")]
    [SerializeField] private float minimumDisplaySeconds = 2f;
    [SerializeField] private float tipIntervalSeconds = 2.2f;

    [Header("Tips")]
    [SerializeField] private string[] activeTips;
    
    [SerializeField]
    private string[] loginTips =
    {
        "Encendiendo las luces...",
        "Calibrando el termostato...",
        "Despertanto a Mía...",
        "Preparando tu hogar...",
    };

    private class Particle 
    {
        public VisualElement Element;
        public float X;     // % del ancho
        public float Y;     // % del alto
        public float Speed;
    }

    private TemplateContainer overlay;
    private VisualElement orbitOuter, orbitMiddle, orbitInner, logo, progressFill, particlesLayer;
    private Label greetingLabel, tipLabel, percentLabel;
    private readonly List<Particle> particles = new List<Particle>();

    private bool isLoading;
    private float outerAngle, middleAngle, innerAngle;
    private float targetProgress, displayedProgress;
    private float tipTimer;
    private int tipIndex;

    /// <summary>
    /// Muestra la pantalla de carga y carga la escena indicada.
    /// </summary>
    /// <param name="sceneName">Nombre de la escena destino (debe estar en Build Settings).</param>
    /// <param name="userName">Nombre para el saludo. Si es vacio se usa un saludo generico.</param>
    public void LoadScene(string sceneName, string userName) 
    {
        if (isLoading)
        {
            return;
        }

        isLoading = true;
        
        activeTips = loginTips;
        BuildOverlayIfNeeded();

        greetingLabel.text = string.IsNullOrWhiteSpace(userName) ? "Bienvenido a casa" : "Bienvenido, " + userName;

        StartCoroutine(LoadRoutine(sceneName));
    }

    public void LoadScene(string sceneName, string greeting, string[] customTips)
    {
        if (isLoading)
        {
            return;
        }

        activeTips = (customTips != null && customTips.Length > 0) ? customTips : loginTips;
        isLoading = true;
        BuildOverlayIfNeeded();
        greetingLabel.text = greeting;
        tipLabel.text = activeTips[0];
        StartCoroutine(LoadRoutine(sceneName));
    }

    private void BuildOverlayIfNeeded() 
    {
        if (overlay != null)
        {
            return;
        }

        VisualElement root = GetComponent<UIDocument>().rootVisualElement;

        if (loadingStyleSheet != null && !root.styleSheets.Contains(loadingStyleSheet))
        {
            root.styleSheets.Add(loadingStyleSheet);
        }

        overlay = loadingTemplate.Instantiate();
        overlay.AddToClassList(CLASS_LOADING);
        overlay.pickingMode = PickingMode.Position; // bloquea clicks en el menu de abajo
        overlay.style.display = DisplayStyle.None;
        root.Add(overlay); // ultimo hijo = se dibuja encima de todo

        orbitOuter = overlay.Q<VisualElement>("Orbit_Outer");
        orbitMiddle = overlay.Q<VisualElement>("Orbit_Middle");
        orbitInner = overlay.Q<VisualElement>("Orbit_Inner");
        logo = overlay.Q<VisualElement>("Loading_Logo");
        progressFill = overlay.Q<VisualElement>("Progress_Fill");
        particlesLayer = overlay.Q<VisualElement>("Particles_Layer");
        greetingLabel = overlay.Q<Label>("Lbl_Greeting");
        tipLabel = overlay.Q<Label>("Lbl_Tip");
        percentLabel = overlay.Q<Label>("Lbl_Percent");

        if (logoTexture != null)
        {
            logo.style.backgroundImage = new StyleBackground(logoTexture);
        }

        VisualElement glow = overlay.Q<VisualElement>("Loading_Glow");

        if (glowTexture != null)
        {
            glow.style.backgroundImage = new StyleBackground(glowTexture);
        }

        CreateParticles();

        if (activeTips.Length > 0)
        {
            tipLabel.text = activeTips[0];
        }
    }

    private void ShowOverlay() 
    {
        overlay.style.display = DisplayStyle.Flex;

        // La clase se agrega un instante despues para que la transicion de opacidad empiece desde 0.
        // Si se agrega en el mismo frame, no hay fade
        overlay.schedule.Execute(() => overlay.AddToClassList(CLASS_LOADING_VISIBLE)).StartingIn(OVERLAY_FADE_DELAY_MS);
    }

    private IEnumerator LoadRoutine(string sceneName) 
    {
        // 1. Primero se desvanece y se oculta el menu del login
        FadeOutLoginUi();

        yield return new WaitForSecondsRealtime(LOGIN_FADE_OUT_SECONDS);

        HideEverythingExceptOverlay();

        // 2. Solo despues de que se desvanezca el menu principal del login, aparecera la pantalla de carga
        ShowOverlay();

        yield return new WaitForSecondsRealtime(FADE_IN_SECONDS);

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;

        float elapsed = 0;

        while (true) 
        {
            elapsed += Time.unscaledDeltaTime;

            float realProgress = Mathf.Clamp01(operation.progress / SCENE_READY_PROGRESS);
            float timeProgress = Mathf.Clamp01(elapsed / minimumDisplaySeconds);
            targetProgress = Mathf.Min(realProgress, timeProgress);

            bool sceneReady = realProgress >= 1f;
            bool minimumTimeMet = timeProgress >= 1f;
            bool barFinished = displayedProgress >= DISPLAY_COMPLETE_THRESHOLD;

            if (sceneReady && minimumTimeMet && barFinished)
            {
                break;
            }

            yield return null;
        }

        yield return new WaitForSecondsRealtime(HOLD_AT_FULL_SECONDS);
        operation.allowSceneActivation = true;
    }

    #region Animacion (cada frame)

    private void Update()
    {
        if (!isLoading || overlay == null)
        {
            return;
        }

        float dt = Time.unscaledDeltaTime;

        AnimateRings(dt);
        AnimateLogoPulse();
        AnimateParticles(dt);
        AnimateProgress(dt);
        AnimateTips(dt);
    }

    private void AnimateRings(float dt) 
    {
        outerAngle += OUTER_RING_DEGREES_PER_SECOND * dt;
        middleAngle += MIDDLE_RING_DEGREES_PER_SECOND * dt;
        innerAngle += INNER_RING_DEGREES_PER_SECOND * dt;

        orbitOuter.style.rotate = new Rotate(new Angle(outerAngle, AngleUnit.Degree));
        orbitMiddle.style.rotate = new Rotate(new Angle(middleAngle, AngleUnit.Degree));
        orbitInner.style.rotate = new Rotate(new Angle(innerAngle, AngleUnit.Degree));
    }

    private void AnimateLogoPulse() 
    {
        float scale = 1f + Mathf.Sin(Time.unscaledTime * LOGO_PULSE_SPEED) * LOGO_PULSE_AMPLITUDE;
        logo.style.scale = new Scale(new Vector3(scale, scale, 1f));
    }

    private void CreateParticles() 
    {
        for (int i = 0; i < PARTICLE_COUNT; i++)
        {
            float size = Random.Range(PARTICLE_MIN_SIZE, PARTICLE_MAX_SIZE);

            VisualElement element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.AddToClassList(CLASS_PARTICLE);
            element.style.width = size;
            element.style.height = size;
            element.style.borderTopLeftRadius = size;
            element.style.borderTopRightRadius = size;
            element.style.borderBottomLeftRadius = size;
            element.style.borderBottomRightRadius = size;
            particlesLayer.Add(element);

            particles.Add(new Particle
            {
                Element = element,
                X = Random.Range(0f, 100f),
                Y = Random.Range(0f, 100f),
                Speed = Random.Range(PARTICLE_MIN_SPEED, PARTICLE_MAX_SPEED)
            });
        }
    }

    private void AnimateParticles(float dt) 
    {
        foreach (Particle p in particles)
        {
            p.Y -= p.Speed * dt;

            if (p.Y < -5f)
            {
                p.Y = 150f;
                p.X = Random.Range(0f, 100f);
            }

            p.Element.style.left = Length.Percent(p.X);
            p.Element.style.top = Length.Percent(p.Y);
        }
    }

    private void AnimateProgress(float dt) 
    {
        displayedProgress = Mathf.Lerp(displayedProgress, targetProgress, 1f - Mathf.Exp(-PROGRESS_SMOOTHING_SPEED * dt));

        progressFill.style.width = Length.Percent(displayedProgress * 100f);
        percentLabel.text = Mathf.RoundToInt(displayedProgress * 100f) + "%";
    }

    private void AnimateTips(float dt) 
    {
        if (activeTips.Length < 2)
        {
            return;
        }

        tipTimer += dt;
        if (tipTimer < tipIntervalSeconds) { return; }
        tipTimer = 0f;

        tipLabel.AddToClassList(CLASS_TIP_HIDDEN);

        tipLabel.schedule.Execute(() => 
        {
            tipIndex = (tipIndex + 1) % activeTips.Length;
            tipLabel.text = activeTips[tipIndex];
            tipLabel.RemoveFromClassList(CLASS_TIP_HIDDEN);
        }).StartingIn(TIP_FADE_MS);
    }

    #endregion

    #region Ocultar otras UI (Menu principal de Login)

    /// <summary>
    /// Oculta el resto de la UI del documento una vez que el overlay ya cubrio
    /// la pantalla. display:none saca los elementos del layout y del render, asi
    /// que no se ven ni consumen recursos mientras carga la escena.
    /// </summary>
    private void HideEverythingExceptOverlay() 
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;

        foreach (VisualElement child in root.Children())
        {
            if (child != overlay)
            {
                child.style.display = DisplayStyle.None;
            }
        }
    }

    /// <summary>
    /// Empieza el desvanecimiento de toda la UI del login (menos el overlay).
    /// La animacion la hace la transicion USS de la clase login--hiding.
    /// </summary>
    private void FadeOutLoginUi() 
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;

        foreach (VisualElement child in root.Children())
        {
            if (child != overlay)
            {
                child.AddToClassList(CLASS_LOGIN_HIDING);
            }
        }
    }

    #endregion
}
