using NUnit.Framework;
using JogoDaTrupe.Core.Events;

namespace JogoDaTrupe.Tests.Core
{
    /// <summary>
    /// Testes da fundação de eventos. Provam que o canal entrega eventos a quem
    /// se inscreveu e para de entregar a quem se desinscreveu.
    /// São testes de lógica pura (EditMode): rodam em milissegundos.
    /// </summary>
    public class EventBusTests
    {
        // Evento mínimo só para os testes. Segue a convenção do projeto de
        // carregar o dono (OwnerId), além de um valor qualquer.
        private struct DummyEvent : IGameEvent
        {
            public int OwnerId;
            public int Value;
        }

        // Garante que o canal começa e termina limpo em cada teste,
        // já que o EventBus é estático (estado global).
        [SetUp]
        public void Setup() => EventBus<DummyEvent>.Clear();

        [TearDown]
        public void Teardown() => EventBus<DummyEvent>.Clear();

        [Test]
        public void Publish_EntregaEventoParaInscrito()
        {
            int recebido = 0;
            EventBus<DummyEvent>.Subscribe(e => recebido = e.Value);

            EventBus<DummyEvent>.Publish(new DummyEvent { OwnerId = 0, Value = 42 });

            Assert.AreEqual(42, recebido, "O inscrito deveria receber o valor publicado.");
        }

        [Test]
        public void Unsubscribe_ParaDeReceberEventos()
        {
            int chamadas = 0;
            void Handler(DummyEvent e) => chamadas++;

            EventBus<DummyEvent>.Subscribe(Handler);
            EventBus<DummyEvent>.Publish(new DummyEvent());

            EventBus<DummyEvent>.Unsubscribe(Handler);
            EventBus<DummyEvent>.Publish(new DummyEvent());

            Assert.AreEqual(1, chamadas, "Após desinscrever, não deveria mais receber eventos.");
        }
    }
}
