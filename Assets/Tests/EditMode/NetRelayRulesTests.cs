using NUnit.Framework;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Net;

namespace JogoDaTrupe.Tests
{
    /// <summary>
    /// Testes das regras puras de relay (M-NET-6 P3). A regra é carregadora de peso:
    /// se quebrar, a vida do inimigo chega 2× no cliente (RPC + NetworkVariable).
    /// </summary>
    public class NetRelayRulesTests
    {
        [Test]
        public void VidaDeInimigo_SemDono_NaoRelaya()
        {
            // Inimigos não têm dono: a vida deles viaja por NetworkVariable (NetworkHealth).
            Assert.IsFalse(NetRelayRules.ShouldRelayHealthChanged(Ownable.NoOwner));
        }

        [Test]
        public void VidaDePlayer_ComDono_Relaya()
        {
            Assert.IsTrue(NetRelayRules.ShouldRelayHealthChanged(Ownable.LocalPlayerId)); // host (0)
            Assert.IsTrue(NetRelayRules.ShouldRelayHealthChanged(2));                     // cliente-2
        }
    }
}
