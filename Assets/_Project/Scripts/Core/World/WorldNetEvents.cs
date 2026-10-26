using JogoDaTrupe.Core.Events;

namespace JogoDaTrupe.Core.World
{
    // Eventos de rede do MUNDO COMPARTILHADO (M-NET-7 P1). Só primitivos — vivem no
    // Core puro (o alvo/ficha viaja por id/guid; quem recebe reconstrói).
    //
    // Os *Requested são INTENÇÕES (NetEventScope.Intent): numa máquina SEM autoridade
    // (cliente), o gameplay não muta o mundo — publica o pedido; o
    // NetworkWorldRequestForwarder o leva ao servidor, que executa DE VERDADE com os
    // mesmos métodos do solo. Intenção nunca é relayada de volta (fluxo é só
    // cliente→servidor). Padrão Input → Intenção → Ação (Multiplayer.md #4), o mesmo
    // do DamageRequested verificado no M-NET-6.

    /// <summary>
    /// Pedido de COLETA de um nó de recurso (tecla E numa máquina sem autoridade).
    /// O nó é identificado pelo EntityGuid — a cena de co-op é o mesmo asset nas duas
    /// máquinas, então o guid authored é idêntico nos dois lados.
    /// </summary>
    public struct HarvestRequested : IGameEvent
    {
        public string NodeGuid; // EntityGuid do nó (igual nas 2 máquinas)
        public int OwnerId;     // quem colhe (o item vai para o inventário dele)
    }

    /// <summary>
    /// Pedido de CRAFT de uma receita. O servidor valida os ingredientes no
    /// inventário AUTORITATIVO (o espelho do cliente pode estar 1 tick atrás).
    /// </summary>
    public struct CraftRequested : IGameEvent
    {
        public string RecipeId; // recipeId → RecipeDefinition via DefinitionRegistry
        public int OwnerId;
    }

    /// <summary>
    /// Pedido de CONSTRUÇÃO de uma estrutura numa posição da grade. O servidor
    /// valida e debita o custo, e spawna o prefab de rede (aparece nas 2 telas).
    /// A posição já vem "snapada" pela prévia local (apresentação).
    /// </summary>
    public struct BuildRequested : IGameEvent
    {
        public string StructureId; // structureId → StructureDefinition via DefinitionRegistry
        public float X, Y, Z;      // posição de encaixe (primitivos — Vector3 não é necessário)
        public int OwnerId;
    }

    /// <summary>
    /// Pedido de SINCRONIZAÇÃO (captura) de uma criatura enfraquecida — o canal de
    /// segurar E completou no cliente. O servidor arbitra: o PRIMEIRO pedido válido
    /// captura ("a criatura é de um jogador só" — decisão do Diretor, 02/07/2026).
    /// </summary>
    public struct SyncRequested : IGameEvent
    {
        public string CreatureGuid; // EntityGuid da criatura (réplica e original compartilham)
        public int OwnerId;
    }

    /// <summary>
    /// SIMULAÇÃO (relayada 1× servidor→clientes): o estoque de um nó de recurso
    /// mudou. O nó espelho do cliente aplica o valor (e escurece ao esgotar) — a
    /// mesma ideia do NetworkHealth, mas por relay (nó não é NetworkObject; o
    /// casamento é pelo EntityGuid compartilhado).
    /// </summary>
    public struct NodeStockChanged : IGameEvent
    {
        public string NodeGuid;
        public int Remaining; // estoque restante após a mudança (0 = esgotado)
    }
}
