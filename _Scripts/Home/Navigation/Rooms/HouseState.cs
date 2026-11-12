using UnityEngine;

public class HouseState : MonoBehaviour
{
    #region Singleton

    public static HouseState Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else 
        {
            Destroy(Instance);
        }
    }

    #endregion

    [Header("Sub-Sistemas (Gerentes)")]
    [Space(10)]
    public RoomManager roomManager;
    public LightManager lightManager;
    public TemperatureManager tempManager;
    // En el futuro se pueden agregar mas subsistemas

    // El cerebro llama a HouseState y HouseState redirige la orden
    public string GetFullSummary() 
    {
        return lightManager.GetSummary() + tempManager.GetSummary() + roomManager.GetSummary();
    }
}
