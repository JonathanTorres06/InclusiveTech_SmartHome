using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Texto central de la pantalla de login (antes un texto fijo de bienvenida) que ahora cuenta
/// lo que esta pasando: indicador circular de color, texto con cambio suave y barra de progreso.
/// Solo presentacion, no conoce la logica de negocio.
/// </summary>
public sealed class LoginStatusView : IDisposable
{
    private const string PULSE_CLASS = "estado_pulso";
    private const string BUSY_CLASS = "estado_ocupado";
    private const string TEXT_CHANGING_CLASS = "texto_cambiando";
    private const string PROGRESS_VISIBLE_CLASS = "estado_progreso_visible";
    private const int PULSE_MS = 600;
    private const int TEXT_FADE_MS = 180;

    private static readonly string[] StatusClasses =
    {
        "estado_info", "estado_exito", "estado_aviso", "estado_ocupado", "estado_error"
    };

    private readonly VisualElement panel;
    private readonly Label label;
    private readonly VisualElement progressTrack;
    private readonly VisualElement progressFill;
    private readonly float holdSeconds;
    private readonly IVisualElementScheduledItem pulseJob;

    private string shownText;
    private string pendingText;
    private bool fading;
    private float holdUntil;
    private LoginContext currentContext = LoginContext.Searching;

    public LoginStatusView(VisualElement root, float holdSeconds)
    {
        this.holdSeconds = holdSeconds;

        panel = root.Q<VisualElement>("Estado_Principal");
        label = root.Q<Label>("Texto_Bienvenida");
        progressTrack = root.Q<VisualElement>("Estado_Progreso");
        progressFill = root.Q<VisualElement>("Estado_Progreso_Relleno");

        shownText = label.text;
        pulseJob = panel.schedule.Execute(TogglePulse).Every(PULSE_MS);
    }

    /// <summary>True mientras un mensaje importante (exito, aviso, error) sigue en su tiempo de lectura.</summary>
    public bool IsHolding => Time.unscaledTime < holdUntil;

    public void Show(LoginContext context, string text, bool animateText)
    {
        ContextStyle style = LoginContextStyles.Of(context);

        // El bucle de deteccion repite el mismo mensaje cada 0,15 s: solo se toca la UI si algo cambio
        if (context != currentContext)
        {
            foreach (string cls in StatusClasses) panel.RemoveFromClassList(cls);
            panel.AddToClassList(style.StatusClass);
            if (style.StatusClass != BUSY_CLASS) panel.RemoveFromClassList(PULSE_CLASS);
            currentContext = context;
        }

        holdUntil = style.HoldsMessage ? Time.unscaledTime + holdSeconds : 0f;
        SetText(text, animateText);
    }

    public void SetProgress(int current, int total)
    {
        if (total <= 0) return;

        progressTrack.AddToClassList(PROGRESS_VISIBLE_CLASS);
        progressFill.style.width = Length.Percent(Mathf.Clamp01(current / (float)total) * 100f);
    }

    public void HideProgress()
    {
        progressTrack.RemoveFromClassList(PROGRESS_VISIBLE_CLASS);
        progressFill.style.width = Length.Percent(0f);
    }

    public void Dispose() => pulseJob?.Pause();

    private void SetText(string text, bool animate)
    {
        if (text == shownText) return;

        shownText = text;
        pendingText = text;

        if (!animate)
        {
            label.text = text;
            return;
        }

        if (fading) return; // el cambio en curso ya tomara el ultimo texto pendiente

        fading = true;
        label.AddToClassList(TEXT_CHANGING_CLASS);
        label.schedule.Execute(() =>
        {
            label.text = pendingText;
            label.RemoveFromClassList(TEXT_CHANGING_CLASS);
            fading = false;
        }).StartingIn(TEXT_FADE_MS);
    }

    private void TogglePulse()
    {
        if (panel.ClassListContains(BUSY_CLASS)) panel.ToggleInClassList(PULSE_CLASS);
        else panel.RemoveFromClassList(PULSE_CLASS);
    }
}
