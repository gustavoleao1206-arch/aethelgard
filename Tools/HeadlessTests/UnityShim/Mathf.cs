using System;

namespace UnityEngine
{
    /// <summary>
    /// Imitação mínima do <c>UnityEngine.Mathf</c>. Só as funções que o Core e os testes
    /// headless usam, escritas como na Unity (UnityCsReference): as funções transcendentes
    /// passam por System.Math em double e voltam para float — não por MathF. Isso reproduz a
    /// FUNÇÃO da Unity, não o arredondamento das EXPRESSÕES: o Mono do Editor calcula float em
    /// double e o .NET 8 arredonda a cada operação, então contas compostas podem diferir em
    /// ULPs. Min/Max usam comparação simples (e não Math.Min) porque é assim que a Unity trata NaN.
    /// As versões int de Min/Max/Abs existem de propósito: sem elas, uma chamada com int cairia
    /// EM SILÊNCIO na versão float (Mathf.Max(3, 1) / 2 daria 1.5f aqui e 1 na Unity).
    /// </summary>
    public struct Mathf
    {
        public const float PI = (float)Math.PI;

        // Mesma expressão da Unity: o compilador dobra a constante do mesmo jeito.
        // (Deg2Rad não é usado pelo Core hoje; fica público porque o Rad2Deg sai dele, como na Unity.)
        public const float Deg2Rad = PI * 2F / 360F;
        public const float Rad2Deg = 1F / Deg2Rad;

        public static float Sin(float f) => (float)Math.Sin(f);
        public static float Cos(float f) => (float)Math.Cos(f);
        public static float Atan(float f) => (float)Math.Atan(f);
        public static float Sqrt(float f) => (float)Math.Sqrt(f);
        public static float Abs(float f) => (float)Math.Abs(f);
        public static int Abs(int value) => Math.Abs(value);

        public static float Min(float a, float b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;

        public static int FloorToInt(float f) => (int)Math.Floor(f);
        public static int CeilToInt(float f) => (int)Math.Ceiling(f);

        public static int Clamp(int value, int min, int max)
        {
            if (value < min)
                value = min;
            else if (value > max)
                value = max;
            return value;
        }

        public static float Clamp01(float value)
        {
            if (value < 0F)
                return 0F;
            if (value > 1F)
                return 1F;
            return value;
        }
    }
}
