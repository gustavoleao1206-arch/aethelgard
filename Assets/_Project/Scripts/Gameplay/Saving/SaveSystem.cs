using System.Collections.Generic;
using System.IO;
using UnityEngine;
using JogoDaTrupe.Core.Events;
using JogoDaTrupe.Core.Ownership;
using JogoDaTrupe.Core.Saving;
using JogoDaTrupe.Data;
using JogoDaTrupe.Input;

namespace JogoDaTrupe.Gameplay.Saving
{
    /// <summary>
    /// Salva e carrega o progresso. Junta o estado de todos os <see cref="ISaveable"/>
    /// da cena, serializa cada um em JSON e grava num único arquivo (em
    /// <c>Application.persistentDataPath</c>). Carregar lê o arquivo e devolve a cada
    /// sistema o seu pedaço (pela chave).
    ///
    /// SAVE DO HOST (M-NET-9 P1): só a AUTORIDADE salva/carrega — o F5/F9 do cliente
    /// vira um toast "só o anfitrião" (o mundo pertence ao host — decisão do Diretor).
    /// No host, o estado de TODOS os jogadores já é a verdade (server-authoritative,
    /// M-NET-8), então varrer os ISaveable da cena captura a progressão de todos; o load
    /// republica os eventos de cada sistema → os companions re-replicam aos clientes. O
    /// arquivo faz MERGE (a progressão de um assento OFFLINE não é apagada — <see
    /// cref="SaveMerge"/>).
    ///
    /// Convenção: o <c>object</c> do ISaveable trafega como STRING JSON — cada sistema
    /// serializa/desserializa o seu próprio tipo. Cada chave inclui o OwnerId (co-op).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SaveSystem : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private DefinitionRegistry _registry;
        [SerializeField] private string _fileName = "slice_save.json";

        private const string HostOnlyMsg = "Só o anfitrião salva/carrega o mundo.";
        private float _nextInputSearch;

        private string FilePath => Path.Combine(Application.persistentDataPath, _fileName);

        private void Awake()
        {
            // Liga o registro de definições (o save resolve ids → fichas por ele).
            if (_registry != null) DefinitionRegistry.Active = _registry;
        }

        private void Update()
        {
            // Fix da pendência antiga (_input nulo no co-op): re-busca o
            // PlayerInputReader do jogador LOCAL (o único HABILITADO — os remotos são
            // desligados pelo NetworkPlayerSetup) até achá-lo (padrão FindPlayer/Bug #9).
            if (_input == null || !_input.isActiveAndEnabled) ResolveLocalInput();
            if (_input == null) return;

            if (_input.SavePressedThisFrame) Save();
            else if (_input.LoadPressedThisFrame) Load();
        }

        private void ResolveLocalInput()
        {
            if (Time.time < _nextInputSearch) return;
            _nextInputSearch = Time.time + 0.5f;

            PlayerInputReader[] readers = FindObjectsByType<PlayerInputReader>();
            for (int i = 0; i < readers.Length; i++)
                if (readers[i] != null && readers[i].isActiveAndEnabled) { _input = readers[i]; return; }
        }

        public void Save()
        {
            if (!SimulationAuthority.IsAuthority)
            {
                EventBus<GameSaved>.Publish(new GameSaved { Ok = false, Message = HostOnlyMsg });
                return;
            }

            var fresh = new List<SaveEntry>();
            foreach (ISaveable s in FindSaveables())
                fresh.Add(new SaveEntry { Key = s.SaveKey, Json = JsonUtility.ToJson(s.CaptureState()) });

            SaveFile existing = File.Exists(FilePath)
                ? JsonUtility.FromJson<SaveFile>(File.ReadAllText(FilePath)) : null;
            SaveFile merged = SaveMerge.Merge(existing, fresh);

            File.WriteAllText(FilePath, JsonUtility.ToJson(merged, true));
            Debug.Log($"[Save] Jogo salvo em {FilePath} ({merged.entries.Count} entradas).");
            EventBus<GameSaved>.Publish(new GameSaved { Ok = true });
        }

        public void Load()
        {
            if (!SimulationAuthority.IsAuthority)
            {
                EventBus<GameLoaded>.Publish(new GameLoaded { Ok = false, Message = HostOnlyMsg });
                return;
            }

            if (!File.Exists(FilePath))
            {
                Debug.LogWarning("[Save] Nenhum save encontrado em " + FilePath);
                EventBus<GameLoaded>.Publish(new GameLoaded { Ok = false });
                return;
            }

            SaveFile file = JsonUtility.FromJson<SaveFile>(File.ReadAllText(FilePath));
            if (file == null) { EventBus<GameLoaded>.Publish(new GameLoaded { Ok = false }); return; }

            foreach (ISaveable s in FindSaveables())
            {
                string json = file.Find(s.SaveKey);
                if (json != null) s.RestoreState(json); // o ISaveable desserializa o próprio tipo
            }

            Debug.Log("[Save] Jogo carregado de " + FilePath);
            EventBus<GameLoaded>.Publish(new GameLoaded { Ok = true });
        }

        // Todos os ISaveable da cena (varre só ao salvar/carregar, não por frame).
        private static IEnumerable<ISaveable> FindSaveables()
        {
            // Overload sem parâmetro: a versão com FindObjectsSortMode foi deprecada na Unity 6.5.
            MonoBehaviour[] all = FindObjectsByType<MonoBehaviour>();
            for (int i = 0; i < all.Length; i++)
                if (all[i] is ISaveable s) yield return s;
        }
    }
}
