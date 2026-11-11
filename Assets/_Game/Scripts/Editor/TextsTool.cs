using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FutebolDeBotao.Editor
{
    /// <summary>
    /// Menu Idiomas: grava no Resources/Idiomas/textos.json os textos que o jogo pediu e não estão lá (jogue no editor
    /// passando pelas telas, depois use o menu). Entram com o português preenchido e os outros idiomas vazios. O arquivo é
    /// regravado com uma entrada por linha, na ordem em que já estava.
    /// </summary>
    internal static class TextsTool
    {
        private const string FilePath = "Assets/_Game/Resources/Idiomas/textos.json";

        [MenuItem("Futebol de Botão/Idiomas/Gravar textos que faltam")]
        private static void SaveMissing()
        {
            var table = Read();
            var known = new HashSet<string>();
            foreach (var entry in table.textos) known.Add(entry.id);
            int added = 0;
            foreach (var id in Txt.Missing)
            {
                if (string.IsNullOrEmpty(id) || known.Contains(id)) continue;
                table.textos.Add(new Txt.Entry { id = id, pt = id, en = string.Empty, es = string.Empty });
                known.Add(id);
                added++;
            }
            Write(table);
            Txt.Reload();
            Debug.Log($"[Idiomas] {added} texto(s) novo(s) em {FilePath} (preencha o en e o es).");
        }

        [MenuItem("Futebol de Botão/Idiomas/Recarregar textos")]
        private static void ReloadTexts()
        {
            AssetDatabase.ImportAsset(FilePath);
            Txt.Reload();
        }

        private static Txt.Table Read()
        {
            if (!File.Exists(FilePath)) return new Txt.Table();
            var table = JsonUtility.FromJson<Txt.Table>(File.ReadAllText(FilePath));
            table ??= new Txt.Table();
            table.textos ??= new List<Txt.Entry>();
            return table;
        }

        /// <summary>Uma entrada por linha, para ler e editar à mão.</summary>
        private static void Write(Txt.Table table)
        {
            var builder = new StringBuilder();
            builder.Append("{ \"textos\": [\n");
            for (int i = 0; i < table.textos.Count; i++)
            {
                var entry = table.textos[i];
                builder.Append("  { \"id\": ").Append(Quote(entry.id))
                    .Append(", \"pt\": ").Append(Quote(entry.pt))
                    .Append(", \"en\": ").Append(Quote(entry.en))
                    .Append(", \"es\": ").Append(Quote(entry.es)).Append(" }");
                builder.Append(i < table.textos.Count - 1 ? ",\n" : "\n");
            }
            builder.Append("] }\n");
            File.WriteAllText(FilePath, builder.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(FilePath);
        }

        private static string Quote(string value)
        {
            var builder = new StringBuilder("\"");
            foreach (char c in value ?? string.Empty)
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': break;
                    case '\t': builder.Append("\\t"); break;
                    default: builder.Append(c); break;
                }
            }
            return builder.Append('"').ToString();
        }
    }
}
