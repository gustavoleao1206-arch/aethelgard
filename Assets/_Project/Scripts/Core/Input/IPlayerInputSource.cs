using UnityEngine;

namespace JogoDaTrupe.Core.Input
{
    /// <summary>
    /// Fonte de "intenções" do jogador. Quem implementa esta interface diz
    /// O QUE o jogador quer fazer — não executa nada.
    ///
    /// Hoje quem implementa é o teclado/mouse/gamepad (PlayerInputReader). Amanhã,
    /// no multiplayer, uma fonte de REDE pode implementar a MESMA interface, e os
    /// sistemas que leem input (PlayerController, Interactor, câmera) poderão
    /// passar a depender desta abstração em vez do leitor concreto — com o mínimo
    /// de retrabalho.
    /// </summary>
    public interface IPlayerInputSource
    {
        /// <summary>Direção de movimento desejada no plano (x = direita, y = frente). Normalizada.</summary>
        Vector2 MoveAxis { get; }

        /// <summary>Variação do olhar neste frame (mouse/stick direito).</summary>
        Vector2 LookDelta { get; }

        /// <summary>Verdadeiro apenas no frame em que o jogador apertou pular.</summary>
        bool JumpPressedThisFrame { get; }

        /// <summary>Verdadeiro apenas no frame em que o jogador apertou interagir.</summary>
        bool InteractPressedThisFrame { get; }

        /// <summary>Verdadeiro enquanto o jogador SEGURA interagir (E) — usado na captura por sincronização.</summary>
        bool InteractHeld { get; }

        /// <summary>Verdadeiro apenas no frame em que o jogador pediu para alternar a câmera (1ª/3ª pessoa).</summary>
        bool ToggleViewPressedThisFrame { get; }

        /// <summary>Verdadeiro apenas no frame em que o jogador pediu para atacar.</summary>
        bool AttackPressedThisFrame { get; }

        /// <summary>
        /// Slot de habilidade pedido neste frame (0 a 4, teclas 1–5), ou -1 se nenhum.
        /// É uma intenção: quem ativa o efeito é a Simulação (AbilityRunner).
        /// </summary>
        int AbilitySlotPressedThisFrame { get; }

        /// <summary>Verdadeiro apenas no frame em que o jogador pediu abrir/fechar a ficha de atributos (tecla C).</summary>
        bool ToggleCharacterPressedThisFrame { get; }

        /// <summary>Verdadeiro apenas no frame em que o jogador pediu abrir/fechar o inventário (tecla I).</summary>
        bool ToggleInventoryPressedThisFrame { get; }

        /// <summary>Verdadeiro apenas no frame em que o jogador pediu entrar/sair do modo construção (tecla B).</summary>
        bool ToggleBuildPressedThisFrame { get; }

        /// <summary>Verdadeiro apenas no frame em que o jogador pediu alternar a estrutura (tecla Q).</summary>
        bool CycleBuildPressedThisFrame { get; }

        /// <summary>Verdadeiro apenas no frame em que o jogador pediu posicionar a estrutura (tecla F).</summary>
        bool PlaceBuildPressedThisFrame { get; }

        /// <summary>Verdadeiro apenas no frame em que o jogador pediu SALVAR o jogo (F5).</summary>
        bool SavePressedThisFrame { get; }

        /// <summary>Verdadeiro apenas no frame em que o jogador pediu CARREGAR o jogo (F9).</summary>
        bool LoadPressedThisFrame { get; }

        /// <summary>Verdadeiro enquanto o jogador SEGURA correr (sprint — Shift).</summary>
        bool RunHeld { get; }

        /// <summary>Verdadeiro enquanto o jogador SEGURA agachar (Ctrl).</summary>
        bool CrouchHeld { get; }

        /// <summary>Verdadeiro só no frame em que apertou agachar (Ctrl) — dispara o deslize ao correr.</summary>
        bool CrouchPressedThisFrame { get; }
    }
}
