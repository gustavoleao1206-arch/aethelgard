using UnityEngine;

namespace JogoDaTrupe.Gameplay.Inventory
{
    /// <summary>
    /// Estoque de um nó de recurso: o ESTADO de runtime (quanto ainda há para
    /// colher). É uma classe PURA (sem MonoBehaviour), com a lógica de esgotamento
    /// testável em EditMode — mesmo padrão Model + componente de
    /// <see cref="InventoryModel"/>/<see cref="PlayerInventory"/>.
    ///
    /// O componente <see cref="ResourceNode"/> a envolve e cuida do visual/cena. Esta
    /// lógica é reusada por dois caminhos: a coleta manual do jogador e o Coletor
    /// Automático — por isso vale ter testada e num só lugar (DRY).
    /// </summary>
    public sealed class ResourceStock
    {
        public ResourceStock(int total)
        {
            Remaining = Mathf.Max(0, total);
        }

        /// <summary>Quanto ainda resta para colher.</summary>
        public int Remaining { get; private set; }

        /// <summary>Verdadeiro quando o estoque acabou.</summary>
        public bool IsDepleted => Remaining <= 0;

        /// <summary>
        /// Quanto desta colheita pode sair AGORA, limitado pelo que resta. Não remove
        /// nada — serve para "ver" antes de depositar (quem chama só remove, via
        /// <see cref="Take"/>, o que de fato coube no inventário).
        /// </summary>
        public int Available(int want)
        {
            if (want <= 0 || Remaining <= 0) return 0;
            return Mathf.Min(want, Remaining);
        }

        /// <summary>
        /// Remove até <paramref name="amount"/> do estoque (nunca fica negativo).
        /// Retorna quanto saiu de fato.
        /// </summary>
        public int Take(int amount)
        {
            if (amount <= 0 || Remaining <= 0) return 0;
            int taken = Mathf.Min(amount, Remaining);
            Remaining -= taken;
            return taken;
        }

        /// <summary>
        /// ESPELHA o estoque vindo da REDE (M-NET-7 P3): a réplica do cliente ajusta o
        /// restante para o valor autoritativo do servidor (chega por <c>NodeStockChanged</c>),
        /// sem passar por <see cref="Take"/>. Clampado em [0, +∞).
        /// </summary>
        public void SyncTo(int remaining)
        {
            Remaining = Mathf.Max(0, remaining);
        }
    }
}
