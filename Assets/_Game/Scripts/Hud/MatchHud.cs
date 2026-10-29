using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// HUD da partida (visual provisório, montado em código).
    /// Tela em pé (celular): uma faixa por jogador logo depois do gol dele (a de cima girada 180° para o jogador de cima),
    /// dentro da moldura que a câmera sempre encaixa (ScreenFit). Na faixa, da esquerda para a direita: relógio e pausa;
    /// placar (abreviações, gols e cartões); vez, toques, timer e o botão de ação.
    /// As mensagens (falta, gol...) aparecem no meio do campo, uma virada para cada jogador. Contra a IA, as duas faixas
    /// são do jogador, em pé: em cima relógio, placar, vez e timer; embaixo só Pausa e o botão de ação. Mensagem só a dele.
    /// Tela deitada (PC): painel único à esquerda e uma mensagem só, sem girar (provisório até refinar o PC).
    /// Também destaca os botões do time da vez.
    /// </summary>
    public sealed class MatchHud : MonoBehaviour
    {
        private const float PixelsPerUnit = 100f;

        [SerializeField] private MatchController match;
        [Tooltip("Nos últimos segundos o timer fica vermelho.")]
        [SerializeField, Min(0f)] private float warningSeconds = 5f;
        [Tooltip("Painel 9-slice das faixas (Hud_BG_Tile da Botões_UI-Sheet). Sem ele, fundo preto meio transparente.")]
        [SerializeField] private Sprite panelSprite;

        private static readonly Color WarningColor = new(1f, 0.25f, 0.2f);
        private static readonly Color PanelColor = new(0f, 0f, 0f, 0.45f);
        private static readonly Color ButtonColor = new(0.95f, 0.95f, 0.9f);
        private static readonly Color ButtonTextColor = new(0.1f, 0.1f, 0.1f);
        // "Vai chutar" pulsando para ninguém esquecer de avisar antes do chute a gol.
        private static readonly Color GlowColor = new(1f, 0.85f, 0.25f);
        private static readonly Color SkinGlowTint = new(1f, 0.8f, 0.55f);
        private const float GlowSpeed = 5f;
        private const float GlowScale = 0.08f;
        private const string ShotCallLabel = "Pro Gol!";
        private const float PcPanelPixelScale = 2f;

        /// <summary>Elementos de um jogador (a faixa dele no celular, ou o painel do PC).</summary>
        private sealed class PlayerView
        {
            public Text Score;
            /// <summary>Cartões embaixo de cada nome (só na faixa do celular; no PC ficam no placar).</summary>
            public Text FirstCards;
            public Text SecondCards;
            public Text Clock;
            public Text Turn;
            public Text Timer;
            public Button Action;
            public Text ActionLabel;
            public Button Pause;
            /// <summary>A faixa inteira (só no celular).</summary>
            public GameObject Root;
        }

        private sealed class MessageView
        {
            public GameObject Root;
            public Text Label;
        }

        private readonly Dictionary<TeamSide, PlayerView> strips = new();
        private readonly Dictionary<TeamSide, MessageView> messages = new();
        private readonly List<Canvas> worldCanvases = new();
        private PlayerView pcPanel;
        // Contra a IA (celular): faixa de cima só com as informações, de baixo só com os botões, as duas para o jogador.
        private PlayerView soloInfo;
        private PlayerView soloButtons;
        private GameObject stripsRoot;
        private GameObject pcRoot;
        private TurnHighlight highlight;
        private Font font;
        private Camera worldCamera;
        private string lastMessage;
        private float messageUntil;
        private bool messageHeld;

        public void Configure(MatchController matchController, Sprite panel = null)
        {
            match = matchController;
            panelSprite = panel;
        }

        private void Awake()
        {
            if (match == null) match = FindAnyObjectByType<MatchController>();
            if (match == null)
            {
                Debug.LogWarning("[Futebol de Botão] MatchHud sem MatchController.");
                enabled = false;
                return;
            }

            match.ExternalHud = true;
            font = GameFont.Get();
            BuildStrips();
            BuildMessages();
            BuildPcPanel();
            highlight = gameObject.AddComponent<TurnHighlight>();
            highlight.Configure(match);
        }

        private void Update()
        {
            bool visible = match.State != MatchState.Waiting && Time.timeScale > 0f;
            bool portrait = Screen.height >= Screen.width;

            worldCamera = WorldCamera.Resolve(worldCamera);
            foreach (var canvas in worldCanvases) canvas.worldCamera = worldCamera;

            stripsRoot.SetActive(visible && portrait);
            pcRoot.SetActive(visible && !portrait);
            UpdateMessages(visible, portrait);
            if (!visible) return;

            if (portrait)
            {
                // Contra a IA: informações em cima e botões embaixo (perto do polegar), as duas para o jogador.
                bool vsAi = match.IsAi(TeamSide.Bottom) || match.IsAi(TeamSide.Top);
                foreach (var view in strips.Values) view.Root.SetActive(!vsAi);
                soloInfo.Root.SetActive(vsAi);
                soloButtons.Root.SetActive(vsAi);
                if (vsAi)
                {
                    var human = match.IsAi(TeamSide.Bottom) ? TeamSide.Top : TeamSide.Bottom;
                    Refresh(soloInfo, human);
                    Refresh(soloButtons, human);
                }
                else
                {
                    Refresh(strips[TeamSide.Bottom], TeamSide.Bottom);
                    Refresh(strips[TeamSide.Top], TeamSide.Top);
                }
            }
            else
            {
                // No PC os dois jogadores olham a mesma tela: o painel mostra o jogador que age agora.
                Refresh(pcPanel, match.ActingSide);
            }
        }

        // ---- Conteúdo ----

        private void Refresh(PlayerView view, TeamSide side)
        {
            // Na faixa, o time do jogador vem primeiro; no painel do PC, sempre o de baixo primeiro.
            // Contra a IA a faixa de cima não tem botões e a de baixo só tem botões: o que não existe fica de fora.
            var other = side == TeamSide.Bottom ? TeamSide.Top : TeamSide.Bottom;
            if (view == pcPanel) view.Score.text = ScoreText(TeamSide.Bottom, TeamSide.Top);
            // Contra a IA o placar tem os cartões do lado de fora de cada time, na mesma linha.
            else if (view == soloInfo) view.Score.text = ScoreText(side, other);
            else if (view.Score != null)
            {
                view.Score.text = $"{Abbreviation(side)} {match.Score(side)} x {match.Score(other)} {Abbreviation(other)}";
                view.FirstCards.text = Cards(side, false).Trim();
                view.SecondCards.text = Cards(other, false).Trim();
            }
            if (view.Clock != null) view.Clock.text = ClockText(match.ClockSeconds);

            var state = match.State;
            var acting = match.ActingSide;
            bool active = state is MatchState.Aim or MatchState.ShotAim or MatchState.GoalKick or MatchState.ShotCall or MatchState.PenaltySetup;
            bool mine = active && acting == side;

            if (view.Action != null)
            {
                string action = ActionFor(side);
                view.Action.gameObject.SetActive(action != null);
                if (action != null) view.ActionLabel.text = action;
                Glow(view.Action, action == ShotCallLabel);
            }
            if (view.Turn == null) return;

            view.Turn.text = !active ? StateText(state)
                : match.IsAi(acting) ? ThinkingText()
                : !mine ? $"Vez do {MatchController.TeamName(acting)}"
                : state == MatchState.ShotCall ? "Ajuste o goleiro"
                : state == MatchState.PenaltySetup ? "Posicione o batedor"
                : state == MatchState.GoalKick ? (match.IsPlacingGoalKick ? "Posicione a bola" : "Tiro de meta")
                : TouchesText(match.TouchesLeft);
            view.Turn.color = TeamColor(active ? acting : side);

            float seconds = match.StateSecondsLeft;
            view.Timer.text = mine ? $"{Mathf.CeilToInt(seconds)} s" : string.Empty;
            view.Timer.color = seconds <= warningSeconds ? WarningColor : Color.white;
        }

        /// <summary>Pulsa a cor, a borda e o tamanho do botão (ou volta ao normal).</summary>
        private static void Glow(Button button, bool on)
        {
            float pulse = on ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * GlowSpeed) : 0f;
            // Com a moldura desenhada o botão já é amarelo: o pulso só puxa a cor para o laranja.
            bool skinned = ButtonSkin.IsSkinned(button);
            if (button.targetGraphic != null)
                button.targetGraphic.color = skinned ? Color.Lerp(Color.white, SkinGlowTint, pulse) : Color.Lerp(ButtonColor, GlowColor, pulse);
            if (button.TryGetComponent<Outline>(out var outline))
                outline.effectColor = new Color(GlowColor.r, GlowColor.g, GlowColor.b, pulse);
            button.transform.localScale = Vector3.one * (1f + GlowScale * pulse);
        }

        /// <summary>Texto do botão de ação desse jogador agora, ou nulo se ele não tem ação.</summary>
        private string ActionFor(TeamSide side)
        {
            if (match.IsAi(side)) return null;
            if (match.CanCallShot && match.Turn == side) return ShotCallLabel;
            if (match.CanConfirmReady && match.ActingSide == side) return "Pronto";
            return null;
        }

        private void OnAction(TeamSide side)
        {
            if (match.IsAi(side)) return;
            if (match.CanCallShot && match.Turn == side) match.TryCallShot();
            else if (match.CanConfirmReady && match.ActingSide == side) match.ConfirmReady();
        }

        private void UpdateMessages(bool visible, bool portrait)
        {
            string message = match.Message;
            if (message != lastMessage)
            {
                lastMessage = message;
                if (!string.IsNullOrEmpty(message)) messageUntil = Time.unscaledTime + match.Options.messageSeconds;
                // Aviso que parou o jogo some junto com a pausa, para não ficar na tela com o jogo andando.
                messageHeld = match.IsHolding;
            }

            bool timeLeft = messageHeld ? match.IsHolding : Time.unscaledTime < messageUntil;
            bool show = visible && !string.IsNullOrEmpty(message) && (timeLeft || match.State == MatchState.End);

            var bottom = messages[TeamSide.Bottom];
            var top = messages[TeamSide.Top];
            // No celular, uma mensagem para cada jogador de verdade (contra a IA, só a do jogador); no PC, uma só.
            bottom.Root.SetActive(show && (!portrait || !match.IsAi(TeamSide.Bottom)));
            top.Root.SetActive(show && portrait && !match.IsAi(TeamSide.Top));
            if (!show) return;

            bottom.Label.text = message;
            top.Label.text = message;
            // Com uma mensagem só (PC) ela fica bem no meio; com duas, uma de cada lado do círculo central.
            bottom.Root.transform.position = new Vector3(0f, portrait ? -1.3f : 0f, 0f);
        }

        /// <summary>Placar lido da esquerda para a direita: <paramref name="first"/> primeiro.</summary>
        private string ScoreText(TeamSide first, TeamSide second) =>
            $"{Cards(first, true)}{Abbreviation(first)} {match.Score(first)} x " +
            $"{match.Score(second)} {Abbreviation(second)}{Cards(second, false)}";

        /// <summary>Abreviação do time (3 letras) na cor dele: o placar fica sempre do mesmo tamanho.</summary>
        private static string Abbreviation(TeamSide side) =>
            $"<color=#{ColorUtility.ToHtmlStringRGB(TeamColor(side))}>{MatchSession.Team(side).Abbreviation}</color>";

        /// <summary>Cartões do jogador ao lado do nome: um quadradinho amarelo e um vermelho por expulsão.</summary>
        private string Cards(TeamSide side, bool before)
        {
            if (!match.HasYellow(side)) return string.Empty;
            string cards = "<color=#FFD21F>■</color>";
            for (int i = 0; i < match.RedCards(side); i++) cards += "<color=#E02424>■</color>";
            return before ? cards + " " : " " + cards;
        }

        private static string ClockText(float clock)
        {
            int minutes = Mathf.FloorToInt(clock / 60f);
            int seconds = Mathf.CeilToInt(clock % 60f);
            if (seconds == 60) { minutes++; seconds = 0; }
            return $"{minutes}:{seconds:00}";
        }

        /// <summary>"IA pensando" com reticências andando, enquanto a IA age.</summary>
        private static string ThinkingText() => "IA pensando" + new string('.', 1 + (int)(Time.unscaledTime * 2f) % 3);

        private static string TouchesText(int touches) => touches == 1 ? "Sua vez: 1 toque" : $"Sua vez: {touches} toques";

        private static string StateText(MatchState state) => state switch
        {
            MatchState.Moving => "Bola rolando",
            MatchState.Goal => "Gol!",
            MatchState.End => "Fim de jogo",
            _ => string.Empty
        };

        /// <summary>Cor do time um pouco mais clara, para ler sobre o fundo escuro do HUD.</summary>
        private static Color TeamColor(TeamSide side) => Color.Lerp(MatchSession.Team(side).TextColor, Color.white, 0.3f);

        // ---- Montagem: faixas (celular) ----

        private void BuildStrips()
        {
            stripsRoot = new GameObject("Faixas (celular)");
            stripsRoot.transform.SetParent(transform, false);

            // Faixa logo depois da rede do gol, na largura da mesa.
            float width = ScreenFit.VisibleHalfWidth * 2f - 0.1f;
            const float height = ScreenFit.StripHeight;
            float centerY = ScreenFit.StripStart + height * 0.5f;
            // Em pixels do canvas (100 por unidade): esquerda 2 unidades, meio e direita 3 cada.
            float canvasWidth = width * PixelsPerUnit;
            float leftX = -canvasWidth * 0.5f + 100f;
            float rightX = canvasWidth * 0.5f - 150f;

            foreach (var side in new[] { TeamSide.Bottom, TeamSide.Top })
            {
                float sign = side == TeamSide.Bottom ? 1f : -1f;
                float rotation = side == TeamSide.Bottom ? 0f : 180f;
                var view = new PlayerView();

                var canvas = CreateWorldCanvas($"Faixa {Name(side)}", stripsRoot.transform,
                    new Vector2(0f, -centerY * sign), new Vector2(width, height), rotation);
                CreateFramedPanel(canvas.transform, 1f);

                // Esquerda: relógio e pausa.
                view.Clock = CreateText(canvas.transform, "Relógio", 40, FontStyle.Bold, new Vector2(leftX, 36f), new Vector2(190f, 60f));
                view.Pause = CreateButton(canvas.transform, "Pausa", "Pausa", 24, new Vector2(leftX, -38f), new Vector2(150f, 52f), out _, ButtonStyle.Green);
                view.Pause.onClick.AddListener(match.RequestPause);

                // Meio: placar um pouco acima do centro e, embaixo de cada nome, os cartões do time.
                view.Score = CreateText(canvas.transform, "Placar", 40, FontStyle.Bold, new Vector2(0f, 22f), new Vector2(290f, 60f));
                FitOneLine(view.Score);
                view.FirstCards = CreateText(canvas.transform, "Cartões (primeiro)", 28, FontStyle.Normal, new Vector2(-95f, -38f), new Vector2(100f, 40f));
                view.SecondCards = CreateText(canvas.transform, "Cartões (segundo)", 28, FontStyle.Normal, new Vector2(95f, -38f), new Vector2(100f, 40f));

                // Direita: vez/toques, timer e o botão de ação.
                view.Turn = CreateText(canvas.transform, "Vez", 24, FontStyle.Bold, new Vector2(rightX, 52f), new Vector2(290f, 34f));
                FitOneLine(view.Turn);
                view.Timer = CreateText(canvas.transform, "Timer", 26, FontStyle.Bold, new Vector2(rightX, 18f), new Vector2(290f, 32f));
                view.Action = CreateButton(canvas.transform, "Ação", ShotCallLabel, 26, new Vector2(rightX, -40f), new Vector2(260f, 56f), out view.ActionLabel, ButtonStyle.Yellow);
                var actionSide = side;
                view.Action.onClick.AddListener(() => OnAction(actionSide));

                view.Root = canvas.gameObject;
                strips[side] = view;
            }

            BuildSoloStrips(width, centerY, leftX, rightX);
        }

        /// <summary>
        /// Contra a IA: em cima, em pé para o jogador, relógio, placar, vez, toques e timer; embaixo só Pausa e o botão de
        /// ação, maiores, com o meio livre (lugar de um controle virtual no futuro).
        /// </summary>
        private void BuildSoloStrips(float width, float centerY, float leftX, float rightX)
        {
            const float height = ScreenFit.StripHeight;
            float canvasWidth = width * PixelsPerUnit;

            var info = new PlayerView();
            var top = CreateWorldCanvas("Faixa contra a IA (informações)", stripsRoot.transform,
                new Vector2(0f, centerY), new Vector2(width, height), 0f);
            CreateFramedPanel(top.transform, 1f);
            // Duas linhas dentro da moldura (150 px de altura): em cima o placar com os cartões; embaixo relógio, vez
            // e timer. A fonte diminui para caber no espaço da linha em vez de invadir a outra.
            const float lineHeight = 52f;
            const float upperY = 28f;
            const float lowerY = -30f;
            info.Score = CreateText(top.transform, "Placar", 40, FontStyle.Bold, new Vector2(0f, upperY), new Vector2(canvasWidth - 60f, lineHeight));
            FitInBox(info.Score);
            info.Clock = CreateText(top.transform, "Relógio", 34, FontStyle.Bold, new Vector2(-canvasWidth * 0.5f + 95f, lowerY), new Vector2(150f, lineHeight));
            FitInBox(info.Clock);
            info.Turn = CreateText(top.transform, "Vez", 28, FontStyle.Bold, new Vector2(0f, lowerY), new Vector2(canvasWidth - 360f, lineHeight));
            FitInBox(info.Turn);
            info.Timer = CreateText(top.transform, "Timer", 34, FontStyle.Bold, new Vector2(canvasWidth * 0.5f - 85f, lowerY), new Vector2(130f, lineHeight));
            FitInBox(info.Timer);
            info.Root = top.gameObject;
            soloInfo = info;

            var buttons = new PlayerView();
            var bottom = CreateWorldCanvas("Faixa contra a IA (botões)", stripsRoot.transform,
                new Vector2(0f, -centerY), new Vector2(width, height), 0f);
            CreateFramedPanel(bottom.transform, 1f);
            buttons.Pause = CreateButton(bottom.transform, "Pausa", "Pausa", 30, new Vector2(-canvasWidth * 0.5f + 125f, 0f),
                new Vector2(210f, 100f), out _, ButtonStyle.Green);
            buttons.Pause.onClick.AddListener(match.RequestPause);
            buttons.Action = CreateButton(bottom.transform, "Ação", ShotCallLabel, 34, new Vector2(canvasWidth * 0.5f - 170f, 0f),
                new Vector2(300f, 106f), out buttons.ActionLabel, ButtonStyle.Yellow);
            buttons.Action.onClick.AddListener(() => OnAction(match.IsAi(TeamSide.Bottom) ? TeamSide.Top : TeamSide.Bottom));
            buttons.Root = bottom.gameObject;
            soloButtons = buttons;
        }

        private void BuildMessages()
        {
            var root = new GameObject("Mensagens").transform;
            root.SetParent(transform, false);
            foreach (var side in new[] { TeamSide.Bottom, TeamSide.Top })
            {
                float y = side == TeamSide.Bottom ? -1.3f : 1.3f;
                var canvas = CreateWorldCanvas($"Mensagem {Name(side)}", root, new Vector2(0f, y), new Vector2(6.4f, 1f),
                    side == TeamSide.Bottom ? 0f : 180f);
                // A mensagem não bloqueia toques no campo.
                Destroy(canvas.GetComponent<GraphicRaycaster>());
                CreatePanel(canvas.transform, new Color(0f, 0f, 0f, 0.65f)).raycastTarget = false;
                var label = CreateText(canvas.transform, "Texto", 32, FontStyle.Bold, Vector2.zero, new Vector2(620f, 96f));
                messages[side] = new MessageView { Root = canvas.gameObject, Label = label };
                canvas.gameObject.SetActive(false);
            }
        }

        // ---- Montagem: painel do PC ----

        private void BuildPcPanel()
        {
            pcRoot = new GameObject("Painel (PC)", typeof(RectTransform));
            pcRoot.transform.SetParent(transform, false);
            var canvas = pcRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = pcRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 1f;
            pcRoot.AddComponent<GraphicRaycaster>();

            // No PC a tela tem 720 de altura e a mesa ~14 unidades: a moldura sai com metade da espessura para o
            // pixel do painel ficar do tamanho do pixel da mesa.
            var panel = CreateFramedPanel(pcRoot.transform, PcPanelPixelScale).rectTransform;
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.sizeDelta = new Vector2(300f, -32f);
            panel.anchoredPosition = new Vector2(16f, -16f);

            var view = new PlayerView();
            view.Score = CreateTopText(panel, "Placar", 30, -40f, 50f);
            FitOneLine(view.Score);
            view.Clock = CreateTopText(panel, "Relógio", 30, -95f, 44f);
            view.Turn = CreateTopText(panel, "Vez", 26, -150f, 60f);
            view.Timer = CreateTopText(panel, "Timer", 26, -205f, 40f);
            view.Action = CreateButton(panel, "Ação", "Pro Gol!", 26, Vector2.zero, new Vector2(240f, 56f), out view.ActionLabel, ButtonStyle.Yellow);
            SetTop((RectTransform)view.Action.transform, -275f);
            view.Action.onClick.AddListener(() => OnAction(match.ActingSide));
            view.Pause = CreateButton(panel, "Pausa", "Pausa (Esc)", 26, Vector2.zero, new Vector2(240f, 56f), out _, ButtonStyle.Green);
            SetTop((RectTransform)view.Pause.transform, -345f);
            view.Pause.onClick.AddListener(match.RequestPause);
            var tips = CreateTopText(panel, "Dicas", 17, -520f, 190f);
            tips.alignment = TextAnchor.UpperLeft;
            tips.text = "Mouse: arraste do botão para trás e solte\n" +
                        "Teclado: A/D escolhe o botão, E seleciona, A/D gira, W/S força, Espaço chuta, Q volta, C Pro Gol!\n" +
                        "Controle: analógico/D-pad escolhe, A seleciona, analógico mira, analógico dir. força, X chuta, B volta, LB/RB Pro Gol!";
            pcPanel = view;
        }

        // ---- UI ----

        private Canvas CreateWorldCanvas(string objectName, Transform parent, Vector2 center, Vector2 sizeUnits, float rotation)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 60;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
            go.AddComponent<GraphicRaycaster>();

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = sizeUnits * PixelsPerUnit;
            rect.localScale = Vector3.one / PixelsPerUnit;
            rect.position = new Vector3(center.x, center.y, 0f);
            rect.rotation = Quaternion.Euler(0f, 0f, rotation);
            worldCanvases.Add(canvas);
            return canvas;
        }

        /// <summary>Fundo de faixa/painel com a arte (9-slice) quando há sprite; senão o retângulo escuro de antes.</summary>
        private Image CreateFramedPanel(Transform parent, float pixelsPerUnitMultiplier)
        {
            var image = CreatePanel(parent, PanelColor);
            if (panelSprite == null) return image;
            image.sprite = panelSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
            image.color = Color.white;
            return image;
        }

        private static Image CreatePanel(Transform parent, Color color)
        {
            var go = new GameObject("Fundo", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text CreateText(Transform parent, string objectName, int size, FontStyle style, Vector2 position, Vector2 box)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = box;
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Placar sempre numa linha: com cartões ao lado das abreviações, a fonte diminui em vez de quebrar.</summary>
        /// <summary>Texto preso à caixa: a fonte diminui até caber, sem quebrar nem passar da altura.</summary>
        private static void FitInBox(Text text)
        {
            FitOneLine(text);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static void FitOneLine(Text text)
        {
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 12;
            text.resizeTextMaxSize = text.fontSize;
        }

                private Text CreateTopText(RectTransform parent, string objectName, int size, float y, float height)
        {
            var text = CreateText(parent, objectName, size, FontStyle.Bold, Vector2.zero, new Vector2(280f, height));
            SetTop(text.rectTransform, y);
            return text;
        }

        private static void SetTop(RectTransform rect, float y)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
        }

        private Button CreateButton(Transform parent, string objectName, string label, int size, Vector2 position, Vector2 box, out Text labelText,
            ButtonStyle style)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = box;
            var image = go.AddComponent<Image>();
            image.color = ButtonColor;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            // Sem seleção depois do clique: senão Espaço/E/A do teclado e do controle "apertam" o botão de novo.
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() =>
            {
                var events = EventSystem.current;
                if (events != null) events.SetSelectedGameObject(null);
            });
            var outline = go.AddComponent<Outline>();
            outline.effectColor = Color.clear;
            outline.effectDistance = new Vector2(4f, -4f);

            labelText = CreateText(go.transform, "Texto", size, FontStyle.Bold, Vector2.zero, box);
            labelText.text = label;
            labelText.color = ButtonTextColor;
            ButtonSkin.Apply(button, style);
            return button;
        }

        private static string Name(TeamSide side) => side == TeamSide.Bottom ? "de baixo" : "de cima";
    }
}
