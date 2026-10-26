using NUnit.Framework;
using JogoDaTrupe.Core.Ownership;

namespace JogoDaTrupe.Tests
{
    /// <summary>
    /// Testes do seam de autoridade da simulação (M-NET-6 P0). O contrato importante é
    /// o DEFAULT: em solo (ninguém escreve) a máquina É a autoridade — IA e EcoDropper
    /// funcionam exatamente como sempre. A rede liga/desliga e o shutdown restaura.
    /// </summary>
    public class SimulationAuthorityTests
    {
        // O seam é estático/global — restaura os defaults entre testes para não vazar.
        [TearDown]
        public void Cleanup()
        {
            SimulationAuthority.IsAuthority = true;
            SimulationAuthority.IsNetworked = false;
        }

        [Test]
        public void Default_EhAutoridade_Solo()
        {
            Assert.IsTrue(SimulationAuthority.IsAuthority,
                "em solo (ninguém escreveu) a máquina deve ser a autoridade — senão a IA morre no singleplayer");
        }

        [Test]
        public void Default_NaoEstaEmRede_Solo()
        {
            Assert.IsFalse(SimulationAuthority.IsNetworked,
                "em solo (ninguém escreveu) não há sessão de rede — a construção deve criar o cubo local, " +
                "não publicar BuildRequested no vazio (M-NET-7 P5)");
        }

        [Test]
        public void Rede_ClienteDesliga_ShutdownRestaura()
        {
            // Cliente conecta: a camada Net desliga a autoridade desta máquina.
            SimulationAuthority.IsAuthority = false;
            Assert.IsFalse(SimulationAuthority.IsAuthority);

            // Shutdown da sessão: a Net restaura o default (voltar ao solo é seguro).
            SimulationAuthority.IsAuthority = true;
            Assert.IsTrue(SimulationAuthority.IsAuthority);
        }
    }
}
