using UnityEngine;
using UnityEngine.InputSystem;
using JogoDaTrupe.Core.Input;

namespace JogoDaTrupe.Input
{
    /// <summary>
    /// Lê os dispositivos do jogador (teclado, mouse, gamepad) e os traduz em
    /// "intenções" simples: para onde quer ir, para onde quer olhar, se quer
    /// pular, se quer interagir.
    ///
    /// Arquitetura: este componente NÃO executa ações — ele só informa a vontade
    /// do jogador. Quem decide e aplica é a Simulação (PlayerController, Interactor).
    /// É isso que permitirá, no multiplayer, enviar a intenção pela rede sem
    /// reescrever a lógica de jogo.
    ///
    /// Por enquanto os controles são fixos (WASD/espaço/E). Remapeamento de teclas
    /// virá num objetivo futuro, usando os Input Actions.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour, IPlayerInputSource
    {
        /// <summary>Direção de movimento desejada no plano (x = direita, y = frente). Normalizada.</summary>
        public Vector2 MoveAxis => ReadMove();

        /// <summary>Variação do olhar neste frame (mouse/stick direito).</summary>
        public Vector2 LookDelta => ReadLook();

        /// <summary>Verdadeiro apenas no frame em que o jogador apertou pular.</summary>
        public bool JumpPressedThisFrame =>
            (Keyboard.current?.spaceKey.wasPressedThisFrame ?? false) ||
            (Gamepad.current?.buttonSouth.wasPressedThisFrame ?? false);

        /// <summary>Verdadeiro apenas no frame em que o jogador apertou interagir.</summary>
        public bool InteractPressedThisFrame =>
            (Keyboard.current?.eKey.wasPressedThisFrame ?? false) ||
            (Gamepad.current?.buttonWest.wasPressedThisFrame ?? false);

        /// <summary>Verdadeiro enquanto o jogador SEGURA interagir (E) — captura por sincronização.</summary>
        public bool InteractHeld =>
            (Keyboard.current?.eKey.isPressed ?? false) ||
            (Gamepad.current?.buttonWest.isPressed ?? false);

        /// <summary>Verdadeiro apenas no frame em que o jogador apertou alternar a câmera (tecla V ou botão Norte do gamepad).</summary>
        public bool ToggleViewPressedThisFrame =>
            (Keyboard.current?.vKey.wasPressedThisFrame ?? false) ||
            (Gamepad.current?.buttonNorth.wasPressedThisFrame ?? false);

        /// <summary>Verdadeiro apenas no frame em que o jogador apertou atacar (botão esquerdo do mouse ou gatilho direito do gamepad).</summary>
        public bool AttackPressedThisFrame =>
            (Mouse.current?.leftButton.wasPressedThisFrame ?? false) ||
            (Gamepad.current?.rightTrigger.wasPressedThisFrame ?? false);

        /// <summary>Slot de habilidade pedido neste frame (teclas 1–5 → 0..4), ou -1 se nenhum.</summary>
        public int AbilitySlotPressedThisFrame => ReadAbilitySlot();

        /// <summary>Verdadeiro apenas no frame em que o jogador apertou abrir/fechar a ficha (tecla C ou botão Select do gamepad).</summary>
        public bool ToggleCharacterPressedThisFrame =>
            (Keyboard.current?.cKey.wasPressedThisFrame ?? false) ||
            (Gamepad.current?.selectButton.wasPressedThisFrame ?? false);

        /// <summary>Verdadeiro apenas no frame em que o jogador apertou abrir/fechar o inventário (tecla I ou botão Start do gamepad).</summary>
        public bool ToggleInventoryPressedThisFrame =>
            (Keyboard.current?.iKey.wasPressedThisFrame ?? false) ||
            (Gamepad.current?.startButton.wasPressedThisFrame ?? false);

        /// <summary>Verdadeiro apenas no frame em que o jogador apertou entrar/sair do modo construção (tecla B).</summary>
        public bool ToggleBuildPressedThisFrame =>
            (Keyboard.current?.bKey.wasPressedThisFrame ?? false) ||
            (Gamepad.current?.dpad.up.wasPressedThisFrame ?? false);

        /// <summary>Verdadeiro apenas no frame em que o jogador apertou alternar a estrutura (tecla Q).</summary>
        public bool CycleBuildPressedThisFrame =>
            (Keyboard.current?.qKey.wasPressedThisFrame ?? false) ||
            (Gamepad.current?.leftShoulder.wasPressedThisFrame ?? false);

        /// <summary>Verdadeiro apenas no frame em que o jogador apertou posicionar a estrutura (tecla F).</summary>
        public bool PlaceBuildPressedThisFrame =>
            (Keyboard.current?.fKey.wasPressedThisFrame ?? false) ||
            (Gamepad.current?.rightShoulder.wasPressedThisFrame ?? false);

        /// <summary>Verdadeiro apenas no frame em que o jogador apertou SALVAR (tecla F5).</summary>
        public bool SavePressedThisFrame => Keyboard.current?.f5Key.wasPressedThisFrame ?? false;

        /// <summary>Verdadeiro apenas no frame em que o jogador apertou CARREGAR (tecla F9).</summary>
        public bool LoadPressedThisFrame => Keyboard.current?.f9Key.wasPressedThisFrame ?? false;

        /// <summary>Verdadeiro enquanto o jogador SEGURA correr (Shift ou L3 do gamepad).</summary>
        public bool RunHeld =>
            (Keyboard.current?.leftShiftKey.isPressed ?? false) ||
            (Keyboard.current?.rightShiftKey.isPressed ?? false) ||
            (Gamepad.current?.leftStickButton.isPressed ?? false);

        /// <summary>Verdadeiro enquanto o jogador SEGURA agachar (Ctrl ou B do gamepad).</summary>
        public bool CrouchHeld =>
            (Keyboard.current?.leftCtrlKey.isPressed ?? false) ||
            (Keyboard.current?.rightCtrlKey.isPressed ?? false) ||
            (Gamepad.current?.buttonEast.isPressed ?? false);

        /// <summary>Verdadeiro enquanto o jogador SEGURA bloquear com o escudo (botão DIREITO do mouse ou gatilho esquerdo do gamepad).</summary>
        public bool BlockHeld =>
            (Mouse.current?.rightButton.isPressed ?? false) ||
            (Gamepad.current?.leftTrigger.isPressed ?? false);

        /// <summary>Verdadeiro só no frame em que apertou agachar (Ctrl / B) — dispara o deslize.</summary>
        public bool CrouchPressedThisFrame =>
            (Keyboard.current?.leftCtrlKey.wasPressedThisFrame ?? false) ||
            (Keyboard.current?.rightCtrlKey.wasPressedThisFrame ?? false) ||
            (Gamepad.current?.buttonEast.wasPressedThisFrame ?? false);

        private static Vector2 ReadMove()
        {
            Vector2 move = Vector2.zero;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed) move.y += 1f;
                if (keyboard.sKey.isPressed) move.y -= 1f;
                if (keyboard.dKey.isPressed) move.x += 1f;
                if (keyboard.aKey.isPressed) move.x -= 1f;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                Vector2 stick = gamepad.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.01f) move += stick;
            }

            return Vector2.ClampMagnitude(move, 1f);
        }

        // Lê as teclas numéricas 1–5 (ou os números do teclado numérico) e devolve
        // o índice de slot correspondente (0..4). -1 se nenhuma foi apertada agora.
        private static int ReadAbilitySlot()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return -1;

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) return 0;
            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) return 1;
            if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) return 2;
            if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame) return 3;
            if (keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame) return 4;

            return -1;
        }

        private static Vector2 ReadLook()
        {
            Vector2 look = Vector2.zero;

            var mouse = Mouse.current;
            if (mouse != null) look += mouse.delta.ReadValue();

            var gamepad = Gamepad.current;
            if (gamepad != null) look += gamepad.rightStick.ReadValue() * 10f;

            return look;
        }
    }
}
