using UnityEngine;
using Unity.Netcode;
using JogoDaTrupe.Core.Combat;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Identity;
using JogoDaTrupe.Gameplay.Combat;

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// Encaminha o DANO do CLIENTE para o servidor (M-NET-6 P4). Companion do
    /// <see cref="Health"/> nos prefabs de inimigo/boss.
    ///
    /// No co-op os inimigos são server-owned e a vida é autoritativa no servidor
    /// (<see cref="NetworkHealth"/>, P3). Quando o jogador de um CLIENTE bate num inimigo,
    /// o <c>Health</c> da réplica (máquina sem autoridade) não muta: publica a INTENÇÃO
    /// <see cref="DamageRequested"/> (padrão Input → Intenção → Ação). Este componente, na
    /// mesma réplica, escuta a intenção, confirma que é ESTE inimigo (casa pelo
    /// <see cref="EntityGuid"/> LOCAL — distingue entre inimigos irmãos na tela do cliente)
    /// e envia o pedido ao servidor por RPC. O servidor aplica o dano de verdade.
    ///
    /// A identidade cruza a rede pelo PRÓPRIO <c>NetworkObject</c> (o RPC executa no mesmo
    /// objeto do servidor, via NetworkObjectId), então os GUIDs das duas máquinas NÃO
    /// precisam coincidir — o GUID só resolve o casamento local no cliente.
    ///
    /// <c>[Rpc(SendTo.Server)]</c> tem <c>InvokePermission = Everyone</c> por padrão (NGO
    /// 2.x), então um cliente NÃO-dono pode invocá-lo num objeto server-owned. No host
    /// nada é encaminhado: lá o <c>Health</c> tem autoridade e aplica direto (nunca publica
    /// <c>DamageRequested</c>) — por isso a inscrição é só do lado cliente.
    ///
    /// Como qualquer cliente pode invocar o RPC, o ATACANTE (quem recebe o Eco como
    /// contribuinte) não viaja no payload: o servidor o deriva da conexão
    /// (<see cref="NetSeat.TryOfSender"/>). O VALOR do dano ainda vem do cliente.
    /// TODO: validar o dano no servidor (teto pela ficha da arma/habilidade, alcance e
    /// cooldown) — hoje um cliente alterado ainda pode exagerar o próprio golpe.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(EntityGuid))]
    public sealed class NetworkDamageForwarder : NetworkBehaviour
    {
        private Health _health;
        private string _guid;

        public override void OnNetworkSpawn()
        {
            _health = GetComponent<Health>();
            _guid = GetComponent<EntityGuid>().Value;

            // Só o cliente publica DamageRequested (o host aplica direto). O servidor
            // apenas precisa do método do RPC — não da inscrição.
            if (!IsServer) EventBus<DamageRequested>.Subscribe(OnDamageRequested);
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer) EventBus<DamageRequested>.Unsubscribe(OnDamageRequested);
        }

        // Roda na máquina que PUBLICOU a intenção (o cliente sem autoridade). Filtra pelo
        // próprio inimigo (GUID local) e encaminha ao servidor.
        private void OnDamageRequested(DamageRequested e)
        {
            if (e.TargetGuid != _guid) return;
            ApplyDamageRpc(e.Amount);
        }

        // Executa no SERVIDOR (autoridade): aplica o dano de verdade no Health, que
        // registra o contribuinte (Eco) e, na morte, dispara EntityDied. A vida nova
        // replica pelo NetworkHealth; o Eco por contribuinte, pelo EcoDropper (server).
        // O atacante é o assento de quem ENVIOU o RPC (conexão), nunca um id do payload.
        [Rpc(SendTo.Server)]
        private void ApplyDamageRpc(float amount, RpcParams rpcParams = default)
        {
            if (!NetSeat.TryOfSender(rpcParams, out int attackerSeat)) return;
            if (_health != null) _health.TakeDamage(amount, attackerSeat);
        }
    }
}
