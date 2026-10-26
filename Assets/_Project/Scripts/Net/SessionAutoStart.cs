using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using JogoDaTrupe.Core.Session;

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// Sobe a sessão pedida pelo MENU (M-NET-9 P4). No <c>Start</c> da cena de co-op, lê
    /// o <see cref="SessionRequest"/> (gravado pelo menu antes de carregar a cena) e
    /// inicia Host ou Client (com o IP do transporte). Companion no GameObject do
    /// <c>NetworkManager</c>, par do <c>NetHotkeyStarter</c>.
    ///
    /// Convive com o atalho dev H/J: se já está ouvindo (MPPM subiu por H/J), não faz
    /// nada; se o modo é None (abriu a cena direto no editor), também não — os atalhos
    /// seguem disponíveis. Assim o fluxo de BUILD (menu → jogo) e o de DEV (MPPM) coexistem.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class SessionAutoStart : MonoBehaviour
    {
        private void Start()
        {
            NetworkManager nm = GetComponent<NetworkManager>();
            if (nm == null || nm.IsListening) return; // já rodando (ex.: MPPM via H/J)

            switch (SessionRequest.Consume())
            {
                case SessionMode.Host:
                    nm.StartHost();
                    Debug.Log("[Rede] Host iniciado pelo menu.");
                    break;

                case SessionMode.Client:
                    string ip = string.IsNullOrEmpty(SessionRequest.Ip) ? "127.0.0.1" : SessionRequest.Ip;
                    if (nm.TryGetComponent(out UnityTransport transport))
                        transport.SetConnectionData(ip, 7777);
                    nm.StartClient();
                    Debug.Log($"[Rede] Client iniciado pelo menu → {ip}:7777.");
                    break;

                // None: abriu a cena direto (editor/MPPM) — deixa os atalhos H/J agirem.
            }
        }
    }
}
