using System.Collections.Generic;

namespace JogoDaTrupe.Gameplay.Incorporation
{
    /// <summary>
    /// Regras PURAS do loadout de habilidades (testáveis sem abrir o jogo).
    /// Centraliza o limite de habilidades equipadas e as condições de equipar —
    /// fonte única de verdade para a regra "5 / uma por criatura / só no Altar".
    /// </summary>
    public static class LoadoutMath
    {
        /// <summary>
        /// Limite de habilidades equipadas simultaneamente. Motivo narrativo:
        /// abrigar frequências demais despedaçaria a mente do Sincronizador.
        /// (Antes 7 → 5 em 28/06/2026.)
        /// </summary>
        public const int MaxAbilities = 5;

        /// <summary>
        /// Pode equipar uma habilidade da família <paramref name="newFamilyId"/>,
        /// dadas as famílias já equipadas e se o jogador está num Altar?
        /// Devolve o motivo da recusa (para a UI) quando não pode.
        /// </summary>
        public static bool CanEquip(IReadOnlyList<string> equippedFamilyIds, string newFamilyId, bool atAltar, out string reason)
        {
            if (!atAltar)
            {
                reason = "Só é possível trocar habilidades num Altar.";
                return false;
            }

            if (string.IsNullOrEmpty(newFamilyId))
            {
                reason = "Habilidade sem família válida.";
                return false;
            }

            if (equippedFamilyIds != null)
            {
                if (equippedFamilyIds.Count >= MaxAbilities)
                {
                    reason = $"Limite de {MaxAbilities} habilidades equipadas atingido.";
                    return false;
                }

                for (int i = 0; i < equippedFamilyIds.Count; i++)
                {
                    if (equippedFamilyIds[i] == newFamilyId)
                    {
                        reason = "Já há uma habilidade desta criatura equipada (uma por criatura).";
                        return false;
                    }
                }
            }

            reason = null;
            return true;
        }
    }
}
