using JogoDaTrupe.Core.Events;

namespace JogoDaTrupe.Core.Incorporation
{
    // Eventos de rede da INCORPORAÇÃO (M-NET-8 P1). Só primitivos — vivem no Core
    // puro (a habilidade viaja por abilityId; quem recebe reconstrói pelo registry).
    //
    // Os *Requested são INTENÇÕES (NetEventScope.Intent): numa máquina SEM autoridade
    // (cliente), a progressão não muta local — publica o pedido; o
    // NetworkWorldRequestForwarder o leva ao servidor, que executa DE VERDADE com os
    // mesmos métodos do solo (IncorporationRequestExecutor). Intenção nunca é relayada
    // de volta. Mesmo padrão do Harvest/Craft/Build/Sync do M-NET-7.

    /// <summary>
    /// Pedido de EQUIPAR uma habilidade incorporável (clique no Altar). O servidor
    /// valida no MemoryBank AUTORITATIVO (o Eco está mesmo incorporável?) e na
    /// PROXIMIDADE REAL do Altar — nunca no bool do cliente.
    /// </summary>
    public struct EquipRequested : IGameEvent
    {
        public string AbilityId; // abilityId → AbilityDefinition via DefinitionRegistry
        public int OwnerId;
    }

    /// <summary>Pedido de DESEQUIPAR o slot (clique no Altar; validação idem equip).</summary>
    public struct UnequipRequested : IGameEvent
    {
        public int Slot;
        public int OwnerId;
    }

    /// <summary>
    /// Pedido de INVESTIR um ponto num nó de especialização da habilidade do slot
    /// (clique no Altar). O servidor valida pontos/teto no estado autoritativo.
    /// </summary>
    public struct InvestNodeRequested : IGameEvent
    {
        public int Slot;
        public int NodeIndex;
        public int OwnerId;
    }

    /// <summary>
    /// Pedido de RESPEC do slot (reverte nós, devolve pontos). O servidor valida a
    /// proximidade do Altar e DEBITA o custo (🎨 2 Pedra — decisão de 03/07/2026)
    /// no inventário autoritativo antes de reverter.
    /// </summary>
    public struct RespecRequested : IGameEvent
    {
        public int Slot;
        public int OwnerId;
    }

    /// <summary>
    /// Pedido de INVESTIR um ponto de atributo (ficha, tecla C — SEM exigência de
    /// Altar, paridade com o solo). O servidor valida os pontos disponíveis.
    /// </summary>
    public struct InvestAttributeRequested : IGameEvent
    {
        public int Attribute; // (int)AttributeType — primitivo, o enum não viaja
        public int OwnerId;
    }
}
