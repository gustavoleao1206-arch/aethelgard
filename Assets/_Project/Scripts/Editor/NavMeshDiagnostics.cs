using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using JogoDaTrupe.Gameplay.Characters;
using JogoDaTrupe.Gameplay.Enemies;

namespace JogoDaTrupe.EditorTools
{
    /// <summary>
    /// Ferramenta de DIAGNÓSTICO da navegação (rodar EM PLAY). Responde, sem chute, por que um
    /// mob anda em linha reta em vez de contornar: ele está sobre a malha? o jogador está sobre
    /// a malha? existe rota entre os dois, e ela DOBRA (mais de 2 cantos) ou é reta?
    ///
    /// Existe porque o fallback de linha reta é silencioso por natureza — sem medir, "a IA está
    /// meio burra" e "não há malha aqui" parecem a mesma coisa na tela.
    /// </summary>
    internal static class NavMeshDiagnostics
    {
        [MenuItem("Jogo da Trupe/Debug/Diagnosticar NavMesh (em Play)")]
        private static void Diagnose()
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player == null) { Debug.LogWarning("[NavMesh] Sem jogador na cena — entre em Play."); return; }

            // Raio generoso: o pivô do jogador é o CENTRO da cápsula, ~1m acima do chão — com 1m
            // de busca o teste dava "fora da malha" mesmo com ele pisando nela.
            const float SampleRadius = 3f;
            Vector3 playerPos = player.transform.position;
            bool playerOnMesh = NavMesh.SamplePosition(playerPos, out NavMeshHit pHit, SampleRadius, NavMesh.AllAreas);
            Debug.Log($"[NavMesh] JOGADOR em {playerPos} → sobre a malha? {playerOnMesh}" +
                      (playerOnMesh ? $" (ponto mais próximo a {Vector3.Distance(playerPos, pHit.position):F2}m)" : $" (NENHUMA malha num raio de {SampleRadius}m)"));

            var mobs = Object.FindObjectsByType<EnemyController>();
            if (mobs.Length == 0) Debug.LogWarning("[NavMesh] Nenhum EnemyController na cena.");

            foreach (EnemyController mob in mobs)
            {
                Vector3 pos = mob.transform.position;
                var nav = mob.GetComponent<EnemyNavigator>();
                string agentInfo = nav == null
                    ? "SEM EnemyNavigator"
                    : $"navegador: malha={nav.HasMesh}, parede entre nós={nav.BlockedTo(playerPos)}";

                bool mobOnMesh = NavMesh.SamplePosition(pos, out NavMeshHit mHit, 3f, NavMesh.AllAreas);

                var path = new NavMeshPath();
                bool calculated = NavMesh.CalculatePath(
                    mobOnMesh ? mHit.position : pos,
                    playerOnMesh ? pHit.position : playerPos,
                    NavMesh.AllAreas, path);

                // 2 cantos = linha reta (origem→destino). 3+ = a rota DOBRA em volta de algo.
                string rota = !calculated
                    ? "NÃO CALCULOU"
                    : $"{path.status}, {path.corners.Length} cantos" +
                      (path.corners.Length > 2 ? " ← CONTORNA" : " ← reta");

                Debug.Log($"[NavMesh] {mob.name}: {agentInfo}; sobre a malha={mobOnMesh}; rota até o jogador: {rota}");
            }
        }
    }
}
