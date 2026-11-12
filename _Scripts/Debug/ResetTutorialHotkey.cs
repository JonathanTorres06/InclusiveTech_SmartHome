/*
Responsabilidad: permitir reiniciar el tutorial y el saludo matutino de Mia
con un atajo de teclado, sin tocar el registro de Windows. Pensado para
dejarlo en la escena de Login y volver a oir el tutorial al entrar a la casa.

Atajo: Ctrl + Shift + R
*/

using UnityEngine;
using UnityEngine.InputSystem;

public class ResetTutorialHotkey : MonoBehaviour
{
    // Mismas claves que usa MorningGreetingManager
    private const string PREFS_KEY_FIRST_TIME_DONE = "Mia_FirstTimeGreetingDone";
    private const string PREFS_KEY_LAST_GREETING_DATE = "Mia_LastGreetingDate";

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) { return; }

        bool ctrl = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
        bool shift = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;

        if (ctrl && shift && keyboard.rKey.wasPressedThisFrame)
        {
            ResetTutorial();
        }
    }

    private void ResetTutorial()
    {
        PlayerPrefs.DeleteKey(PREFS_KEY_FIRST_TIME_DONE);
        PlayerPrefs.DeleteKey(PREFS_KEY_LAST_GREETING_DATE);
        PlayerPrefs.Save();
        Debug.Log("[ResetTutorialHotkey] Tutorial y saludo matutino reiniciados. Suena como primera vez al entrar a la casa.");
    }
}