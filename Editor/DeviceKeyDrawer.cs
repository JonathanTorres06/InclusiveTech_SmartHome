#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// Dibuja en el Inspector los campos marcados con [DeviceKey] como un
/// desplegable con los dispositivos del backend, mas un boton para refrescar.
/// Si el valor guardado ya no existe en el backend lo marca como NO REGISTRADO
/// en lugar de borrarlo en silencio.
/// </summary>
[CustomPropertyDrawer(typeof(DeviceKeyAttribute))]
public class DeviceKeyDrawer : PropertyDrawer
{
    private const float RefreshButtonWidth = 26f;
    private const float Spacing = 2f;
    private const string UnassignedLabel = "(sin asignar)";
    private const string UnregisteredSuffix = "  [NO REGISTRADO]";

    // Cache estatico por tipo: evita pedir al backend en cada repintado
    private static readonly Dictionary<string, List<DeviceDirectoryClient.DeviceEntry>> Cache =
        new Dictionary<string, List<DeviceDirectoryClient.DeviceEntry>>();
    private static readonly HashSet<string> Loading = new HashSet<string>();
    private static readonly Dictionary<string, string> Errors = new Dictionary<string, string>();

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var attr = (DeviceKeyAttribute)attribute;
        float line = EditorGUIUtility.singleLineHeight;
        return Errors.ContainsKey(attr.DeviceType) ? line * 2f + Spacing : line;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var attr = (DeviceKeyAttribute)attribute;

        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.LabelField(position, label.text, "Usa [DeviceKey] solo con campos string.");
            return;
        }

        EnsureLoaded(attr.DeviceType);

        float line = EditorGUIUtility.singleLineHeight;
        var popupRect = new Rect(position.x, position.y, position.width - RefreshButtonWidth - Spacing, line);
        var buttonRect = new Rect(popupRect.xMax + Spacing, position.y, RefreshButtonWidth, line);

        Cache.TryGetValue(attr.DeviceType, out var devices);

        // Opciones: [0] = sin asignar, luego los dispositivos del backend
        var keys = new List<string> { string.Empty };
        var labels = new List<string> { UnassignedLabel };

        if (devices != null)
        {
            foreach (var device in devices)
            {
                keys.Add(device.device_key);
                // El "/" crearia submenus en el popup de Unity, se reemplaza
                labels.Add($"{device.device_key}  ({device.display_name})".Replace("/", "-"));
            }
        }

        string current = property.stringValue ?? string.Empty;
        int index = keys.IndexOf(current);

        if (index < 0)
        {
            keys.Add(current);
            labels.Add(current + UnregisteredSuffix);
            index = keys.Count - 1;
        }

        EditorGUI.BeginChangeCheck();
        int selected = EditorGUI.Popup(popupRect, label.text, index, labels.ToArray());
        if (EditorGUI.EndChangeCheck())
        {
            ApplySelection(property, attr, keys[selected], devices);
        }

        var refreshIcon = EditorGUIUtility.IconContent("Refresh", "Releer dispositivos del backend");
        if (GUI.Button(buttonRect, refreshIcon))
        {
            Reload(attr.DeviceType);
        }

        if (Errors.TryGetValue(attr.DeviceType, out string error))
        {
            var errorRect = new Rect(position.x, position.y + line + Spacing, position.width, line);
            EditorGUI.HelpBox(errorRect, $"Backend no disponible: {error}", MessageType.Warning);
        }
    }

    /// <summary>
    /// Guarda la clave elegida y, si el atributo lo pide, copia el
    /// InternalName de la habitacion al campo hermano indicado.
    /// </summary>
    private static void ApplySelection(SerializedProperty property, DeviceKeyAttribute attr, string key, List<DeviceDirectoryClient.DeviceEntry> devices)
    {
        property.stringValue = key;

        if (string.IsNullOrEmpty(attr.NodeNameField) || devices == null) { return; }

        var entry = devices.Find(d => d.device_key == key);
        if (entry == null || string.IsNullOrEmpty(entry.NodeInternalName)) { return; }

        var nodeNameProperty = property.serializedObject.FindProperty(attr.NodeNameField);
        if (nodeNameProperty != null)
        {
            nodeNameProperty.stringValue = entry.NodeInternalName;
        }
    }

    /// <summary>Pide la lista solo la primera vez (no reintenta solo si hubo error).</summary>
    private static void EnsureLoaded(string deviceType)
    {
        if (Cache.ContainsKey(deviceType) || Loading.Contains(deviceType) || Errors.ContainsKey(deviceType))
        {
            return;
        }

        Reload(deviceType);
    }

    private static void Reload(string deviceType)
    {
        if (Loading.Contains(deviceType)) { return; }

        Loading.Add(deviceType);
        Errors.Remove(deviceType);

        DeviceDirectoryClient.FetchDevices(
            deviceType,
            devices =>
            {
                Cache[deviceType] = devices;
                Loading.Remove(deviceType);
                InternalEditorUtility.RepaintAllViews();
            },
            error =>
            {
                Errors[deviceType] = error;
                Loading.Remove(deviceType);
                InternalEditorUtility.RepaintAllViews();
            });
    }
}
#endif
