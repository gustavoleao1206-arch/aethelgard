using System.Collections.Generic;
using UnityEngine;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Core.Saving;
using JogoDaTrupe.Data;

namespace JogoDaTrupe.Gameplay.Incorporation
{
    /// <summary>
    /// A "Memória" do Sincronizador: guarda os Ecos coletados (por família) e o
    /// quanto cada criatura foi dominada (Maestria). Fica no objeto do jogador,
    /// é por-jogador (filtra pelo Ownable) e, no futuro, vai para o save.
    ///
    /// NOTA DE DESIGN (slice): para evitar dois componentes guardando o mesmo mapa
    /// (DRY), a contagem de Maestria vive AQUI, junto do catálogo de Ecos, em vez
    /// de um componente CreatureMastery separado como no plano original. Quando o
    /// catálogo crescer, dá para extrair a Maestria sem afetar quem usa esta classe.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Ownable))]
    public sealed class MemoryBank : MonoBehaviour, ISaveable
    {
        /// <summary>Um Eco guardado na Memória (catálogo + maestria de uma família).</summary>
        public sealed class Eco
        {
            public string FamilyId;
            public AbilityDefinition Ability;
            public int[] Thresholds;
            public int Defeats;
            public bool AnyLayerUnlocked;
            public IncorporationLayer HighestLayer;
        }

        private readonly Dictionary<string, Eco> _ecos = new Dictionary<string, Eco>();
        private Ownable _ownable;

        private void Awake() => _ownable = GetComponent<Ownable>();

        private void OnEnable() => EventBus<EcoCollected>.Subscribe(OnEcoCollected);
        private void OnDisable() => EventBus<EcoCollected>.Unsubscribe(OnEcoCollected);

        private void OnEcoCollected(EcoCollected e)
        {
            // Só a AUTORIDADE ingere (M-NET-8 P0): no cliente o EcoCollected relayado é
            // APRESENTAÇÃO (toast) — o estado chega replicado do servidor (SyncFrom).
            // Sem o gate, relay + espelho entregariam a maestria 2×.
            if (!SimulationAuthority.IsAuthority) return;

            // Só reage aos Ecos deste jogador.
            if (e.OwnerId != _ownable.OwnerId) return;
            if (e.Source == null || e.Source.grantedAbility == null) return;

            AbilityDefinition ability = e.Source.grantedAbility;
            string familyId = ability.familyId;
            if (string.IsNullOrEmpty(familyId)) return;

            if (!_ecos.TryGetValue(familyId, out Eco eco))
            {
                eco = new Eco { FamilyId = familyId };
                _ecos[familyId] = eco;
            }

            eco.Ability = ability;
            eco.Thresholds = e.Source.masteryThresholds;
            eco.Defeats++;
            eco.AnyLayerUnlocked = MasteryMath.TryHighestLayer(eco.Defeats, eco.Thresholds, out IncorporationLayer layer);
            eco.HighestLayer = layer;

            EventBus<MasteryChanged>.Publish(new MasteryChanged
            {
                OwnerId = _ownable.OwnerId,
                FamilyId = familyId,
                Defeats = eco.Defeats,
                AnyLayerUnlocked = eco.AnyLayerUnlocked,
                HighestLayer = layer
            });
        }

        /// <summary>TODOS os Ecos (inclusive os ainda sem camada liberada) — a réplica de
        /// rede espelha o catálogo inteiro (progresso parcial de maestria também conta).</summary>
        public IEnumerable<Eco> AllEcos => _ecos.Values;

        /// <summary>Ecos já incorporáveis (com ao menos a camada Vestígio liberada).</summary>
        public IEnumerable<Eco> IncorporableEcos
        {
            get
            {
                foreach (Eco eco in _ecos.Values)
                {
                    if (eco.AnyLayerUnlocked) yield return eco;
                }
            }
        }

        public bool TryGetEco(string familyId, out Eco eco) => _ecos.TryGetValue(familyId, out eco);

        /// <summary>
        /// ESPELHO da réplica (M-NET-8): aplica a memória AUTORITATIVA recebida do
        /// servidor (via NetworkMemoryBank), reconstruindo cada Eco por id — o mesmo
        /// desenho do RestoreState do save. Bypassa o gate de autoridade de propósito
        /// e republica MasteryChanged para as HUDs locais.
        /// Tupla: (familyId, abilityId, defeats, t0, t1, t2) — os 3 limiares viajam
        /// junto (vivem na ficha da CRIATURA, não na habilidade — sem eles o espelho
        /// não recomputa a camada).
        /// </summary>
        public void SyncFrom(IReadOnlyList<(string familyId, string abilityId, int defeats, int t0, int t1, int t2)> entries)
        {
            _ecos.Clear();
            if (entries == null) return;

            for (int i = 0; i < entries.Count; i++)
            {
                var (familyId, abilityId, defeats, t0, t1, t2) = entries[i];
                if (string.IsNullOrEmpty(familyId)) continue;

                AbilityDefinition ability = DefinitionRegistry.Active != null
                    ? DefinitionRegistry.Active.AbilityById(abilityId) : null;

                var eco = new Eco
                {
                    FamilyId = familyId,
                    Ability = ability,
                    Thresholds = new[] { t0, t1, t2 },
                    Defeats = defeats
                };
                eco.AnyLayerUnlocked = MasteryMath.TryHighestLayer(eco.Defeats, eco.Thresholds, out IncorporationLayer layer);
                eco.HighestLayer = layer;
                _ecos[familyId] = eco;

                EventBus<MasteryChanged>.Publish(new MasteryChanged
                {
                    OwnerId = _ownable.OwnerId,
                    FamilyId = familyId,
                    Defeats = eco.Defeats,
                    AnyLayerUnlocked = eco.AnyLayerUnlocked,
                    HighestLayer = layer
                });
            }
        }

        // ---- Save (ISaveable) ----

        public string SaveKey => "memory:" + _ownable.OwnerId;

        [System.Serializable]
        private struct EcoSave
        {
            public string familyId;
            public string abilityId;
            public int[] thresholds;
            public int defeats;
        }

        [System.Serializable]
        private struct MemorySave { public List<EcoSave> ecos; }

        public object CaptureState()
        {
            var data = new MemorySave { ecos = new List<EcoSave>() };
            foreach (Eco eco in _ecos.Values)
            {
                data.ecos.Add(new EcoSave
                {
                    familyId = eco.FamilyId,
                    abilityId = eco.Ability != null ? eco.Ability.abilityId : null,
                    thresholds = eco.Thresholds,
                    defeats = eco.Defeats
                });
            }
            return data;
        }

        public void RestoreState(object state)
        {
            var data = JsonUtility.FromJson<MemorySave>((string)state);
            _ecos.Clear();
            if (data.ecos == null) return;

            foreach (EcoSave es in data.ecos)
            {
                if (string.IsNullOrEmpty(es.familyId)) continue;

                AbilityDefinition ability = DefinitionRegistry.Active != null
                    ? DefinitionRegistry.Active.AbilityById(es.abilityId) : null;

                var eco = new Eco
                {
                    FamilyId = es.familyId,
                    Ability = ability,
                    Thresholds = es.thresholds,
                    Defeats = es.defeats
                };
                eco.AnyLayerUnlocked = MasteryMath.TryHighestLayer(eco.Defeats, eco.Thresholds, out IncorporationLayer layer);
                eco.HighestLayer = layer;
                _ecos[es.familyId] = eco;
            }
        }
    }
}
