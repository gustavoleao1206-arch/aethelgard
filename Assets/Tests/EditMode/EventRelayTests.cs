using System;
using NUnit.Framework;
using JogoDaTrupe.Net;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Combat;

namespace JogoDaTrupe.Tests
{
    /// <summary>
    /// Testes da guarda de host do escopo de eventos (M-NET-4 P2). Provam que republicar
    /// um evento de rede NÃO faz laço (a captura do servidor o ignora) e que a flag é
    /// sempre limpa. Usa <c>HealthChanged</c> como cobaia (é struct : IGameEvent).
    /// </summary>
    public class EventRelayTests
    {
        // O EventBus<T> é estático/global — limpa os ouvintes entre testes para não vazar.
        [TearDown]
        public void Cleanup() => EventBus<HealthChanged>.Clear();

        [Test]
        public void Republish_SetsFlagDuring_AndClearsAfter()
        {
            bool duringWasRelaying = false;
            Action<HealthChanged> listener = _ => duringWasRelaying = EventRelay.IsRelaying;
            EventBus<HealthChanged>.Subscribe(listener);

            EventRelay.Republish(new HealthChanged { OwnerId = 1, Current = 5f, Max = 10f });

            Assert.IsTrue(duringWasRelaying, "durante o republish a flag deve estar LIGADA");
            Assert.IsFalse(EventRelay.IsRelaying, "após o republish a flag deve estar DESLIGADA");
        }

        [Test]
        public void ServerCapture_IgnoresRelay_ButCatchesLocalPublish()
        {
            int captured = 0;
            // Simula a captura do servidor: só age em publish LOCAL (não em relay de rede).
            Action<HealthChanged> serverCapture = _ => { if (!EventRelay.IsRelaying) captured++; };
            EventBus<HealthChanged>.Subscribe(serverCapture);

            // (1) Evento vindo da REDE (republicado) → a captura ignora (não reenviaria: sem laço).
            EventRelay.Republish(new HealthChanged { OwnerId = 1, Current = 5f, Max = 10f });
            Assert.AreEqual(0, captured, "relay de rede NÃO deve ser recapturado pelo servidor");

            // (2) Evento LOCAL de simulação (publish normal) → a captura age (relayaria aos clientes).
            EventBus<HealthChanged>.Publish(new HealthChanged { OwnerId = 1, Current = 4f, Max = 10f });
            Assert.AreEqual(1, captured, "publish local DEVE ser capturado");

            EventBus<HealthChanged>.Unsubscribe(serverCapture);
        }

        [Test]
        public void Republish_ClearsFlag_EvenIfListenerThrows()
        {
            Action<HealthChanged> throwing = _ => throw new InvalidOperationException("boom");
            EventBus<HealthChanged>.Subscribe(throwing);

            Assert.Throws<InvalidOperationException>(() =>
                EventRelay.Republish(new HealthChanged { OwnerId = 0, Current = 1f, Max = 2f }));

            Assert.IsFalse(EventRelay.IsRelaying, "a flag deve ser limpa mesmo com exceção (try/finally)");

            EventBus<HealthChanged>.Unsubscribe(throwing);
        }
    }
}
