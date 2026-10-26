using JogoDaTrupe.Data;

namespace JogoDaTrupe.Gameplay.Incorporation
{
    /// <summary>
    /// Lógica PURA da Maestria (sem MonoBehaviour, testável sem abrir o jogo).
    /// Dada a quantidade de derrotas de uma criatura e seus limiares, decide qual
    /// camada (Vestígio/Ressonância/Núcleo) está liberada.
    ///
    /// Maestria é DETERMINÍSTICA (sem sorte): atingiu o limiar, liberou a camada.
    /// Ver 01_GameDesign/Incorporacao.md (seção 3).
    /// </summary>
    public static class MasteryMath
    {
        /// <summary>
        /// Retorna true e a camada mais alta liberada para o nº de derrotas dado.
        /// Retorna false se nenhuma camada foi liberada ainda (ou limiares inválidos).
        /// <paramref name="thresholds"/> = [Vestígio, Ressonância, Núcleo] (derrotas necessárias).
        /// </summary>
        public static bool TryHighestLayer(int defeats, int[] thresholds, out IncorporationLayer layer)
        {
            layer = IncorporationLayer.Vestigio;
            if (thresholds == null || thresholds.Length == 0) return false;

            int unlockedIndex = -1;
            // Percorre na ordem das camadas; o índice (0,1,2) coincide com o valor do enum.
            int count = thresholds.Length < 3 ? thresholds.Length : 3;
            for (int i = 0; i < count; i++)
            {
                if (defeats >= thresholds[i]) unlockedIndex = i;
            }

            if (unlockedIndex < 0) return false;

            layer = (IncorporationLayer)unlockedIndex;
            return true;
        }
    }
}
