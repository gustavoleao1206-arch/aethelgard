using UnityEngine;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Interaction;

namespace JogoDaTrupe.Gameplay.Crafting
{
    /// <summary>
    /// Bancada de crafting: interagir (E) abre/fecha a tela de crafting. Não conhece
    /// a UI — publica <see cref="CraftingToggled"/> e a CraftingUI reage (mesmo padrão
    /// do Altar e da ficha). Assim a camada Gameplay não depende da camada UI.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class Workbench : MonoBehaviour, IInteractable
    {
        public string Prompt => "Usar Bancada";

        public void Interact(GameObject interactor)
        {
            if (interactor == null) return;

            int ownerId = interactor.TryGetComponent(out Ownable ownable)
                ? ownable.OwnerId
                : Ownable.LocalPlayerId;

            EventBus<CraftingToggled>.Publish(new CraftingToggled { OwnerId = ownerId });
        }
    }
}
