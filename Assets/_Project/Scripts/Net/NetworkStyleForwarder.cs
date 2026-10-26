using UnityEngine;
using Unity.Netcode;
using JogoDaTrupe.Core.Combat;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Data;

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// Leva o ESTILO do jogador do CLIENTE para o servidor (M-NET-6 P4). Companion no
    /// prefab <c>P_Player</c>.
    ///
    /// A IA dos inimigos e a resistência do boss são server-only (gate <c>SimulationAuthority</c>,
    /// P0), mas leem o RITMO do jogador (<see cref="PlayerAttacked"/>) e os ELEMENTOS que
    /// ele usa (<see cref="AbilityUsed"/>). No host esses eventos já nascem no bus do
    /// servidor; num CLIENTE eles ficam só na máquina dele. Este componente, no player do
    /// dono-cliente, escuta os dois e os reenvia ao servidor por RPC, onde são
    /// republicados no bus para o <c>EnemyController</c>/<c>BossController</c> reagirem.
    ///
    /// <b>Não relayamos de volta aos clientes</b> (desvio consciente do plano original,
    /// que previa "destravar os 2 relays servidor→clientes"): ao ler o código, o
    /// <c>AffinityState</c> do jogador JÁ processa o <c>AbilityUsed</c> LOCAL — reenviar o
    /// evento de volta duplicaria a afinidade do próprio jogador; e nenhum ouvinte de
    /// cliente precisa desses eventos (a IA que reage é server-only). Decisão técnica
    /// registrada no log de decisões do projeto.
    ///
    /// Só o DONO que é CLIENTE encaminha: o host não (seus eventos já estão no bus do
    /// servidor — reenviar duplicaria o ritmo/estilo lá). <c>AbilityUsed</c> carrega um
    /// ScriptableObject → viaja o <c>abilityId</c> e o servidor reconstrói pela ficha
    /// (mesmo padrão do <see cref="EventScopeAdapter"/>).
    ///
    /// O dono NÃO viaja no payload: o servidor o deriva da conexão que enviou o RPC
    /// (<see cref="NetSeat.TryOfSender"/>), para um cliente não atribuir ritmo/estilo a
    /// outro jogador.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkStyleForwarder : NetworkBehaviour
    {
        private bool _forwarding; // este é o player local de um CLIENTE → encaminha

        public override void OnNetworkSpawn()
        {
            _forwarding = IsOwner && !IsServer;
            if (!_forwarding) return;

            EventBus<PlayerAttacked>.Subscribe(OnPlayerAttacked);
            EventBus<AbilityUsed>.Subscribe(OnAbilityUsed);
        }

        public override void OnNetworkDespawn()
        {
            if (!_forwarding) return;

            EventBus<PlayerAttacked>.Unsubscribe(OnPlayerAttacked);
            EventBus<AbilityUsed>.Unsubscribe(OnAbilityUsed);
        }

        private void OnPlayerAttacked(PlayerAttacked e)
        {
            if (e.OwnerId != NetSeat.Of(this)) return; // só o MEU estilo (defensivo)
            ForwardAttackRpc();
        }

        private void OnAbilityUsed(AbilityUsed e)
        {
            if (e.OwnerId != NetSeat.Of(this)) return;
            string abilityId = e.Ability != null ? e.Ability.abilityId : string.Empty;
            ForwardAbilityRpc(e.Slot, abilityId);
        }

        // Executa no SERVIDOR: republica no bus (via EventRelay — guarda anti-laço) para a
        // IA e o boss reagirem ao estilo do cliente. O EventRelay não é estritamente
        // necessário aqui (o adaptador não relaya estes eventos), mas mantém o padrão e
        // protege contra um laço se algum dia forem relayados. O dono vem da CONEXÃO
        // (NetSeat.TryOfSender), nunca do payload.
        [Rpc(SendTo.Server)]
        private void ForwardAttackRpc(RpcParams rpcParams = default)
        {
            if (!NetSeat.TryOfSender(rpcParams, out int seat)) return;
            EventRelay.Republish(new PlayerAttacked { OwnerId = seat });
        }

        [Rpc(SendTo.Server)]
        private void ForwardAbilityRpc(int slot, string abilityId, RpcParams rpcParams = default)
        {
            if (!NetSeat.TryOfSender(rpcParams, out int seat)) return;
            AbilityDefinition ability = DefinitionRegistry.Active != null
                ? DefinitionRegistry.Active.AbilityById(abilityId)
                : null;
            if (ability == null)
            {
                Debug.LogWarning($"[Rede] AbilityUsed do cliente descartado no servidor: habilidade '{abilityId}' não encontrada no DefinitionRegistry.Active.");
                return;
            }
            EventRelay.Republish(new AbilityUsed { OwnerId = seat, Slot = slot, Ability = ability });
        }
    }
}
