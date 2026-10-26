using JogoDaTrupe.Core.Events;

namespace JogoDaTrupe.Data
{
    // NOTA DE ARQUITETURA: estes eventos carregam REFERÊNCIAS da camada Data
    // (EnemyDefinition, AbilityDefinition). Como o Core não pode depender de Data
    // (asmdef do Core sem referências), eles vivem aqui, na camada Data, e apenas
    // implementam a interface IGameEvent (do Core). Continuam trafegando pelo
    // mesmo EventBus<T> e carregam o OwnerId (multiplayer-ready), igual aos
    // eventos de combate. Eventos só com primitivos seguem no Core.

    /// <summary>
    /// Disparado quando um jogador coleta o Eco de uma criatura derrotada.
    /// No slice solo, o dono é o jogador local; no co-op, será publicado uma vez
    /// por participante que contribuiu na derrota.
    /// </summary>
    public struct EcoCollected : IGameEvent
    {
        public int OwnerId;
        public EnemyDefinition Source; // de quem veio o Eco (tem grantedAbility + limiares de maestria)
    }

    /// <summary>
    /// Disparado quando a Maestria de uma criatura/família muda (mais uma derrota),
    /// possivelmente liberando uma camada superior. A UI escuta para dar feedback.
    /// </summary>
    public struct MasteryChanged : IGameEvent
    {
        public int OwnerId;
        public string FamilyId;
        public int Defeats;
        public bool AnyLayerUnlocked;
        public IncorporationLayer HighestLayer; // válido só se AnyLayerUnlocked
    }

    /// <summary>Disparado quando uma habilidade é equipada num slot do loadout.</summary>
    public struct AbilityEquipped : IGameEvent
    {
        public int OwnerId;
        public int Slot;
        public AbilityDefinition Ability;
    }

    /// <summary>Disparado quando um slot do loadout é esvaziado.</summary>
    public struct AbilityUnequipped : IGameEvent
    {
        public int OwnerId;
        public int Slot;
    }

    /// <summary>Disparado quando o jogador ativa (usa) uma habilidade equipada.</summary>
    public struct AbilityUsed : IGameEvent
    {
        public int OwnerId;
        public int Slot;
        public AbilityDefinition Ability;
    }

    /// <summary>
    /// Disparado quando o jogador abre um Altar (libera trocas de loadout).
    /// Só carrega o OwnerId: a UI (que pode referenciar Gameplay) encontra a
    /// Memória/loadout do jogador. Mantém Gameplay desacoplado da UI.
    /// </summary>
    public struct AltarOpened : IGameEvent
    {
        public int OwnerId;
    }

    /// <summary>Disparado quando o jogador fecha o Altar.</summary>
    public struct AltarClosed : IGameEvent
    {
        public int OwnerId;
    }

    /// <summary>
    /// Disparado quando uma habilidade equipada sobe de nível (concede ponto de
    /// especialização). A UI escuta para dar feedback ("subiu de nível!").
    /// </summary>
    public struct AbilityLeveledUp : IGameEvent
    {
        public int OwnerId;
        public int Slot;
        public AbilityDefinition Ability;
        public int Level;
    }

    /// <summary>
    /// Disparado quando os atributos do personagem mudam (subiu de nível e ganhou
    /// pontos, ou distribuiu um ponto). A UI de atributos escuta para se atualizar.
    /// </summary>
    public struct AttributesChanged : IGameEvent
    {
        public int OwnerId;
        public int Level;
        public int PointsAvailable;
    }

    /// <summary>
    /// Disparado quando o jogador pede para abrir/fechar a ficha de atributos (tecla C).
    /// Só carrega o OwnerId; a UI encontra os atributos do jogador.
    /// </summary>
    public struct CharacterSheetToggled : IGameEvent
    {
        public int OwnerId;
    }

    /// <summary>
    /// Disparado quando a afinidade de um elemento muda (após usar uma habilidade).
    /// Carrega o elemento alterado e seu novo valor; a UI (roda de afinidade) lê o
    /// estado completo para se atualizar. O "mundo reativo" (cosmético) também pode
    /// escutar isto no futuro.
    /// </summary>
    public struct AffinityChanged : IGameEvent
    {
        public int OwnerId;
        public ElementType Element;
        public float Value;
    }
}
