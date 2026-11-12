using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Ambiente de la pantalla de login segun el contexto: color del recuadro de la camara,
/// barra de escaneo, latido, tinte de fondo, bloqueo del menu y sacudida al denegar el acceso.
/// Cada clase se aplica al elemento que la usa, nunca al contenedor raiz.
/// </summary>
public sealed class LoginAtmosphereView : IDisposable
{
    private const string PULSE_CLASS = "camara_pulso";
    private const string SCAN_DOWN_CLASS = "escaner_abajo";
    private const string MENU_LOCKED_CLASS = "botones_bloqueados";
    private const string QUIT_LOCKED_CLASS = "salir_bloqueado";
    private const int PULSE_MS = 700;
    private const int SCAN_MS = 1100;
    private const int SHAKE_STEP_MS = 55;

    // Desplazamientos horizontales (px) de la sacudida, de mayor a menor
    private static readonly float[] ShakeOffsets = { -10f, 10f, -8f, 8f, -5f, 5f, 0f };

    private readonly VisualElement cameraColumn;
    private readonly VisualElement camera;
    private readonly VisualElement scanner;
    private readonly VisualElement ambient;
    private readonly VisualElement menu;
    private readonly VisualElement quitButton;
    private readonly IVisualElementScheduledItem pulseJob;
    private readonly IVisualElementScheduledItem scanJob;

    private ContextStyle currentStyle = LoginContextStyles.Of(LoginContext.Searching);
    private LoginContext currentContext = LoginContext.Searching;
    private bool contextLocksMenu;   // el contexto actual bloquea el menu (capturando, procesando...)
    private bool interactionLocked;  // bloqueo explicito de menu e icono de salir (ej. mientras se valida)

    public LoginAtmosphereView(VisualElement root)
    {
        cameraColumn = root.Q<VisualElement>("Columna_Camara");
        camera = root.Q<VisualElement>("Reconocimiento_Facial");
        scanner = root.Q<VisualElement>("Camara_Escaner");
        ambient = root.Q<VisualElement>("Ambiente");
        menu = root.Q<VisualElement>("Botones");
        quitButton = root.Q<VisualElement>("Btn_Salir");

        pulseJob = cameraColumn.schedule.Execute(TogglePulse).Every(PULSE_MS);
        scanJob = scanner.schedule.Execute(ToggleScan).Every(SCAN_MS);
    }

    /// <param name="replay">true si es una accion explicita; repite efectos como la sacudida aunque el contexto no cambie.</param>
    public void Apply(LoginContext context, bool replay)
    {
        bool changed = context != currentContext;

        if (changed)
        {
            ContextStyle next = LoginContextStyles.Of(context);

            cameraColumn.RemoveFromClassList(currentStyle.CameraClass);
            cameraColumn.AddToClassList(next.CameraClass);
            ambient.RemoveFromClassList(currentStyle.AmbientClass);
            ambient.AddToClassList(next.AmbientClass);

            currentStyle = next;
            currentContext = context;
            contextLocksMenu = next.LocksMenu;
            RefreshLocks();
        }

        if (context == LoginContext.Denied && (changed || replay)) Shake();
    }

    /// <summary>
    /// Bloquea o libera todo lo que el usuario puede pulsar (menu e icono de salir), sin importar el contexto.
    /// </summary>
    public void SetInteractionLocked(bool locked)
    {
        interactionLocked = locked;
        RefreshLocks();
    }

    private void RefreshLocks()
    {
        bool menuLocked = contextLocksMenu || interactionLocked;
        menu.SetEnabled(!menuLocked);
        menu.EnableInClassList(MENU_LOCKED_CLASS, menuLocked);

        quitButton.SetEnabled(!interactionLocked);
        quitButton.EnableInClassList(QUIT_LOCKED_CLASS, interactionLocked);
    }

    public void Dispose()
    {
        pulseJob?.Pause();
        scanJob?.Pause();
    }

    private void TogglePulse()
    {
        if (currentStyle.Pulses) cameraColumn.ToggleInClassList(PULSE_CLASS);
        else cameraColumn.RemoveFromClassList(PULSE_CLASS);
    }

    private void ToggleScan()
    {
        if (currentStyle.Scans) scanner.ToggleInClassList(SCAN_DOWN_CLASS);
        else scanner.RemoveFromClassList(SCAN_DOWN_CLASS);
    }

    private void Shake()
    {
        int step = 0;
        camera.schedule.Execute(() =>
        {
            camera.style.translate = new Translate(new Length(ShakeOffsets[step]), new Length(0f));
            step++;
        }).Every(SHAKE_STEP_MS).Until(() => step >= ShakeOffsets.Length);
    }
}