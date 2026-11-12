// =============================================================
// Contrato para obtener los nombres (internal_name, display_name,
// aliases) de cada habitacion.
//
// Responsabilidad: Desacoplar de donde vienen esos nombres
// (valores por defecto de Unity, o el microservicio de
// navegacion con su panel web) del resto del sistema.
//
// Patron Strategy: Misma logica aplicada a BaseTTSClient,
// BaseSTTClient y BaseLLMClient, ahora para el origen de
// nombres de habitaciones.
// =============================================================

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Provee los nombres completos (internal_name, display_name,
/// aliases) para cada RoomID.
/// </summary>
public interface IRoomNameProvider
{
    Dictionary<string, RoomNameData> GetAllRoomNames();
}
