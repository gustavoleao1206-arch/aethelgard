using System.Collections.Generic;
using UnityEngine;
using JogoDaTrupe.Core.Combat;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Core.Timing;
using JogoDaTrupe.Core.World;
using JogoDaTrupe.Gameplay.Building;
using JogoDaTrupe.Gameplay.Capture;
using JogoDaTrupe.Gameplay.Combat;
using JogoDaTrupe.Gameplay.Incorporation;
using JogoDaTrupe.Gameplay.Interaction;

namespace JogoDaTrupe.Gameplay.Characters
{
    /// <summary>
    /// RESPAWN do jogador + fim do "zumbi jogável" (M-NET-9 P0). Sem o respawn, morrer
    /// deixava o boneco imóvel mas AINDA respondendo ao input; agora a morte desliga o
    /// controle e a AUTORIDADE revive o jogador no próprio ponto de spawn após um tempo.
    ///
    /// Desenho sem RPC (o relay já resolve o cross-machine):
    /// • Ao morrer (<see cref="EntityDied"/> do MEU dono), TODA máquina desliga os
    ///   controles que estavam LIGADOS — e a lista do que desliguei codifica "eu
    ///   controlo este player" (no boneco remoto nada está ligado: o NetworkPlayerSetup
    ///   já o desligou no spawn). Só a autoridade agenda o renascer (o servidor decide o
    ///   tempo de todos; no cliente ele chega pelo <see cref="PlayerRespawned"/> relayado).
    /// • Ao renascer, quem CONTROLA o player se teleporta ao spawn e re-liga o controle;
    ///   as demais máquinas ignoram (a posição vem do NetworkTransform).
    ///
    /// Em SOLO é idêntico (autoridade local; o <see cref="PlayerRespawned"/> publicado
    /// localmente reativa o próprio controle). Melhora o singleplayer de graça.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Ownable))]
    [RequireComponent(typeof(Health))]
    public sealed class PlayerRespawner : MonoBehaviour
    {
        [Tooltip("Segundos entre morrer e renascer (🎨 5s no hub — M-NET-9).")]
        [SerializeField] private float _respawnDelay = 5f;

        private Ownable _ownable;
        private Health _health;
        private CharacterController _controller;
        private Behaviour[] _control;
        private readonly List<Behaviour> _disabled = new List<Behaviour>();

        private bool _pending;
        private float _respawnAt;

        private void Awake()
        {
            _ownable = GetComponent<Ownable>();
            _health = GetComponent<Health>();
            _controller = GetComponent<CharacterController>();

            // Os atores que consomem o input do jogador (mexem/atacam/interagem). Desligar
            // PlayerInputReader não bastaria: suas propriedades leem o input AO VIVO, então
            // são os CONSUMIDORES que precisam parar. Mesmo conjunto do DisableLocalOnly.
            _control = new Behaviour[]
            {
                GetComponent<PlayerController>(),
                GetComponent<GroundCheck>(),
                GetComponent<MeleeAttack>(),
                GetComponent<AbilityRunner>(),
                GetComponent<Interactor>(),
                GetComponent<BuildController>(),
                GetComponent<Synchronizer>()
            };
        }

        private void OnEnable()
        {
            EventBus<EntityDied>.Subscribe(OnEntityDied);
            EventBus<PlayerRespawned>.Subscribe(OnPlayerRespawned);
        }

        private void OnDisable()
        {
            EventBus<EntityDied>.Unsubscribe(OnEntityDied);
            EventBus<PlayerRespawned>.Unsubscribe(OnPlayerRespawned);
        }

        private void OnEntityDied(EntityDied e)
        {
            if (e.OwnerId != _ownable.OwnerId) return;

            DisableControl(); // fim do zumbi (só desliga o que estava ligado nesta máquina)

            // Só a autoridade agenda; no cliente o renascer vem pelo relay.
            if (SimulationAuthority.IsAuthority)
            {
                _pending = true;
                _respawnAt = GameTime.Time + Mathf.Max(0f, _respawnDelay);
            }
        }

        private void Update()
        {
            if (!_pending || !SimulationAuthority.IsAuthority) return;
            if (GameTime.Time < _respawnAt) return;
            _pending = false;

            _health.Revive(); // vida cheia (replica no co-op pelo NetworkHealth)
            EventBus<PlayerRespawned>.Publish(new PlayerRespawned { OwnerId = _ownable.OwnerId });
        }

        private void OnPlayerRespawned(PlayerRespawned e)
        {
            if (e.OwnerId != _ownable.OwnerId) return;
            if (_disabled.Count == 0) return; // não controlo este player → posição vem do NetworkTransform

            Teleport(MapLayout.SpawnFor(_ownable.OwnerId));
            EnableControl();
        }

        private void DisableControl()
        {
            _disabled.Clear();
            for (int i = 0; i < _control.Length; i++)
            {
                Behaviour b = _control[i];
                if (b != null && b.enabled) { b.enabled = false; _disabled.Add(b); }
            }
        }

        private void EnableControl()
        {
            for (int i = 0; i < _disabled.Count; i++)
                if (_disabled[i] != null) _disabled[i].enabled = true;
            _disabled.Clear();
        }

        // Teleporte seguro: o CharacterController ignora escrita direta em transform
        // enquanto ativo — desliga, move, religa.
        private void Teleport(Vector3 position)
        {
            if (_controller != null) _controller.enabled = false;
            transform.position = position;
            if (_controller != null) _controller.enabled = true;
        }
    }
}
