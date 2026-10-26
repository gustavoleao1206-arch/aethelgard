using UnityEngine;

namespace JogoDaTrupe.Core.Ownership
{
    /// <summary>
    /// Marca qual jogador é o "dono" lógico desta entidade.
    ///
    /// Jogando solo, o dono é sempre o jogador local (id 0). Quando a camada
    /// de rede entrar, este id passará a indicar qual jogador controla a
    /// entidade — sem precisarmos reescrever a lógica de jogo, porque ela já
    /// raciocina em termos de "dono", e não de "o único jogador".
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Ownable : MonoBehaviour
    {
        /// <summary>Id usado para o jogador local enquanto o jogo é singleplayer.</summary>
        public const int LocalPlayerId = 0;

        /// <summary>Id para entidades que não pertencem a nenhum jogador (inimigos, objetos do mundo).</summary>
        public const int NoOwner = -1;

        [SerializeField] private int _ownerId = LocalPlayerId;

        /// <summary>Id do jogador dono desta entidade.</summary>
        public int OwnerId => _ownerId;

        /// <summary>Verdadeiro se esta entidade pertence ao jogador local.</summary>
        public bool IsOwnedByLocalPlayer => _ownerId == LocalPlayerId;

        /// <summary>Define o dono. Será usado pela camada de rede no futuro.</summary>
        public void SetOwner(int ownerId) => _ownerId = ownerId;
    }
}
