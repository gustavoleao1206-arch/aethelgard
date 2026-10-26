using NUnit.Framework;
using JogoDaTrupe.Gameplay.Inventory;

namespace JogoDaTrupe.Tests.Gameplay
{
    /// <summary>
    /// Testa a lógica pura do estoque de um nó de recurso (EditMode): limitar a
    /// colheita ao que resta, esgotar sem ficar negativo e ignorar pedidos inválidos.
    /// Essa lógica é reusada pela coleta manual (ResourceNode.Interact) e pelo Coletor
    /// Automático (AutoCollector.HarvestOnce). Ver ResourceStock.
    /// </summary>
    public class ResourceStockTests
    {
        [Test]
        public void Available_LimitaPeloQueResta()
        {
            var stock = new ResourceStock(3);
            Assert.AreEqual(3, stock.Available(5)); // pediu 5, só há 3
            Assert.AreEqual(2, stock.Available(2)); // pediu 2, há 3 → 2
        }

        [Test]
        public void Available_PedidoInvalidoOuEsgotado_RetornaZero()
        {
            var stock = new ResourceStock(9);
            Assert.AreEqual(0, stock.Available(0));
            Assert.AreEqual(0, stock.Available(-4));

            var vazio = new ResourceStock(0);
            Assert.AreEqual(0, vazio.Available(3));
            Assert.IsTrue(vazio.IsDepleted);
        }

        [Test]
        public void Take_RemoveERetornaQuantoSaiu()
        {
            var stock = new ResourceStock(9);
            int saiu = stock.Take(3);

            Assert.AreEqual(3, saiu);
            Assert.AreEqual(6, stock.Remaining);
            Assert.IsFalse(stock.IsDepleted);
        }

        [Test]
        public void Take_AlemDoEstoque_EsgotaSemFicarNegativo()
        {
            var stock = new ResourceStock(9);
            int saiu = stock.Take(100); // pediu mais do que há

            Assert.AreEqual(9, saiu);            // só saiu o que existia
            Assert.AreEqual(0, stock.Remaining); // nunca negativo
            Assert.IsTrue(stock.IsDepleted);
            Assert.AreEqual(0, stock.Available(1)); // esgotado não entrega mais
        }

        [Test]
        public void Take_ZeroOuNegativo_NaoFazNada()
        {
            var stock = new ResourceStock(5);
            Assert.AreEqual(0, stock.Take(0));
            Assert.AreEqual(0, stock.Take(-2));
            Assert.AreEqual(5, stock.Remaining); // intacto
        }

        [Test]
        public void Take_Parcial_AvailableRefleteORestante()
        {
            var stock = new ResourceStock(10);
            stock.Take(4);

            Assert.AreEqual(6, stock.Remaining);
            Assert.AreEqual(6, stock.Available(99)); // limita pelo que sobrou
        }

        [Test]
        public void Construtor_TotalNegativo_ClampaParaZero()
        {
            var stock = new ResourceStock(-7);

            Assert.AreEqual(0, stock.Remaining);
            Assert.IsTrue(stock.IsDepleted);
        }

        // ---- M-NET-7 P3: espelho do estoque vindo da rede ----

        [Test]
        public void SyncTo_AjustaRestanteParaOValorAutoritativo()
        {
            var stock = new ResourceStock(9);
            stock.SyncTo(4); // servidor diz que restam 4 (outro jogador colheu)

            Assert.AreEqual(4, stock.Remaining);
            Assert.IsFalse(stock.IsDepleted);
        }

        [Test]
        public void SyncTo_Zero_Esgota()
        {
            var stock = new ResourceStock(9);
            stock.SyncTo(0);

            Assert.AreEqual(0, stock.Remaining);
            Assert.IsTrue(stock.IsDepleted);
        }

        [Test]
        public void SyncTo_Negativo_ClampaParaZero()
        {
            var stock = new ResourceStock(9);
            stock.SyncTo(-3);

            Assert.AreEqual(0, stock.Remaining); // nunca negativo
            Assert.IsTrue(stock.IsDepleted);
        }
    }
}
