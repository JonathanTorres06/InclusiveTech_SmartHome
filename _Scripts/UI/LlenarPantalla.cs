using UnityEngine;
using UnityEngine.UIElements;

public class LlenarPantalla : MonoBehaviour
{
    void Start()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        root.style.flexGrow = 1;
        root.style.height = Length.Percent(100);
    }
}