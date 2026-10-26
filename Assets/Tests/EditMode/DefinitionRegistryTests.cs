using NUnit.Framework;
using UnityEngine;
using JogoDaTrupe.Data;

namespace JogoDaTrupe.Tests.Gameplay
{
    /// <summary>
    /// Testa o lookup puro do registro de definições (EditMode): é por ele que o save
    /// reencontra a "ficha" a partir do id (referências de ScriptableObject não voltam
    /// do JSON). Ver DefinitionRegistry.
    /// </summary>
    public class DefinitionRegistryTests
    {
        private static ItemDefinition Item(string id)
        {
            var d = ScriptableObject.CreateInstance<ItemDefinition>();
            d.itemId = id;
            return d;
        }

        private static AbilityDefinition Ability(string id)
        {
            var d = ScriptableObject.CreateInstance<AbilityDefinition>();
            d.abilityId = id;
            return d;
        }

        private static EnemyDefinition Enemy(string id)
        {
            var d = ScriptableObject.CreateInstance<EnemyDefinition>();
            d.enemyId = id;
            return d;
        }

        private static RecipeDefinition Recipe(string id)
        {
            var d = ScriptableObject.CreateInstance<RecipeDefinition>();
            d.recipeId = id;
            return d;
        }

        private static StructureDefinition Structure(string id)
        {
            var d = ScriptableObject.CreateInstance<StructureDefinition>();
            d.structureId = id;
            return d;
        }

        [Test]
        public void ItemById_EncontraPorId()
        {
            var madeira = Item("madeira");
            var pedra = Item("pedra");
            var reg = ScriptableObject.CreateInstance<DefinitionRegistry>();
            reg.items = new[] { madeira, pedra };

            Assert.AreSame(pedra, reg.ItemById("pedra"));
            Assert.AreSame(madeira, reg.ItemById("madeira"));

            Object.DestroyImmediate(madeira); Object.DestroyImmediate(pedra); Object.DestroyImmediate(reg);
        }

        [Test]
        public void ItemById_IdInexistente_RetornaNull()
        {
            var madeira = Item("madeira");
            var reg = ScriptableObject.CreateInstance<DefinitionRegistry>();
            reg.items = new[] { madeira };

            Assert.IsNull(reg.ItemById("inexistente"));

            Object.DestroyImmediate(madeira); Object.DestroyImmediate(reg);
        }

        [Test]
        public void ItemById_IdNuloOuVazio_RetornaNull()
        {
            var reg = ScriptableObject.CreateInstance<DefinitionRegistry>();
            reg.items = new[] { Item("madeira") };

            Assert.IsNull(reg.ItemById(null));
            Assert.IsNull(reg.ItemById(""));

            foreach (var it in reg.items) Object.DestroyImmediate(it);
            Object.DestroyImmediate(reg);
        }

        [Test]
        public void ItemById_ArrayNulo_NaoLanca_RetornaNull()
        {
            var reg = ScriptableObject.CreateInstance<DefinitionRegistry>();
            reg.items = null;

            Assert.IsNull(reg.ItemById("madeira"));

            Object.DestroyImmediate(reg);
        }

        [Test]
        public void ItemById_PulaSlotsNulos()
        {
            var madeira = Item("madeira");
            var reg = ScriptableObject.CreateInstance<DefinitionRegistry>();
            reg.items = new ItemDefinition[] { null, madeira };

            Assert.AreSame(madeira, reg.ItemById("madeira")); // não estoura no slot nulo

            Object.DestroyImmediate(madeira); Object.DestroyImmediate(reg);
        }

        [Test]
        public void AbilityById_EncontraEDescartaInvalidos()
        {
            var mordida = Ability("mordida");
            var reg = ScriptableObject.CreateInstance<DefinitionRegistry>();
            reg.abilities = new[] { mordida };

            Assert.AreSame(mordida, reg.AbilityById("mordida"));
            Assert.IsNull(reg.AbilityById("cipo"));
            Assert.IsNull(reg.AbilityById(null));

            Object.DestroyImmediate(mordida); Object.DestroyImmediate(reg);
        }

        [Test]
        public void AbilityById_ArrayNulo_NaoLanca()
        {
            var reg = ScriptableObject.CreateInstance<DefinitionRegistry>();
            Assert.IsNull(reg.AbilityById("mordida"));
            Object.DestroyImmediate(reg);
        }

