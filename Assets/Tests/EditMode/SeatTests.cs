using System.Collections.Generic;
using NUnit.Framework;
using JogoDaTrupe.Net;

namespace JogoDaTrupe.Tests
{
    /// <summary>
    /// Testa os ASSENTOS lógicos (M-NET-9 P2) — a base do drop-in/drop-out: quem sai
    /// libera o assento; quem entra pega o menor livre; reconectar no mesmo assento é o
    /// que permite retomar a progressão (SaveKeys por assento).
    /// </summary>
    public class SeatTests
    {
        [SetUp]
        public void Setup() => SeatRegistry.Clear();

        [TearDown]
        public void Cleanup() => SeatRegistry.Clear();

        // ---- SeatMath (puro) ----

        [Test]
        public void LowestFree_VazioDaZero_CheioDaMenosUm()
        {
            Assert.AreEqual(0, SeatMath.LowestFree(new HashSet<int>(), 4));
            Assert.AreEqual(2, SeatMath.LowestFree(new HashSet<int> { 0, 1 }, 4));
            Assert.AreEqual(1, SeatMath.LowestFree(new HashSet<int> { 0, 2, 3 }, 4), "acha o BURACO no meio");
            Assert.AreEqual(-1, SeatMath.LowestFree(new HashSet<int> { 0, 1, 2, 3 }, 4), "lotado");
        }

        // ---- SeatRegistry ----

        [Test]
        public void Claim_DaOMenorLivre_EEhIdempotente()
        {
            Assert.AreEqual(0, SeatRegistry.Claim(100ul, 4)); // host (clientId 100) → assento 0
            Assert.AreEqual(1, SeatRegistry.Claim(101ul, 4)); // amigo → assento 1
            Assert.AreEqual(1, SeatRegistry.Claim(101ul, 4), "pedir de novo devolve o mesmo assento");
            Assert.AreEqual(1, SeatRegistry.SeatOf(101ul));
        }

        [Test]
        public void Lotacao_QuintoJogador_ERecusado()
        {
            SeatRegistry.Claim(1ul, 4);
            SeatRegistry.Claim(2ul, 4);
            SeatRegistry.Claim(3ul, 4);
            SeatRegistry.Claim(4ul, 4);
            Assert.AreEqual(-1, SeatRegistry.Claim(5ul, 4), "o 5º não tem assento (limite de 4)");
        }

        [Test]
        public void DropInDropOut_ReconectarReusaOAssentoLiberado()
        {
            SeatRegistry.Claim(100ul, 4); // host → 0
            int amigoSeat = SeatRegistry.Claim(101ul, 4); // amigo → 1
            Assert.AreEqual(1, amigoSeat);

            SeatRegistry.Release(101ul); // amigo desconecta → assento 1 livre
            Assert.AreEqual(-1, SeatRegistry.SeatOf(101ul));

            // Reconecta com um clientId NOVO (o NGO nunca reusa id) → mas pega o assento 1
            // de volta (o menor livre) → suas SaveKeys "…​:1" batem → progressão retomável.
            int reconnectSeat = SeatRegistry.Claim(202ul, 4);
            Assert.AreEqual(1, reconnectSeat, "o assento liberado é reusado — a essência do drop-in");
            Assert.IsTrue(SeatRegistry.TryClientIdOf(1, out ulong cid) && cid == 202ul);
        }

        [Test]
        public void ClientIdOf_SoResolveAssentoOcupado()
        {
            SeatRegistry.Claim(100ul, 4); // assento 0
            Assert.IsTrue(SeatRegistry.TryClientIdOf(0, out ulong c) && c == 100ul);
            Assert.IsFalse(SeatRegistry.TryClientIdOf(3, out _), "assento vazio não resolve");
        }

        // ---- Dono de um pedido vindo da rede (correção: ownerId confiado do payload) ----

        [Test]
        public void TryResolveSender_DaOAssentoDaConexao_NaoOutro()
        {
            SeatRegistry.Claim(100ul, 4); // host → assento 0
            SeatRegistry.Claim(101ul, 4); // amigo → assento 1

            // O pedido do amigo resolve SEMPRE para o assento dele, seja qual for o id
            // que um cliente alterado escrevesse no payload (o payload nem leva mais o id).
            Assert.IsTrue(SeatRegistry.TryResolveSender(101ul, out int seat));
            Assert.AreEqual(1, seat, "o dono do pedido é quem enviou, não o host (assento 0)");
        }

        [Test]
        public void TryResolveSender_RemetenteSemAssento_EhRecusado()
        {
            SeatRegistry.Claim(100ul, 4); // host → assento 0

            Assert.IsFalse(SeatRegistry.TryResolveSender(999ul, out int desconhecido), "cliente nunca aprovado");
            Assert.AreEqual(-1, desconhecido);

            SeatRegistry.Claim(101ul, 4);
            SeatRegistry.Release(101ul); // saiu: pedido atrasado dele não pode agir pelo assento livre
            Assert.IsFalse(SeatRegistry.TryResolveSender(101ul, out _), "cliente que já saiu");
        }
    }
}
