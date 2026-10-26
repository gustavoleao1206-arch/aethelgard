namespace JogoDaTrupe.Core.Session
{
    /// <summary>Como iniciar a sessão de rede (M-NET-9 P4).</summary>
    public enum SessionMode { None, Host, Client }

    /// <summary>
    /// Pedido de sessão do MENU para a cena de jogo (M-NET-9 P4). O menu grava o modo
    /// (Hospedar/Entrar) + IP e carrega a cena de co-op; lá, o <c>SessionAutoStart</c>
    /// lê e sobe Host/Client. Estático porque sobrevive à troca de cena — é intenção de
    /// INICIALIZAÇÃO (estado de processo), não estado de jogo. Só primitivos → fica no
    /// Core puro (o menu é UI, o auto-start é Net; ambos dependem só disto).
    /// </summary>
    public static class SessionRequest
    {
        public static SessionMode Mode = SessionMode.None;
        public static string Ip = "127.0.0.1";

        /// <summary>Lê e ZERA o modo (consumido uma vez): reabrir a cena no editor não re-inicia.</summary>
        public static SessionMode Consume()
        {
            SessionMode m = Mode;
            Mode = SessionMode.None;
            return m;
        }
    }
}
