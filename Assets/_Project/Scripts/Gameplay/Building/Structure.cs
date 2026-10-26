using UnityEngine;
using JogoDaTrupe.Data;

namespace JogoDaTrupe.Gameplay.Building
{
    /// <summary>
    /// IDENTIDADE de uma estrutura construída (M-NET-7 P5). Fica no prefab de rede
    /// <c>P_Structure_*</c> com a <see cref="StructureDefinition"/> BAKED — do mesmo jeito
    /// que o inimigo carrega a EnemyDefinition no seu controller. Serve para:
    ///
    /// • o <see cref="JogoDaTrupe.Net.WorldRequestExecutor"/> achar o prefab certo pelo
    ///   <see cref="StructureId"/> ao atender um <c>BuildRequested</c> e ler o custo
    ///   (<see cref="Definition"/>) da fonte autoritativa;
    /// • o save de mundo (M-NET-8) saber QUE estrutura recriar em cada posição.
    ///
    /// Sem lógica de rede aqui (camada Gameplay, pura de NGO): é só um portador de ficha.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Structure : MonoBehaviour
    {
        [SerializeField] private StructureDefinition _definition;

        /// <summary>A ficha desta estrutura (custo, tamanho, cor). Baked no prefab.</summary>
        public StructureDefinition Definition => _definition;

        /// <summary>Id estável da estrutura (para rede/save). Null se sem ficha (defensivo).</summary>
        public string StructureId => _definition != null ? _definition.structureId : null;
    }
}
