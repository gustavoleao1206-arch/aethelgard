namespace JogoDaTrupe.Core.Timing
{
    /// <summary>
    /// Provedor do TEMPO DE SIMULAÇÃO. Hoje (solo) reflete o relógio da Unity; na
    /// camada de rede será trocado pelo tempo do servidor (NetworkManager.ServerTime),
    /// para que cooldowns, durações de efeito (root), canal de captura e ciclos de
    /// automação fiquem IGUAIS em todas as máquinas. (Princípio multiplayer #8.)
    /// </summary>
    public interface ITimeProvider
    {
        /// <summary>Tempo de simulação acumulado, em segundos.</summary>
        float Time { get; }

        /// <summary>Passo do frame/tick atual, em segundos.</summary>
        float DeltaTime { get; }
    }

    /// <summary>Implementação solo: encapsula o relógio da Unity (<c>UnityEngine.Time</c>).</summary>
    public sealed class SystemTimeProvider : ITimeProvider
    {
        public float Time => UnityEngine.Time.time;
        public float DeltaTime => UnityEngine.Time.deltaTime;
    }

    /// <summary>
    /// Ponto ÚNICO de acesso ao tempo de simulação. A simulação lê <c>GameTime.Time</c>/
    /// <c>GameTime.DeltaTime</c> em vez de <c>UnityEngine.Time</c>, para que a camada de
    /// rede troque UMA referência (o <see cref="Provider"/>) e todo o jogo passe a usar o
    /// tempo do servidor — sem mexer em quem consome.
    ///
    /// NÃO guarda estado por-jogador (é tempo global da simulação) — por isso não fere o
    /// princípio multiplayer #6 ("evitar singletons com estado por-jogador"); é justamente
    /// a centralização que o princípio #8 pede.
    /// </summary>
    public static class GameTime
    {
        /// <summary>Provedor ativo. Solo = <see cref="SystemTimeProvider"/>; rede troca por um baseado no servidor.</summary>
        public static ITimeProvider Provider { get; set; } = new SystemTimeProvider();

        public static float Time => Provider.Time;
        public static float DeltaTime => Provider.DeltaTime;
    }
}
