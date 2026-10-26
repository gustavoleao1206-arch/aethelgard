using UnityEngine;

namespace JogoDaTrupe.Data
{
    /// <summary>
    /// "Ficha técnica" de uma automação (Definição, não Estado). No slice há um
    /// exemplo: o <b>Coletor Automático</b> — uma máquina que colhe um nó de recurso
    /// sozinha e deposita no inventário do dono.
    ///
    /// O Diretor ajusta tudo aqui no Editor sem programar: o ritmo (segundos por
    /// ciclo), quanto colhe por ciclo, o alcance de busca e o visual do bloco. O
    /// ESTADO de runtime (tempo acumulado, alvo atual) vive no componente que usa
    /// esta ficha, nunca aqui (regra Definição ≠ Estado).
    /// </summary>
    [CreateAssetMenu(fileName = "Automation", menuName = "Jogo da Trupe/Automação")]
    public sealed class AutomationDefinition : ScriptableObject
    {
        public string displayName = "Coletor Automático";

        [Header("Ritmo")]
        [Tooltip("Segundos entre cada colheita automática.")]
        [Min(0.1f)] public float cycleSeconds = 1.5f;

        [Tooltip("Quantas unidades colhe a cada ciclo.")]
        [Min(1)] public int amountPerCycle = 1;

        [Tooltip("Raio (em metros) no qual procura um nó de recurso para colher.")]
        [Min(0f)] public float searchRadius = 4f;

        [Header("Visual / encaixe")]
        public Vector3 size = new Vector3(0.9f, 0.9f, 0.9f);
        public Color color = new Color(0.85f, 0.75f, 0.2f);

        [Tooltip("Altura do CENTRO quando posicionada (a base encosta no chão).")]
        public float baseY = 0.45f;
    }
}
