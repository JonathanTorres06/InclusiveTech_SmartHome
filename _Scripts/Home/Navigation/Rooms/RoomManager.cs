// =============================================================
//
// Responsabilidad: Punto unico de traduccion entre RoomID
// (identificador interno estable de Unity) y los nombres que
// el usuario reconoce por voz (display_name + aliases).
//
// IMPORTANTE: internal_name NUNCA se usa para reconocimiento
// de voz. Solo display_name y aliases compiten en la busqueda,
// en igualdad de condiciones.
// =============================================================

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RoomManager : MonoBehaviour
{
    // Por defecto usa el proveedor sin backend. Se puede inyectar
    // NavigationServiceAliasProvider mas adelante sin tocar nada mas.
    private IRoomNameProvider _nameProvider = new DefaultRoomNameProvider();
    private Dictionary<string, RoomNameData> _currentRoomNames;

    private void Awake()
    {
        RefreshRoomNames();
    }

    /// <summary>
    /// Recarga la tabla de nombres desde el proveedor actual.
    /// Llamar de nuevo si el usuario edita nombres en tiempo real
    /// desde el panel web mientras la app esta abierta.
    /// </summary>
    public void RefreshRoomNames() 
    {
        _currentRoomNames = _nameProvider.GetAllRoomNames();
    }

    /// <summary>
    /// Permite reemplazar el proveedor de nombres en tiempo de
    /// ejecucion (por ejemplo, al inyectar el proveedor real del
    /// microservicio de navegacion cuando este listo).
    /// </summary>
    public void SetNameProvider(IRoomNameProvider provider) 
    {
        _nameProvider = provider;
        RefreshRoomNames();
    }

    // Devuelve el display_name actual de una habitacion
    public string GetDisplayName(string internalName) 
    {
        if (string.IsNullOrWhiteSpace(internalName))
        {
            return internalName;
        }

        string normalized = internalName.Trim().ToUpperInvariant();

        return _currentRoomNames.TryGetValue(normalized, out var data) ? data.DisplayName : internalName;
    }

    /// <summary>
    /// Busca a que RoomID corresponde un texto dicho por el usuario
    /// o devuelto por el LLM. Compara contra display_name y aliases
    /// EN IGUALDAD DE CONDICIONES. internal_name nunca participa
    /// en esta busqueda.
    /// </summary>
    /// <param name="spokenText">Texto a resolver. Ej: "mi cuarto".</param>
    /// <returns>RoomID encontrado, o RoomID.None si no hay coincidencia.</returns>
    public string ResolveNodeName(string spokenText) 
    {
        if (string.IsNullOrEmpty(spokenText))
        {
            return null;
        }

        string normalized = spokenText.Trim().ToLowerInvariant();

        foreach (var pair in _currentRoomNames)
        {
            string internalName = pair.Key;
            RoomNameData data = pair.Value;

            // display_name compite en igualdad de condiciones con los aliases
            if (data.DisplayName.Trim().ToLowerInvariant() == normalized)
            {
                return internalName;
            }

            if (data.Aliases.Any(alias => alias.Trim().ToLowerInvariant() == normalized))
            {
                return internalName;
            }
        }

        Debug.LogWarning("[RoomManager] No se encontro ninguna habitacion para: " + spokenText);

        return null;
    }

    /// <summary>
    /// Lista combinada de todos los nombres reconocibles por voz
    /// (display_name + aliases de todas las habitaciones), usada
    /// para construir available_rooms en el HouseContext que se
    /// envia al servidor it_ai_assistant. Le da al LLM maxima
    /// capacidad de reconocer lo que el usuario dijo.
    /// </summary>
    public List<string> GetAllSearchableNames() 
    {
        var names = new List<string>();

        foreach (var data in _currentRoomNames.Values)
        {
            names.Add(data.DisplayName);
            names.AddRange(data.Aliases);
        }

        return names;
    }

    public int GetTotalRooms() 
    {
        return _currentRoomNames.Count;
    }

    public string GetSummary() 
    {
        return "La casa cuenta con " + GetTotalRooms() + " lugares en la casa.";
    }

    /// <summary>
    /// Lista de los nombres principales (display_name) de cada habitacion,
    /// uno por habitacion, sin incluir aliases. Usada cuando el usuario
    /// pide que se le nombren las habitaciones, para no confundirlo
    /// listando tambien los alias.
    /// </summary>
    public List<string> GetAllDisplayNames() 
    {
        return _currentRoomNames.Values.Select(data => data.DisplayName).ToList();
    }
}
