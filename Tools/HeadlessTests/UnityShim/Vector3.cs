using System;
using System.Globalization;

namespace UnityEngine
{
    /// <summary>
    /// Imitação mínima do <c>UnityEngine.Vector3</c> para rodar o Core fora da Unity.
    /// Só os membros usados pelo Core e pelos testes headless, com as fórmulas da Unity
    /// (UnityCsReference). Membro faltando = erro de compilação = adicione igual ao da Unity.
    /// </summary>
    public struct Vector3 : IEquatable<Vector3>
    {
        // Mesmo limiar da Unity: é o que torna o == aproximado.
        public const float kEpsilon = 0.00001F;

        public float x;
        public float y;
        public float z;

        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Vector3 zero => new Vector3(0F, 0F, 0F);

        public static Vector3 Cross(Vector3 lhs, Vector3 rhs)
        {
            return new Vector3(
                lhs.y * rhs.z - lhs.z * rhs.y,
                lhs.z * rhs.x - lhs.x * rhs.z,
                lhs.x * rhs.y - lhs.y * rhs.x);
        }

        // A Unity calcula em float com a raiz de System.Math (double) convertida para float.
        public static float Distance(Vector3 a, Vector3 b)
        {
            float diffX = a.x - b.x;
            float diffY = a.y - b.y;
            float diffZ = a.z - b.z;
            return (float)Math.Sqrt(diffX * diffX + diffY * diffY + diffZ * diffZ);
        }

        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator /(Vector3 a, float d) => new Vector3(a.x / d, a.y / d, a.z / d);

        // Na Unity o == é APROXIMADO (distância ao quadrado < kEpsilon²) e o Equals é EXATO.
        // O HouseBlueprintTests compara pivôs com Equals; trocar um pelo outro mudaria o resultado.
        // O == não é usado hoje: fica como EXCEÇÃO DELIBERADA, já com a semântica certa.
        public static bool operator ==(Vector3 lhs, Vector3 rhs)
        {
            float diffX = lhs.x - rhs.x;
            float diffY = lhs.y - rhs.y;
            float diffZ = lhs.z - rhs.z;
            return (diffX * diffX + diffY * diffY + diffZ * diffZ) < kEpsilon * kEpsilon;
        }

        public static bool operator !=(Vector3 lhs, Vector3 rhs) => !(lhs == rhs);

        public bool Equals(Vector3 other) => x == other.x && y == other.y && z == other.z;

        public override bool Equals(object other) => other is Vector3 v && Equals(v);

        // Mesma mistura de bits da Unity (obrigatório ao sobrescrever Equals).
        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2);

        // Igual ao da Unity (formato F2, cultura invariante). É herdado de object: se faltasse,
        // não daria erro — devolveria o nome do tipo EM SILÊNCIO.
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0}, {1}, {2})",
            x.ToString("F2", CultureInfo.InvariantCulture.NumberFormat),
            y.ToString("F2", CultureInfo.InvariantCulture.NumberFormat),
            z.ToString("F2", CultureInfo.InvariantCulture.NumberFormat));
    }
}
