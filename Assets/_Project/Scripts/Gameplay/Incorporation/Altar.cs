using System.Collections.Generic;
using UnityEngine;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Interaction;

namespace JogoDaTrupe.Gameplay.Incorporation
{
    /// <summary>
    /// Altar: local seguro onde o jogador planeja o loadout (incorporar/equipar/
    /// trocar). É um IInteractable — interagir (E) abre; interagir de novo fecha.
    /// Enquanto aberto, libera as trocas (AbilityLoadout.AtAltar = true).
    ///
    /// MULTIUSUÁRIO (M-NET-8 P4): a sessão é POR DONO (mapa OwnerId→loadout) — dois
    /// jogadores usam o Altar AO MESMO TEMPO sem se expulsar (antes, um campo único
    /// `_currentUser` fazia o Interact do 2º fechar a sessão do 1º). O toggle é
    /// sempre da sessão DO INTERATOR. Sem estado de rede aqui: cada máquina rastreia
    /// as sessões que vê (o AtAltar é UX local) — a segurança REAL das operações é a
    /// validação de posição no servidor (IncorporationRequestExecutor, P3).
    ///
    /// Não conhece a UI: publica AltarOpened/AltarClosed (só OwnerId) e a AltarUI
    /// reage. Assim a camada Gameplay não depende da camada UI (respeita os asmdefs).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class Altar : MonoBehaviour, IInteractable
    {
        private readonly Dictionary<int, AbilityLoadout> _sessions = new Dictionary<int, AbilityLoadout>();

        // O prompt é apresentação LOCAL: cada máquina tem um jogador local — mostra
        // o estado da sessão DELE (no solo, id 0: comportamento idêntico ao antigo).
        public string Prompt => _sessions.ContainsKey(LocalPlayer.Id) ? "Fechar Altar" : "Usar Altar";

        /// <summary>A sessão deste dono está aberta? (testável; usado pelo Prompt.)</summary>
        public bool HasSession(int ownerId) => _sessions.ContainsKey(ownerId);

        public void Interact(GameObject interactor)
        {
            if (interactor == null) return;

            int ownerId = interactor.TryGetComponent(out Ownable ownable)
                ? ownable.OwnerId
                : Ownable.LocalPlayerId;

            // Toggle da sessão DO INTERATOR — as dos outros ficam intocadas.
            if (_sessions.TryGetValue(ownerId, out AbilityLoadout open))
            {
                Close(ownerId, open);
                return;
            }

            if (!interactor.TryGetComponent(out AbilityLoadout loadout)) return;

            _sessions[ownerId] = loadout;
            loadout.AtAltar = true;

            EventBus<AltarOpened>.Publish(new AltarOpened { OwnerId = ownerId });
        }

        private void Close(int ownerId, AbilityLoadout loadout)
        {
            _sessions.Remove(ownerId);
            if (loadout != null) loadout.AtAltar = false;

            EventBus<AltarClosed>.Publish(new AltarClosed { OwnerId = ownerId });
        }
    }
}
