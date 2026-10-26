using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Building;

namespace JogoDaTrupe.Tests.Gameplay
{
    /// <summary>
    /// Testa a IDENTIDADE da estrutura (M-NET-7 P5): o WorldRequestExecutor casa o prefab a
    /// spawnar pelo <see cref="Structure.StructureId"/>. Se esse contrato quebrar (id errado
    /// ou NRE sem ficha), a construção em rede não acha o prefab.
    /// </summary>
    public class StructureTests
    {
        // Structure._definition é [SerializeField] private — em teste injetamos por reflexão
        // (mesmo efeito do que o gerador faz com SetObjectField no import do prefab).
        private static void SetDefinition(Structure structure, StructureDefinition def)
        {
            FieldInfo field = typeof(Structure).GetField("_definition", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(structure, def);
        }

        [Test]
        public void StructureId_DevolveOIdDaFicha()
        {
            var def = ScriptableObject.CreateInstance<StructureDefinition>();
            def.structureId = "structure_parede";

            var go = new GameObject("EstruturaDeTeste");
            var structure = go.AddComponent<Structure>();
            SetDefinition(structure, def);

            Assert.AreEqual("structure_parede", structure.StructureId);
            Assert.AreSame(def, structure.Definition);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void StructureId_SemFicha_EhNull_NaoLanca()
        {
            var go = new GameObject("EstruturaSemFicha");
            var structure = go.AddComponent<Structure>();

            string id = null;
            Assert.DoesNotThrow(() => id = structure.StructureId);
            Assert.IsNull(id, "sem StructureDefinition o id deve ser null (defensivo), não NRE");
            Assert.IsNull(structure.Definition);

            Object.DestroyImmediate(go);
        }
    }
}
