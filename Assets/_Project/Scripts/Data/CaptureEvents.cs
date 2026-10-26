using JogoDaTrupe.Core.Events;

namespace JogoDaTrupe.Data
{
    // Evento de captura. Carrega uma referência de Data (EnemyDefinition), por isso
    // vive aqui (mesma razão dos eventos de Incorporação/Inventário): o Core não pode
    // depender de Data. Carrega o OwnerId (multiplayer-ready).

    /// <summary>
    /// Disparado quando o jogador captura (sincroniza) uma criatura capturável.
    /// A captura também publica um <see cref="EcoCollected"/> (a habilidade entra na
    /// Memória, igual a derrotar uma criatura); este evento é o gancho de FEEDBACK
    /// ("Sincronizado!"), para a UI avisar sem acoplar à simulação.
    /// </summary>
    public struct CreatureCaptured : IGameEvent
    {
        public int OwnerId;
        public EnemyDefinition Source; // a criatura capturada (tem a habilidade concedida)
    }
}
