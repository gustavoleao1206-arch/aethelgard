namespace JogoDaTrupe.Core.Ownership
{
    /// <summary>
    /// Diz se ESTA máquina é a autoridade da simulação de mundo (M-NET-6 P0).
    ///
    /// • <b>Solo:</b> permanece <c>true</c> — esta máquina é o mundo inteiro; nada muda.
    /// • <b>Rede:</b> a camada Net grava no início da sessão: host/servidor → <c>true</c>,
    ///   cliente → <c>false</c> (e volta a <c>true</c> no shutdown). Quem consulta:
    ///   a IA (EnemyController/BossController) e o EcoDropper — no cliente eles não
    ///   simulam; as réplicas dos inimigos são movidas pelo NetworkTransform e a vida
    ///   vem replicada (senão o cliente duplicaria movimento, morte e Eco).
    ///
    /// É o mesmo padrão dos seams <c>GameTime.Provider</c> e <c>LocalPlayer.Id</c>:
    /// default solo-correto no Core puro; a rede troca o valor sem tocar os consumidores.
    /// É estado de PROCESSO (um valor por máquina), não por-entidade — estático sem ferir
    /// o princípio multiplayer #6 ("quem manda nesta tela?", não estado de jogador).
    /// </summary>
    public static class SimulationAuthority
    {
        /// <summary>Verdadeiro se esta máquina decide a simulação de mundo. Default: solo.</summary>
        public static bool IsAuthority = true;

        /// <summary>
        /// Verdadeiro quando há uma SESSÃO DE REDE ativa (host OU cliente). Default: solo
        /// (<c>false</c>). O <see cref="JogoDaTrupe.Net.NetAuthorityBinder"/> liga no início
        /// da sessão e desliga no shutdown, junto de <see cref="IsAuthority"/>.
        ///
        /// Uso: distinguir "criar algo local" (solo) de "pedir ao servidor que spawne um
        /// objeto de rede" (co-op) — necessário para a CONSTRUÇÃO (M-NET-7 P5), onde até o
        /// HOST (que É autoridade) precisa spawnar um prefab de rede para a estrutura
        /// aparecer nas duas telas; um cubo local só o host veria. Em solo (<c>false</c>) a
        /// construção segue criando o primitivo local como sempre (regressão-zero).
        /// </summary>
        public static bool IsNetworked = false;
    }
}
