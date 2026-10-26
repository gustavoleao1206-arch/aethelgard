using NUnit.Framework;
using UnityEngine;
using JogoDaTrupe.Core.Saving;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Inventory;

namespace JogoDaTrupe.Tests.Gameplay
{
    /// <summary>
    /// Testa as peças puras do save (EditMode): o arquivo de save (busca por chave e
    /// round-trip JSON) e o Clear do inventário usado na restauração.
    /// </summary>
    public class SaveFileTests
    {
        [Test]
        public void SaveFile_Find_DevolveOJsonDaChave()
        {
            var file = new SaveFile();
            file.entries.Add(new SaveEntry { Key = "a", Json = "{\"x\":1}" });
            file.entries.Add(new SaveEntry { Key = "b", Json = "{\"y\":2}" });

            Assert.AreEqual("{\"y\":2}", file.Find("b"));
            Assert.IsNull(file.Find("inexistente"));
        }

        [Test]
        public void SaveFile_RoundTrip_PreservaAsEntradas()
        {
            var file = new SaveFile();
            file.entries.Add(new SaveEntry { Key = "inventory:0", Json = "{\"itemIds\":[\"madeira\"]}" });

            string json = JsonUtility.ToJson(file);
            SaveFile back = JsonUtility.FromJson<SaveFile>(json);

            Assert.AreEqual(1, back.entries.Count);
            Assert.AreEqual("inventory:0", back.entries[0].Key);
            Assert.AreEqual(file.entries[0].Json, back.Find("inventory:0"));
        }

        [Test]
        public void InventoryModel_Clear_EsvaziaOsSlots()
        {
            var madeira = ScriptableObject.CreateInstance<ItemDefinition>();
            madeira.maxStack = 99;
            var inv = new InventoryModel(10);
            inv.Add(madeira, 5);
            Assert.AreEqual(5, inv.CountOf(madeira));

            inv.Clear();

            Assert.AreEqual(0, inv.CountOf(madeira));
            Assert.AreEqual(0, inv.Slots.Count);

            Object.DestroyImmediate(madeira);
        }
    }
}
