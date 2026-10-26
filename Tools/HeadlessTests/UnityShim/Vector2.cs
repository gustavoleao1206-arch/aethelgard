using System;
using System.Globalization;

namespace UnityEngine
{
    /// <summary>
    /// Imitação mínima do <c>UnityEngine.Vector2</c> para rodar o Core fora da Unity.
    /// Só tem os membros que o Core e os testes headless usam; o resto da API fica de fora
    /// de propósito (membro faltando = erro de compilação = adicione com a semântica da Unity).
    /// As fórmulas copiam as da Unity (UnityCsReference), não a versão "óbvia".
    /// </summary>
    public struct Vector2 : IEquatable<Vector2>
    {
        // Mesmo limiar da Unity: é o que torna o == aproximado.
        public const float kEpsilon = 0.00001F;

        public float x;
        public float y;

        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public static Vector2 zero => new Vector2(0F, 0F);

        // A Unity calcula em float com a raiz de System.Math (double) convertida para float.
        public float magnitude => (float)Math.Sqrt(x * x + y * y);
        public float sqrMagnitude => x * x + y * y;

        public static float Distance(Vector2 a, Vector2 b)
        {
            float diffX = a.x - b.x;
            float diffY = a.y - b.y;
            return (float)Math.Sqrt(diffX * diffX + diffY * diffY);
        }

        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator *(Vector2 a, float d) => new Vector2(a.x * d, a.y * d);
        public static Vector2 operator /(Vector2 a, float d) => new Vector2(a.x / d, a.y / d);

        // Na Unity o == é APROXIMADO (distância ao quadrado < kEpsilon²) e o Equals é EXATO.
        // Nenhum teste usa o == de Vector2 hoje: ele fica como EXCEÇÃO DELIBERADA à regra "só o
        // que é usado", já com a semântica certa, para ninguém reimplementá-lo como exato depois.
        public static bool operator ==(Vector2 lhs, Vector2 rhs)
        {
            float diffX = lhs.x - rhs.x;
            float diffY = lhs.y - rhs.y;
            return (diffX * diffX + diffY * diffY) < kEpsilon * kEpsilon;
        }

        public static bool operator !=(Vector2 lhs, Vector2 rhs) => !(lhs == rhs);

        public bool Equals(Vector2 other) => x == other.x && y == other.y;

        public override bool Equals(object other) => other is Vector2 v && Equals(v);

        // Mesma mistura de bits da Unity (obrigatório ao sobrescrever Equals).
        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2);

        // Igual ao da Unity (formato F2, cultura invariante). É herdado de object: se faltasse,
        // não daria erro — devolveria o nome do tipo EM SILÊNCIO.
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0}, {1})",
            x.ToString("F2", CultureInfo.InvariantCulture.NumberFormat),
            y.ToString("F2", CultureInfo.InvariantCulture.NumberFormat));
    }
}
