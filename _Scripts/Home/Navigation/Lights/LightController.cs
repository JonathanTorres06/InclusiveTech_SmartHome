using System.Collections.Generic;
using UnityEngine;

public class LightController : MonoBehaviour
{
    [Header("Identificador Tecnico")]
    [Tooltip("Dispositivo registrado en el panel de desarrollo. Al elegirlo se autocompleta solo internalName.")]
    [DeviceKey(DeviceTypes.Light, nameof(internalName))]
    public string deviceKey;

    [Tooltip("Se autocompleta solo al seleccionar deviceKey. OJO, NO EDITAR A MANO.")]
    public string internalName;

    [Header("Materiales Lamparas")]
    [Tooltip("Materiales de las lamparas para hacer el cambio del material dependiendo si la lampara esta prendida o apagada")]
    public Material lightOn;
    public Material lightOff;

    [Header("Lights Settings")]
    [Tooltip("Se asignan las luces dependiendo de la cantidad de luces que hay en una habitacion")]
    public List<Light> lightsSource;

    public List<Renderer> meshRenderers;

    public void ToggleLights(bool state) 
    {
        if (lightsSource != null) 
        {
            foreach (Light light in lightsSource)
            {
                light.enabled = state;
            }
        }

        if (state)
        {
            for (int i = 0; i < meshRenderers.Count; i++)
            {
                meshRenderers[i].material = lightOn;
            }
        }
        else 
        {
            for (int i = 0; i < meshRenderers.Count; i++)
            {
                meshRenderers[i].material = lightOff;
            }
        }
    }
}