        [Test]
        public void EnemyById_EncontraEDescartaInvalidos()
        {
            var planta = Enemy("creature_planta");
            var boss = Enemy("boss_ecocorrompido");
            var reg = ScriptableObject.CreateInstance<DefinitionRegistry>();
            reg.enemies = new[] { planta, boss };

            Assert.AreSame(boss, reg.EnemyById("boss_ecocorrompido"));
            Assert.AreSame(planta, reg.EnemyById("creature_planta"));
            Assert.IsNull(reg.EnemyById("inexistente"));
            Assert.IsNull(reg.EnemyById(null));

            Object.DestroyImmediate(planta); Object.DestroyImmediate(boss); Object.DestroyImmediate(reg);
        }

        [Test]
        public void EnemyById_ArrayNulo_NaoLanca_EPulaSlotsNulos()
        {
            var boss = Enemy("boss");
            var vazio = ScriptableObject.CreateInstance<DefinitionRegistry>();
            Assert.IsNull(vazio.EnemyById("boss")); // array nulo não estoura
            Object.DestroyImmediate(vazio);

            var reg = ScriptableObject.CreateInstance<DefinitionRegistry>();
            reg.enemies = new EnemyDefinition[] { null, boss };
            Assert.AreSame(boss, reg.EnemyById("boss")); // pula o slot nulo

            Object.DestroyImmediate(boss); Object.DestroyImmediate(reg);
        }

        // ---- M-NET-7 P0: receitas e estruturas (destravam craft/construção em rede) ----

        [Test]
        public void RecipeById_EncontraEDescartaInvalidos()
        {
            var tabua = Recipe("recipe_tabua");
            var machado = Recipe("recipe_machado");
            var reg = ScriptableObject.CreateInstance<DefinitionRegistry>();
            reg.recipes = new[] { tabua, machado };

            Assert.AreSame(machado, reg.RecipeById("recipe_machado"));
            Assert.AreSame(tabua, reg.RecipeById("recipe_tabua"));
            Assert.IsNull(reg.RecipeById("inexistente"));
            Assert.IsNull(reg.RecipeById(null));

            Object.DestroyImmediate(tabua); Object.DestroyImmediate(machado); Object.DestroyImmediate(reg);
        }

        [Test]
        public void RecipeById_ArrayNulo_NaoLanca_EPulaSlotsNulos()
        {
            var tabua = Recipe("recipe_tabua");
            var vazio = ScriptableObject.CreateInstance<DefinitionRegistry>();
            Assert.IsNull(vazio.RecipeById("recipe_tabua")); // array nulo não estoura
            Object.DestroyImmediate(vazio);

            var reg = ScriptableObject.CreateInstance<DefinitionRegistry>();
            reg.recipes = new RecipeDefinition[] { null, tabua };
            Assert.AreSame(tabua, reg.RecipeById("recipe_tabua")); // pula o slot nulo

            Object.DestroyImmediate(tabua); Object.DestroyImmediate(reg);
        }

        [Test]
        public void StructureById_EncontraEDescartaInvalidos()
        {
            var parede = Structure("structure_parede");
            var piso = Structure("structure_piso");
            var reg = ScriptableObject.CreateInstance<DefinitionRegistry>();
            reg.structures = new[] { parede, piso };

            Assert.AreSame(piso, reg.StructureById("structure_piso"));
            Assert.AreSame(parede, reg.StructureById("structure_parede"));
            Assert.IsNull(reg.StructureById("inexistente"));
            Assert.IsNull(reg.StructureById(null));

            Object.DestroyImmediate(parede); Object.DestroyImmediate(piso); Object.DestroyImmediate(reg);
        }

        [Test]
        public void StructureById_ArrayNulo_NaoLanca_EPulaSlotsNulos()
        {
            var parede = Structure("structure_parede");
            var vazio = ScriptableObject.CreateInstance<DefinitionRegistry>();
            Assert.IsNull(vazio.StructureById("structure_parede")); // array nulo não estoura
            Object.DestroyImmediate(vazio);

            var reg = ScriptableObject.CreateInstance<DefinitionRegistry>();
            reg.structures = new StructureDefinition[] { null, parede };
            Assert.AreSame(parede, reg.StructureById("structure_parede")); // pula o slot nulo

            Object.DestroyImmediate(parede); Object.DestroyImmediate(reg);
        }
    }
}
