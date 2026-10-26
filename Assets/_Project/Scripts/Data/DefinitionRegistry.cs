using UnityEngine;

namespace JogoDaTrupe.Data
{
    /// <summary>
    /// Catálogo de todas as Definições (ScriptableObjects) por id. Serve ao save para
    /// reencontrar a "ficha" a partir do id que foi salvo (ex.: itemId, abilityId, enemyId) —
    /// referências de ScriptableObject não voltam do JSON, mas o id sim. Também é usado
    /// pela camada de rede (M-NET-4) para reconstruir eventos que carregam definições.
    ///
    /// Guarda só dados IMUTÁVEIS (sem estado por-jogador). É exposto por um estático
    /// (<see cref="Active"/>) por conveniência do save: como é só leitura de
    /// definições (não estado de jogo), não fere a regra de evitar singletons de estado.
    /// Populado pelo PlayerSceneSetup.
    /// </summary>
    [CreateAssetMenu(fileName = "DefinitionRegistry", menuName = "Jogo da Trupe/Registro de Definições")]
    public sealed class DefinitionRegistry : ScriptableObject
    {
        public ItemDefinition[] items;
        public AbilityDefinition[] abilities;
        public EnemyDefinition[] enemies;
        public RecipeDefinition[] recipes;       // M-NET-7 P0: destrava o relay de ItemCrafted e o CraftRequested
        public StructureDefinition[] structures; // M-NET-7 P0: destrava o BuildRequested (construção em rede)

        /// <summary>Registro ativo na cena (ligado pelo SaveSystem no Awake).</summary>
        public static DefinitionRegistry Active { get; set; }

        public ItemDefinition ItemById(string id)
        {
            if (items == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < items.Length; i++)
                if (items[i] != null && items[i].itemId == id) return items[i];
            return null;
        }

        public AbilityDefinition AbilityById(string id)
        {
            if (abilities == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < abilities.Length; i++)
                if (abilities[i] != null && abilities[i].abilityId == id) return abilities[i];
            return null;
        }

        public EnemyDefinition EnemyById(string id)
        {
            if (enemies == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < enemies.Length; i++)
                if (enemies[i] != null && enemies[i].enemyId == id) return enemies[i];
            return null;
        }

        public RecipeDefinition RecipeById(string id)
        {
            if (recipes == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < recipes.Length; i++)
                if (recipes[i] != null && recipes[i].recipeId == id) return recipes[i];
            return null;
        }

        public StructureDefinition StructureById(string id)
        {
            if (structures == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < structures.Length; i++)
                if (structures[i] != null && structures[i].structureId == id) return structures[i];
            return null;
        }
    }
}
