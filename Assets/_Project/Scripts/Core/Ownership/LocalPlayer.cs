namespace JogoDaTrupe.Core.Ownership
{
    /// <summary>
    /// Id do jogador LOCAL desta máquina, em runtime. A "lente local" pela qual as HUDs
    /// filtram os eventos do PRÓPRIO jogador (e não os do amigo).
    ///
    /// • <b>Solo:</b> permanece <see cref="Ownable.LocalPlayerId"/> (0) — nada muda.
    /// • <b>Rede:</b> ao nascer o player do dono, o NetworkPlayerSetup grava aqui o
    ///   <c>OwnerClientId</c> desta máquina. Assim, no cliente-2 (OwnerId 1), as HUDs
    ///   comparam contra 1 e reagem aos próprios eventos (sem isto, ficariam mudas).
    ///
    /// É estado de PROCESSO (um valor por máquina), não por-entidade — por isso é estático
    /// e não fere o princípio multiplayer #6 (que proíbe singletons com estado por-jogador
    /// da simulação): aqui é só "quem sou eu nesta tela".
    /// </summary>
    public static class LocalPlayer
    {
        /// <summary>Id do jogador local. 0 em solo; definido pela rede no spawn do dono.</summary>
        public static int Id = Ownable.LocalPlayerId;
    }
}
