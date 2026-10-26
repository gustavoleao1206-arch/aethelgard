using UnityEngine;
using Unity.Netcode;
using JogoDaTrupe.Gameplay.Combat;

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// AUTORIDADE DURA da vida em rede (M-NET-6 P3) — companion do <see cref="Health"/>
    /// nos prefabs de inimigo/boss. O padrão desenhado no plano do M-NET-5 §4.2:
    /// <c>Health</c> continua <c>MonoBehaviour</c> puro na camada Gameplay (que não
    /// referencia o NGO); ESTE componente, na camada Net, replica o valor.
    ///
    /// • <b>Servidor:</b> é a fonte — semeia a <see cref="NetworkVariable{T}"/> no spawn
    ///   e a empurra quando a simulação muda a vida (write-server-only é o default).
    /// • <b>Cliente:</b> espelha o valor recebido no <c>Health</c> local via
    ///   <see cref="Health.SyncFrom"/> (que republica o <c>HealthChanged</c> local).
    ///   A morte NÃO nasce aqui: <c>EntityDied</c> é server-only (chega pelo relay)
    ///   e o corpo some pelo despawn.
    ///
    /// Com a vida do inimigo viajando por NetworkVariable, o <c>EventScopeAdapter</c>
    /// deixa de relayar <c>HealthChanged</c> de <c>NoOwner</c> (senão chegaria 2× no
    /// cliente) — regra em <see cref="NetRelayRules"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class NetworkHealth : NetworkBehaviour
    {
        // Defaults do NGO: leitura Everyone, escrita Server — exatamente o que queremos.
        private readonly NetworkVariable<float> _current = new NetworkVariable<float>();

        private Health _health;

        public override void OnNetworkSpawn()
        {
            _health = GetComponent<Health>();

            if (IsServer)
            {
                _current.Value = _health.Current; // semeia (em OnNetworkSpawn, não em Awake)
            }
            else
            {
                _health.SyncFrom(_current.Value); // late-join: espelha o estado atual
            }

            _current.OnValueChanged += OnCurrentChanged;
        }

        public override void OnNetworkDespawn() => _current.OnValueChanged -= OnCurrentChanged;

        // Servidor: empurra a vida da simulação para a variável quando mudar.
        // (Poll barato por frame — HealthChanged global não identifica a instância.)
        private void Update()
        {
            if (!IsServer || _health == null) return;
            if (!Mathf.Approximately(_current.Value, _health.Current))
            {
                _current.Value = _health.Current;
            }
        }

        private void OnCurrentChanged(float previous, float now)
        {
            if (IsServer) return; // no servidor o Health É a fonte; só a réplica espelha

            _health.SyncFrom(now);
            // Diagnóstico do co-op (raro: só quando a vida replicada muda no cliente).
            Debug.Log($"[Rede] Vida replicada de {name}: {now:0}.");
        }
    }
}
