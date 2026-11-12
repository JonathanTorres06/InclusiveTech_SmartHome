using UnityEngine;

public class TemperatureManager : MonoBehaviour
{
    public float temperaturaActual = 22.0f;

    public void ModifyTemp(float delta) 
    {
        temperaturaActual += delta;
        Debug.Log("<color=orange>[TEMP]</color> Nueva Temperatura: " + temperaturaActual);
    }

    public string GetSummary() 
    {
        return "La temperatura actual es de " + temperaturaActual + "°C";
    }
}
