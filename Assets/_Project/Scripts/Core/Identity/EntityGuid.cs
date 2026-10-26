using UnityEngine;

namespace JogoDaTrupe.Core.Identity
{
    /// <summary>
    /// Identificador estável e único de uma entidade do jogo (GUID em texto).
    ///
    /// (Renomeado de "EntityId" para "EntityGuid": o Unity 6 introduziu um tipo
    /// interno <c>UnityEngine.EntityId</c> e os nomes colidiam, quebrando a
    /// compilação. "EntityGuid" deixa claro que guardamos um GUID e evita o conflito.)
    ///
    /// Tudo que precise ser salvo ou, no futuro, sincronizado pela rede (criatura,
    /// construção, item largado no mundo) deve ter um EntityGuid. O id é gerado uma
    /// única vez e nunca muda — nem entre sessões — para que o save e a rede
    /// consigam reencontrar "a mesma entidade" com segurança.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EntityGuid : MonoBehaviour
    {
        [SerializeField] private string _id;

        /// <summary>O identificador único desta entidade (GUID em texto).</summary>
        public string Value => _id;

        /// <summary>Verdadeiro se já há um id atribuído.</summary>
        public bool HasId => !string.IsNullOrEmpty(_id);

        // No Editor, gera o id ao adicionar/validar o componente.
        private void Reset() => GenerateIfEmpty();
        private void OnValidate() => GenerateIfEmpty();

        // Em runtime/build, Reset e OnValidate NÃO rodam — então entidades criadas em
        // jogo (ex.: estruturas construídas via AddComponent) ficariam com id vazio.
        // Awake garante o id também em runtime, corrigindo esse bug. (Pré-requisito da
        // rede: sem id estável não dá para mapear EntityGuid ↔ NetworkObjectId.)
        private void Awake() => GenerateIfEmpty();

        /// <summary>
        /// Atribui um id específico SE ainda estiver vazio (não sobrescreve um id já
        /// existente). Retorna true se atribuiu. A camada de rede (host) usa isto para
        /// fixar um id escolhido numa entidade recém-instanciada — atribuindo antes de
        /// ela ativar (e rodar o Awake), os clientes adotam o mesmo id do host.
        /// </summary>
        public bool AssignIfEmpty(string id)
        {
            if (HasId || string.IsNullOrEmpty(id)) return false;
            _id = id;
            return true;
        }

        /// <summary>
        /// Garante que há um id (gera um GUID se estiver vazio) e o retorna. Útil para
        /// quem cria a entidade em runtime e precisa do id na hora.
        /// </summary>
        public string EnsureId()
        {
            GenerateIfEmpty();
            return _id;
        }

        private void GenerateIfEmpty()
        {
            if (string.IsNullOrEmpty(_id))
            {
                _id = System.Guid.NewGuid().ToString();
            }
        }
    }
}
