namespace JogoDaTrupe.Net
{
    /// <summary>
    /// Escopo de rede de um evento do <c>EventBus</c> (M-NET-4). Diz se um evento é:
    ///
    /// • <b>Presentation</b> — local de cada cliente; NUNCA trafega pela rede (ex.: abrir
    ///   o inventário, abrir o Altar). Abrir a MINHA UI não abre a do amigo.
    /// • <b>Simulation</b> — decidido no servidor e replicado a todos os clientes (ex.:
    ///   vida mudou, item coletado, habilidade equipada). É o que a camada de escopo relaya.
    /// • <b>Intent</b> — PEDIDO de uma máquina sem autoridade ao servidor (ex.:
    ///   DamageRequested, HarvestRequested). Flui SÓ cliente→servidor (forwarders);
    ///   nunca é relayado de volta — o resultado volta como Simulation/estado replicado.
    /// • <b>Session</b> — global da sessão, sem dono (ex.: salvar/carregar o jogo). Ainda
    ///   NÃO usado — depende da decisão de experiência do Diretor (ver §6 do plano
    ///   <c>PlanoTecnico_EventScope.md</c>): o aviso de "salvou" aparece em todas as telas
    ///   ou só na de quem salvou?
    ///
    /// Este enum é só o CONTRATO (o rótulo). Quem o consome para replicar de fato é o
    /// <c>EventScopeAdapter</c> (P2+), que só entra em cena com a rede ativa.
    /// </summary>
    public enum NetEventScope
    {
        /// <summary>Local de cada cliente; nunca sai da máquina. É o DEFAULT (falha segura).</summary>
        Presentation,

        /// <summary>Decidido no servidor; replicado a todos os clientes.</summary>
        Simulation,

        /// <summary>Global da sessão (sem dono). Reservado — ainda não mapeado.</summary>
        Session,

        /// <summary>Pedido cliente→servidor (M-NET-7 P1). Nunca relayado de volta.</summary>
        Intent
    }
}
