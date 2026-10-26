using UnityEngine;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Data;
using JogoDaTrupe.Input;

namespace JogoDaTrupe.Gameplay.Incorporation
{
    /// <summary>
    /// Lê a intenção de abrir/fechar a ficha de atributos (tecla C) e publica o
    /// evento <see cref="CharacterSheetToggled"/>. Mantém a UI desacoplada da
    /// Simulação (a UI não lê input direto; reage ao evento), igual ao Altar.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterSheetToggle : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader _input;
        private Ownable _ownable;

        private void Awake()
        {
            if (_input == null) _input = GetComponent<PlayerInputReader>();
            _ownable = GetComponent<Ownable>();
        }

        private void Update()
        {
            if (_input == null || !_input.ToggleCharacterPressedThisFrame) return;

            EventBus<CharacterSheetToggled>.Publish(new CharacterSheetToggled
            {
                OwnerId = _ownable != null ? _ownable.OwnerId : Ownable.LocalPlayerId
            });
        }
    }
}
