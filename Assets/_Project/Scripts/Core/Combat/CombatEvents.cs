using JogoDaTrupe.Core.Events;

namespace JogoDaTrupe.Core.Combat
{
    /// <summary>
    /// Disparado sempre que a vida de uma entidade muda. Carrega o dono (OwnerId)
    /// para que, por exemplo, o HUD mostre só a vida do jogador local — e, no
    /// multiplayer, a vida do jogador certo.
    /// </summary>
    public struct HealthChanged : IGameEvent
    {
        public int OwnerId;
        public float Current;
        public float Max;
    }

    /// <summary>
    /// Disparado quando uma entidade morre (vida chega a zero).
    /// </summary>
    public struct EntityDied : IGameEvent
    {
        public int OwnerId;
    }

    /// <summary>
    /// Disparado pela AUTORIDADE quando um jogador renasce (M-NET-9 P0), depois do
    /// tempo de respawn. Relayado servidor→clientes: cada máquina que CONTROLA aquele
    /// jogador (o dono) se teleporta ao próprio ponto de spawn e re-liga o controle;
    /// as demais ignoram (a posição chega pelo NetworkTransform). O ponto de spawn é
    /// derivado do <see cref="OwnerId"/> (MapLayout), então não viaja no payload.
    /// </summary>
    public struct PlayerRespawned : IGameEvent
    {
        public int OwnerId;
    }

    /// <summary>
    /// Disparado quando um jogador executa um ataque. A IA dos inimigos escuta
    /// isso para medir o ritmo do jogador (e adaptar a postura) e para, às vezes,
    /// imitar/contra-atacar. Carrega o dono (qual jogador atacou).
    /// </summary>
    public struct PlayerAttacked : IGameEvent
    {
        public int OwnerId;
    }

    /// <summary>
    /// INTENÇÃO de dano publicada por uma máquina SEM autoridade (cliente no co-op,
    /// M-NET-6 P4). Em vez de mutar a cópia local, o <c>Health</c> do alvo publica
    /// este pedido; o encaminhador de rede do alvo (<c>NetworkDamageForwarder</c>)
    /// o filtra pelo <see cref="TargetGuid"/> (casamento LOCAL — a identidade cruza
    /// a rede pelo próprio NetworkObject) e envia ao servidor, que aplica o dano de
    /// verdade. Padrão Input → Intenção → Ação (Multiplayer.md #4). Só primitivos.
    /// </summary>
    public struct DamageRequested : IGameEvent
    {
        public string TargetGuid;   // EntityGuid do alvo (na máquina que pediu)
        public float Amount;
        public int AttackerOwnerId; // quem bateu (contribuinte de Eco)
    }
}
