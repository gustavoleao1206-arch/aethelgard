using JogoDaTrupe.Core.Ownership;

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// Regras PURAS de relay do <c>EventScopeAdapter</c> (testáveis em EditMode,
    /// no espírito do <c>EventScopeRegistry</c>/<c>EventRelay</c>).
    /// </summary>
    public static class NetRelayRules
    {
        /// <summary>
        /// A vida de INIMIGO (sem dono, <see cref="Ownable.NoOwner"/>) NÃO é relayada
        /// por RPC: desde o M-NET-6 P3 ela viaja por <c>NetworkVariable</c>
        /// (<c>NetworkHealth</c>) — relayar também duplicaria a mudança no cliente.
        /// A vida de PLAYER (com dono) segue no relay.
        /// </summary>
        public static bool ShouldRelayHealthChanged(int ownerId) => ownerId != Ownable.NoOwner;
    }
}
