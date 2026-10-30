using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Tela Estatísticas (do Menu): abas Geral, Fácil, Médio, Difícil e Última partida, com uma lista que rola (nome à
    /// esquerda, valor à direita). Só partidas contra a IA (<see cref="PlayerStats"/>). Esquerda/direita na linha das
    /// abas (ou LB/RB) troca a aba; o analógico direito, PageUp/PageDown, a roda do mouse e arrastar rolam a lista.
    /// "Zerar estatísticas" pergunta antes e apaga tudo. Monta a UI sozinha (como os Controles).
    /// </summary>
    public sealed class StatsScreen : MonoBehaviour
    {
        private static readonly string[] TabNames = { "Geral", "Fácil", "Médio", "Difícil", "Última\npartida" };
        private const int LastTab = 4;
        private const float TabY = 440f;
        private const float TabSpacing = 136f;
        private static readonly Vector2 TabSize = new(128f, 76f);
        private static readonly Vector2 ListCenter = new(0f, -10f);
        private static readonly Vector2 ListSize = new(680f, 800f);
        private const float ListPadding = 18f;
        private const float HeaderHeight = 64f;
        private const float RowHeight = 48f;
        private const float StickScrollSpeed = 900f;
        private const float PageScroll = 400f;
        private static readonly Color HeaderColor = new(1f, 0.85f, 0.25f);
        private static readonly Color StripeColor = new(1f, 1f, 1f, 0.07f);
        private static readonly Color PanelColor = new(0f, 0f, 0f, 0.55f);
        private static readonly Color DimColor = new(0f, 0f, 0f, 0.7f);
        private static readonly Color ShadowColor = new(0f, 0f, 0f, 0.6f);

        [SerializeField] private MenuScreen menu;

        private readonly Button[] tabs = new Button[TabNames.Length];
        private Font font;
        private bool built;
        private int tab;
        private ScrollRect scroll;
        private RectTransform content;
        private MenuNavigator navigator;
        private GameObject dialog;

        public void Configure(MenuScreen menuScreen) => menu = menuScreen;

        /// <summary>Abre na aba Geral, no topo da lista.</summary>
        public void Open()
        {
            Build();
            CloseDialog();
            ShowTab(0);
        }

        private void Update()
        {
            if (!built || (dialog != null && dialog.activeSelf)) return;

            float stick = 0f;
            foreach (var gamepad in Gamepad.all)
            {
                if (gamepad.leftShoulder.wasPressedThisFrame) ShowTab(tab - 1);
                if (gamepad.rightShoulder.wasPressedThisFrame) ShowTab(tab + 1);
                float y = gamepad.rightStick.y.ReadValue();
                if (Mathf.Abs(y) > Mathf.Abs(stick)) stick = y;
            }
            if (Mathf.Abs(stick) > 0.2f) ScrollBy(-stick * StickScrollSpeed * Time.unscaledDeltaTime);

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.pageDownKey.wasPressedThisFrame) ScrollBy(PageScroll);
                if (keyboard.pageUpKey.wasPressedThisFrame) ScrollBy(-PageScroll);
            }
        }

        /// <summary>Rola a lista <paramref name="amount"/> unidades para baixo (negativo: para cima).</summary>
        private void ScrollBy(float amount)
        {
            float overflow = content.rect.height - scroll.viewport.rect.height;
            if (overflow <= 0f) return;
            content.anchoredPosition = new Vector2(0f, Mathf.Clamp(content.anchoredPosition.y + amount, 0f, overflow));
        }

        private void Back()
        {
            if (menu != null) menu.ShowMode();
        }

        // ---- Abas ----

        private void ShowTab(int index)
        {
            tab = ((index % TabNames.Length) + TabNames.Length) % TabNames.Length;
            for (int i = 0; i < tabs.Length; i++)
                ButtonSkin.Apply(tabs[i], i == tab ? ButtonStyle.Yellow : ButtonStyle.Green);
            FillList();
        }

        private void FillList()
        {
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);

            var rows = new List<(string label, string value)>();
            var stats = GameSave.Data.stats;
            string empty = null;
            if (tab == LastTab)
            {
                if (stats.hasLast) AddLast(rows, stats.last);
                else empty = "Termine uma partida contra a IA para ver aqui.";
            }
            else
            {
                var level = tab == 0 ? stats.total : stats.levels[tab - 1];
                if (level.Played > 0) AddLevel(rows, level);
                else empty = tab == 0
                    ? "Jogue contra a IA para ver suas estatísticas."
                    : $"Jogue contra a IA no nível {TabNames[tab]} para ver suas estatísticas.";
            }

            float width = ListSize.x - 2f * ListPadding;
            float y = 0f;
            if (empty != null)
            {
                var text = CreateText(content, "Vazio", empty, 30, FontStyle.Normal, Color.white, Vector2.zero, new Vector2(width - 40f, 200f));
                PlaceTop(text.rectTransform, y, 200f);
                y += 200f;
            }

            int stripe = 0;
            foreach (var (label, value) in rows)
            {
                bool header = value == null;
                float height = header ? HeaderHeight : RowHeight;
                var row = CreateRect(content, label, Vector2.zero, Vector2.zero);
                PlaceTop(row, y, height);
                if (header) stripe = 0;
                else if (stripe++ % 2 == 1)
                {
                    var band = row.gameObject.AddComponent<Image>();
                    band.color = StripeColor;
                    band.raycastTarget = false;
                }

                var name = CreateText(row, "Nome", label, header ? 32 : 26, header ? FontStyle.Bold : FontStyle.Normal,
                    header ? HeaderColor : Color.white, Vector2.zero, Vector2.zero);
                name.alignment = header ? TextAnchor.LowerLeft : TextAnchor.MiddleLeft;
                Stretch(name.rectTransform, new Vector2(0f, 0f), new Vector2(header ? 1f : 0.62f, 1f), 12f);
                if (!header)
                {
                    var amount = CreateText(row, "Valor", value, 26, FontStyle.Bold, Color.white, Vector2.zero, Vector2.zero);
                    amount.alignment = TextAnchor.MiddleRight;
                    Stretch(amount.rectTransform, new Vector2(0.55f, 0f), Vector2.one, 12f);
                }
                y += height;
            }

            content.sizeDelta = new Vector2(0f, y + 10f);
            content.anchoredPosition = Vector2.zero;
            scroll.velocity = Vector2.zero;
        }

        private static void AddLevel(List<(string, string)> rows, SaveData.LevelStats s)
        {
            int played = s.Played;
            int finished = s.Finished;
            var n = s.numbers;

            rows.Add(("Resultado", null));
            rows.Add(("Partidas", played.ToString()));
            rows.Add(("Vitórias", WithPercent(s.wins, played)));
            rows.Add(("Empates", WithPercent(s.draws, played)));
            rows.Add(("Derrotas", WithPercent(s.losses, played)));
            rows.Add(("Abandonadas", WithPercent(s.abandoned, played)));
            rows.Add(("Sequência de vitórias", s.streak.ToString()));
            rows.Add(("Recorde de sequência", s.bestStreak.ToString()));

            rows.Add(("Gols", null));
            rows.Add(("Gols feitos", n.goals.ToString()));
            rows.Add(("Gols sofridos", n.conceded.ToString()));
            rows.Add(("Gols contra", n.ownGoals.ToString()));
            int balance = n.goals - n.conceded;
            rows.Add(("Saldo de gols", balance > 0 ? $"+{balance}" : balance.ToString()));
            rows.Add(("Sofridos por gol feito", n.goals > 0 ? FormatNumber((float)n.conceded / n.goals, 2) : "–"));
            rows.Add(("Média de gols feitos", Average(n.goals, finished)));
            rows.Add(("Média de gols sofridos", Average(n.conceded, finished)));

            rows.Add(("Jogo (total e por partida)", null));
            rows.Add(("Chutes a gol (Pro Gol!)", WithAverage(n.shots, finished)));
            rows.Add(("Conversão", Conversion(n)));
            rows.Add(("Faltas", WithAverage(n.fouls, finished)));
            rows.Add(("Cartões amarelos", WithAverage(n.yellow, finished)));
            rows.Add(("Cartões vermelhos", WithAverage(n.red, finished)));
            rows.Add(("Toques na bola", WithAverage(n.touches, finished)));
            rows.Add(("Toques errados", WithAverage(n.misses, finished)));
        }

        private static void AddLast(List<(string, string)> rows, SaveData.LastMatchData last)
        {
            var n = last.numbers;
            string result = last.playerScore > last.aiScore ? "Vitória"
                : last.playerScore < last.aiScore ? "Derrota" : "Empate";
            if (last.walkover) result += " por W.O.";

            rows.Add(("Partida", null));
            rows.Add(("Nível", AiDifficulty.DisplayName((AiLevel)Mathf.Clamp(last.level, 0, 2))));
            rows.Add(("Placar (você x IA)", $"{last.playerScore} x {last.aiScore}"));
            rows.Add(("Resultado", result));

            rows.Add(("Gols", null));
            rows.Add(("Gols feitos", n.goals.ToString()));
            rows.Add(("Gols sofridos", n.conceded.ToString()));
            rows.Add(("Gols contra", n.ownGoals.ToString()));

            rows.Add(("Jogo", null));
            rows.Add(("Chutes a gol (Pro Gol!)", n.shots.ToString()));
            rows.Add(("Conversão", Conversion(n)));
            rows.Add(("Faltas", n.fouls.ToString()));
            rows.Add(("Cartões amarelos", n.yellow.ToString()));
            rows.Add(("Cartões vermelhos", n.red.ToString()));
            rows.Add(("Toques na bola", n.touches.ToString()));
            rows.Add(("Toques errados", n.misses.ToString()));
        }

        // ---- Números ----

        private static string FormatNumber(float value, int places) =>
            value.ToString(places == 0 ? "0" : places == 1 ? "0.0" : "0.00", CultureInfo.InvariantCulture).Replace('.', ',');

        private static string WithPercent(int count, int total) =>
            total > 0 ? $"{count} ({FormatNumber(100f * count / total, 0)}%)" : count.ToString();

        private static string Average(int total, int matches) => matches > 0 ? FormatNumber((float)total / matches, 1) : "–";

        private static string WithAverage(int total, int matches) =>
            matches > 0 ? $"{total} ({FormatNumber((float)total / matches, 1)})" : total.ToString();

        /// <summary>Gols que saíram de um "Pro Gol!" sobre os "Pro Gol!" avisados.</summary>
        private static string Conversion(SaveData.MatchNumbers n) =>
            n.shots > 0 ? $"{FormatNumber(100f * n.shotGoals / n.shots, 0)}%" : "–";

        // ---- Zerar ----

        private void AskReset()
        {
            navigator.enabled = false;
            dialog.SetActive(true);
        }

        private void CloseDialog()
        {
            if (dialog == null) return;
            dialog.SetActive(false);
            navigator.enabled = true;
        }

        private void ConfirmReset()
        {
            PlayerStats.ResetAll();
            CloseDialog();
            ShowTab(tab);
        }

        // ---- UI ----

        private void Build()
        {
            if (built) return;
            built = true;
            font = GameFont.Get();
            var root = (RectTransform)transform;

            CreateText(root, "Título", "Estatísticas", 52, FontStyle.Bold, Color.white, new Vector2(0f, 545f), new Vector2(680f, 90f));

            var tabRow = CreateRect(root, "Abas", new Vector2(0f, TabY), new Vector2(TabSpacing * TabNames.Length, TabSize.y));
            var tabAreas = new RectTransform[TabNames.Length];
            for (int i = 0; i < TabNames.Length; i++)
            {
                int index = i;
                float x = (i - (TabNames.Length - 1) * 0.5f) * TabSpacing;
                tabs[i] = CreateButton(tabRow, TabNames[i], TabNames[i], new Vector2(x, 0f), TabSize, () => ShowTab(index));
                var label = tabs[i].GetComponentInChildren<Text>();
                label.fontSize = i == LastTab ? 20 : 26;
                label.lineSpacing = 0.85f;
                tabAreas[i] = (RectTransform)tabs[i].transform;
            }
            // Esquerda/direita na linha das abas: botões sem imagem que só trocam a aba.
            var previousTab = CreateHiddenButton(tabRow, "Aba anterior", () => ShowTab(tab - 1));
            var nextTab = CreateHiddenButton(tabRow, "Próxima aba", () => ShowTab(tab + 1));
            var tabEntry = tabRow.gameObject.AddComponent<MenuEntry>();
            tabEntry.Configure(null, previousTab, nextTab, tabAreas);

            // Lista: fundo da HUD, a parte de dentro rola.
            var panel = CreateRect(root, "Lista", ListCenter, ListSize).gameObject.AddComponent<Image>();
            var panelSprite = ButtonArt.Find(ButtonSkin.HudPanelName);
            if (panelSprite != null)
            {
                panel.sprite = panelSprite;
                panel.type = Image.Type.Sliced;
            }
            else panel.color = PanelColor;

            var viewport = CreateRect(panel.transform, "Área", Vector2.zero, Vector2.zero);
            Stretch(viewport, Vector2.zero, Vector2.one, ListPadding);
            viewport.gameObject.AddComponent<RectMask2D>();
            // Imagem transparente: arrastar em qualquer lugar da lista rola.
            var catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = Color.clear;

            content = CreateRect(viewport, "Conteúdo", Vector2.zero, Vector2.zero);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);

            scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;

            var back = CreateButton(root, "Voltar", "Voltar", new Vector2(-180f, -500f), new Vector2(260f, 80f), Back, ButtonStyle.Red);
            var reset = CreateButton(root, "Zerar", "Zerar estatísticas", new Vector2(150f, -500f), new Vector2(340f, 80f), AskReset, ButtonStyle.Brown);

            navigator = gameObject.AddComponent<MenuNavigator>();
            navigator.Configure(new[] { tabEntry, Entry(back), Entry(reset) }, back);

            BuildDialog(root);
        }

        private void BuildDialog(RectTransform root)
        {
            var dim = CreateRect(root, "Confirmar zerar", Vector2.zero, Vector2.zero);
            Stretch(dim, Vector2.zero, Vector2.one, 0f);
            // Tampa a tela: os cliques não passam para a lista e os botões de baixo.
            var shade = dim.gameObject.AddComponent<Image>();
            shade.color = DimColor;

            var box = CreateRect(dim, "Caixa", Vector2.zero, new Vector2(620f, 380f)).gameObject.AddComponent<Image>();
            var boxSprite = ButtonArt.Find(ButtonSkin.HudPanelName);
            if (boxSprite != null)
            {
                box.sprite = boxSprite;
                box.type = Image.Type.Sliced;
            }
            else box.color = PanelColor;

            CreateText(box.transform, "Pergunta", "Apagar todas as estatísticas?\nNão dá para desfazer.", 34, FontStyle.Bold, Color.white,
                new Vector2(0f, 70f), new Vector2(560f, 160f));
            var erase = CreateButton(box.transform, "Apagar", "Apagar", new Vector2(-140f, -100f), new Vector2(240f, 80f), ConfirmReset, ButtonStyle.Red);
            var cancel = CreateButton(box.transform, "Cancelar", "Cancelar", new Vector2(140f, -100f), new Vector2(240f, 80f), CloseDialog);

            dialog = dim.gameObject;
            dialog.SetActive(false);
            // Começa no Cancelar; Q/B (e o Voltar do Android) também cancela.
            var dialogNavigator = dialog.AddComponent<MenuNavigator>();
            dialogNavigator.Configure(new[] { Entry(erase), Entry(cancel) }, cancel, 1);
            dialogNavigator.BackAction = CloseDialog;
        }

        private static MenuEntry Entry(Button button)
        {
            var entry = button.gameObject.AddComponent<MenuEntry>();
            entry.Configure(button);
            return entry;
        }

        private static void PlaceTop(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(0f, height);
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, float inset)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static RectTransform CreateRect(Transform parent, string objectName, Vector2 position, Vector2 size)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private Text CreateText(Transform parent, string objectName, string content, int size, FontStyle style, Color color,
            Vector2 position, Vector2 box)
        {
            var text = CreateRect(parent, objectName, position, box).gameObject.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.gameObject.AddComponent<Shadow>().effectColor = ShadowColor;
            return text;
        }

        private Button CreateButton(Transform parent, string objectName, string label, Vector2 position, Vector2 size, UnityAction action,
            ButtonStyle style = ButtonStyle.Green)
        {
            var rect = CreateRect(parent, objectName, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(action);
            var text = CreateText(rect, "Texto", label, 30, FontStyle.Bold, ButtonSkin.TextColor(style), Vector2.zero, size);
            Destroy(text.GetComponent<Shadow>());
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, 0f);
            ButtonSkin.Apply(button, style);
            return button;
        }

        /// <summary>Botão sem imagem nem clique: só para o teclado/controle apertar (MenuEntry).</summary>
        private static Button CreateHiddenButton(Transform parent, string objectName, UnityAction action)
        {
            var button = CreateRect(parent, objectName, Vector2.zero, Vector2.zero).gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(action);
            return button;
        }
    }
}
