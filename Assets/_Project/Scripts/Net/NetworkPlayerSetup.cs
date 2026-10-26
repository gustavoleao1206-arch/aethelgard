using UnityEngine;
using Unity.Netcode;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Input;
using JogoDaTrupe.Gameplay.Characters;
using JogoDaTrupe.Gameplay.Interaction;
using JogoDaTrupe.Gameplay.Combat;
using JogoDaTrupe.Gameplay.Incorporation;
using JogoDaTrupe.Gameplay.Inventory;
using JogoDaTrupe.Gameplay.Building;
using JogoDaTrupe.Gameplay.Capture;
using JogoDaTrupe.Presentation.Camera;
using JogoDaTrupe.UI;

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// Orquestrador de dono do player em rede (M-NET-3c). No prefab <c>P_Player</c>. Quando
    /// o player nasce:
    /// • <b>Não-dono (player remoto):</b> desliga os componentes que leem o input LOCAL,
    ///   para o boneco do amigo não responder ao meu teclado. Vira um "boneco visível e
    ///   inerte", cuja posição vem do <see cref="ClientNetworkTransform"/>.
    /// • <b>Dono (meu player):</b> grava o id local (<see cref="LocalPlayer"/>), faz a
    ///   câmera local me seguir e religa as HUDs locais aos MEUS componentes.
    ///
    /// Em solo (sem rede) nada disto roda (não há OnNetworkSpawn) — o player authored e o
    /// PlayerSceneSetup cuidam de tudo como sempre.
    /// </summary>
    [RequireComponent(typeof(Ownable))]
    public sealed class NetworkPlayerSetup : NetworkBehaviour
    {
        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                DisableLocalOnly();
                return;
            }

            // Sou o jogador local desta máquina. O LocalPlayer.Id (= ASSENTO) é gravado
            // pelo NetworkOwnable (M-NET-9 P2), que tem o assento replicado — aqui só
            // religamos câmera e HUDs (o filtro por id já usa o assento).
            BindCamera();
            BindHuds();
        }

        // Desliga tudo que dirige o input/controle local (não deve rodar no boneco remoto).
        private void DisableLocalOnly()
        {
            Disable<PlayerInputReader>();
            Disable<PlayerController>();
            Disable<GroundCheck>();
            Disable<Interactor>();
            Disable<MeleeAttack>();
            Disable<AbilityRunner>();
            Disable<CharacterSheetToggle>();
            Disable<InventoryToggle>();
            Disable<BuildController>();
            Disable<Synchronizer>();
        }

        private void Disable<T>() where T : Behaviour
        {
            if (TryGetComponent(out T c)) c.enabled = false;
        }

        // Aponta a câmera local (uma por cliente) para ESTE player.
        private void BindCamera()
        {
            PlayerCamera cam = FindAnyObjectByType<PlayerCamera>();
            if (cam != null) cam.SetTarget(transform, GetComponent<PlayerInputReader>());
        }

        // Religa as HUDs locais (uma vez por cliente) aos componentes deste player.
        // FindAnyObjectByType retorna null se a HUD não existir na cena → ?. é seguro.
        private void BindHuds()
        {
            AbilityLoadout loadout = GetComponent<AbilityLoadout>();
            PlayerAttributes attributes = GetComponent<PlayerAttributes>();
            AffinityState affinity = GetComponent<AffinityState>();
            MemoryBank memory = GetComponent<MemoryBank>();
            PlayerInventory inventory = GetComponent<PlayerInventory>();
            BuildController build = GetComponent<BuildController>();
            Synchronizer sync = GetComponent<Synchronizer>();

            FindAnyObjectByType<AbilityBarHUD>()?.Bind(loadout, attributes, affinity);
            FindAnyObjectByType<AltarUI>()?.Bind(memory, loadout);
            FindAnyObjectByType<AttributesUI>()?.Bind(attributes, affinity);
            FindAnyObjectByType<InventoryHUD>()?.Bind(inventory);
            FindAnyObjectByType<CraftingUI>()?.Bind(inventory);
            FindAnyObjectByType<BuildHUD>()?.Bind(build);
            FindAnyObjectByType<CaptureHUD>()?.Bind(sync);
        }
    }
}
