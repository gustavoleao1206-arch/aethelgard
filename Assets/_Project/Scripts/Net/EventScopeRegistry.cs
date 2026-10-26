using System;
using System.Collections.Generic;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Combat;
using JogoDaTrupe.Core.Incorporation;
using JogoDaTrupe.Core.World;
using JogoDaTrupe.Data;

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// Tabela única <c>tipo de evento → <see cref="NetEventScope"/></c> (M-NET-4 P1).
    ///
    /// É metadado imutável (só leitura), no mesmo espírito aceito do <c>DefinitionRegistry</c>:
    /// não guarda estado de jogo, então não fere o princípio anti-singleton (Multiplayer #6).
    ///
    /// <b>Falha segura (regra central):</b> o default é <see cref="NetEventScope.Presentation"/>.
    /// Um evento novo que alguém esqueça de classificar aqui NÃO vaza pela rede por acidente —
    /// fica "local" até ser explicitamente promovido a <see cref="NetEventScope.Simulation"/>.
    /// Por isso só os eventos de SIMULAÇÃO entram no mapa; apresentação e derivados ficam de
    /// fora de propósito (a ausência É o bloqueio).
    ///
    /// Vive na camada <c>Net</c> (e não no Core) porque referencia eventos da camada Data
    /// (<c>EcoCollected</c> etc.) — e o Core é puro (não pode depender de Data). Nada aqui
    /// depende do Netcode; é só a fronteira que o <c>EventScopeAdapter</c> (P2+) vai consultar.
    ///
    /// Contrato completo (os 22 eventos) em <c>05_Sistemas/PlanoTecnico_EventScope.md</c> §4.
    /// </summary>
    public static class EventScopeRegistry
    {
        // Eventos de SIMULAÇÃO e de INTENÇÃO entram aqui (Intent listado de propósito:
        // documenta o contrato e deixa a trava do adaptador acusar se alguém tentar
        // relayar um pedido). Tudo o que não está listado cai no default (Presentation)
        // via ScopeOf — ver a "falha segura" no resumo acima.
        private static readonly Dictionary<Type, NetEventScope> Map = new Dictionary<Type, NetEventScope>
        {
            // Combate / mundo (Core.Combat)
            { typeof(HealthChanged),     NetEventScope.Simulation },
            { typeof(EntityDied),        NetEventScope.Simulation },
            { typeof(PlayerRespawned),   NetEventScope.Simulation }, // M-NET-9 P0: renascer relayado ao dono
            { typeof(PlayerAttacked),    NetEventScope.Simulation },

            // Mundo compartilhado (Core.World — M-NET-7)
            { typeof(NodeStockChanged),  NetEventScope.Simulation }, // estoque do nó, relayado 1× (P3)

            // INTENÇÕES (cliente→servidor pelos forwarders; NUNCA relayadas de volta)
            { typeof(DamageRequested),   NetEventScope.Intent }, // M-NET-6 P4 (formalizado aqui)
            { typeof(HarvestRequested),  NetEventScope.Intent },
            { typeof(CraftRequested),    NetEventScope.Intent },
            { typeof(BuildRequested),    NetEventScope.Intent },
            { typeof(SyncRequested),     NetEventScope.Intent },

            // Intenções de INCORPORAÇÃO (Core.Incorporation — M-NET-8 P1)
            { typeof(EquipRequested),           NetEventScope.Intent },
            { typeof(UnequipRequested),         NetEventScope.Intent },
            { typeof(InvestNodeRequested),      NetEventScope.Intent },
            { typeof(RespecRequested),          NetEventScope.Intent },
            { typeof(InvestAttributeRequested), NetEventScope.Intent },

            // Incorporação (Data)
            { typeof(EcoCollected),      NetEventScope.Simulation }, // FONTE (MasteryChanged/AttributesChanged derivam dele → recriados no cliente)
            { typeof(AbilityEquipped),   NetEventScope.Simulation },
            { typeof(AbilityUnequipped), NetEventScope.Simulation },
            { typeof(AbilityLeveledUp),  NetEventScope.Simulation },
            { typeof(AbilityUsed),       NetEventScope.Simulation }, // FONTE (AffinityChanged deriva dele → recriado no cliente)

            // Inventário / crafting / captura (Data)
            { typeof(InventoryChanged),  NetEventScope.Simulation },
            { typeof(ItemCollected),     NetEventScope.Simulation },
            { typeof(ItemCrafted),       NetEventScope.Simulation },
            { typeof(CreatureCaptured),  NetEventScope.Simulation },

            // NÃO entram de propósito (default Presentation):
            //  • Apresentação (só-tela): AltarOpened, AltarClosed, InventoryToggled,
            //    CraftingToggled, CharacterSheetToggled.
            //  • Derivados (recriados no cliente, não relayados): AffinityChanged,
            //    MasteryChanged, AttributesChanged.
            //  • Sessão (pendente da decisão §6): GameSaved, GameLoaded.
        };

        /// <summary>Escopo de rede do evento <typeparamref name="T"/> (default Presentation).</summary>
        public static NetEventScope ScopeOf<T>() where T : IGameEvent
            => Map.TryGetValue(typeof(T), out NetEventScope scope) ? scope : NetEventScope.Presentation;

        /// <summary>
        /// Versão por <see cref="Type"/> (útil para varrer uma lista de tipos). Mesmo default
        /// seguro: tipo não listado → Presentation (não trafega).
        /// </summary>
        public static NetEventScope ScopeOf(Type eventType)
            => eventType != null && Map.TryGetValue(eventType, out NetEventScope scope)
                ? scope
                : NetEventScope.Presentation;
    }
}
