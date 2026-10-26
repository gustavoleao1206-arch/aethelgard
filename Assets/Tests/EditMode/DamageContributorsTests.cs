using System.Linq;
using NUnit.Framework;
using JogoDaTrupe.Gameplay.Combat;
using JogoDaTrupe.Core.Ownership;

namespace JogoDaTrupe.Tests
{
    /// <summary>
    /// Testes da regra pura de contribuintes (M-NET-5 P1): quem causou dano vira dono de
    /// Eco, dano de IA/ambiente (NoOwner) não, e cada jogador conta uma vez.
    /// </summary>
    public class DamageContributorsTests
    {
        [Test]
        public void Register_AdicionaDonosDistintos()
        {
            var c = new DamageContributors();
            c.Register(0);
            c.Register(1);

            Assert.AreEqual(2, c.Count);
            Assert.IsTrue(c.Ids.Contains(0));
            Assert.IsTrue(c.Ids.Contains(1));
        }

        [Test]
        public void Register_DeduplicaMesmoDono()
        {
            var c = new DamageContributors();
            c.Register(0); c.Register(0); c.Register(0);

            Assert.AreEqual(1, c.Count); // quem bateu 3x conta 1x
        }

        [Test]
        public void Register_IgnoraNoOwner()
        {
            var c = new DamageContributors();
            c.Register(Ownable.NoOwner); // dano de IA/ambiente

            Assert.AreEqual(0, c.Count);
        }

        [Test]
        public void Register_DoisJogadores_DoisContribuintes_IgnorandoIA()
        {
            var c = new DamageContributors();
            c.Register(0);               // player local
            c.Register(1);               // cliente-2
            c.Register(Ownable.NoOwner); // o inimigo também bateu → ignorado

            Assert.AreEqual(2, c.Count);
        }

        [Test]
        public void Clear_Esvazia()
        {
            var c = new DamageContributors();
            c.Register(0); c.Register(1);
            c.Clear();

            Assert.AreEqual(0, c.Count);
        }
    }
}
