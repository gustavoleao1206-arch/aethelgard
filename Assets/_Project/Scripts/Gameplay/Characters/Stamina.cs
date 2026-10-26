using UnityEngine;

namespace JogoDaTrupe.Gameplay.Characters
{
    /// <summary>
    /// Estamina do jogador (estilo Palworld): correr e deslizar CONSOMEM; regenera quando
    /// para de gastar (após um pequeno atraso). O <see cref="PlayerController"/> dirige o
    /// gasto/regen; a HUD só lê <see cref="Normalized"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Stamina : MonoBehaviour
    {
        [SerializeField] private float _max = 100f;
        [SerializeField] private float _regenPerSecond = 16f;
        [SerializeField] private float _regenDelay = 0.6f; // espera após gastar p/ voltar a regenerar

        /// <summary>Estamina máxima.</summary>
        public float Max => _max;

        /// <summary>Estamina atual (0.._max).</summary>
        public float Current { get; private set; }

        /// <summary>Fração 0..1 (para a barra de HUD).</summary>
        public float Normalized => _max > 0f ? Mathf.Clamp01(Current / _max) : 0f;

        /// <summary>Verdadeiro quando praticamente zerada (não dá mais pra correr).</summary>
        public bool IsEmpty => Current <= 0.5f;

        private float _regenBlockedUntil;

        private void Awake() => Current = _max;

        /// <summary>Tenta gastar um custo pontual (ex.: o deslize). false se não há o suficiente.</summary>
        public bool TrySpend(float amount)
        {
            if (Current < amount) return false;
            Current -= amount;
            BlockRegen();
            return true;
        }

        /// <summary>Dreno CONTÍNUO (correr): gasta o que puder e adia a regeneração.</summary>
        public void Drain(float amount)
        {
            if (amount <= 0f) return;
            Current = Mathf.Max(0f, Current - amount);
            BlockRegen();
        }

        private void BlockRegen() => _regenBlockedUntil = Time.time + _regenDelay;

        private void Update()
        {
            if (Time.time >= _regenBlockedUntil && Current < _max)
                Current = Mathf.Min(_max, Current + _regenPerSecond * Time.deltaTime);
        }
    }
}
