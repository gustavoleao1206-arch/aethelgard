using System.Collections.Generic;
using UnityEngine;

namespace JogoDaTrupe.Presentation.Vfx
{
    /// <summary>
    /// Fábrica de VFX de partículas POR CÓDIGO (fase de arte). Constrói ParticleSystems
    /// descartáveis (auto-destroem ao terminar): <see cref="Burst"/> (impacto de habilidade,
    /// colorido pelo elemento) e <see cref="Absorb"/> (absorção de Eco — motes de Ressonância
    /// convergindo no jogador).
    ///
    /// A COR vem de um MATERIAL aditivo por cor (shader JogoDaTrupe/ParticleGlow, propriedade
    /// <c>_Color</c>), CACHEADO por cor (≤7 materiais). Não depende da cor de vértice do
    /// ParticleSystem (que pode não chegar a um material custom) — o glow aparece de forma
    /// confiável e é realçado pelo bloom. O <see cref="VfxDirector"/> injeta o material-base
    /// (asset) só para carregar o shader no build; as instâncias por cor derivam do shader dele.
    /// </summary>
    public static class ParticleFactory
    {
        private static Shader _shader;
        private static readonly Dictionary<Color, Material> _matByColor = new Dictionary<Color, Material>();

        /// <summary>Injeta o material-base (do asset) — usamos o SHADER dele por cor.</summary>
        public static void SetMaterial(Material material)
        {
            if (material != null && material.shader != null) _shader = material.shader;
        }

        private static Material MatFor(Color color)
        {
            if (_matByColor.TryGetValue(color, out Material m) && m != null) return m;
            Shader s = _shader != null ? _shader : Shader.Find("JogoDaTrupe/ParticleGlow");
            if (s == null) s = Shader.Find("Sprites/Default");
            m = new Material(s);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            _matByColor[color] = m;
            return m;
        }

        /// <summary>Burst radial de impacto (habilidade), na cor do elemento.</summary>
        public static void Burst(Vector3 position, Color color, float radius)
        {
            const float life = 0.55f;
            const int count = 26;

            var go = new GameObject("VFX_Burst");
            go.SetActive(false);
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.06f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = life;
            main.startSpeed = Mathf.Max(radius, 0.6f) * 3.2f;
            main.startSize = 0.55f;
            main.startColor = color;
            main.gravityModifier = 0.25f;
            main.maxParticles = 128;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            Shrink(ps, 1f, 0f); // some encolhendo
            ApplyRenderer(ps, color);

            go.SetActive(true);
            ps.Play();
            Object.Destroy(go, life + 0.4f);
        }

        /// <summary>Absorção de Eco: motes de Ressonância convergindo/subindo no jogador.</summary>
        public static void Absorb(Vector3 center, Color color)
        {
            const float life = 0.9f;
            const int count = 30;

            var go = new GameObject("VFX_Absorb");
            go.SetActive(false);
            go.transform.position = center;
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.06f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = life;
            main.startSpeed = 0f;
            main.startSize = 0.45f;
            main.startColor = color;
            main.gravityModifier = -0.12f; // sobe de leve
            main.maxParticles = 128;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1.4f; // nasce ao redor do jogador...

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.radial = new ParticleSystem.MinMaxCurve(-2.6f); // ...e converge para o centro

            Shrink(ps, 1f, 0f);
            ApplyRenderer(ps, color);

            go.SetActive(true);
            ps.Play();
            Object.Destroy(go, life + 0.4f);
        }

        /// <summary>Jato de FOGO em cone pra frente — a cuspida do dragão (da boca ao alvo).</summary>
        public static void FireBreath(Vector3 origin, Vector3 direction, Color color)
        {
            const float life = 0.5f;
            const int count = 26;

            var go = new GameObject("VFX_FireBreath");
            go.SetActive(false);
            go.transform.position = origin;
            go.transform.rotation = Quaternion.LookRotation(
                direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector3.forward, Vector3.up);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.06f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = life;
            main.startSpeed = 13f;          // jato rápido pra frente
            main.startSize = 1.1f;
            main.startColor = color;
            main.gravityModifier = -0.05f;  // fogo sobe de leve
            main.maxParticles = 128;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            // Cone emitindo ao longo do +Z local (o GO já aponta pra 'direction').
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 17f;
            shape.radius = 0.3f;

            Shrink(ps, 0.6f, 1.7f); // a labareda CRESCE enquanto avança (bola de fogo abrindo)
            ApplyRenderer(ps, color);

            go.SetActive(true);
            ps.Play();
            Object.Destroy(go, life + 0.4f);
        }

        private static void Shrink(ParticleSystem ps, float start, float end)
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, start, 1f, end));
        }

        private static void ApplyRenderer(ParticleSystem ps, Color color)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = MatFor(color);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = -5f; // desenha um pouco à frente para o glow aparecer
        }
    }
}
