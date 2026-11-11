using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FutebolDeBotao.Editor
{
    /// <summary>
    /// Janela Idiomas/Textos: a tabela do Resources/Idiomas/textos.json para ajustes pontuais (português, inglês e
    /// espanhol lado a lado, com busca e filtros) e a aba Itens, com os nomes dos assets (camisas, bolas, mesas, feltros,
    /// cores do time, cores da trave e torneios) para traduzir. O nome escrito no asset é o id em português. Nada vai para
    /// o arquivo até apertar Gravar; com o jogo rodando, a tela troca na hora.
    /// </summary>
    internal sealed class TextsWindow : EditorWindow
    {
        private enum Filter
        {
            Todos,
            FaltaIngles,
            FaltaEspanhol,
            Longos
        }

        private static readonly string[] FilterNames = { "Todos", "Falta inglês", "Falta espanhol", "Longos" };
        private static readonly string[] TabNames = { "Textos", "Itens" };

        /// <summary>Tradução com mais que isso do tamanho do português (e 4 letras a mais) aparece como longa.</summary>
        private const float LongRatio = 1.3f;

        private Txt.Table table;
        private Dictionary<string, Txt.Entry> byId;
        private bool dirty;
        private int tab;
        private Filter filter;
        private string search = string.Empty;
        private Vector2 scroll;
        private List<(string group, string name, Object asset)> items;

        private GUIStyle areaStyle;
        private GUIStyle longStyle;

        [MenuItem("Futebol de Botão/Idiomas/Textos")]
        private static void Open() => GetWindow<TextsWindow>("Textos").minSize = new Vector2(700f, 300f);

        private void OnEnable() => Load();

        private void Load()
        {
            table = TextsTool.Read();
            byId = new Dictionary<string, Txt.Entry>();
            foreach (var entry in table.textos)
                if (entry != null && !string.IsNullOrEmpty(entry.id)) byId[entry.id] = entry;
            items = null;
            dirty = false;
        }

        private void OnGUI()
        {
            if (table == null) Load();
            areaStyle ??= new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            longStyle ??= new GUIStyle(areaStyle) { normal = { textColor = new Color(1f, 0.8f, 0.2f) } };

            DrawToolbar();
            EditorGUILayout.Space(4f);
            if (tab == 0) DrawTexts();
            else DrawItems();
        }

        // ---- Barra de cima ----

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                int newTab = GUILayout.Toolbar(tab, TabNames, EditorStyles.toolbarButton, GUILayout.Width(160f));
                if (newTab != tab)
                {
                    tab = newTab;
                    scroll = Vector2.zero;
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Pegar os que faltam", "Textos que o jogo pediu (no Play) e não estão no arquivo."),
                        EditorStyles.toolbarButton))
                    AddMissing();
                if (GUILayout.Button(new GUIContent("Recarregar", "Lê o arquivo de novo (perde o que não foi gravado)."),
                        EditorStyles.toolbarButton) &&
                    (!dirty || EditorUtility.DisplayDialog("Textos", "Descartar as mudanças que não foram gravadas?", "Descartar", "Cancelar")))
                    Load();
                using (new EditorGUI.DisabledScope(!dirty))
                    if (GUILayout.Button(dirty ? "Gravar *" : "Gravar", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                        Save();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                search = EditorGUILayout.TextField("Buscar", search);
                filter = (Filter)EditorGUILayout.Popup((int)filter, FilterNames, GUILayout.Width(120f));
            }
        }

        // ---- Aba Textos ----

        private void DrawTexts()
        {
            int shown = 0;
            DrawHeader();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            Txt.Entry remove = null;
            foreach (var entry in table.textos)
            {
                if (entry == null || !Matches(entry.id, entry)) continue;
                shown++;
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (DrawRow(entry, true)) remove = entry;
                }
            }
            EditorGUILayout.EndScrollView();
            if (remove != null && EditorUtility.DisplayDialog("Textos", $"Apagar \"{remove.id}\"?", "Apagar", "Cancelar"))
            {
                table.textos.Remove(remove);
                byId.Remove(remove.id);
                dirty = true;
            }
            EditorGUILayout.LabelField($"{shown} de {table.textos.Count} textos", EditorStyles.miniLabel);
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                float width = ColumnWidth();
                GUILayout.Label("Português", EditorStyles.boldLabel, GUILayout.Width(width));
                GUILayout.Label("English", EditorStyles.boldLabel, GUILayout.Width(width));
                GUILayout.Label("Español", EditorStyles.boldLabel, GUILayout.Width(width));
            }
        }

        private float ColumnWidth() => Mathf.Max(120f, (position.width - 60f) / 3f);

        /// <summary>As três colunas de um texto. Devolve verdadeiro se apertou para apagar.</summary>
        private bool DrawRow(Txt.Entry entry, bool canDelete)
        {
            float width = ColumnWidth();
            EditorGUI.BeginChangeCheck();
            var pt = EditorGUILayout.TextArea(entry.pt ?? string.Empty, areaStyle, GUILayout.Width(width));
            var en = EditorGUILayout.TextArea(entry.en ?? string.Empty, IsLong(entry.pt, entry.en) ? longStyle : areaStyle, GUILayout.Width(width));
            var es = EditorGUILayout.TextArea(entry.es ?? string.Empty, IsLong(entry.pt, entry.es) ? longStyle : areaStyle, GUILayout.Width(width));
            if (EditorGUI.EndChangeCheck())
            {
                entry.pt = pt;
                entry.en = en;
                entry.es = es;
                dirty = true;
            }
            if (!canDelete) return false;
            return GUILayout.Button(new GUIContent("X", $"Apagar. Id: {entry.id}"), GUILayout.Width(22f));
        }

        private static bool IsLong(string pt, string other)
        {
            if (string.IsNullOrEmpty(pt) || string.IsNullOrEmpty(other)) return false;
            return other.Length > pt.Length * LongRatio && other.Length - pt.Length >= 4;
        }

        private bool Matches(string name, Txt.Entry entry)
        {
            if (!string.IsNullOrEmpty(search))
            {
                bool found = Contains(name, search) || (entry != null && (Contains(entry.pt, search) || Contains(entry.en, search) || Contains(entry.es, search)));
                if (!found) return false;
            }
            return filter switch
            {
                Filter.FaltaIngles => entry == null || string.IsNullOrEmpty(entry.en),
                Filter.FaltaEspanhol => entry == null || string.IsNullOrEmpty(entry.es),
                Filter.Longos => entry != null && (IsLong(entry.pt, entry.en) || IsLong(entry.pt, entry.es)),
                _ => true
            };
        }

        private static bool Contains(string value, string part) =>
            !string.IsNullOrEmpty(value) && value.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0;

        // ---- Aba Itens ----

        private void DrawItems()
        {
            items ??= CollectItems();
            DrawHeader();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            string group = null;
            foreach (var (itemGroup, name, asset) in items)
            {
                byId.TryGetValue(name, out var entry);
                if (!Matches(name, entry)) continue;
                if (itemGroup != group)
                {
                    group = itemGroup;
                    EditorGUILayout.Space(6f);
                    EditorGUILayout.LabelField(group, EditorStyles.boldLabel);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    float width = ColumnWidth();
                    // O português é o nome no asset: muda-se lá. Clicar mostra o asset.
                    if (GUILayout.Button(new GUIContent(name, "O nome está no asset; clique para achar."), EditorStyles.label, GUILayout.Width(width)))
                        EditorGUIUtility.PingObject(asset);
                    EditorGUI.BeginChangeCheck();
                    var en = EditorGUILayout.TextField(entry?.en ?? string.Empty, GUILayout.Width(width));
                    var es = EditorGUILayout.TextField(entry?.es ?? string.Empty, GUILayout.Width(width));
                    if (EditorGUI.EndChangeCheck())
                    {
                        entry ??= Add(name);
                        entry.en = en;
                        entry.es = es;
                        dirty = true;
                    }
                }
            }
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("Procurar os assets de novo")) items = null;
        }

        private static List<(string, string, Object)> CollectItems()
        {
            var list = new List<(string, string, Object)>();
            var seen = new HashSet<string>();

            void AddItem(string group, string name, Object asset)
            {
                if (string.IsNullOrEmpty(name) || !seen.Add(group + "\n" + name)) return;
                list.Add((group, name, asset));
            }

            foreach (var shirt in ShirtPattern.All()) AddItem("Camisas", shirt.displayName, shirt);
            foreach (var ball in BallTheme.All()) AddItem("Bolas", ball.displayName, ball);
            foreach (var table in TableTheme.All()) AddItem("Mesas", table.displayName, table);
            foreach (var felt in FeltTheme.All()) AddItem("Feltros", felt.displayName, felt);
            var palette = TeamColorPalette.Load();
            if (palette != null)
                foreach (var color in palette.colors)
                    if (color != null) AddItem("Cores do time", color.name, palette);
            foreach (var table in TableTheme.All())
                foreach (var goal in table.goalColors)
                    if (goal != null) AddItem("Cores da trave", goal.name, table);
            foreach (var config in TournamentConfig.All()) AddItem("Torneios", config.displayName, config);
            return list;
        }

        // ---- Arquivo ----

        private Txt.Entry Add(string id)
        {
            var entry = new Txt.Entry { id = id, pt = id, en = string.Empty, es = string.Empty };
            table.textos.Add(entry);
            byId[id] = entry;
            return entry;
        }

        private void AddMissing()
        {
            int added = 0;
            foreach (var id in Txt.Missing)
            {
                if (string.IsNullOrEmpty(id) || byId.ContainsKey(id)) continue;
                Add(id);
                added++;
            }
            if (added > 0) dirty = true;
            ShowNotification(new GUIContent(added > 0 ? $"{added} texto(s) novo(s). Grave para guardar." : "Nenhum texto faltando."));
        }

        private void Save()
        {
            TextsTool.Write(table);
            Txt.Reload();
            dirty = false;
            ShowNotification(new GUIContent("Gravado"));
        }
    }
}
