using UnityEngine;

namespace JogoDaTrupe.Gameplay.Interaction
{
    /// <summary>
    /// Implementado por qualquer objeto do mundo com que o jogador possa interagir
    /// (NPC, baú, recurso, alavanca...). Mantém a interação desacoplada: o
    /// Interactor não precisa conhecer o tipo concreto, só este contrato.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Texto curto do que acontece ao interagir (ex.: "Abrir", "Coletar").</summary>
        string Prompt { get; }

        /// <summary>Executa a interação. Recebe quem interagiu (o jogador).</summary>
        void Interact(GameObject interactor);
    }
}
