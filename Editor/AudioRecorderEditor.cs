#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Le decimos a Unity que este script modifica la forma en que se ve AudioRecorder
[CustomEditor(typeof(AudioRecorder))]
public class AudioRecorderEditor : Editor
{
    SerializedProperty selectedDeviceProp;

    void OnEnable()
    {
        // Conectamos con la variable oculta del script AudioRecorder
        selectedDeviceProp = serializedObject.FindProperty("selectedMicrophoneDevice");
    }

    public override void OnInspectorGUI()
    {
        // 1. Dibuja el Inspector normal (para que sigan saliendo AssistantConfig y tus eventos)
        DrawDefaultInspector();

        serializedObject.Update();

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Configuración de Hardware", EditorStyles.boldLabel);

        // 2. Obtenemos los microfonos disponibles en tiempo real (Edit Mode)
        string[] devices = Microphone.devices;

        if (devices.Length == 0)
        {
            EditorGUILayout.HelpBox("No se detectaron micrófonos conectados al PC.", MessageType.Error);
            return;
        }

        // 3. Buscamos que indice tiene el microfono que ya teniamos guardado
        int currentIndex = Mathf.Max(0, System.Array.IndexOf(devices, selectedDeviceProp.stringValue));

        // 4. Se dibuja el Popup (Dropdown) que muestra las opciones de microfonos.
        int newIndex = EditorGUILayout.Popup("Microfono Seleccionado", currentIndex, devices);

        // 5. Guardamos la seleccion
        selectedDeviceProp.stringValue = devices[newIndex];

        serializedObject.ApplyModifiedProperties();
    }
}
#endif