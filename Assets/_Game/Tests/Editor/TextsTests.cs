using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace FutebolDeBotao.Tests
{
    /// <summary>
    /// Resources/Idiomas/textos.json: abre, não repete id, as traduções têm os mesmos {0} {1} do português,
    /// e todo texto que o código pede com Txt.Get("...") ou Txt.Format("...") existe no arquivo.
    /// </summary>
    public class TextsTests
    {
        private static Txt.Table table;

        private static Txt.Table Table()
        {
            if (table != null) return table;
            var asset = Resources.Load<TextAsset>("Idiomas/textos");
            Assert.IsNotNull(asset, "Resources/Idiomas/textos.json não encontrado");
            table = JsonUtility.FromJson<Txt.Table>(asset.text);
            Assert.IsNotNull(table?.textos, "textos.json não abriu");
            return table;
        }

        private static readonly Regex Placeholder = new(@"\{\d+(?:[,:][^}]*)?\}");

        private static List<string> Placeholders(string text)
        {
            var list = new List<string>();
            foreach (Match m in Placeholder.Matches(text ?? string.Empty))
                if (!list.Contains(m.Value)) list.Add(m.Value);
            list.Sort(System.StringComparer.Ordinal);
            return list;
        }

        [Test]
        public void IdsSemRepetir_E_PortuguesPreenchido()
        {
            var ids = new HashSet<string>();
            foreach (var entry in Table().textos)
            {
                Assert.IsFalse(string.IsNullOrEmpty(entry.id), "texto sem id");
                Assert.IsTrue(ids.Add(entry.id), $"id repetido: {entry.id}");
                Assert.IsFalse(string.IsNullOrEmpty(entry.pt), $"sem português: {entry.id}");
            }
        }

        [Test]
        public void Traducoes_TemOsMesmosNumeros()
        {
            var problems = new StringBuilder();
            foreach (var entry in Table().textos)
            {
                var expected = string.Join(" ", Placeholders(entry.id));
                foreach (var (language, text) in new[] { ("pt", entry.pt), ("en", entry.en), ("es", entry.es) })
                {
                    if (string.IsNullOrEmpty(text)) continue;
                    var got = string.Join(" ", Placeholders(text));
                    if (got != expected) problems.AppendLine($"{language}: \"{entry.id}\" tem [{got}], devia ter [{expected}]");
                }
            }
            Assert.IsEmpty(problems.ToString(), problems.ToString());
        }

        /// <summary>Textos ainda sem inglês ou espanhol (os nomes de itens ainda vão ser revisados): só avisa no console.</summary>
        [Test]
        public void TextosSemTraducao_SoAviso()
        {
            var en = new List<string>();
            var es = new List<string>();
            foreach (var entry in Table().textos)
            {
                if (string.IsNullOrEmpty(entry.en)) en.Add(entry.id);
                if (string.IsNullOrEmpty(entry.es)) es.Add(entry.id);
            }
            if (en.Count > 0) Debug.Log($"[Testes] {en.Count} textos sem inglês: {string.Join(" | ", en)}");
            if (es.Count > 0) Debug.Log($"[Testes] {es.Count} textos sem espanhol: {string.Join(" | ", es)}");
            Assert.Pass($"{en.Count} sem inglês, {es.Count} sem espanhol (lista no console).");
        }

        // Txt.Get("...") ou Txt.Format("...", ...) com o texto inteiro numa string só (as montadas com + ficam de fora).
        private static readonly Regex Call = new(@"Txt\.(?:Get|Format)\(\s*""((?:[^""\\]|\\.)*)""\s*[,)]");

        [Test]
        public void TodoTextoDoCodigo_EstaNoArquivo()
        {
            var ids = new HashSet<string>();
            foreach (var entry in Table().textos) ids.Add(entry.id);

            var missing = new StringBuilder();
            string scripts = Path.Combine(Application.dataPath, "_Game", "Scripts");
            foreach (var file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                string code = File.ReadAllText(file);
                foreach (Match m in Call.Matches(code))
                {
                    string id = Regex.Unescape(m.Groups[1].Value);
                    if (!ids.Contains(id)) missing.AppendLine($"{Path.GetFileName(file)}: \"{id}\"");
                }
            }
            Assert.IsEmpty(missing.ToString(), "Textos do código que faltam no textos.json (menu Idiomas > Textos):\n" + missing);
        }
    }
}
