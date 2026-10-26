using NUnit.Framework;
using JogoDaTrupe.Core.Session;

namespace JogoDaTrupe.Tests
{
    /// <summary>
    /// Testa o pedido de sessão do menu (M-NET-9 P4): consumir lê e ZERA o modo, para
    /// reabrir a cena no editor não re-iniciar host/client por acidente.
    /// </summary>
    public class SessionRequestTests
    {
        [TearDown]
        public void Cleanup() { SessionRequest.Mode = SessionMode.None; SessionRequest.Ip = "127.0.0.1"; }

        [Test]
        public void Consume_LeEZeraOModo()
        {
            SessionRequest.Mode = SessionMode.Host;
            Assert.AreEqual(SessionMode.Host, SessionRequest.Consume());
            Assert.AreEqual(SessionMode.None, SessionRequest.Mode, "consumido uma vez — não re-inicia ao reabrir a cena");
            Assert.AreEqual(SessionMode.None, SessionRequest.Consume(), "segunda leitura já é None");
        }

        [Test]
        public void Default_EhNone()
        {
            Assert.AreEqual(SessionMode.None, SessionRequest.Mode, "sem menu (MPPM/editor) o modo é None — deixa os atalhos H/J agirem");
        }
    }
}
