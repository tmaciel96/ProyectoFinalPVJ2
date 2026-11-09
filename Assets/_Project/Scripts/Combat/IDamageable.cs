// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// Contrato compartido de daño (plan de implementación, "Contratos compartidos"; dueño: M4).
// Lo implementan los enemigos y lo usan la pala (M1-3), la piedra (M1-5) y las misiones (M3).
// =================================================================================================

using UnityEngine;

public interface IDamageable
{
    /// <param name="amount">Daño a aplicar.</param>
    /// <param name="origin">Posición desde donde vino el golpe (para retroceso o feedback direccional).</param>
    void TakeDamage(int amount, Vector3 origin);
}
