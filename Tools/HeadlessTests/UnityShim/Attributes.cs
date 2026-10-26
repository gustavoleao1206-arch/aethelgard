using System;

namespace UnityEngine
{
    // Na Unity os dois atributos se chamam assim mesmo (sem o sufixo "Attribute").
    // Aqui não fazem nada: o Core só precisa que existam para compilar.

    /// <summary>Imitação vazia do <c>UnityEngine.SerializeField</c>.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeField : Attribute
    {
    }

    /// <summary>Imitação vazia do <c>UnityEngine.DisallowMultipleComponent</c>.</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DisallowMultipleComponent : Attribute
    {
    }
}
