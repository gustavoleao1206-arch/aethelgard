namespace JogoDaTrupe.Core.View
{
    /// <summary>
    /// Seam Apresentação → Jogo: o MODO da câmera LOCAL. A Presentation (`PlayerCamera`)
    /// escreve; o Gameplay (`PlayerController`) lê para girar o corpo junto com a câmera em
    /// 1ª pessoa (estilo Skyrim) — sem o Gameplay depender da Presentation (seria referência
    /// circular, já que Presentation → Gameplay).
    ///
    /// É estado LOCAL do processo (como <c>LocalPlayer</c>): cada cliente tem a sua câmera, e o
    /// `PlayerController` só roda no player do DONO. Default <c>false</c> = 3ª pessoa, que é o
    /// comportamento de sempre (solo e testes sem câmera continuam iguais).
    /// </summary>
    public static class CameraView
    {
        /// <summary>A câmera local está em 1ª pessoa?</summary>
        public static bool IsFirstPerson { get; set; }
    }
}
