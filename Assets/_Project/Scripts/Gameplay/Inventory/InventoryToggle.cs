using UnityEngine;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Data;
using JogoDaTrupe.Input;

namespace JogoDaTrupe.Gameplay.Inventory
{
    /// <summary>
    /// Lê a intenção de abrir/fechar o inventário (tecla I) e publica
    /// <see cref="InventoryToggled"/>. Mantém a UI desacoplada da Simulação
    /// (a UI reage ao evento; não lê input direto), igual ao Altar e à ficha.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventoryToggle : MonoBehaviour
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
            if (_input == null || !_input.ToggleInventoryPressedThisFrame) return;

            EventBus<InventoryToggled>.Publish(new InventoryToggled
            {
                OwnerId = _ownable != null ? _ownable.OwnerId : Ownable.LocalPlayerId
            });
        }
    }
}
