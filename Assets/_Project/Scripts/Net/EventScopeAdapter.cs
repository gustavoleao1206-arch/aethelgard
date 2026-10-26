using System;
using UnityEngine;
using Unity.Netcode;
using JogoDaTrupe.Core.Combat;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.World;
using JogoDaTrupe.Data;

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// O "correio" de eventos de simulação (M-NET-5 P4). SÓ o servidor assina o
    /// <c>EventBus</c> e reenvia cada evento aos clientes remotos por RPC; no cliente,
    /// o evento é republicado no bus local via <see cref="EventRelay.Republish{T}"/>,
    /// e os ouvintes normais (MemoryBank, HUDs…) reagem sem saber que veio da rede.
    ///
    /// Entrega EXATAMENTE-UMA-VEZ no host: o mecanismo primário é o
    /// <c>[Rpc(SendTo.NotServer)]</c> — o corpo do RPC NÃO roda no host (que já viveu o
    /// evento original); a guarda <see cref="EventRelay.IsRelaying"/> fica como Defesa B
    /// (a captura do servidor ignora o que ele mesmo republicou — quebra qualquer laço).
    ///
    /// Precisa de um <c>NetworkObject</c> PRÓPRIO: vive no prefab <c>P_NetSystems</c>,
    /// spawnado pelo <see cref="NetSystemsSpawner"/> quando o servidor sobe (um
    /// NetworkBehaviour no GameObject do NetworkManager nunca receberia
    /// <c>OnNetworkSpawn</c>; e um NetworkObject in-scene gerado por código fica com
    /// hash 0 e quebra o cliente — Bugs.md #7). Em solo (cena sem rede) este objeto
    /// não existe — nada muda.
    ///
    /// Eventos que carregam ScriptableObject (EnemyDefinition etc.) não trafegam por RPC:
    /// vai o ID (string) e o cliente reconstrói pela ficha via <see cref="DefinitionRegistry.Active"/>.
    ///
    /// Assina só o SUBCONJUNTO SEGURO do M-NET-5 (fontes que nascem 1× no processo do
    /// host). <c>PlayerAttacked</c>/<c>AbilityUsed</c> NÃO são relayados aqui (M-NET-6 P4):
    /// a IA é server-only, então esses eventos só precisam ir do CLIENTE PARA O SERVIDOR —
    /// papel do <see cref="NetworkStyleForwarder"/> no player. Relayá-los de volta aos
    /// clientes duplicaria a afinidade do jogador (o <c>AffinityState</c> já processa o
    /// <c>AbilityUsed</c> local). <c>ItemCrafted</c> ainda espera um <c>RecipeById</c>.
    /// Ver <c>05_Sistemas/PlanoTecnico_CombateAutoritativo.md</c> §5 e <c>DECISOES.md</c> (P4).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class EventScopeAdapter : NetworkBehaviour
    {
        // Trava de propósito: tudo que este adaptador relaya TEM que estar classificado
        // como Simulação no EventScopeRegistry (a fronteira criada no M-NET-4). A lista
        // vive em NetRelayContract (dado puro, testável em EditMode — M-NET-7 P2).
        // ItemCollected/InventoryChanged SAÍRAM do relay: o inventário viaja por
        // NetworkList (NetworkInventory) — relayar entregaria a mudança 2× no cliente.
        public override void OnNetworkSpawn()
        {
            if (!IsServer) return; // só o servidor captura e relaya

            foreach (Type t in NetRelayContract.RelayedByRpc)
            {
                if (EventScopeRegistry.ScopeOf(t) != NetEventScope.Simulation)
                    Debug.LogError($"[Rede] {t.Name} não está classificado como Simulação no EventScopeRegistry — não deveria ser relayado.");
            }

            EventBus<HealthChanged>.Subscribe(OnHealthChanged);
            EventBus<EntityDied>.Subscribe(OnEntityDied);
            EventBus<PlayerRespawned>.Subscribe(OnPlayerRespawned);
            EventBus<EcoCollected>.Subscribe(OnEcoCollected);
            EventBus<CreatureCaptured>.Subscribe(OnCreatureCaptured);
            EventBus<NodeStockChanged>.Subscribe(OnNodeStockChanged);
            EventBus<ItemCrafted>.Subscribe(OnItemCrafted);
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer) return; // simetria EXATA do gate de inscrição

            EventBus<HealthChanged>.Unsubscribe(OnHealthChanged);
            EventBus<EntityDied>.Unsubscribe(OnEntityDied);
            EventBus<PlayerRespawned>.Unsubscribe(OnPlayerRespawned);
            EventBus<EcoCollected>.Unsubscribe(OnEcoCollected);
            EventBus<CreatureCaptured>.Unsubscribe(OnCreatureCaptured);
            EventBus<NodeStockChanged>.Unsubscribe(OnNodeStockChanged);
            EventBus<ItemCrafted>.Unsubscribe(OnItemCrafted);
        }

        // ── Combate (payload só de primitivos) ─────────────────────────────────────────

        private void OnHealthChanged(HealthChanged e)
        {
            if (EventRelay.IsRelaying) return; // Defesa B

            // Vida de INIMIGO (NoOwner) viaja por NetworkVariable (NetworkHealth,
            // M-NET-6 P3) — relayar aqui chegaria 2× no cliente. Player segue no relay.
            if (!NetRelayRules.ShouldRelayHealthChanged(e.OwnerId)) return;

            RelayHealthChangedRpc(e.OwnerId, e.Current, e.Max);
        }

        [Rpc(SendTo.NotServer)]
        private void RelayHealthChangedRpc(int ownerId, float current, float max)
            => EventRelay.Republish(new HealthChanged { OwnerId = ownerId, Current = current, Max = max });

        private void OnEntityDied(EntityDied e)
        {
            if (EventRelay.IsRelaying) return;
            RelayEntityDiedRpc(e.OwnerId);
        }

        [Rpc(SendTo.NotServer)]
        private void RelayEntityDiedRpc(int ownerId)
        {
            Debug.Log($"[Rede] EntityDied recebido do host (dono {ownerId}).");
            EventRelay.Republish(new EntityDied { OwnerId = ownerId });
        }

        private void OnPlayerRespawned(PlayerRespawned e)
        {
            if (EventRelay.IsRelaying) return;
            RelayPlayerRespawnedRpc(e.OwnerId);
        }

        [Rpc(SendTo.NotServer)]
        private void RelayPlayerRespawnedRpc(int ownerId)
        {
            Debug.Log($"[Rede] PlayerRespawned recebido do host (dono {ownerId}).");
            EventRelay.Republish(new PlayerRespawned { OwnerId = ownerId });
        }

        // ── Incorporação / captura (ScriptableObject → viaja o id, reconstrói na ficha) ─

        private void OnEcoCollected(EcoCollected e)
        {
            if (EventRelay.IsRelaying) return;
            RelayEcoCollectedRpc(e.OwnerId, IdOf(e.Source));
        }

        [Rpc(SendTo.NotServer)]
        private void RelayEcoCollectedRpc(int ownerId, string enemyId)
        {
            EnemyDefinition source = FindEnemy(enemyId);
            if (source == null) { WarnMissing("EcoCollected", enemyId); return; }
            Debug.Log($"[Rede] EcoCollected recebido do host (dono {ownerId}, criatura '{enemyId}').");
            EventRelay.Republish(new EcoCollected { OwnerId = ownerId, Source = source });
        }

        private void OnCreatureCaptured(CreatureCaptured e)
        {
            if (EventRelay.IsRelaying) return;
            RelayCreatureCapturedRpc(e.OwnerId, IdOf(e.Source));
        }

        [Rpc(SendTo.NotServer)]
        private void RelayCreatureCapturedRpc(int ownerId, string enemyId)
        {
            EnemyDefinition source = FindEnemy(enemyId);
            if (source == null) { WarnMissing("CreatureCaptured", enemyId); return; }
            Debug.Log($"[Rede] CreatureCaptured recebido do host (dono {ownerId}, criatura '{enemyId}').");
            EventRelay.Republish(new CreatureCaptured { OwnerId = ownerId, Source = source });
        }

        // ── Inventário: NÃO relayado desde o M-NET-7 P2 ─────────────────────────────────
        // ItemCollected/InventoryChanged viajam por ESTADO (NetworkList do
        // NetworkInventory no P_Player); o espelho republica o InventoryChanged local.

        // ── Loadout: NÃO relayado desde o M-NET-8 P3 ────────────────────────────────────
        // AbilityEquipped/AbilityUnequipped/AbilityLeveledUp viajam por ESTADO
        // (NetworkList do NetworkLoadout no P_Player); o espelho republica os eventos
        // locais ao aplicar o SyncFrom — relayar também entregaria 2× no cliente
        // (contrato em NetRelayContract, mesmo movimento do inventário no P2).

        // ── Mundo compartilhado: estoque do nó (M-NET-7 P3) ─────────────────────────────

        private void OnNodeStockChanged(NodeStockChanged e)
        {
            if (EventRelay.IsRelaying) return;
            RelayNodeStockChangedRpc(e.NodeGuid, e.Remaining);
        }

        [Rpc(SendTo.NotServer)]
        private void RelayNodeStockChangedRpc(string nodeGuid, int remaining)
            => EventRelay.Republish(new NodeStockChanged { NodeGuid = nodeGuid, Remaining = remaining });

        // Craft (M-NET-7 P4): gancho de feedback. O item craftado em si chega pela
        // replicação do NetworkInventory; isto só reconstrói o evento p/ ouvintes futuros
        // (toast "Craftado!"). RecipeDefinition (SO) viaja por recipeId.
        private void OnItemCrafted(ItemCrafted e)
        {
            if (EventRelay.IsRelaying) return;
            RelayItemCraftedRpc(e.OwnerId, e.Recipe != null ? e.Recipe.recipeId : string.Empty);
        }

        [Rpc(SendTo.NotServer)]
        private void RelayItemCraftedRpc(int ownerId, string recipeId)
        {
            RecipeDefinition recipe = DefinitionRegistry.Active != null ? DefinitionRegistry.Active.RecipeById(recipeId) : null;
            if (recipe == null) { WarnMissing("ItemCrafted", recipeId); return; }
            EventRelay.Republish(new ItemCrafted { OwnerId = ownerId, Recipe = recipe });
        }

        // ── Helpers de reconstrução por id ──────────────────────────────────────────────

        private static string IdOf(EnemyDefinition def) => def != null ? def.enemyId : string.Empty;

        private static EnemyDefinition FindEnemy(string id)
            => DefinitionRegistry.Active != null ? DefinitionRegistry.Active.EnemyById(id) : null;

        // Ficha não encontrada no cliente = evento não republicável. Avisa alto (em vez de
        // republicar com Source nulo e estourar num ouvinte longe da causa).
        private static void WarnMissing(string eventName, string id)
            => Debug.LogWarning($"[Rede] {eventName} descartado no cliente: definição '{id}' não encontrada no DefinitionRegistry.Active.");
    }
}
