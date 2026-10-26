using UnityEngine;
using Unity.Netcode;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Incorporation;
using JogoDaTrupe.Core.World;

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// Leva os PEDIDOS DE MUNDO do cliente ao servidor (M-NET-7 P1) — o "carteiro"
    /// único das intenções do mundo compartilhado. Companion no prefab <c>P_Player</c>,
    /// par do <see cref="NetworkStyleForwarder"/>.
    ///
    /// Numa máquina SEM autoridade, o gameplay não muta o mundo: publica
    /// <see cref="HarvestRequested"/>/<see cref="CraftRequested"/>/<see cref="BuildRequested"/>/
    /// <see cref="SyncRequested"/> (escopo <see cref="NetEventScope.Intent"/>). Este
    /// componente, no player do dono-cliente, escuta os quatro e os reenvia ao servidor
    /// por <c>[Rpc(SendTo.Server)]</c>, onde são republicados no bus — os EXECUTORES
    /// server-side (P3+) reagem e executam de verdade. O resultado volta como estado
    /// replicado (NetworkList/NetworkVariable/spawn) e/ou evento de Simulação relayado.
    ///
    /// Só o DONO que é CLIENTE encaminha (no host, os gates de autoridade nem desviam:
    /// a execução é direta). Payloads são só primitivos (guids/ids em string) — nenhuma
    /// ficha viaja. O <see cref="EventRelay"/> na republicação mantém o padrão anti-laço
    /// da casa (defensivo: intenções nunca são relayadas de volta, mas custa nada).
    ///
    /// O DONO do pedido NÃO viaja no payload: o servidor o deriva da CONEXÃO que enviou
    /// o RPC (<c>RpcParams.Receive.SenderClientId</c> → assento no
    /// <see cref="SeatRegistry"/>). Antes o cliente mandava o <c>ownerId</c> e o servidor
    /// confiava nele — um build alterado podia coletar, craftar, construir, capturar ou
    /// mexer no loadout/atributos EM NOME DE OUTRO jogador.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkWorldRequestForwarder : NetworkBehaviour
    {
        private bool _forwarding; // este é o player local de um CLIENTE → encaminha

        public override void OnNetworkSpawn()
        {
            _forwarding = IsOwner && !IsServer;
            if (!_forwarding) return;

            EventBus<HarvestRequested>.Subscribe(OnHarvest);
            EventBus<CraftRequested>.Subscribe(OnCraft);
            EventBus<BuildRequested>.Subscribe(OnBuild);
            EventBus<SyncRequested>.Subscribe(OnSync);

            // Incorporação (M-NET-8 P1) — o mesmo carteiro leva os pedidos do Altar/ficha.
            EventBus<EquipRequested>.Subscribe(OnEquip);
            EventBus<UnequipRequested>.Subscribe(OnUnequip);
            EventBus<InvestNodeRequested>.Subscribe(OnInvestNode);
            EventBus<RespecRequested>.Subscribe(OnRespec);
            EventBus<InvestAttributeRequested>.Subscribe(OnInvestAttribute);
        }

        public override void OnNetworkDespawn()
        {
            if (!_forwarding) return;

            EventBus<HarvestRequested>.Unsubscribe(OnHarvest);
            EventBus<CraftRequested>.Unsubscribe(OnCraft);
            EventBus<BuildRequested>.Unsubscribe(OnBuild);
            EventBus<SyncRequested>.Unsubscribe(OnSync);

            EventBus<EquipRequested>.Unsubscribe(OnEquip);
            EventBus<UnequipRequested>.Unsubscribe(OnUnequip);
            EventBus<InvestNodeRequested>.Unsubscribe(OnInvestNode);
            EventBus<RespecRequested>.Unsubscribe(OnRespec);
            EventBus<InvestAttributeRequested>.Unsubscribe(OnInvestAttribute);
        }

        // Cada handler filtra pelo MEU dono (defensivo — o mesmo do NetworkStyleForwarder):
        // um pedido de outro dono não deve sair desta máquina por engano. O dono em si
        // não vai no RPC: quem decide é o servidor, pela conexão (ver os RPCs abaixo).

        private void OnHarvest(HarvestRequested e)
        {
            if (e.OwnerId != NetSeat.Of(this)) return;
            ForwardHarvestRpc(e.NodeGuid);
        }

        private void OnCraft(CraftRequested e)
        {
            if (e.OwnerId != NetSeat.Of(this)) return;
            ForwardCraftRpc(e.RecipeId);
        }

        private void OnBuild(BuildRequested e)
        {
            if (e.OwnerId != NetSeat.Of(this)) return;
            ForwardBuildRpc(e.StructureId, e.X, e.Y, e.Z);
        }

        private void OnSync(SyncRequested e)
        {
            if (e.OwnerId != NetSeat.Of(this)) return;
            ForwardSyncRpc(e.CreatureGuid);
        }

        private void OnEquip(EquipRequested e)
        {
            if (e.OwnerId != NetSeat.Of(this)) return;
            ForwardEquipRpc(e.AbilityId);
        }

        private void OnUnequip(UnequipRequested e)
        {
            if (e.OwnerId != NetSeat.Of(this)) return;
            ForwardUnequipRpc(e.Slot);
        }

        private void OnInvestNode(InvestNodeRequested e)
        {
            if (e.OwnerId != NetSeat.Of(this)) return;
            ForwardInvestNodeRpc(e.Slot, e.NodeIndex);
        }

        private void OnRespec(RespecRequested e)
        {
            if (e.OwnerId != NetSeat.Of(this)) return;
            ForwardRespecRpc(e.Slot);
        }

        private void OnInvestAttribute(InvestAttributeRequested e)
        {
            if (e.OwnerId != NetSeat.Of(this)) return;
            ForwardInvestAttributeRpc(e.Attribute);
        }

        // Executam no SERVIDOR: publicam o pedido no bus local — os executores
        // server-side assinam e executam com autoridade.
        //
        // O dono (assento) sai da CONEXÃO, nunca do payload: NetSeat.TryOfSender lê o
        // SenderClientId que o próprio NGO preenche no servidor. Remetente sem assento
        // (não aprovado / já saiu) → pedido descartado.
        //
        // Publicação PURA (não EventRelay.Republish): a intenção não é relayada de volta
        // (não há laço a proteger), e — crucial — o executor reage SÍNCRONO publicando
        // eventos de SIMULAÇÃO em cadeia (ex.: NodeStockChanged). Se a flag IsRelaying
        // ficasse ligada aqui, o EventScopeAdapter (Defesa B) SUPRIMIRIA esses eventos e
        // as réplicas não atualizariam. Mesmo princípio do NetworkDamageForwarder, que
        // chama TakeDamage direto (deixando EntityDied fluir para o relay).

        [Rpc(SendTo.Server)]
        private void ForwardHarvestRpc(string nodeGuid, RpcParams rpcParams = default)
        {
            if (!NetSeat.TryOfSender(rpcParams, out int seat)) return;
            EventBus<HarvestRequested>.Publish(new HarvestRequested { NodeGuid = nodeGuid, OwnerId = seat });
        }

        [Rpc(SendTo.Server)]
        private void ForwardCraftRpc(string recipeId, RpcParams rpcParams = default)
        {
            if (!NetSeat.TryOfSender(rpcParams, out int seat)) return;
            EventBus<CraftRequested>.Publish(new CraftRequested { RecipeId = recipeId, OwnerId = seat });
        }

        [Rpc(SendTo.Server)]
        private void ForwardBuildRpc(string structureId, float x, float y, float z, RpcParams rpcParams = default)
        {
            if (!NetSeat.TryOfSender(rpcParams, out int seat)) return;
            EventBus<BuildRequested>.Publish(new BuildRequested { StructureId = structureId, X = x, Y = y, Z = z, OwnerId = seat });
        }

        [Rpc(SendTo.Server)]
        private void ForwardSyncRpc(string creatureGuid, RpcParams rpcParams = default)
        {
            if (!NetSeat.TryOfSender(rpcParams, out int seat)) return;
            EventBus<SyncRequested>.Publish(new SyncRequested { CreatureGuid = creatureGuid, OwnerId = seat });
        }

        // Incorporação (M-NET-8 P1) — mesmos princípios (Publish puro, payload primitivo,
        // dono pela conexão).

        [Rpc(SendTo.Server)]
        private void ForwardEquipRpc(string abilityId, RpcParams rpcParams = default)
        {
            if (!NetSeat.TryOfSender(rpcParams, out int seat)) return;
            EventBus<EquipRequested>.Publish(new EquipRequested { AbilityId = abilityId, OwnerId = seat });
        }

        [Rpc(SendTo.Server)]
        private void ForwardUnequipRpc(int slot, RpcParams rpcParams = default)
        {
            if (!NetSeat.TryOfSender(rpcParams, out int seat)) return;
            EventBus<UnequipRequested>.Publish(new UnequipRequested { Slot = slot, OwnerId = seat });
        }

        [Rpc(SendTo.Server)]
        private void ForwardInvestNodeRpc(int slot, int nodeIndex, RpcParams rpcParams = default)
        {
            if (!NetSeat.TryOfSender(rpcParams, out int seat)) return;
            EventBus<InvestNodeRequested>.Publish(new InvestNodeRequested { Slot = slot, NodeIndex = nodeIndex, OwnerId = seat });
        }

        [Rpc(SendTo.Server)]
        private void ForwardRespecRpc(int slot, RpcParams rpcParams = default)
        {
            if (!NetSeat.TryOfSender(rpcParams, out int seat)) return;
            EventBus<RespecRequested>.Publish(new RespecRequested { Slot = slot, OwnerId = seat });
        }

        [Rpc(SendTo.Server)]
        private void ForwardInvestAttributeRpc(int attribute, RpcParams rpcParams = default)
        {
            if (!NetSeat.TryOfSender(rpcParams, out int seat)) return;
            EventBus<InvestAttributeRequested>.Publish(new InvestAttributeRequested { Attribute = attribute, OwnerId = seat });
        }
    }
}
