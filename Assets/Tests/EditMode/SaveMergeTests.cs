using System.Collections.Generic;
using NUnit.Framework;
using JogoDaTrupe.Core.Saving;

namespace JogoDaTrupe.Tests
{
    /// <summary>
    /// Testa a fusão de saves (M-NET-9 P1): o host salva quem está conectado, mas a
    /// progressão de um assento OFFLINE (chave antiga ausente nas frescas) deve
    /// sobreviver — senão salvar apagaria o progresso de um amigo que se ausentou.
    /// </summary>
    public class SaveMergeTests
    {
        private static SaveEntry E(string key, string json) => new SaveEntry { Key = key, Json = json };

        private static string Find(SaveFile f, string key) => f.Find(key);

        [Test]
        public void Frescas_SobrescrevemPorChave()
        {
            var existing = new SaveFile();
            existing.entries.Add(E("inventory:0", "old0"));

            SaveFile merged = SaveMerge.Merge(existing, new List<SaveEntry> { E("inventory:0", "new0") });

            Assert.AreEqual("new0", Find(merged, "inventory:0"), "a entrada fresca sobrescreve a antiga");
            Assert.AreEqual(1, merged.entries.Count);
        }

        [Test]
        public void ChaveOffline_AusenteNasFrescas_EhPreservada()
        {
            var existing = new SaveFile();
            existing.entries.Add(E("memory:0", "host"));
            existing.entries.Add(E("memory:1", "amigo_que_saiu")); // assento 1 offline agora

            // O host salva só o assento 0 (o amigo desconectou).
            SaveFile merged = SaveMerge.Merge(existing, new List<SaveEntry> { E("memory:0", "host_novo") });

            Assert.AreEqual("host_novo", Find(merged, "memory:0"), "o online é atualizado");
            Assert.AreEqual("amigo_que_saiu", Find(merged, "memory:1"), "o assento offline SOBREVIVE (não apaga o amigo)");
            Assert.AreEqual(2, merged.entries.Count);
        }

        [Test]
        public void SemArquivoAntigo_UsaSoAsFrescas()
        {
            SaveFile merged = SaveMerge.Merge(null, new List<SaveEntry> { E("attributes:0", "a") });
            Assert.AreEqual("a", Find(merged, "attributes:0"));
            Assert.AreEqual(1, merged.entries.Count);
        }

        [Test]
        public void FrescasDuplicadas_EEntradasNulas_NaoQuebram()
        {
            var existing = new SaveFile();
            existing.entries.Add(null);                 // entrada nula defensiva
            existing.entries.Add(E("", "sem_chave"));   // chave vazia ignorada
            existing.entries.Add(E("loadout:0", "old"));

            var fresh = new List<SaveEntry>
            {
                E("loadout:0", "primeira"),
                E("loadout:0", "segunda"), // duplicata: a 1ª vence
                null
            };

            SaveFile merged = SaveMerge.Merge(existing, fresh);
            Assert.AreEqual("primeira", Find(merged, "loadout:0"), "dedup: a primeira fresca vence");
            Assert.AreEqual(1, merged.entries.Count, "nulas e chave-vazia não entram");
        }
    }
}
