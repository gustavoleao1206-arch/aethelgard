using UnityEngine;
using Unity.Netcode;
using JogoDaTrupe.Gameplay.Capture;

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// Replica o estado "SINCRONIZÁVEL" da criatura capturável (M-NET-7 P7) — o mesmo
    /// molde do <see cref="NetworkHealth"/>: <see cref="CapturableCreature"/> continua
    /// MonoBehaviour puro na camada Gameplay; ESTE companion, na camada Net, replica.
    ///
    /// • <b>Servidor:</b> é a fonte — quando a simulação enfraquece a criatura até o
    ///   limiar (avaliado só na autoridade), empurra a <see cref="NetworkVariable{T}"/>
    ///   (write-server é o default).
    /// • <b>Cliente:</b> espelha via <see cref="CapturableCreature.SyncSynchronizable"/>
    ///   — a réplica tinge de verde e vira alvo válido do canal local de captura.
    ///
    /// A CAPTURA em si não passa por aqui: o canal completo publica a intenção
    /// <c>SyncRequested</c> (via forwarder) e o servidor arbitra — o corpo some das
    /// duas telas pelo próprio despawn do prefab de rede.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CapturableCreature))]
    public sealed class NetworkCapture : NetworkBehaviour
    {
        // Defaults do NGO: leitura Everyone, escrita Server — exatamente o que queremos.
        private readonly NetworkVariable<bool> _synchronizable = new NetworkVariable<bool>();

        private CapturableCreature _creature;

        public override void OnNetworkSpawn()
        {
            _creature = GetComponent<CapturableCreature>();

            if (IsServer)
            {
                _synchronizable.Value = _creature.CanSynchronize; // semeia (normalmente false)
            }
            else if (_synchronizable.Value)
            {
                _creature.SyncSynchronizable(); // late-join: já estava pronta
            }

            _synchronizable.OnValueChanged += OnChanged;
        }

        public override void OnNetworkDespawn() => _synchronizable.OnValueChanged -= OnChanged;

        // Servidor: empurra o estado da simulação para a variável quando mudar.
        // (Poll barato por frame — mesmo padrão do NetworkHealth. Só liga uma vez:
        // capturada = despawn, então o "false" de volta nunca precisa viajar.)
        private void Update()
        {
            if (!IsServer || _creature == null) return;
            if (_creature.CanSynchronize && !_synchronizable.Value)
            {
                _synchronizable.Value = true;
            }
        }

        private void OnChanged(bool previous, bool now)
        {
            if (IsServer) return; // no servidor a criatura É a fonte; só a réplica espelha
            if (!now) return;

            _creature.SyncSynchronizable();
            // Diagnóstico do co-op (raro: uma vez por criatura).
            Debug.Log($"[Rede] Criatura sincronizável (replicado): {name}.");
        }
    }
}
