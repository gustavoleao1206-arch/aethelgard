using System;
using JogoDaTrupe.Core.Events;

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// Mecanismo de "republicação de rede" + guarda de reentrância (M-NET-4 P2).
    ///
    /// É a <b>Defesa B</b> contra a DUPLICAÇÃO de eventos no host. O <c>EventBus</c> é
    /// estático e global (<c>Core/Events/EventBus.cs</c>): no host (servidor+cliente no
    /// mesmo processo) um evento de simulação atinge todos os ouvintes de uma vez. Quando o
    /// adaptador de rede (<c>EventScopeAdapter</c>, P4) receber um evento pela rede e o
    /// <see cref="Republish{T}"/> localmente, a CAPTURA DO SERVIDOR (que assina o mesmo bus)
    /// não pode recapturá-lo e reenviá-lo — senão vira um laço infinito. Por isso todo
    /// republish liga <see cref="IsRelaying"/>, e a captura do servidor ignora o que chega
    /// com a flag ligada (<c>if (EventRelay.IsRelaying) return;</c>).
    ///
    /// É PURO (sem Netcode): só <c>EventBus</c> + uma flag. Assim a guarda é testável em
    /// EditMode sem <c>NetworkManager</c>. O <c>EventScopeAdapter</c> (NetworkBehaviour, P4)
    /// consome isto nos seus RPCs e capturas. Ver <c>05_Sistemas/PlanoTecnico_EventScope.md</c> §3.
    /// </summary>
    public static class EventRelay
    {
        // Por-thread: o republish e a eventual recaptura acontecem na MESMA thread (a main
        // do Unity). ThreadStatic evita que uma thread de fundo veja/afete a flag por engano.
        [ThreadStatic] private static bool _relaying;

        /// <summary>Verdadeiro enquanto um evento vindo da REDE está sendo republicado localmente.</summary>
        public static bool IsRelaying => _relaying;

        /// <summary>
        /// Republica localmente um evento que veio da rede, marcando-o como "relay" para a
        /// captura do servidor ignorá-lo (quebra o laço). A flag é sempre limpa no fim —
        /// inclusive se um ouvinte lançar exceção (try/finally).
        /// </summary>
        public static void Republish<T>(T gameEvent) where T : struct, IGameEvent
        {
            _relaying = true;
            try
            {
                EventBus<T>.Publish(gameEvent);
            }
            finally
            {
                _relaying = false;
            }
        }
    }
}
