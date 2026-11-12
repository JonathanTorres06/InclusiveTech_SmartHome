// =============================================================
// Representa los tres nombres que puede tener una habitacion.
//
// internal_name: tecnico, inmutable, definido una sola vez desde
//                Unity. Nunca se usa para reconocimiento de voz.
//
// display_name:  nombre principal, editable por el usuario desde
//                el panel web. Participa en el reconocimiento.
//
// aliases:       nombres alternativos adicionales, tambien
//                editables. Participan en el reconocimiento en
//                igualdad de condiciones con display_name.
// =============================================================

using System.Collections.Generic;

/// <summary>
/// Conjunto de nombres asociados a una habitacion.
/// </summary>
public class RoomNameData
{
    // Identificador tecnico inmutable. Nunca se compara contra voz.
    public string InternalName { get; set; }

    // Nombre principal visible, editable por el usuario
    public string DisplayName { get; set; }

    // Nombres alternativos adicionales, editables por el usuario. Maximo 5
    public List<string> Aliases { get; set; } = new List<string>();
}
