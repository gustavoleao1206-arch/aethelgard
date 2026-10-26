using UnityEngine;
using Unity.Netcode;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

namespace JogoDaTrupe.Net
{
    /// <summary>
    /// Atalho de teclado DEV-ONLY para subir a sessão de rede sem o Inspector
    /// (M-NET-7). Motivação: no Multiplayer Play Mode a janela do jogador VIRTUAL nem
    /// sempre expõe o Inspector do NetworkManager de forma clicável (a janela é de outro
    /// processo; o popup de layout fica bloqueado). Com este componente basta focar a
    /// Game view de cada janela e teclar:
    ///   • <b>H</b> — Start Host (janela principal)
    ///   • <b>J</b> — Start Client (janela do Player 2/3/4)
    /// Teclas escolhidas por NÃO colidirem com o gameplay (WASD/E/I/C/B/Q/F/V/1–5).
    ///
    /// A classe existe sempre (para a cena não ficar com "missing script" num build),
    /// mas a lógica só roda no Editor (<c>#if UNITY_EDITOR</c>) — em build é inerte. É um
    /// par dos botões do Inspector, não um substituto: a verificação de rede segue sendo
    /// "MPPM manual + logs" (decisão do M-NET-5 P3), só que agora dirigível por teclado.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class NetHotkeyStarter : MonoBehaviour
    {
        private NetworkManager _manager;

        private void Awake() => _manager = GetComponent<NetworkManager>();

#if UNITY_EDITOR
        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || _manager == null || _manager.IsListening) return;

            if (kb.hKey.wasPressedThisFrame)
            {
                _manager.StartHost();
                Debug.Log("[Rede] Start Host (atalho H).");
            }
            else if (kb.jKey.wasPressedThisFrame)
            {
                _manager.StartClient();
                Debug.Log("[Rede] Start Client (atalho J).");
            }
        }
#endif
    }
}
