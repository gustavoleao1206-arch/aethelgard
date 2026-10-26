using UnityEngine;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Core.Saving;
using JogoDaTrupe.Data;

namespace JogoDaTrupe.Gameplay.Incorporation
{
    /// <summary>
    /// Afinidade elemental do personagem (estado por-jogador, vai para o save no
    /// futuro). Guarda 0–100% por elemento. Usar uma habilidade (evento
    /// <see cref="AbilityUsed"/>) sobe a afinidade do elemento dela e aplica as
    /// regras de oposto/especialização (lógica pura em <see cref="AffinityMath"/>).
    ///
    /// Não conhece quem usa o bônus: expõe <see cref="SpecialistBonus"/> e o
    /// AbilityRunner consulta ao calcular o dano. Multiplayer-ready (filtra OwnerId).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Ownable))]
    public sealed class AffinityState : MonoBehaviour, ISaveable
    {
        [Tooltip("Afinidade inicial de cada elemento (o personagem começa generalista, com afinidade latente). Dá margem para o oposto cair e tornar a regra visível.")]
        [SerializeField] private float _startingAffinity = 30f;

        private const int ElementCount = 6; // = nº de ElementType
        private readonly float[] _affinities = new float[ElementCount];

        private Ownable _ownable;

        private void Awake()
        {
            _ownable = GetComponent<Ownable>();
            float start = Mathf.Clamp(_startingAffinity, 0f, AffinityMath.Max);
            for (int i = 0; i < _affinities.Length; i++) _affinities[i] = start;
        }

        private void OnEnable() => EventBus<AbilityUsed>.Subscribe(OnAbilityUsed);
        private void OnDisable() => EventBus<AbilityUsed>.Unsubscribe(OnAbilityUsed);

        private void OnAbilityUsed(AbilityUsed e)
        {
            // Só a AUTORIDADE ingere (M-NET-8 P0): o AbilityUsed do dono-cliente chega
            // ao servidor pelo NetworkStyleForwarder (M-NET-6) e a cópia de lá ingere;
            // no cliente o estado volta replicado (SyncFrom). Sem o gate, dono e
            // servidor subiriam a afinidade em paralelo (drift).
            if (!SimulationAuthority.IsAuthority) return;

            if (e.OwnerId != _ownable.OwnerId) return;
            if (e.Ability == null) return;

            ElementType element = e.Ability.element;
            AffinityMath.ApplyUse(_affinities, element);

            EventBus<AffinityChanged>.Publish(new AffinityChanged
            {
                OwnerId = _ownable.OwnerId,
                Element = element,
                Value = _affinities[(int)element]
            });
        }

        public float GetAffinity(ElementType type) => _affinities[(int)type];

        /// <summary>
        /// ESPELHO da réplica (M-NET-8): aplica as afinidades AUTORITATIVAS recebidas
        /// do servidor (via NetworkAffinity) e republica AffinityChanged por elemento
        /// (a roda da ficha atualiza).
        /// </summary>
        public void SyncFrom(float[] affinities)
        {
            if (affinities == null) return;
            for (int i = 0; i < _affinities.Length && i < affinities.Length; i++)
            {
                _affinities[i] = affinities[i];
                EventBus<AffinityChanged>.Publish(new AffinityChanged
                {
                    OwnerId = _ownable.OwnerId,
                    Element = (ElementType)i,
                    Value = _affinities[i]
                });
            }
        }

        /// <summary>Bônus de dano por ser especialista no elemento da habilidade (0..MaxSpecialistBonus).</summary>
        public float SpecialistBonus(ElementType type) => AffinityMath.SpecialistBonus(_affinities[(int)type]);

        // ---- Save (ISaveable) ----

        public string SaveKey => "affinity:" + _ownable.OwnerId;

        [System.Serializable]
        private struct AffinitySave { public float[] affinities; }

        public object CaptureState() => new AffinitySave { affinities = (float[])_affinities.Clone() };

        public void RestoreState(object state)
        {
            var d = JsonUtility.FromJson<AffinitySave>((string)state);
            if (d.affinities != null)
                for (int i = 0; i < _affinities.Length && i < d.affinities.Length; i++)
                    _affinities[i] = d.affinities[i];
            // A ficha (tecla C) relê a afinidade ao abrir; sem evento por elemento aqui.
        }
    }
}
