using System.Collections.Generic;
using JogoDaTrupe.Data;

namespace JogoDaTrupe.Gameplay.Incorporation
{
    /// <summary>
    /// Regra pura do <b>"Eco a todos os contribuintes"</b> (M-NET-5): quem recebe o Eco de
    /// uma entidade derrotada. Cada contribuinte (jogador que causou dano) recebe UM Eco —
    /// desde que a entidade conceda uma habilidade (<c>grantedAbility</c>). Sem habilidade,
    /// sem contribuintes ou sem ficha → ninguém recebe.
    ///
    /// Pura (sem Unity) — o <c>EcoDropper</c> (MonoBehaviour) a consome ao reagir à morte.
    /// A dedução de "quem contribuiu" (dedupe, ignorar IA) já foi feita pelo
    /// <c>DamageContributors</c> no <c>Health</c>; aqui só se aplica o gate da habilidade.
    /// </summary>
    public static class EcoDrop
    {
        public static IEnumerable<int> Recipients(EnemyDefinition source, IReadOnlyCollection<int> contributors)
        {
            if (source == null || source.grantedAbility == null || contributors == null || contributors.Count == 0)
                return System.Array.Empty<int>();
            return contributors;
        }
    }
}
