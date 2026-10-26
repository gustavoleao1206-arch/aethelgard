using UnityEngine;
using JogoDaTrupe.Core.Rng;

namespace JogoDaTrupe.Gameplay.Interaction
{
    /// <summary>
    /// Objeto interativo de exemplo, só para validar o sistema de interação no
    /// Vertical Slice: ao interagir, ele muda de cor. Será substituído por
    /// interativos reais (baús, recursos, NPCs) mais adiante.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class SampleInteractable : MonoBehaviour, IInteractable
    {
        public string Prompt => "Interagir";

        private Renderer _renderer;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
        }

        public void Interact(GameObject interactor)
        {
            // Troca por uma cor aleatória para dar um retorno visual claro.
            if (_renderer != null)
            {
                _renderer.material.color = new Color(GameRandom.Value, GameRandom.Value, GameRandom.Value);
            }

            Debug.Log($"[Interação] {name} foi ativado por {interactor.name}.");
        }
    }
}
