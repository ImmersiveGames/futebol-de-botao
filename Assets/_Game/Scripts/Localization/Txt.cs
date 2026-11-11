using System;
using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Textos do jogo em vários idiomas. O id de cada texto é o próprio texto em português (o código continua legível);
    /// as traduções ficam em Resources/Idiomas/textos.json, uma entrada por texto com os campos pt, en e es. Texto sem
    /// tradução (ou campo vazio) aparece em português. Frases com números usam {0}, {1}... (<see cref="Format"/>).
    /// O idioma vem da tela Configurações (vazio = Automático: português num aparelho em português, senão inglês).
    /// No editor, os ids pedidos que não estão no arquivo ficam em <see cref="Missing"/> (menu Idiomas grava no json).
    /// </summary>
    public static class Txt
    {
        public const string Portuguese = "pt";
        public const string English = "en";
        public const string Spanish = "es";
        private const string ResourcePath = "Idiomas/textos";

        [Serializable]
        public sealed class Entry
        {
            public string id;
            public string pt;
            public string en;
            public string es;

            public string Get(string language) => language switch
            {
                English => en,
                Spanish => es,
                _ => pt
            };
        }

        [Serializable]
        public sealed class Table
        {
            public List<Entry> textos = new();
        }

        private static Dictionary<string, Entry> entries;
        private static string current;
        private static readonly HashSet<string> missing = new();

        /// <summary>O idioma mudou (Configurações): quem mostra texto atualiza.</summary>
        public static event Action Changed;

        /// <summary>Ids pedidos que não estão no textos.json (só no editor).</summary>
        public static IReadOnlyCollection<string> Missing => missing;

        /// <summary>Idioma em uso ("pt", "en" ou "es").</summary>
        public static string Language => current ??= Resolve(Choice);

        /// <summary>Escolha guardada: vazio = Automático.</summary>
        public static string Choice
        {
            get => GameSave.Data.settings.language ?? string.Empty;
            set
            {
                GameSave.Data.settings.language = value ?? string.Empty;
                string language = Resolve(value);
                if (language == current) return;
                current = language;
                Changed?.Invoke();
            }
        }

        /// <summary>Idiomas que dá para escolher: português, inglês e os outros que já tiverem tradução no arquivo.</summary>
        public static List<string> Available()
        {
            var list = new List<string> { Portuguese, English };
            Load();
            foreach (var entry in entries.Values)
                if (!string.IsNullOrEmpty(entry.es))
                {
                    list.Add(Spanish);
                    break;
                }
            return list;
        }

        /// <summary>Nome do idioma escrito nele mesmo.</summary>
        public static string NativeName(string language) => language switch
        {
            English => "English",
            Spanish => "Español",
            _ => "Português"
        };

        /// <summary>O texto no idioma em uso (o próprio id, se não houver tradução).</summary>
        public static string Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            Load();
            if (!entries.TryGetValue(id, out var entry))
            {
                if (Application.isEditor && HasLetter(id)) missing.Add(id);
                return id;
            }
            string text = entry.Get(Language);
            if (string.IsNullOrEmpty(text)) text = entry.pt;
            return string.IsNullOrEmpty(text) ? id : text;
        }

        /// <summary>Frase com números ou nomes: <c>Txt.Format("Faltam {0} toques", 2)</c>.</summary>
        public static string Format(string id, params object[] args)
        {
            try { return string.Format(Get(id), args); }
            catch (FormatException) { return string.Format(id, args); }
        }

        /// <summary>Lê o arquivo de novo (o menu Idiomas usa depois de gravar).</summary>
        public static void Reload()
        {
            entries = null;
            Load();
            Changed?.Invoke();
        }

        private static void Load()
        {
            if (entries != null) return;
            entries = new Dictionary<string, Entry>();
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null) return;
            Table table;
            try { table = JsonUtility.FromJson<Table>(asset.text); }
            catch (Exception e)
            {
                Debug.LogWarning($"[Idiomas] textos.json com erro: {e.Message}");
                return;
            }
            if (table?.textos == null) return;
            foreach (var entry in table.textos)
                if (entry != null && !string.IsNullOrEmpty(entry.id)) entries[entry.id] = entry;
        }

        private static bool HasLetter(string value)
        {
            foreach (char c in value)
                if (char.IsLetter(c)) return true;
            return false;
        }

        private static string Resolve(string choice)
        {
            if (choice == Portuguese || choice == English || choice == Spanish) return choice;
            return Application.systemLanguage == SystemLanguage.Portuguese ? Portuguese : English;
        }

        // Com o "Enter Play Mode" sem recarregar o domínio, os estáticos sobrevivem entre execuções no editor.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            entries = null;
            current = null;
            Changed = null;
        }
    }
}
