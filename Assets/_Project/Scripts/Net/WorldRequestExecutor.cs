using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Saving;
using JogoDaTrupe.Core.World;
using JogoDaTrupe.Data;
using JogoDaTrupe.Gameplay.Building;
using JogoDaTrupe.Gameplay.Capture;
using JogoDaTrupe.Gameplay.Inventory;

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// EXECUTOR server-side dos pedidos de mundo (M-NET-7 P3+). Vive no prefab
    /// <c>P_NetSystems</c> (par do <see cref="EventScopeAdapter"/>), spawnado quando o
    /// servidor sobe. SÓ o servidor age: assina as intenções que chegaram ao bus
    /// (publicadas localmente no host, ou trazidas do cliente pelo
    /// <see cref="NetworkWorldRequestForwarder"/>) e executa a operação REAL — reusando
    /// os mesmos métodos do solo (nenhuma lógica duplicada).
    ///
    /// A resolução "ownerId → player" usa a fonte autoritativa do próprio NGO
    /// (<c>ConnectedClients[id].PlayerObject</c>), evitando um registro estático e os
    /// problemas de ordem de Awake. O nó vem do <see cref="WorldNodeIndex"/> (guid→nó,
    /// idêntico nas 2 telas porque a cena é o mesmo asset).
    ///
    /// P3 cobre a COLETA (<see cref="HarvestRequested"/>), P4 o CRAFT
    /// (<see cref="CraftRequested"/>), P5 a CONSTRUÇÃO (<see cref="BuildRequested"/>:
    /// valida/debita o custo no inventário autoritativo e SPAWNA o prefab de rede da
    /// estrutura, casado por structureId em <c>_structurePrefabs</c>) e P7 a CAPTURA
    /// (<see cref="SyncRequested"/>: o servidor ARBITRA — o 1º pedido válido leva,
    /// 🎨 decisão do Diretor; o corpo some das 2 telas pelo despawn).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class WorldRequestExecutor : NetworkBehaviour, ISaveable
    {
        [Tooltip("Prefabs de rede das estruturas construíveis (P_Structure_*), casados por structureId (M-NET-7 P5).")]
        [SerializeField] private GameObject[] _structurePrefabs;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            EventBus<HarvestRequested>.Subscribe(OnHarvestRequested);
            EventBus<CraftRequested>.Subscribe(OnCraftRequested);
            EventBus<BuildRequested>.Subscribe(OnBuildRequested);
            EventBus<SyncRequested>.Subscribe(OnSyncRequested);
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;
            EventBus<HarvestRequested>.Unsubscribe(OnHarvestRequested);
            EventBus<CraftRequested>.Unsubscribe(OnCraftRequested);
            EventBus<BuildRequested>.Unsubscribe(OnBuildRequested);
            EventBus<SyncRequested>.Unsubscribe(OnSyncRequested);
        }

        private void OnHarvestRequested(HarvestRequested e)
        {
            ResourceNode node = WorldNodeIndex.Find(e.NodeGuid);
            if (node == null)
            {
                Debug.LogWarning($"[Rede] HarvestRequested ignorado: nó '{e.NodeGuid}' não encontrado (esgotado?).");
                return;
            }

            GameObject player = ResolvePlayer(e.OwnerId);
            if (player == null)
            {
                Debug.LogWarning($"[Rede] HarvestRequested ignorado: player do dono {e.OwnerId} não encontrado no servidor.");
                return;
            }

            // No servidor a autoridade é verdadeira → Interact colhe de verdade,
            // deposita no inventário do dono (replicado pelo NetworkInventory) e publica
            // NodeStockChanged (relayado às réplicas).
            node.Interact(player);
        }

        private void OnCraftRequested(CraftRequested e)
        {
            RecipeDefinition recipe = DefinitionRegistry.Active != null
                ? DefinitionRegistry.Active.RecipeById(e.RecipeId) : null;
            if (recipe == null)
            {
                Debug.LogWarning($"[Rede] CraftRequested ignorado: receita '{e.RecipeId}' não encontrada no DefinitionRegistry.Active.");
                return;
            }

            GameObject player = ResolvePlayer(e.OwnerId);
            if (player == null || !player.TryGetComponent(out PlayerInventory inventory))
            {
                Debug.LogWarning($"[Rede] CraftRequested ignorado: inventário do dono {e.OwnerId} não encontrado no servidor.");
                return;
            }

            // No servidor (autoridade) o TryCraft valida os ingredientes no inventário
            // AUTORITATIVO, consome e adiciona o resultado — replicado pelo NetworkInventory.
            inventory.TryCraft(recipe);
        }

        private void OnBuildRequested(BuildRequested e)
        {
            GameObject prefab = FindStructurePrefab(e.StructureId);
            if (prefab == null)
            {
                Debug.LogWarning($"[Rede] BuildRequested ignorado: prefab da estrutura '{e.StructureId}' não encontrado no executor.");
                return;
            }

            // O custo vem da ficha BAKED no prefab (mesmo asset que o cliente vê): é a
            // fonte autoritativa e evita depender do registry estar populado.
            Structure structure = prefab.GetComponent<Structure>();
            StructureDefinition def = structure != null ? structure.Definition : null;
            if (def == null)
            {
                Debug.LogWarning($"[Rede] BuildRequested ignorado: prefab '{e.StructureId}' sem StructureDefinition baked.");
                return;
            }

            GameObject player = ResolvePlayer(e.OwnerId);
            if (player == null || !player.TryGetComponent(out PlayerInventory inventory))
            {
                Debug.LogWarning($"[Rede] BuildRequested ignorado: inventário do dono {e.OwnerId} não encontrado no servidor.");
                return;
            }

            // Debita no inventário AUTORITATIVO (SpendCost só paga se puder — atômico).
            // Se não pagou (material insuficiente no servidor), não constrói.
            if (!inventory.SpendCost(def.cost))
            {
                Debug.Log($"[Rede] Construção de '{e.StructureId}' negada: dono {e.OwnerId} sem material no servidor.");
                return;
            }

            // Spawna o prefab de rede na posição pedida (já encaixada na grade pela prévia
            // local). Server-owned; o NGO replica nas duas telas e o destrói no shutdown.
            SpawnStructure(prefab, new Vector3(e.X, e.Y, e.Z));
            Debug.Log($"[Rede] Estrutura '{e.StructureId}' construída pelo dono {e.OwnerId} em ({e.X:0.0}, {e.Y:0.0}, {e.Z:0.0}).");
        }

        // Instancia + spawna uma estrutura de rede (server-owned). Reusado pelo build e
        // pela restauração do save de mundo (M-NET-9 P3).
        private void SpawnStructure(GameObject prefab, Vector3 position)
        {
            GameObject go = Instantiate(prefab, position, Quaternion.identity);
            go.GetComponent<NetworkObject>().Spawn();
        }

        // ---- Save do MUNDO: estruturas construídas (ISaveable, M-NET-9 P3) ----
        // O host salva a lista de estruturas VIVAS (id + posição); carregar despawna as
        // atuais e re-spawna a lista. Chave única do mundo (não por jogador). Só em co-op
        // (este executor vive no P_NetSystems, que só existe em rede); a estrutura do solo
        // é primitivo local — não persiste (limitação anotada, unificação = polish).

        public string SaveKey => "world:structures";

        [System.Serializable]
        private struct StructureSave { public string structureId; public float x, y, z; }

        [System.Serializable]
        private struct StructuresSave { public List<StructureSave> list; }

        public object CaptureState()
        {
            var data = new StructuresSave { list = new List<StructureSave>() };
            foreach (Structure s in FindObjectsByType<Structure>())
            {
                if (s == null || string.IsNullOrEmpty(s.StructureId)) continue;
                Vector3 p = s.transform.position;
                data.list.Add(new StructureSave { structureId = s.StructureId, x = p.x, y = p.y, z = p.z });
            }
            return data;
        }

        public void RestoreState(object state)
        {
            if (!IsServer) return; // spawn/despawn é do servidor (o Load já é host-only)

            // Despawna as estruturas atuais (o NGO as remove das 2 telas).
            foreach (Structure s in FindObjectsByType<Structure>())
                if (s != null && s.TryGetComponent(out NetworkObject no) && no.IsSpawned) no.Despawn();

            var data = JsonUtility.FromJson<StructuresSave>((string)state);
            if (data.list == null) return;

            for (int i = 0; i < data.list.Count; i++)
            {
                StructureSave e = data.list[i];
                GameObject prefab = FindStructurePrefab(e.structureId);
                if (prefab != null) SpawnStructure(prefab, new Vector3(e.x, e.y, e.z));
            }
            Debug.Log($"[Rede] Mundo restaurado: {data.list.Count} estrutura(s) re-spawnada(s).");
        }

        private void OnSyncRequested(SyncRequested e)
        {
            CapturableCreature creature = FindCreature(e.CreatureGuid);
            if (creature == null)
            {
                // Normal na corrida: o 2º pedido chega DEPOIS do despawn — o perdedor
                // só vê a criatura sumir (🎨 "um leva", decisão do Diretor 02/07/2026).
                Debug.Log($"[Rede] SyncRequested ignorado: criatura '{e.CreatureGuid}' não existe mais (capturada?).");
                return;
            }

            if (!creature.CanSynchronize)
            {
                // Também cobre a corrida no MESMO frame: o 1º Synchronize marca
                // _captured síncrono e o 2º pedido cai aqui.
                Debug.Log($"[Rede] Captura negada ao dono {e.OwnerId}: '{creature.Label}' não está sincronizável.");
                return;
            }

            // Autoridade: captura de verdade — EcoCollected/CreatureCaptured saem daqui
            // (relayados; o VENCEDOR recebe nos 2 lados) e o corpo some pelas 2 telas
            // via despawn do prefab de rede.
            creature.Synchronize(e.OwnerId);
            Debug.Log($"[Rede] Captura concedida: '{creature.Label}' sincronizada pelo dono {e.OwnerId}.");
        }

        // Acha a criatura capturável pelo guid compartilhado (varredura — há UMA criatura
        // no slice; se um dia houver muitas, promover a um índice como o WorldNodeIndex).
        private static CapturableCreature FindCreature(string creatureGuid)
        {
            if (string.IsNullOrEmpty(creatureGuid)) return null;
            CapturableCreature[] creatures = Object.FindObjectsByType<CapturableCreature>();
            for (int i = 0; i < creatures.Length; i++)
            {
                if (creatures[i] != null && creatures[i].Guid == creatureGuid) return creatures[i];
            }
            return null;
        }

        // Acha o prefab cuja ficha baked tem este structureId (varredura — poucas estruturas).
        private GameObject FindStructurePrefab(string structureId)
        {
            if (_structurePrefabs == null || string.IsNullOrEmpty(structureId)) return null;
            for (int i = 0; i < _structurePrefabs.Length; i++)
            {
                GameObject p = _structurePrefabs[i];
                if (p == null) continue;
                Structure s = p.GetComponent<Structure>();
                if (s != null && s.StructureId == structureId) return p;
            }
            return null;
        }

        // Resolve o GameObject do player a partir do ASSENTO do dono (M-NET-9 P2): assento
        // → clientId (SeatRegistry) → PlayerObject (NGO, fonte autoritativa).
        private GameObject ResolvePlayer(int ownerId)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null) return null;
            if (!SeatRegistry.TryClientIdOf(ownerId, out ulong clientId)) return null;
            if (!nm.ConnectedClients.TryGetValue(clientId, out NetworkClient client)) return null;
            return client.PlayerObject != null ? client.PlayerObject.gameObject : null;
        }
    }
}
