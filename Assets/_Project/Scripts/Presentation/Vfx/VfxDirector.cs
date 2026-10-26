using System.Collections.Generic;
using UnityEngine;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Data;

namespace JogoDaTrupe.Presentation.Vfx
{
    /// <summary>
    /// Diretor de VFX (camada Presentation): assina os eventos de gameplay e dispara as
    /// partículas na posição certa — SEM tocar na lógica (idem <c>CaptureHUD</c>). Resolve
    /// a posição pelo <c>OwnerId</c> (encontra o jogador dono), porque os eventos não
    /// carregam posição — e o <see cref="EcoCollected"/> é relayado na rede, então não
    /// convém alterá-lo.
    ///
    /// Mostra os efeitos de TODOS os jogadores (não filtra por LocalPlayer): em co-op você
    /// vê o brilho dos companheiros. Robusto a respawn/reconexão: cache OwnerId→Transform
    /// com re-busca quando o alvo some (nova instância do player).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VfxDirector : MonoBehaviour
    {
        // Material aditivo do glow (JogoDaTrupe/ParticleGlow), ligado pelo gerador de cena
        // como asset — assim o shader entra no build (Shader.Find seria frágil no build).
        [SerializeField] private Material _glowMaterial;

        private readonly Dictionary<int, Transform> _players = new Dictionary<int, Transform>();

        private void Awake()
        {
            if (_glowMaterial != null) ParticleFactory.SetMaterial(_glowMaterial);
        }

        private void OnEnable()
        {
            EventBus<EcoCollected>.Subscribe(OnEcoCollected);
            EventBus<AbilityUsed>.Subscribe(OnAbilityUsed);
        }

        private void OnDisable()
        {
            EventBus<EcoCollected>.Unsubscribe(OnEcoCollected);
            EventBus<AbilityUsed>.Unsubscribe(OnAbilityUsed);
        }

        // Absorção de Eco: motes de Ressonância convergindo no jogador que absorveu.
        private void OnEcoCollected(EcoCollected e)
        {
            Transform p = ResolvePlayer(e.OwnerId);
            if (p == null) return;
            ParticleFactory.Absorb(p.position + Vector3.up * 1.0f, VfxElements.Resonance);
        }

        // Uso de habilidade: burst colorido pelo elemento no ponto de impacto (à frente).
        private void OnAbilityUsed(AbilityUsed e)
        {
            if (e.Ability == null) return;
            Transform p = ResolvePlayer(e.OwnerId);
            if (p == null) return;
            // Espelha o cálculo do impacto no AbilityRunner (origem + frente * alcance/2).
            Vector3 center = p.position + p.forward * (e.Ability.baseRange * 0.5f) + Vector3.up * 0.8f;
            ParticleFactory.Burst(center, VfxElements.ForElement(e.Ability.element), e.Ability.baseRadius);
        }

        // OwnerId → transform do jogador (cache + re-busca por tag se sumiu).
        private Transform ResolvePlayer(int ownerId)
        {
            if (_players.TryGetValue(ownerId, out Transform cached) && cached != null)
                return cached;

            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            foreach (GameObject go in players)
            {
                if (go.TryGetComponent(out Ownable o) && o.OwnerId == ownerId)
                {
                    _players[ownerId] = go.transform;
                    return go.transform;
                }
            }
            // Fallback (solo/single-player): se o OwnerId não casar exatamente (ex.: seam
            // LocalPlayer/Ownable ainda não sincronizado), usa o primeiro jogador da cena.
            return players.Length > 0 ? players[0].transform : null;
        }
    }
}
