using System.Collections.Generic;

namespace JogoDaTrupe.Gameplay.Inventory
{
    /// <summary>
    /// Índice LOCAL (por máquina) de nós de recurso por <c>EntityGuid</c> (M-NET-7 P3).
    /// Cada <see cref="ResourceNode"/> se registra no <c>Awake</c> e sai no <c>OnDestroy</c>.
    ///
    /// Serve ao executor de rede (server-side) para resolver, sem <c>FindObjects</c>,
    /// QUAL nó um <c>HarvestRequested</c> pediu — o casamento cruza a rede pelo guid, que
    /// é IDÊNTICO nos dois lados porque a cena de co-op é o mesmo asset (o guid é
    /// serializado). É um índice de CENA (lookup), não estado de jogo — mesmo espírito
    /// aceito do <c>DefinitionRegistry</c>; não é o <c>EntityGuidRegistry</c> global
    /// (esse segue adiado para o save de mundo).
    ///
    /// Estático porque é estado de PROCESSO (um índice por máquina). No MPPM cada player
    /// virtual roda em processo próprio, então não há contaminação entre eles.
    /// </summary>
    public static class WorldNodeIndex
    {
        private static readonly Dictionary<string, ResourceNode> Nodes = new Dictionary<string, ResourceNode>();

        public static void Register(string guid, ResourceNode node)
        {
            if (string.IsNullOrEmpty(guid) || node == null) return;
            Nodes[guid] = node;
        }

        public static void Unregister(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return;
            Nodes.Remove(guid);
        }

        /// <summary>O nó com este guid nesta máquina, ou null se não houver (ex.: já esgotado/destruído).</summary>
        public static ResourceNode Find(string guid)
            => !string.IsNullOrEmpty(guid) && Nodes.TryGetValue(guid, out ResourceNode node) ? node : null;

        /// <summary>Todos os nós registrados nesta máquina (M-NET-9 P3: o LateJoinWorldSync re-anuncia o estoque de cada um a quem entra).</summary>
        public static IEnumerable<ResourceNode> All => Nodes.Values;
    }
}
