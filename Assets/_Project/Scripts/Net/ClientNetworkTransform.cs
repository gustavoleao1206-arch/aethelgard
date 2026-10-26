using UnityEngine;
using Unity.Netcode.Components;

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// <see cref="NetworkTransform"/> com autoridade do DONO (não do servidor): cada
    /// jogador escreve a própria posição e os demais clientes a recebem. É o modelo mais
    /// simples e responsivo para co-op entre amigos (sem latência de input para o próprio
    /// jogador). O modelo server-authoritative com client prediction fica como refinamento
    /// futuro (ver PlanoTecnico_Coop.md).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClientNetworkTransform : NetworkTransform
    {
        // false = autoridade do dono (o cliente que possui escreve a transform).
        protected override bool OnIsServerAuthoritative() => false;
    }
}
