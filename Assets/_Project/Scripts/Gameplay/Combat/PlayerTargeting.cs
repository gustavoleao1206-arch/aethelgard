using System.Collections.Generic;
using UnityEngine;
using JogoDaTrupe.Core.Combat;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Core.Timing;

namespace JogoDaTrupe.Gameplay.Combat
{
    /// <summary>
    /// Seleção de alvo da IA (M-NET-6 P2): mantém como alvo o jogador VIVO MAIS PERTO,
    /// reavaliando com intervalo (0,5 s — barato e suficiente). Com 2+ jogadores no
    /// co-op, o inimigo caça quem está perto de verdade (antes mirava "o primeiro
    /// achado" e ignorava o outro); se o alvo morrer, ele o SOLTA e procura outro
    /// (nada de abraçar defunto). Também cobre o spawn tardio da cena de rede
    /// (jogadores nascem depois da cena — Bug #9).
    ///
    /// Classe simples (não-MonoBehaviour) COMPARTILHADA por EnemyController e
    /// BossController — evita duplicar a busca nos dois cérebros (DRY). A matemática
    /// pura do "mais perto" vive em <see cref="TargetMath"/> (testada).
    /// </summary>
    public sealed class PlayerTargeting
    {
        private const float SearchInterval = 0.5f;

        private readonly List<GameObject> _candidates = new List<GameObject>(4);
        private readonly List<Vector3> _positions = new List<Vector3>(4);
        private float _nextSearch;

        /// <summary>Transform do alvo atual (nulo sem alvo; vira "nulo Unity" se destruído).</summary>
        public Transform Target { get; private set; }

        /// <summary>Vida do alvo atual (para atacar).</summary>
        public IDamageable TargetDamageable { get; private set; }

        /// <summary>Dono do alvo atual — para filtrar eventos por-jogador (ex.: ritmo de
        /// ataque conta SÓ o alvo). <see cref="Ownable.NoOwner"/> sem alvo.</summary>
        public int TargetOwnerId { get; private set; } = Ownable.NoOwner;

        /// <summary>Reavalia o alvo (com intervalo) a partir da posição do inimigo.</summary>
        public void Refresh(Vector3 origin)
        {
            if (GameTime.Time < _nextSearch) return;
            _nextSearch = GameTime.Time + SearchInterval;

            _candidates.Clear();
            _positions.Clear();
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            foreach (GameObject p in players)
            {
                if (p == null) continue;
                // Morto não é alvo — o inimigo solta e vai atrás de quem ainda luta.
                if (p.TryGetComponent(out IDamageable dmg) && dmg.IsDead) continue;
                _candidates.Add(p);
                _positions.Add(p.transform.position);
            }

            int best = TargetMath.NearestIndex(origin, _positions);
            if (best < 0)
            {
                Target = null;
                TargetDamageable = null;
                TargetOwnerId = Ownable.NoOwner;
                return;
            }

            GameObject chosen = _candidates[best];
            Target = chosen.transform;
            chosen.TryGetComponent(out IDamageable damageable);
            TargetDamageable = damageable;
            TargetOwnerId = chosen.TryGetComponent(out Ownable o) ? o.OwnerId : Ownable.LocalPlayerId;
        }
    }
}
