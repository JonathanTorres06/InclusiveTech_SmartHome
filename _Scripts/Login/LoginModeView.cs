using UnityEngine.UIElements;

/// <summary>
/// Etiqueta de modo sobre el texto central ("INICIO DE SESION", "REGISTRO DE USUARIO"...)
/// y los pasos del registro (Rostro, Nombre). Solo presentacion.
/// </summary>
public sealed class LoginModeView
{
    private const string STEPS_VISIBLE_CLASS = "pasos_visibles";
    private const string STEP_ACTIVE_CLASS = "paso_activo";
    private const string STEP_DONE_CLASS = "paso_hecho";

    private static readonly string[] ModeClasses = { "modo_signin", "modo_primera", "modo_registro", "modo_sesion" };

    private readonly VisualElement chip;
    private readonly Label chipLabel;
    private readonly VisualElement stepFace;
    private readonly VisualElement stepName;

    public LoginModeView(VisualElement root)
    {
        chip = root.Q<VisualElement>("Contexto_Chip");
        chipLabel = root.Q<Label>("Chip_Texto");
        stepFace = root.Q<VisualElement>("Paso_Rostro");
        stepName = root.Q<VisualElement>("Paso_Nombre");
    }

    public void SetMode(LoginMode mode)
    {
        foreach (string cls in ModeClasses) chip.RemoveFromClassList(cls);

        switch (mode)
        {
            case LoginMode.FirstSetup:
                chipLabel.text = "PRIMERA CONFIGURACIÓN";
                chip.AddToClassList("modo_primera");
                break;
            case LoginMode.Registration:
                chipLabel.text = "REGISTRO DE USUARIO";
                chip.AddToClassList("modo_registro");
                break;
            case LoginMode.SessionActive:
                chipLabel.text = "SESIÓN ACTIVA";
                chip.AddToClassList("modo_sesion");
                break;
            default:
                chipLabel.text = "INICIO DE SESIÓN";
                chip.AddToClassList("modo_signin");
                break;
        }

        chip.EnableInClassList(STEPS_VISIBLE_CLASS, mode == LoginMode.Registration);
    }

    public void SetStep(RegistrationStep step)
    {
        SetStepState(stepFace, active: step == RegistrationStep.Face, done: step == RegistrationStep.Name || step == RegistrationStep.Done);
        SetStepState(stepName, active: step == RegistrationStep.Name, done: step == RegistrationStep.Done);
    }

    private static void SetStepState(VisualElement step, bool active, bool done)
    {
        step.EnableInClassList(STEP_ACTIVE_CLASS, active);
        step.EnableInClassList(STEP_DONE_CLASS, done);
    }
}
