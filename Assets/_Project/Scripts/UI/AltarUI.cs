using UnityEngine;
using UnityEngine.UI;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Incorporation;

namespace JogoDaTrupe.UI
{
    /// <summary>
    /// Tela do Altar: aparece quando o jogador abre um Altar e permite incorporar
    /// Ecos (camada Vestígio no slice) e equipar até 5 habilidades. Reage aos
    /// eventos AltarOpened/AltarClosed (desacoplada da Simulação) e lê a Memória /
    /// loadout do jogador (referências ligadas na cena).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AltarUI : MonoBehaviour
    {
        [SerializeField] private MemoryBank _memory;
        [SerializeField] private AbilityLoadout _loadout;

        /// <summary>Liga esta tela à Memória/loadout do jogador local (rede, no spawn do dono).</summary>
        public void Bind(MemoryBank memory, AbilityLoadout loadout)
        {
            _memory = memory;
            _loadout = loadout;
        }

        private GameObject _panel;
        private Transform _ecoList;
        private Transform _slotList;
        private Text _status;

        private void Awake()
        {
            BuildPanel();
            _panel.SetActive(false);
        }

        private void OnEnable()
        {
            EventBus<AltarOpened>.Subscribe(OnAltarOpened);
            EventBus<AltarClosed>.Subscribe(OnAltarClosed);
            EventBus<AbilityEquipped>.Subscribe(OnLoadoutMirrored);
            EventBus<AbilityUnequipped>.Subscribe(OnUnequipMirrored);
        }

        private void OnDisable()
        {
            EventBus<AltarOpened>.Unsubscribe(OnAltarOpened);
            EventBus<AltarClosed>.Unsubscribe(OnAltarClosed);
            EventBus<AbilityEquipped>.Unsubscribe(OnLoadoutMirrored);
            EventBus<AbilityUnequipped>.Unsubscribe(OnUnequipMirrored);
        }

        // No co-op (M-NET-8), o clique do cliente vira "Pedido enviado ao servidor…" e o
        // resultado chega 1 tick depois pelo ESPELHO (NetworkLoadout → SyncFrom), que
        // republica estes eventos — re-desenha o painel aberto para o slot aparecer.
        // No solo é idempotente (o clique já reconstruiu).
        private void OnLoadoutMirrored(AbilityEquipped e)
        {
            if (e.OwnerId != LocalPlayer.Id || !_panel.activeSelf) return;
            Rebuild();
        }

        private void OnUnequipMirrored(AbilityUnequipped e)
        {
            if (e.OwnerId != LocalPlayer.Id || !_panel.activeSelf) return;
            Rebuild();
        }

        private void OnAltarOpened(AltarOpened e)
        {
            if (e.OwnerId != LocalPlayer.Id) return;
            _panel.SetActive(true);
            _status.text = "Incorpore um Eco e equipe (limite de 5).";
            Rebuild();
        }

        private void OnAltarClosed(AltarClosed e)
        {
            if (e.OwnerId != LocalPlayer.Id) return;
            _panel.SetActive(false);
        }

        // Reconstrói as duas listas: Ecos incorporáveis e slots equipados.
        private void Rebuild()
        {
            ClearChildren(_ecoList);
            ClearChildren(_slotList);

            // Sem o loadout ligado não há o que montar (defensivo — na cena gerada
            // as referências vêm ligadas pelo PlayerSceneSetup).
            if (_loadout == null) return;

            Text ecoHeader = UiFactory.CreateText(_ecoList, "ECOS NA MEMÓRIA", 18, TextAnchor.MiddleLeft);
            ecoHeader.color = UiTheme.Text;
            UiFactory.AddTextShadow(ecoHeader);
            bool anyEco = false;
            if (_memory != null)
            {
                foreach (MemoryBank.Eco eco in _memory.IncorporableEcos)
                {
                    anyEco = true;
                    MemoryBank.Eco captured = eco;
                    string label = $"Incorporar: {eco.Ability.displayName} [{eco.HighestLayer}]  (família {eco.FamilyId})";
                    Button b = UiFactory.CreateButton(_ecoList, label, new Vector2(440f, 34f));
                    b.onClick.AddListener(() => Incorporate(captured));
                }
            }
            if (!anyEco)
            {
                Text ecoHint = UiFactory.CreateText(_ecoList, "(derrote criaturas para coletar Ecos)", 14, TextAnchor.MiddleLeft);
                ecoHint.color = UiTheme.TextDim;
            }

            Text slotHeader = UiFactory.CreateText(_slotList, "EQUIPADAS (1–5)", 18, TextAnchor.MiddleLeft);
            slotHeader.color = UiTheme.Text;
            UiFactory.AddTextShadow(slotHeader);
            for (int i = 0; i < _loadout.Capacity; i++)
            {
                AbilityInstance inst = _loadout.GetSlot(i);
                int slot = i;

                if (inst == null)
                {
                    Text emptySlot = UiFactory.CreateText(_slotList, $"Slot {i + 1}: (vazio)", 15, TextAnchor.MiddleLeft);
                    emptySlot.color = UiTheme.TextDim;
                    continue;
                }

                Button unequip = UiFactory.CreateButton(_slotList,
                    $"Slot {i + 1}: {inst.Definition.displayName}  (nível {inst.Level})  [X] desequipar", new Vector2(440f, 32f));
                unequip.onClick.AddListener(() => Unequip(slot));

                BuildSpecialization(inst);
            }
        }

        // Mostra a especialização da habilidade: pontos disponíveis, nós para
        // investir e o respec. Só faz sentido no Altar (estamos nele aqui).
        private void BuildSpecialization(AbilityInstance inst)
        {
            Text pts = UiFactory.CreateText(_slotList, $"   Pontos: {inst.PointsAvailable}   XP: {inst.Xp}", 14, TextAnchor.MiddleLeft);
            pts.color = UiTheme.TextDim;

            SpecializationNode[] nodes = inst.Definition.specializations;
            if (nodes != null)
            {
                for (int n = 0; n < nodes.Length; n++)
                {
                    int nodeIndex = n;
                    SpecializationNode node = nodes[n];
                    string label = $"   [+] {node.label}  ({inst.NodeLevels[n]}/{node.maxLevel}, custo {node.pointCost})";
                    Button b = UiFactory.CreateButton(_slotList, label, new Vector2(420f, 28f));
                    b.onClick.AddListener(() => Invest(inst, nodeIndex));
                }
            }

            Button respec = UiFactory.CreateButton(_slotList, "   Respec (devolve os pontos; custa material)", new Vector2(420f, 28f));
            int slotIndex = FindSlotOf(inst);
            respec.onClick.AddListener(() => Respec(slotIndex));
        }

        // As operações passam pelo LOADOUT (não direto na instância): é lá que vivem
        // os gates de rede (M-NET-8 — sem autoridade viram pedidos ao servidor) e o
        // custo do respec (🎨 2 Pedra).
        private void Invest(AbilityInstance inst, int nodeIndex)
        {
            int slot = FindSlotOf(inst);
            if (_loadout.TryInvestNode(slot, nodeIndex, out string reason))
                _status.text = "Especialização aplicada.";
            else
                _status.text = reason;
            Rebuild();
        }

        private void Respec(int slot)
        {
            if (_loadout.TryRespec(slot, out string reason))
                _status.text = "Especialização revertida.";
            else
                _status.text = reason;
            Rebuild();
        }

        private int FindSlotOf(AbilityInstance inst)
        {
            for (int i = 0; i < _loadout.Capacity; i++)
                if (ReferenceEquals(_loadout.GetSlot(i), inst)) return i;
            return -1;
        }

        private void Incorporate(MemoryBank.Eco eco)
        {
            var instance = new AbilityInstance(eco.Ability, eco.HighestLayer);
            if (_loadout.TryEquip(instance, out string reason))
                _status.text = $"Incorporada e equipada: {eco.Ability.displayName}.";
            else
                _status.text = reason;
            Rebuild();
        }

        private void Unequip(int slot)
        {
            if (_loadout.Unequip(slot))
                _status.text = "Slot liberado.";
            Rebuild();
        }

        // ---- Construção da UI (uma vez, no Awake) ----

        private void BuildPanel()
        {
            Image bg = UiFactory.CreatePanel(transform, UiTheme.Card, rounded: true);
            _panel = bg.gameObject;
            _panel.name = "Painel";
            StretchFull((RectTransform)_panel.transform, 80f);

            Text title = UiFactory.CreateText(_panel.transform, "ALTAR  —  pressione E para fechar", 26, TextAnchor.UpperCenter);
            title.color = UiTheme.Text;
            UiFactory.AddTextShadow(title);
            AnchorTop(title.rectTransform, 24f);

            _status = UiFactory.CreateText(_panel.transform, "", 16, TextAnchor.UpperCenter);
            _status.color = UiTheme.TextDim;
            AnchorTop(_status.rectTransform, 64f);

            _ecoList = CreateColumn("ColunaEcos", new Vector2(0.06f, 0.06f), new Vector2(0.5f, 0.84f));
            _slotList = CreateColumn("ColunaSlots", new Vector2(0.52f, 0.06f), new Vector2(0.96f, 0.84f));
        }

        private Transform CreateColumn(string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(_panel.transform, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperLeft;
            return go.transform;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }

        private static void StretchFull(RectTransform rt, float margin)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(margin, margin);
            rt.offsetMax = new Vector2(-margin, -margin);
        }

        private static void AnchorTop(RectTransform rt, float fromTop)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -fromTop);
            rt.sizeDelta = new Vector2(0f, 36f);
        }
    }
}
