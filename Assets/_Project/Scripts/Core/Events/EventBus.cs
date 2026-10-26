using System;

namespace JogoDaTrupe.Core.Events
{
    /// <summary>
    /// Canal de eventos tipado do projeto. Permite que sistemas conversem sem
    /// se conhecerem: um sistema publica um evento, outros escutam e reagem.
    ///
    /// Uso:
    ///   EventBus&lt;PlayerDamaged&gt;.Subscribe(AoLevarDano);
    ///   EventBus&lt;PlayerDamaged&gt;.Publish(new PlayerDamaged { OwnerId = 0, Amount = 10 });
    ///   EventBus&lt;PlayerDamaged&gt;.Unsubscribe(AoLevarDano);
    ///
    /// Nota de arquitetura: por enquanto é estático (global), o que é simples e
    /// suficiente para o Vertical Slice solo. Como cada evento carrega o dono
    /// (ver IGameEvent), a evolução para multiplayer continua viável. Se um dia
    /// precisarmos de instâncias separadas por sessão de rede, trocamos só esta
    /// classe, sem mexer em quem publica/escuta.
    /// </summary>
    public static class EventBus<T> where T : IGameEvent
    {
        private static Action<T> _listeners;

        /// <summary>Registra um ouvinte para este tipo de evento.</summary>
        public static void Subscribe(Action<T> listener) => _listeners += listener;

        /// <summary>Remove um ouvinte previamente registrado.</summary>
        public static void Unsubscribe(Action<T> listener) => _listeners -= listener;

        /// <summary>Dispara o evento para todos os ouvintes registrados.</summary>
        public static void Publish(T gameEvent) => _listeners?.Invoke(gameEvent);

        /// <summary>Remove todos os ouvintes. Útil para limpeza de cena e testes.</summary>
        public static void Clear() => _listeners = null;
    }
}
