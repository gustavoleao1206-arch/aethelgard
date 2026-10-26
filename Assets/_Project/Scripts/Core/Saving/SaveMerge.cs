using System.Collections.Generic;

namespace JogoDaTrupe.Core.Saving
{
    /// <summary>
    /// Fusão PURA de saves (M-NET-9 P1, testável sem I/O). O host salva o estado dos
    /// jogadores CONECTADOS no momento; as chaves de quem está OFFLINE (um assento que
    /// saiu) devem sobreviver no arquivo — senão salvar apagaria a progressão de um amigo
    /// que se ausentou. Regra: as entradas FRESCAS sobrescrevem por chave; as antigas
    /// ausentes nas frescas são preservadas.
    /// </summary>
    public static class SaveMerge
    {
        public static SaveFile Merge(SaveFile existing, IReadOnlyList<SaveEntry> fresh)
        {
            var result = new SaveFile();
            var freshKeys = new HashSet<string>();

            if (fresh != null)
            {
                for (int i = 0; i < fresh.Count; i++)
                {
                    SaveEntry e = fresh[i];
                    if (e == null || string.IsNullOrEmpty(e.Key)) continue;
                    if (freshKeys.Add(e.Key)) result.entries.Add(e); // dedup por chave (a 1ª vence)
                }
            }

            if (existing != null && existing.entries != null)
            {
                for (int i = 0; i < existing.entries.Count; i++)
                {
                    SaveEntry old = existing.entries[i];
                    if (old == null || string.IsNullOrEmpty(old.Key)) continue;
                    if (!freshKeys.Contains(old.Key)) result.entries.Add(old); // preserva o offline
                }
            }

            return result;
        }
    }
}
