using System;

namespace UnityEngine
{
    // Hierarquia vazia de propósito: existe só para EntityGuid e Ownable (MonoBehaviour do Core)
    // compilarem. Não há GameObject, ciclo de vida (Awake/OnValidate) nem o "null falso" da Unity:
    // teste que precise disso roda só no Test Runner da Unity.

    /// <summary>Imitação vazia do <c>UnityEngine.Object</c>.</summary>
    public class Object
    {
        // O == da Unity tem o "null falso" (objeto destruído ou criado com new compara == null).
        // Aqui ele seria igualdade de referência EM SILÊNCIO — então vira ERRO de compilação:
        // teste que compare objeto da Unity com null só vale no Test Runner da Unity.
        [Obsolete("null falso da Unity: comparar Object com == só no Test Runner da Unity", true)]
        public static bool operator ==(Object x, Object y) => ReferenceEquals(x, y);

        [Obsolete("null falso da Unity: comparar Object com != só no Test Runner da Unity", true)]
        public static bool operator !=(Object x, Object y) => !ReferenceEquals(x, y);

        public override bool Equals(object other) => ReferenceEquals(this, other);
        public override int GetHashCode() => base.GetHashCode();
    }

    /// <summary>Imitação vazia do <c>UnityEngine.Component</c>.</summary>
    public class Component : Object
    {
    }

    /// <summary>Imitação vazia do <c>UnityEngine.MonoBehaviour</c>.</summary>
    public class MonoBehaviour : Component
    {
    }
}
