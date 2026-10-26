using System;
using System.Collections.Generic;

namespace JogoDaTrupe.Core.Saving
{
    /// <summary>
    /// Uma entrada do arquivo de save: a chave do sistema (ISaveable.SaveKey) + o seu
    /// estado já serializado em JSON. Guardar o estado como string permite que cada
    /// sistema serialize/desserialize o SEU próprio tipo, respeitando o JsonUtility
    /// (que não serializa <c>object</c> nem <c>Dictionary</c>).
    /// </summary>
    [Serializable]
    public sealed class SaveEntry
    {
        public string Key;
        public string Json;
    }

    /// <summary>O arquivo de save inteiro: uma lista de entradas (uma por ISaveable).</summary>
    [Serializable]
    public sealed class SaveFile
    {
        public List<SaveEntry> entries = new List<SaveEntry>();

        /// <summary>JSON guardado para a chave, ou null se não houver.</summary>
        public string Find(string key)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && entries[i].Key == key) return entries[i].Json;
            return null;
        }
    }
}
