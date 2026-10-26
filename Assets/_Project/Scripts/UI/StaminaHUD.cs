using UnityEngine;
using JogoDaTrupe.Gameplay.Characters;

namespace JogoDaTrupe.UI
{
    /// <summary>
    /// (Aposentada) A estamina agora aparece na HUD de vitais bonita do
    /// <see cref="HealthBarHUD"/> (barra ciano abaixo da vida). Esta classe é mantida
    /// só para o componente que já está serializado no P_Player/cena não virar
    /// "script faltando" — fica inerte. Pode ser removida do prefab num passe de limpeza.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StaminaHUD : MonoBehaviour
    {
        [SerializeField] private Stamina _stamina; // legado (não usado)
    }
}
