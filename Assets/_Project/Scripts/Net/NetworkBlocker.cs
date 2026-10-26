using UnityEngine;
using Unity.Netcode;
using JogoDaTrupe.Gameplay.Combat;

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// Replica o estado de GUARDA do jogador (M-NET-9+). Companion do <see cref="Blocker"/> no
    /// prefab <c>P_Player</c>.
    ///
    /// O problema que resolve: o botão de bloquear é apertado no CLIENTE, mas quem mitiga o dano
    /// é o SERVIDOR (autoridade), onde o input do boneco do cliente está desligado. Sem replicar,
    /// a guarda do cliente não segura golpe nenhum. Aqui a intenção vira uma
    /// <see cref="NetworkVariable{T}"/> com escrita do DONO: o cliente escreve, o servidor lê.
    ///
    /// Bônus: como TODA máquina espelha o valor no <c>Blocker</c>, os players REMOTOS passam a ver
    /// o escudo levantado uns dos outros (antes a guarda era invisível fora do dono).
    ///
    /// É estado CONTÍNUO (segurar), então uma NetworkVariable é o encaixe certo — não um RPC de
    /// evento como o <see cref="NetworkStyleForwarder"/> (que carrega gestos pontuais).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkBlocker : NetworkBehaviour
    {
        // Escrita do DONO: cada cliente é a fonte da verdade da PRÓPRIA guarda; todos leem.
        private readonly NetworkVariable<bool> _blocking = new(
            writePerm: NetworkVariableWritePermission.Owner);

        private Blocker _blocker;

        private void Awake() => _blocker = GetComponent<Blocker>();

        private void Update()
        {
            // Antes do spawn de rede o IsOwner e a NetworkVariable ainda não valem; e marcar o
            // Blocker como "controlado pela rede" cedo demais tiraria a guarda local sem motivo.
            if (!IsSpawned || _blocker == null) return;

            // O dono publica a PRÓPRIA intenção. A NetworkVariable só manda delta quando o valor
            // muda de verdade, então escrever a cada frame não gera tráfego à toa.
            if (IsOwner && _blocking.Value != _blocker.WantsToBlock)
                _blocking.Value = _blocker.WantsToBlock;

            // Todo mundo (dono, servidor, players remotos) espelha o valor de volta no Blocker,
            // que a partir daí anima/mitiga por ele. No dono a escrita acima é síncrona, então
            // não há atraso de 1 frame na guarda dele mesmo.
            _blocker.SyncBlocking(_blocking.Value);
        }
    }
}
