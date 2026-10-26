using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Netcode;

namespace JogoDaTrupe.EditorTools
{
    /// <summary>
    /// Correção DURÁVEL do pipeline de PREFABS DE REDE (achado do playtest de 05/07/2026 —
    /// ver 06_Testes/Melhorias.md). Concentra os dois pontos do conserto:
    ///
    /// 1) <see cref="SavePrefabStable"/> — salva o prefab NO MESMO caminho SEM deletar o
    ///    asset antes, preservando o GUID. O <c>GlobalObjectIdHash</c> do NGO (o hash que
    ///    casa o prefab entre servidor e cliente) DERIVA do GUID do asset
    ///    (<c>NetworkObject.OnValidate</c>: <c>GlobalObjectIdHash = GlobalObjectId(...).Hash32()</c>).
    ///    O antigo <c>DeleteAsset</c> + <c>SaveAsPrefabAsset</c> trocava o GUID a CADA
    ///    regeração da cena → o hash de rede mudava e (a) a auto-lista do NGO perdia a
    ///    entrada e (b) as duas instâncias do MPPM divergiam de hash
    ///    ("NetworkPrefab could not be found"). Reusar o asset (GUID estável) é o MESMO
    ///    padrão já adotado para as fichas (<c>CreateEnemyDef</c>/<c>CreateRecipe</c>/…).
    ///
    /// 2) <see cref="EnsureRegistered"/> — garante DETERMINISTICAMENTE, no momento da
    ///    geração, que todo prefab de rede está numa <see cref="NetworkPrefabsList"/> do
    ///    manager. Se a auto-lista (<c>DefaultNetworkPrefabs</c>) tiver defasado, ADICIONA
    ///    só o que falta — idempotente (guardado por <c>Contains</c>), então NÃO cria uma
    ///    2ª fonte de registro (sem o risco do Bug #8). No caso saudável é no-op. Substitui
    ///    o antigo "validar e falhar alto": em vez de só acusar, o gerador AGORA conserta.
    /// </summary>
    internal static class NetPrefabTooling
    {
        // Caminho da auto-lista mantida pelo NGO ("Generate Default Network Prefabs").
        private const string DefaultListPath = "Assets/DefaultNetworkPrefabs.asset";

        /// <summary>
        /// Salva/atualiza o prefab em <paramref name="path"/> preservando o GUID (não deleta
        /// o .meta) e destrói o objeto temporário. Retorna o asset de prefab carregado.
        /// GUID estável ⇒ <c>GlobalObjectIdHash</c> estável ⇒ hash de rede idêntico nas 2 telas.
        /// </summary>
        internal static GameObject SavePrefabStable(GameObject temp, string path)
        {
            // SaveAsPrefabAsset sobre um caminho existente SUBSTITUI o conteúdo mas mantém o
            // GUID do asset (o .meta não é tocado). Sem DeleteAsset de propósito — é o cerne
            // da correção: é o delete que gerava um GUID novo e derrubava o hash de rede.
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        /// <summary>
        /// Garante que <paramref name="prefab"/> esteja registrado em alguma
        /// <see cref="NetworkPrefabsList"/> do <paramref name="nm"/>. No-op se já estiver;
        /// senão adiciona à lista do manager (a auto-lista do NGO) — reparo determinístico
        /// e idempotente. Falha alto só se não houver NENHUMA lista para registrar.
        /// </summary>
        internal static void EnsureRegistered(NetworkManager nm, GameObject prefab)
        {
            if (prefab == null || nm == null) return;
            List<NetworkPrefabsList> lists = nm.NetworkConfig.Prefabs.NetworkPrefabsLists;

            // Já registrado em alguma lista? Então nada a fazer (caso saudável).
            foreach (NetworkPrefabsList list in lists)
                if (list != null && list.Contains(prefab)) return;

            // Não está: repara. Alvo = a 1ª lista do manager (a DefaultNetworkPrefabs, ligada
            // pelo Reset do NetworkManager); se o manager não tiver nenhuma, carrega/attacha a
            // auto-lista do NGO do disco.
            NetworkPrefabsList target = FirstNonNull(lists);
            if (target == null)
            {
                target = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(DefaultListPath);
                if (target != null) lists.Add(target);
            }

            if (target == null)
            {
                Debug.LogError("[Setup] Nenhuma NetworkPrefabsList disponível para registrar " +
                    prefab.name + " — o cliente não conseguiria spawná-lo. Habilite 'Generate Default " +
                    "Network Prefabs' em Project Settings > Netcode for GameObjects e gere a cena de novo.");
                return;
            }

            target.Add(new NetworkPrefab { Prefab = prefab });
            EditorUtility.SetDirty(target);
            Debug.LogWarning("[Setup] " + prefab.name + " não estava na lista de prefabs de rede — " +
                "adicionado automaticamente (reparo determinístico do gerador; a auto-lista do NGO " +
                "havia defasado). O AssetDatabase.SaveAssets no fim da geração persiste a correção.");
        }

        /// <summary>
        /// Remove entradas DUPLICADAS das listas de prefabs de rede do <paramref name="nm"/>.
        /// O <see cref="EnsureRegistered"/> é idempotente ao ADICIONAR (guarda por Contains), mas
        /// não cura uma duplicata que já exista no asset — e ela aparece quando o auto-registro
        /// do NGO no import e o reparo do gerador inserem o MESMO prefab (ex.: P_NetSystems foi
        /// parar 2× na DefaultNetworkPrefabs). O NGO reclama no Start Host
        /// ("duplicate GlobalObjectIdHash") e descarta uma. Aqui a geração AUTO-CURA a lista.
        /// </summary>
        internal static void DedupeRegistry(NetworkManager nm)
        {
            if (nm == null) return;

            foreach (NetworkPrefabsList list in nm.NetworkConfig.Prefabs.NetworkPrefabsLists)
            {
                if (list == null) continue;

                var seen = new HashSet<GameObject>();
                var duplicates = new List<NetworkPrefab>();
                foreach (NetworkPrefab entry in list.PrefabList)
                {
                    if (entry?.Prefab == null) continue;
                    if (!seen.Add(entry.Prefab)) duplicates.Add(entry); // 2ª vez em diante = duplicata
                }

                foreach (NetworkPrefab dup in duplicates)
                {
                    list.Remove(dup);
                    Debug.LogWarning("[Setup] Entrada duplicada de '" + dup.Prefab.name +
                        "' removida da lista de prefabs de rede (auto-cura — evita o erro " +
                        "'duplicate GlobalObjectIdHash' no Start Host).");
                }

                if (duplicates.Count > 0) EditorUtility.SetDirty(list);
            }
        }

        private static NetworkPrefabsList FirstNonNull(List<NetworkPrefabsList> lists)
        {
            foreach (NetworkPrefabsList l in lists)
                if (l != null) return l;
            return null;
        }
    }
}
