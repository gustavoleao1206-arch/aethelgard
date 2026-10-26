using UnityEngine;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Incorporation;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Core.Saving;
using JogoDaTrupe.Data;

namespace JogoDaTrupe.Gameplay.Incorporation
{
    /// <summary>
    /// Ficha de atributos do personagem (estado por-jogador, vai para o save). Guarda
    /// os 6 atributos, o nível do personagem e os pontos a distribuir. O personagem
    /// ganha XP ao derrotar criaturas (via Eco) → sobe de nível → ganha pontos
    /// (`AttributeMath.PointsPerLevel`) para distribuir.
    ///
    /// No slice, só a **Força** tem efeito de gameplay (requisito da Mordida); os
    /// outros 5 já são distribuíveis e ligam a stats reais nas próximas fases.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Ownable))]
    public sealed class PlayerAttributes : MonoBehaviour, ISaveable
    {
        [Tooltip("Valor inicial de cada atributo.")]
        [SerializeField] private int _startingValue = 5;

        [Tooltip("XP concedido ao personagem por criatura derrotada.")]
        [SerializeField] private int _xpPerDefeat = 50;

        private const int AttributeCount = 6; // = nº de AttributeType
        private readonly int[] _values = new int[AttributeCount];

        private Ownable _ownable;

        public int Level { get; private set; } = 1;
        public int Xp { get; private set; }
        public int PointsAvailable { get; private set; }

        private void Awake()
        {
            _ownable = GetComponent<Ownable>();
            for (int i = 0; i < _values.Length; i++) _values[i] = _startingValue;
        }

        private void OnEnable() => EventBus<EcoCollected>.Subscribe(OnEcoCollected);
        private void OnDisable() => EventBus<EcoCollected>.Unsubscribe(OnEcoCollected);

        // Derrotar uma criatura (que deixou Eco para este jogador) dá XP ao personagem.
        private void OnEcoCollected(EcoCollected e)
        {
            // Só a AUTORIDADE ingere (M-NET-8 P0): no cliente o estado chega replicado
            // (SyncFrom) — sem o gate, relay + espelho dariam XP 2×.
            if (!SimulationAuthority.IsAuthority) return;

            if (e.OwnerId != _ownable.OwnerId) return;
            AddXp(_xpPerDefeat);
        }

        public int GetValue(AttributeType type) => _values[(int)type];

        /// <summary>
        /// Distribui um ponto no atributo, se houver ponto disponível.
        /// SEM AUTORIDADE (cliente, M-NET-8): não muta — publica a INTENÇÃO
        /// <see cref="InvestAttributeRequested"/> (o servidor valida os pontos no
        /// estado autoritativo e o resultado volta replicado) e retorna false.
        /// </summary>
        public bool TryInvest(AttributeType type)
        {
            if (!SimulationAuthority.IsAuthority)
            {
                EventBus<InvestAttributeRequested>.Publish(new InvestAttributeRequested
                {
                    Attribute = (int)type,
                    OwnerId = _ownable.OwnerId
                });
                return false;
            }

            if (PointsAvailable <= 0) return false;

            PointsAvailable--;
            _values[(int)type]++;
            PublishChanged();
            return true;
        }

        /// <summary>
        /// ESPELHO da réplica (M-NET-8): aplica o snapshot AUTORITATIVO recebido do
        /// servidor (via NetworkAttributes), bypassando os gates, e republica
        /// AttributesChanged para as HUDs locais.
        /// </summary>
        public void SyncFrom(int level, int xp, int points, int[] values)
        {
            Level = level;
            Xp = xp;
            PointsAvailable = points;
            if (values != null)
                for (int i = 0; i < _values.Length && i < values.Length; i++) _values[i] = values[i];
            PublishChanged();
        }

        private void AddXp(int amount)
        {
            if (amount <= 0) return;
            Xp += amount;

            while (Xp >= AttributeMath.XpForPlayerLevel(Level))
            {
                Xp -= AttributeMath.XpForPlayerLevel(Level);
                Level++;
                PointsAvailable += AttributeMath.PointsPerLevel;
            }
            PublishChanged();
        }

        private void PublishChanged()
        {
            EventBus<AttributesChanged>.Publish(new AttributesChanged
            {
                OwnerId = _ownable.OwnerId,
                Level = Level,
                PointsAvailable = PointsAvailable
            });
        }

        // ---- Save (ISaveable) ----

        public string SaveKey => "attributes:" + _ownable.OwnerId;

        [System.Serializable]
        private struct AttributesSave
        {
            public int level;
            public int xp;
            public int points;
            public int[] values;
        }

        public object CaptureState() => new AttributesSave
        {
            level = Level,
            xp = Xp,
            points = PointsAvailable,
            values = (int[])_values.Clone()
        };

        public void RestoreState(object state)
        {
            var d = JsonUtility.FromJson<AttributesSave>((string)state);
            Level = d.level;
            Xp = d.xp;
            PointsAvailable = d.points;
            if (d.values != null)
                for (int i = 0; i < _values.Length && i < d.values.Length; i++) _values[i] = d.values[i];
            PublishChanged();
        }
    }
}
