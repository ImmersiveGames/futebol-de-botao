using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Tela do Torneio (no Menu), com três páginas montadas aqui mesmo:
    /// <list type="bullet">
    /// <item>Torneio em andamento: Continuar, Novo torneio ou Voltar (Jogar → Torneio com um guardado).</item>
    /// <item>Chave: uma aba por fase (Quartas, Semi, Final...), os jogos empilhados (o de cima é o mandante), o jogo do
    /// jogador em destaque, e Jogar (o próximo jogo dele) ou Resultado (quando ele terminou).</item>
    /// <item>Fim do torneio: troféu, colocação e moedas (das partidas e o prêmio), Ver chave e Menu.</item>
    /// </list>
    /// Esquerda/direita na linha das abas (ou LB/RB) troca a fase; a lista rola como a das Estatísticas.
    /// </summary>
    public sealed class TournamentScreen : MonoBehaviour
    {
        private const float TabY = 400f;
        private const float TabSpacing = 140f;
        private static readonly Vector2 TabSize = new(132f, 72f);
        private static readonly Vector2 ListCenter = new(0f, -40f);
        private static readonly Vector2 ListSize = new(680f, 760f);
        private const float ListPadding = 18f;
        private const float CardHeight = 132f;
        private const float CardGap = 20f;
        private const float LabelHeight = 44f;
        private const float StickScrollSpeed = 900f;
        private static readonly Color HighlightColor = new(1f, 0.85f, 0.25f);
        private static readonly Color CardColor = new(1f, 1f, 1f, 0.08f);
        private static readonly Color PlayerCardColor = new(1f, 0.85f, 0.25f, 0.18f);
        private static readonly Color LoserColor = new(1f, 1f, 1f, 0.55f);
        private static readonly Color PanelColor = new(0f, 0f, 0f, 0.55f);
        private static readonly Color ShadowColor = new(0f, 0f, 0f, 0.6f);
        private static readonly Color[] TrophyColors = { new(1f, 0.8f, 0.2f), new(0.82f, 0.84f, 0.9f), new(0.8f, 0.5f, 0.25f) };

        [SerializeField] private MenuScreen menu;

        private Font font;
        private bool built;
        private int tab;
        private int rounds;
        private GameObject askPage, bracketPage, endPage;
        private Text titleText, statusText;
        private readonly List<Button> tabs = new();
        private RectTransform tabRow;
        private MenuEntry tabEntry;
        private Button previousTab, nextTab;
        private ScrollRect scroll;
        private RectTransform content;
        private Button actionButton;
        private Text actionLabel;
        private MenuNavigator bracketNavigator;
        private Text endName, endPosition, endCoins;
        private Image endTrophy;

        public void Configure(MenuScreen menuScreen) => menu = menuScreen;

        /// <summary>Jogar → Torneio com um torneio em andamento: pergunta se continua ou começa outro.</summary>
        public void OpenAsk()
        {
            Build();
            ShowPage(askPage);
        }

        /// <summary>
        /// Abre a chave na fase do próximo jogo do jogador; com o torneio terminado (e acabou de terminar, voltando do
        /// Resultado), abre o fim do torneio.
        /// </summary>
        public void Open()
        {
            Build();
            if (!Tournament.Run.exists)
            {
                Back();
                return;
            }
            if (!Tournament.InProgress) ShowEnd();
            else ShowBracket();
        }

        private void Back()
        {
            if (menu != null) menu.ShowMode();
        }

        private void Continue() => ShowBracket();

        private void NewTournament()
        {
            if (menu != null) menu.NewTournament();
        }

        private void Play()
        {
            if (menu != null) menu.PlayTournamentMatch();
        }

        private void Update()
        {
            if (!built || bracketPage == null || !bracketPage.activeSelf) return;
            float stick = 0f;
            foreach (var gamepad in Gamepad.all)
            {
                if (gamepad.leftShoulder.wasPressedThisFrame) ShowTab(tab - 1);
                if (gamepad.rightShoulder.wasPressedThisFrame) ShowTab(tab + 1);
                float y = gamepad.rightStick.y.ReadValue();
                if (Mathf.Abs(y) > Mathf.Abs(stick)) stick = y;
            }
            if (Mathf.Abs(stick) > 0.2f) ScrollBy(-stick * StickScrollSpeed * Time.unscaledDeltaTime);
        }

        private void ScrollBy(float amount)
        {
            float overflow = content.rect.height - scroll.viewport.rect.height;
            if (overflow <= 0f) return;
            content.anchoredPosition = new Vector2(0f, Mathf.Clamp(content.anchoredPosition.y + amount, 0f, overflow));
        }

        private void ShowPage(GameObject page)
        {
            foreach (var other in new[] { askPage, bracketPage, endPage })
                if (other != null) other.SetActive(other == page);
        }

        // ---- Chave ----

        private void ShowBracket()
        {
            ShowPage(bracketPage);
            var run = Tournament.Run;
            var config = Tournament.Config;
            rounds = Tournament.Rounds;
            titleText.text = config.displayName;

            var next = Tournament.PlayerMatch();
            string level = AiDifficulty.DisplayName((AiLevel)Mathf.Clamp(run.level, 0, AiDifficulty.LevelCount - 1));
            if (next != null)
            {
                int opponent = next.home == Tournament.Player ? next.away : next.home;
                string where = next.home == Tournament.Player ? "em casa" : "fora";
                string round = next.third ? "Disputa de 3º lugar" : Tournament.RoundName(next.round, rounds);
                statusText.text = $"{level} · {round}: contra {Tournament.TeamName(opponent)} ({where})";
            }
            else statusText.text = $"{level} · {Tournament.PositionText(run)}";

            actionLabel.text = next != null ? "Jogar" : "Resultado";
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(next != null ? (UnityAction)Play : ShowEnd);

            BuildTabs();
            ShowTab(next != null ? next.round : Tournament.CurrentRound());
        }

        private void BuildTabs()
        {
            foreach (var old in tabs) Destroy(old.gameObject);
            tabs.Clear();
            var areas = new List<RectTransform>();
            for (int i = 0; i < rounds; i++)
            {
                int index = i;
                float x = (i - (rounds - 1) * 0.5f) * TabSpacing;
                var button = CreateButton(tabRow, $"Aba {i}", Tournament.TabName(i, rounds), new Vector2(x, 0f), TabSize, () => ShowTab(index));
                button.GetComponentInChildren<Text>().fontSize = 26;
                tabs.Add(button);
                areas.Add((RectTransform)button.transform);
            }
            tabEntry.Configure(null, previousTab, nextTab, areas.ToArray());
        }

        private void ShowTab(int index)
        {
            if (rounds <= 0) return;
            tab = Mathf.Clamp(index, 0, rounds - 1);
            for (int i = 0; i < tabs.Count; i++)
                ButtonSkin.Apply(tabs[i], i == tab ? ButtonStyle.Yellow : ButtonStyle.Green);
            FillList();
        }

        private void FillList()
        {
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            float width = ListSize.x - 2f * ListPadding;
            float y = 0f;

            var matches = Tournament.MatchesOf(tab);
            if (matches.Count == 0)
            {
                var empty = CreateText(content, "Vazio", "Os jogos desta fase saem quando a fase anterior terminar.", 30,
                    FontStyle.Normal, Color.white, Vector2.zero, new Vector2(width - 40f, 200f));
                PlaceTop(empty.rectTransform, 0f, 200f);
                y = 200f;
            }

            bool finalRound = tab == rounds - 1;
            foreach (var match in matches)
            {
                if (finalRound)
                {
                    var label = CreateText(content, "Nome do jogo", match.third ? "Disputa de 3º lugar" : "Final", 30,
                        FontStyle.Bold, HighlightColor, Vector2.zero, Vector2.zero);
                    label.alignment = TextAnchor.LowerLeft;
                    PlaceTop(label.rectTransform, y, LabelHeight);
                    y += LabelHeight;
                }
                BuildCard(match, y);
                y += CardHeight + CardGap;
            }

            content.sizeDelta = new Vector2(0f, y);
            content.anchoredPosition = Vector2.zero;
            scroll.velocity = Vector2.zero;
        }

        /// <summary>Um jogo: mandante em cima, visitante embaixo, cada um com a cor do time, o nome e os gols.</summary>
        private void BuildCard(SaveData.TournamentMatch match, float top)
        {
            var card = CreateRect(content, $"Jogo {match.order + 1}", Vector2.zero, Vector2.zero);
            PlaceTop(card, top, CardHeight);
            var background = card.gameObject.AddComponent<Image>();
            background.color = match.Has(Tournament.Player) ? PlayerCardColor : CardColor;
            background.raycastTarget = false;

            float rowHeight = CardHeight * 0.5f;
            BuildTeamRow(card, match, true, 0f, rowHeight);
            BuildTeamRow(card, match, false, rowHeight, rowHeight);
        }

        private void BuildTeamRow(RectTransform card, SaveData.TournamentMatch match, bool home, float top, float height)
        {
            int team = home ? match.home : match.away;
            var data = Tournament.TeamOf(team);
            bool player = team == Tournament.Player;
            bool lost = match.played && match.Winner != team;

            var row = CreateRect(card, home ? "Mandante" : "Visitante", Vector2.zero, Vector2.zero);
            PlaceTop(row, top, height);

            // Cor do time: a principal com um miolo da segunda.
            var chip = CreateRect(row, "Cor", Vector2.zero, new Vector2(40f, 40f));
            chip.anchorMin = chip.anchorMax = new Vector2(0f, 0.5f);
            chip.anchoredPosition = new Vector2(36f, 0f);
            var outer = chip.gameObject.AddComponent<Image>();
            outer.color = data.PrimaryColor;
            outer.raycastTarget = false;
            var inner = CreateRect(chip, "Cor 2", Vector2.zero, new Vector2(16f, 16f)).gameObject.AddComponent<Image>();
            inner.color = data.SecondaryColor;
            inner.raycastTarget = false;

            Color color = player ? HighlightColor : lost ? LoserColor : Color.white;
            var name = CreateText(row, "Nome", data.TeamName, 30, match.played && !lost ? FontStyle.Bold : FontStyle.Normal, color,
                Vector2.zero, Vector2.zero);
            name.alignment = TextAnchor.MiddleLeft;
            Stretch(name.rectTransform, Vector2.zero, new Vector2(0.75f, 1f), 0f);
            name.rectTransform.offsetMin = new Vector2(76f, 0f);

            string goals = "–";
            if (match.played)
            {
                goals = (home ? match.homeGoals : match.awayGoals).ToString();
                if (match.HasPenalties) goals += $" ({(home ? match.homePenalties : match.awayPenalties)})";
                if (match.walkover && lost) goals += " W.O.";
            }
            var score = CreateText(row, "Gols", goals, 30, FontStyle.Bold, color, Vector2.zero, Vector2.zero);
            score.alignment = TextAnchor.MiddleRight;
            Stretch(score.rectTransform, new Vector2(0.6f, 0f), Vector2.one, 0f);
            score.rectTransform.offsetMax = new Vector2(-20f, 0f);
        }

        // ---- Fim ----

        private void ShowEnd()
        {
            ShowPage(endPage);
            var run = Tournament.Run;
            var config = Tournament.Config;
            endName.text = config.displayName;
            endPosition.text = Tournament.PositionText(run);

            var sprite = run.abandoned ? null : config.TrophyFor(run.position);
            if (sprite == null && !run.abandoned && run.position >= 1 && run.position <= TrophyColors.Length)
                sprite = PlaceholderTrophy(run.position);
            endTrophy.sprite = sprite;
            endTrophy.enabled = sprite != null;

            var lines = new List<string> { $"Moedas das partidas: +{run.matchCoins}" };
            if (run.prize > 0) lines.Add($"Prêmio: +{run.prize}");
            lines.Add($"Saldo: {Shop.Coins}");
            endCoins.text = string.Join("\n", lines);
        }

        private static readonly Dictionary<int, Sprite> placeholders = new();

        /// <summary>Taça simples em pixel art (ouro, prata ou bronze) até chegar a arte do troféu.</summary>
        private static Sprite PlaceholderTrophy(int position)
        {
            if (placeholders.TryGetValue(position, out var cached) && cached != null) return cached;
            string[] shape =
            {
                "................",
                "..############..",
                ".#.##########.#.",
                "#..##########..#",
                "#..##########..#",
                ".#.##########.#.",
                "..############..",
                "...##########...",
                "....########....",
                "......####......",
                ".......##.......",
                ".......##.......",
                "......####......",
                "....########....",
                "....########....",
                "................",
            };
            var color = TrophyColors[position - 1];
            var dark = Color.Lerp(color, Color.black, 0.35f);
            var light = Color.Lerp(color, Color.white, 0.45f);
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int row = 0; row < 16; row++)
                for (int x = 0; x < 16; x++)
                {
                    bool filled = shape[row][x] == '#';
                    // Luz à esquerda, sombra à direita (como a luz da mesa).
                    var pixel = !filled ? Color.clear : x == 4 || x == 5 ? light : x >= 10 ? dark : color;
                    texture.SetPixel(x, 15 - row, pixel);
                }
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
            sprite.name = $"Troféu {position} (provisório)";
            placeholders[position] = sprite;
            return sprite;
        }

        // ---- UI ----

        private void Build()
        {
            if (built) return;
            built = true;
            font = GameFont.Get();
            var root = (RectTransform)transform;
            BuildAsk(root);
            BuildBracket(root);
            BuildEnd(root);
        }

        private void BuildAsk(RectTransform root)
        {
            var page = CreateRect(root, "Torneio em andamento", Vector2.zero, Vector2.zero);
            Stretch(page, Vector2.zero, Vector2.one, 0f);
            askPage = page.gameObject;
            CreateText(page, "Título", "Torneio em andamento", 52, FontStyle.Bold, Color.white, new Vector2(0f, 300f), new Vector2(680f, 90f));
            CreateText(page, "Pergunta", "Continuar o torneio guardado ou começar outro?\nComeçar outro abandona o guardado.", 30,
                FontStyle.Normal, Color.white, new Vector2(0f, 170f), new Vector2(640f, 140f));
            var go = CreateButton(page, "Continuar", "Continuar", new Vector2(0f, -40f), new Vector2(420f, 90f), Continue, ButtonStyle.Yellow);
            var fresh = CreateButton(page, "Novo torneio", "Novo torneio", new Vector2(0f, -150f), new Vector2(420f, 90f), NewTournament);
            var back = CreateButton(page, "Voltar", "Voltar", new Vector2(0f, -260f), new Vector2(420f, 90f), () => menu.ShowPlay(), ButtonStyle.Red);
            var navigator = page.gameObject.AddComponent<MenuNavigator>();
            navigator.Configure(new[] { Entry(go), Entry(fresh), Entry(back) }, back);
        }

        private void BuildBracket(RectTransform root)
        {
            var page = CreateRect(root, "Chave", Vector2.zero, Vector2.zero);
            Stretch(page, Vector2.zero, Vector2.one, 0f);
            bracketPage = page.gameObject;

            titleText = CreateText(page, "Título", "Torneio", 52, FontStyle.Bold, Color.white, new Vector2(0f, 545f), new Vector2(680f, 90f));
            statusText = CreateText(page, "Situação", "-", 28, FontStyle.Normal, HighlightColor, new Vector2(0f, 475f), new Vector2(680f, 70f));

            tabRow = CreateRect(page, "Abas", new Vector2(0f, TabY), new Vector2(680f, TabSize.y));
            previousTab = CreateHiddenButton(tabRow, "Aba anterior", () => ShowTab(tab - 1));
            nextTab = CreateHiddenButton(tabRow, "Próxima aba", () => ShowTab(tab + 1));
            tabEntry = tabRow.gameObject.AddComponent<MenuEntry>();
            tabEntry.Configure(null, previousTab, nextTab, tabRow);

            var panel = CreateRect(page, "Lista", ListCenter, ListSize).gameObject.AddComponent<Image>();
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

            var back = CreateButton(page, "Voltar", "Voltar", new Vector2(-160f, -500f), new Vector2(280f, 80f), Back, ButtonStyle.Red);
            actionButton = CreateButton(page, "Jogar", "Jogar", new Vector2(160f, -500f), new Vector2(280f, 80f), Play, ButtonStyle.Yellow);
            actionLabel = actionButton.GetComponentInChildren<Text>();

            bracketNavigator = page.gameObject.AddComponent<MenuNavigator>();
            bracketNavigator.Configure(new[] { tabEntry, Entry(back), Entry(actionButton) }, back, 2);
        }

        private void BuildEnd(RectTransform root)
        {
            var page = CreateRect(root, "Fim do torneio", Vector2.zero, Vector2.zero);
            Stretch(page, Vector2.zero, Vector2.one, 0f);
            endPage = page.gameObject;

            CreateText(page, "Título", "Fim do torneio", 56, FontStyle.Bold, Color.white, new Vector2(0f, 520f), new Vector2(680f, 100f));
            endName = CreateText(page, "Torneio", "-", 34, FontStyle.Normal, Color.white, new Vector2(0f, 440f), new Vector2(680f, 60f));
            var trophy = CreateRect(page, "Troféu", new Vector2(0f, 200f), new Vector2(320f, 320f));
            endTrophy = trophy.gameObject.AddComponent<Image>();
            endTrophy.preserveAspect = true;
            endTrophy.raycastTarget = false;
            endPosition = CreateText(page, "Colocação", "-", 56, FontStyle.Bold, HighlightColor, new Vector2(0f, -20f), new Vector2(680f, 100f));
            endCoins = CreateText(page, "Moedas", "-", 32, FontStyle.Normal, Color.white, new Vector2(0f, -180f), new Vector2(680f, 170f));

            var bracket = CreateButton(page, "Ver chave", "Ver chave", new Vector2(0f, -390f), new Vector2(420f, 90f), ShowBracket);
            var toMenu = CreateButton(page, "Menu", "Menu", new Vector2(0f, -500f), new Vector2(420f, 90f), Back, ButtonStyle.Yellow);
            var navigator = page.gameObject.AddComponent<MenuNavigator>();
            navigator.Configure(new[] { Entry(bracket), Entry(toMenu) }, toMenu, 1);
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

        private Text CreateText(Transform parent, string objectName, string text, int size, FontStyle style, Color color,
            Vector2 position, Vector2 box)
        {
            var label = CreateRect(parent, objectName, position, box).gameObject.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 14;
            label.resizeTextMaxSize = size;
            label.raycastTarget = false;
            label.gameObject.AddComponent<Shadow>().effectColor = ShadowColor;
            return label;
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
