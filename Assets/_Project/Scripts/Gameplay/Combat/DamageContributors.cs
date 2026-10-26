using System.Collections.Generic;
using JogoDaTrupe.Core.Ownership;

namespace JogoDaTrupe.Gameplay.Combat
{
    /// <summary>
    /// Conjunto de "contribuintes" de uma entidade: os jogadores (por <c>OwnerId</c>) que
    /// causaram dano nela. É a base do <b>"Eco a todos os contribuintes"</b> (M-NET-5): na
    /// morte, cada contribuinte recebe um Eco (o <c>EcoDropper</c> lê isto — P2).
    ///
    /// Regra pura (testável): dano SEM dono (IA/ambiente = <see cref="Ownable.NoOwner"/>)
    /// NÃO entra — só jogadores viram donos de Eco. O <see cref="HashSet{T}"/> DEDUPLICA:
    /// quem bateu 10× conta 1×.
    ///
    /// Sem Unity — o <c>Health</c> o compõe, no mesmo espírito de <c>ResourceStock</c>/
    /// <c>InventoryModel</c> (lógica pura embrulhada por um MonoBehaviour).
    /// </summary>
    public sealed class DamageContributors
    {
        private readonly HashSet<int> _ids = new HashSet<int>();

        /// <summary>Donos (OwnerId) que contribuíram com dano. Só leitura.</summary>
        public IReadOnlyCollection<int> Ids => _ids;

        /// <summary>Quantidade de contribuintes distintos.</summary>
        public int Count => _ids.Count;

        /// <summary>
        /// Registra o atacante. Ignora <see cref="Ownable.NoOwner"/> (dano de IA/ambiente,
        /// que não é um jogador contribuinte).
        /// </summary>
        public void Register(int attackerOwnerId)
        {
            if (attackerOwnerId != Ownable.NoOwner) _ids.Add(attackerOwnerId);
        }

        /// <summary>Esvazia o conjunto (ex.: reaproveitar a entidade).</summary>
        public void Clear() => _ids.Clear();
    }
}
