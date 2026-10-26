using JogoDaTrupe.Core.Events;

namespace JogoDaTrupe.Core.Saving
{
    /// <summary>Disparado quando o jogo é salvo (a UI avisa). Só primitivos → fica no Core.</summary>
    public struct GameSaved : IGameEvent
    {
        public bool Ok;
        public string Message; // vazio = mensagem padrão; preenchido (ex.: "só o anfitrião") sobrepõe (M-NET-9 P1)
    }

    /// <summary>Disparado quando o jogo é carregado (Ok=false se não havia save).</summary>
    public struct GameLoaded : IGameEvent
    {
        public bool Ok;
        public string Message;
    }
}
