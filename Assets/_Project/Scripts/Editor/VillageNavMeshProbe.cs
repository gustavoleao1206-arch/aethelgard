using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using JogoDaTrupe.Core.World;

namespace JogoDaTrupe.EditorTools
{
    /// <summary>
    /// DIAGNÓSTICO da NavMesh em volta das vilas (M-TERR-5): escreve em <c>Temp/NavMesh_Vilas.txt</c> um
    /// mapa ASCII (1 caractere = 1 m) de cada pad — '#' tem malha, '.' não tem, 'H' é caixa de casa —
    /// e o status da rota do hub até pontos dentro/atrás da vila. Serve para VER onde a malha está
    /// cortada quando o Diretor relata "o mob bate num limite invisível e volta".
    /// </summary>
    public static class VillageNavMeshProbe
    {
        [MenuItem("Jogo da Trupe/Debug/Diagnosticar NavMesh das vilas (Temp)")]
        public static void Run()
        {
            var sb = new StringBuilder();
            foreach (string vila in new[] { "Vila Oeste", "Vila Leste" })
            {
                GameObject go = GameObject.Find(vila);
                if (go == null) { sb.AppendLine(vila + ": não encontrada na cena"); continue; }
                Vector3 c = go.transform.position;
                BoxCollider[] boxes = go.GetComponentsInChildren<BoxCollider>();
                sb.AppendLine("=== " + vila + " centro " + c + " casas=" + boxes.Length + " (norte em cima, leste à direita) ===");
                for (int dz = 18; dz >= -18; dz--)
                {
                    for (int dx = -18; dx <= 18; dx++)
                    {
                        float x = c.x + dx, z = c.z + dz;
                        var p = new Vector3(x, WorldHeight.SampleHeight(x, z), z);
                        bool inHouse = false;
                        foreach (BoxCollider b in boxes)
                        {
                            Vector3 local = b.transform.InverseTransformPoint(new Vector3(x, 1f, z)) - b.center;
                            if (Mathf.Abs(local.x) <= b.size.x * 0.5f && Mathf.Abs(local.z) <= b.size.z * 0.5f) { inHouse = true; break; }
                        }
                        bool onMesh = NavMesh.SamplePosition(p, out NavMeshHit hit, 0.6f, NavMesh.AllAreas)
                                      && new Vector2(hit.position.x - x, hit.position.z - z).magnitude < 0.45f;
                        char ch = inHouse ? 'H' : onMesh ? '#' : '.';
                        if (dx == 0 && dz == 0) ch = onMesh ? 'C' : 'c';
                        sb.Append(ch);
                    }
                    sb.AppendLine();
                }

                // Rotas do hub até a praça e até 4 pontos ATRÁS do anel de casas.
                var path = new NavMeshPath();
                Vector3 hub = new Vector3(0f, 0f, 0f);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 45f * Mathf.Deg2Rad;
                    float r = i % 2 == 0 ? 11.5f : 2f;
                    Vector3 target = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    string status = "sem malha no alvo";
                    if (NavMesh.SamplePosition(target, out NavMeshHit th, 2f, NavMesh.AllAreas)
                        && NavMesh.SamplePosition(hub, out NavMeshHit hh, 2f, NavMesh.AllAreas))
                    {
                        NavMesh.CalculatePath(hh.position, th.position, NavMesh.AllAreas, path);
                        status = path.status + " (" + path.corners.Length + " cantos)";
                    }
                    sb.AppendLine("rota hub -> ângulo " + (i * 45) + "° raio " + r + ": " + status);
                }
                sb.AppendLine();
            }

            NavMeshBuildSettings s = NavMesh.GetSettingsByIndex(0);
            sb.AppendLine("agente: raio " + s.agentRadius + " altura " + s.agentHeight + " rampa " + s.agentSlope + "° degrau " + s.agentClimb);
            System.IO.File.WriteAllText("Temp/NavMesh_Vilas.txt", sb.ToString());
            Debug.Log("[NavMesh] Mapa das vilas em Temp/NavMesh_Vilas.txt");
        }
    }
}
