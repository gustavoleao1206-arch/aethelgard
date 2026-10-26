namespace JogoDaTrupe.Core.Saving
{
    /// <summary>
    /// Implementado por qualquer sistema que precise salvar e carregar seu estado.
    ///
    /// O SaveSystem (a ser criado na Fase 5) percorre todos os ISaveable, junta
    /// o estado de cada um e grava num único arquivo. Para incluir um sistema
    /// novo no save, basta implementar esta interface — nada mais precisa mudar.
    /// </summary>
    public interface ISaveable
    {
        /// <summary>Chave única que identifica este pedaço dentro do arquivo de save.</summary>
        string SaveKey { get; }

        /// <summary>Devolve o estado atual (um objeto serializável) para ser salvo.</summary>
        object CaptureState();

        /// <summary>Restaura o estado a partir do que foi lido do save.</summary>
        void RestoreState(object state);
    }
}
