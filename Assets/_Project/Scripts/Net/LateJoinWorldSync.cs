using UnityEngine;
using Unity.Netcode;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.World;
using JogoDaTrupe.Gameplay.Inventory;

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// Sincroniza o ESTADO DE MUNDO com quem entra tarde (M-NET-9 P3, drop-in). O
    /// <c>NodeStockChanged</c> é evento AO VIVO (não estado replicado — o nó não é
    /// NetworkObject de propósito, Bug #7), então um cliente que conecta DEPOIS de o host
    /// já ter colhido veria o nó cheio. Aqui, quando um cliente conecta, o servidor
    /// RE-ANUNCIA o estoque de cada nó (idempotente — quem já tinha o valor certo não
    /// muda). Fecha a lacuna registrada no Melhorias.
    ///
    /// Companion no <c>P_NetSystems</c> (server-only). As estruturas construídas já são
    /// NetworkObjects (o NGO as sincroniza no spawn do late-joiner); os inventários/
    /// progressão idem (NetworkList/NetworkVariable). Aqui só os NÓS precisam do empurrão.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class LateJoinWorldSync : NetworkBehaviour
    {
        public override void OnNetworkSpawn()
        {
            if (!IsServer || NetworkManager.Singleton == null) return;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }

        private void OnClientConnected(ulong clientId)
        {
            // Re-anuncia o estoque de cada nó. O host publica → o EventScopeAdapter relaya
            // a TODOS os clientes; o recém-chegado espelha ao aplicar (os demais já tinham
            // o valor certo — idempotente). Nós ainda cheios reafirmam o valor cheio: sem
            // efeito. Barato (poucos nós, uma vez por conexão).
            foreach (ResourceNode node in WorldNodeIndex.All)
            {
                if (node == null) continue;
                EventBus<NodeStockChanged>.Publish(new NodeStockChanged
                {
                    NodeGuid = node.Guid,
                    Remaining = node.Remaining
                });
            }
        }
    }
}
