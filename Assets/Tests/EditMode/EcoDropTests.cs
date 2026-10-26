using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using JogoDaTrupe.Gameplay.Incorporation;
using JogoDaTrupe.Data;

namespace JogoDaTrupe.Tests
{
    /// <summary>
    /// Testes da regra pura do "Eco a todos os contribuintes" (M-NET-5 P2): um Eco por
    /// contribuinte quando a criatura concede habilidade; ninguém se não concede, se não
    /// há contribuintes, ou entradas nulas.
    /// </summary>
    public class EcoDropTests
    {
        private static EnemyDefinition DefComHabilidade()
        {
            var d = ScriptableObject.CreateInstance<EnemyDefinition>();
            d.grantedAbility = ScriptableObject.CreateInstance<AbilityDefinition>();
            return d;
        }

        [Test]
        public void Recipients_UmPorContribuinte()
        {
            var def = DefComHabilidade();
            var recips = EcoDrop.Recipients(def, new HashSet<int> { 0, 1 }).ToList();

            Assert.AreEqual(2, recips.Count);
            Assert.IsTrue(recips.Contains(0));
            Assert.IsTrue(recips.Contains(1));

            Object.DestroyImmediate(def.grantedAbility);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void Recipients_SemHabilidade_Ninguem()
        {
            var def = ScriptableObject.CreateInstance<EnemyDefinition>(); // grantedAbility == null

            Assert.IsEmpty(EcoDrop.Recipients(def, new HashSet<int> { 0, 1 }));

            Object.DestroyImmediate(def);
        }

        [Test]
        public void Recipients_SemContribuintes_Ninguem()
        {
            var def = DefComHabilidade();

            Assert.IsEmpty(EcoDrop.Recipients(def, new HashSet<int>()));

            Object.DestroyImmediate(def.grantedAbility);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void Recipients_EntradasNulas_Ninguem()
        {
            Assert.IsEmpty(EcoDrop.Recipients(null, new HashSet<int> { 0 }));

            var def = DefComHabilidade();
            Assert.IsEmpty(EcoDrop.Recipients(def, null));

            Object.DestroyImmediate(def.grantedAbility);
            Object.DestroyImmediate(def);
        }
    }
}
