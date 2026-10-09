using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// HUD da partida (visual provisório, montado em código).
    /// Tela em pé (celular): 4 cantos ao lado dos gols, 2 por jogador; os de cima giram 180° para o jogador de cima.
    /// Canto esquerdo de cada jogador: placar, relógio e pausa. Canto direito: vez, toques, timer e o botão de ação.
    /// As mensagens (falta, gol...) aparecem no meio do campo, uma virada para cada jogador.
    /// Tela deitada (PC): painel único à esquerda e uma mensagem só, sem girar (provisório até refinar o PC).
    /// Também destaca os botões do time da vez.
    /// </summary>
    public sealed class MatchHud : MonoBehaviour
    {
        private const float PixelsPerUnit = 100f;

        [SerializeField] private MatchController match;
        [Tooltip("Nos últimos segundos o timer fica vermelho.")]
        [SerializeField, Min(0f)] private float warningSeconds = 5f;

        private static readonly Color WarningColor = new(1f, 0.25f, 0.2f);
        private static readonly Color PanelColor = new(0f, 0f, 0f, 0.45f);
        private static readonly Color ButtonColor = new(0.95f, 0.95f, 0.9f);
        private static readonly Color ButtonTextColor = new(0.1f, 0.1f, 0.1f);

        /// <summary>Elementos de um jogador (seus dois cantos no celular, ou o painel do PC).</summary>
        private sealed class PlayerView
        {
            public Text Score;
            public Text Clock;
            public Text Turn;
            public Text Timer;
            public Button Action;
            public Text ActionLabel;
            public Button Pause;
        }

        private sealed class MessageView
        {
            public GameObject Root;
            public Text Label;
        }

        private readonly Dictionary<TeamSide, PlayerView> corners = new();
        private readonly Dictionary<TeamSide, MessageView> messages = new();
        private readonly List<Canvas> worldCanvases = new();
        private PlayerView pcPanel;
        private GameObject cornersRoot;
        private GameObject pcRoot;
        private TurnHighlight highlight;
        private Font font;
        private Camera worldCamera;
        private string lastMessage;
        private float messageUntil;
        private bool messageHeld;

        public void Configure(MatchController matchController) => match = matchController;

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
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildCorners();
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

            cornersRoot.SetActive(visible && portrait);
            pcRoot.SetActive(visible && !portrait);
            UpdateMessages(visible, portrait);
            if (!visible) return;

            if (portrait)
            {
                Refresh(corners[TeamSide.Bottom], TeamSide.Bottom);
                Refresh(corners[TeamSide.Top], TeamSide.Top);
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
            view.Score.text = ScoreText();
            view.Clock.text = ClockText(match.ClockSeconds);

            var state = match.State;
            var acting = match.ActingSide;
            bool active = state is MatchState.Aim or MatchState.ShotAim or MatchState.GoalKick or MatchState.ShotCall or MatchState.PenaltySetup;
            bool mine = active && acting == side;

            view.Turn.text = !active ? StateText(state)
                : match.IsAi(acting) ? ThinkingText()
                : !mine ? $"Vez do {MatchController.TeamName(acting)}"
                : state == MatchState.ShotCall ? "Ajuste o goleiro"
                : state == MatchState.PenaltySetup ? "Posicione o batedor"
                : state == MatchState.GoalKick ? "Tiro de meta"
                : TouchesText(match.TouchesLeft);
            view.Turn.color = TeamColor(active ? acting : side);

            float seconds = match.StateSecondsLeft;
            view.Timer.text = mine ? $"{Mathf.CeilToInt(seconds)} s" : string.Empty;
            view.Timer.color = seconds <= warningSeconds ? WarningColor : Color.white;

            string action = ActionFor(side);
            view.Action.gameObject.SetActive(action != null);
            if (action != null) view.ActionLabel.text = action;
        }

        /// <summary>Texto do botão de ação desse jogador agora, ou nulo se ele não tem ação.</summary>
        private string ActionFor(TeamSide side)
        {
            if (match.IsAi(side)) return null;
            if (match.CanCallShot && match.Turn == side) return "Vai chutar";
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
            bottom.Root.SetActive(show);
            top.Root.SetActive(show && portrait);
            if (!show) return;

            bottom.Label.text = message;
            top.Label.text = message;
            // Com uma mensagem só (PC) ela fica bem no meio; com duas, uma de cada lado do círculo central.
            bottom.Root.transform.position = new Vector3(0f, portrait ? -1.3f : 0f, 0f);
        }

        private string ScoreText() =>
            $"{Cards(TeamSide.Bottom)}{Abbreviation(TeamSide.Bottom)} {match.Score(TeamSide.Bottom)} x " +
            $"{match.Score(TeamSide.Top)} {Abbreviation(TeamSide.Top)}{Cards(TeamSide.Top)}";

        /// <summary>Abreviação do time (3 letras) na cor dele: o placar fica sempre do mesmo tamanho.</summary>
        private static string Abbreviation(TeamSide side) =>
            $"<color=#{ColorUtility.ToHtmlStringRGB(TeamColor(side))}>{MatchSession.Team(side).Abbreviation}</color>";

        /// <summary>Cartões do jogador ao lado do nome: um quadradinho amarelo e um vermelho por expulsão.</summary>
        private string Cards(TeamSide side)
        {
            if (!match.HasYellow(side)) return string.Empty;
            string cards = "<color=#FFD21F>■</color>";
            for (int i = 0; i < match.RedCards(side); i++) cards += "<color=#E02424>■</color>";
            return side == TeamSide.Bottom ? cards + " " : " " + cards;
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

        // ---- Montagem: cantos (celular) ----

        private void BuildCorners()
        {
            cornersRoot = new GameObject("Cantos (celular)");
            cornersRoot.transform.SetParent(transform, false);

            // Faixa entre a rede do gol e a lateral, atrás da linha de fundo.
            const float innerX = FieldLayout.GoalWidth * 0.5f + 0.2f;
            const float outerX = FieldLayout.HalfWidth + 0.5f;
            const float nearY = FieldLayout.HalfHeight + 0.05f;
            const float farY = 6.85f;
            var size = new Vector2(outerX - innerX, farY - nearY);
            float centerX = (innerX + outerX) * 0.5f;
            float centerY = (nearY + farY) * 0.5f;

            foreach (var side in new[] { TeamSide.Bottom, TeamSide.Top })
            {
                // O jogador de cima vê tudo girado: a esquerda dele é a direita da tela.
                float sign = side == TeamSide.Bottom ? 1f : -1f;
                float rotation = side == TeamSide.Bottom ? 0f : 180f;
                var view = new PlayerView();

                var left = CreateWorldCanvas($"Canto esquerdo {Name(side)}", cornersRoot.transform,
                    new Vector2(-centerX * sign, -centerY * sign), size, rotation);
                CreatePanel(left.transform, PanelColor);
                view.Score = CreateText(left.transform, "Placar", 28, FontStyle.Bold, new Vector2(0f, 40f), new Vector2(250f, 40f));
                view.Clock = CreateText(left.transform, "Relógio", 28, FontStyle.Bold, new Vector2(0f, 2f), new Vector2(250f, 36f));
                view.Pause = CreateButton(left.transform, "Pausa", "Pausa", 22, new Vector2(0f, -40f), new Vector2(130f, 40f), out _);
                view.Pause.onClick.AddListener(match.RequestPause);

                var right = CreateWorldCanvas($"Canto direito {Name(side)}", cornersRoot.transform,
                    new Vector2(centerX * sign, -centerY * sign), size, rotation);
                CreatePanel(right.transform, PanelColor);
                view.Turn = CreateText(right.transform, "Vez", 24, FontStyle.Bold, new Vector2(0f, 42f), new Vector2(250f, 36f));
                view.Timer = CreateText(right.transform, "Timer", 26, FontStyle.Bold, new Vector2(0f, 6f), new Vector2(250f, 34f));
                view.Action = CreateButton(right.transform, "Ação", "Vai chutar", 24, new Vector2(0f, -38f), new Vector2(220f, 44f), out view.ActionLabel);
                var actionSide = side;
                view.Action.onClick.AddListener(() => OnAction(actionSide));

                corners[side] = view;
            }
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

            var panel = CreatePanel(pcRoot.transform, PanelColor).rectTransform;
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.sizeDelta = new Vector2(300f, -32f);
            panel.anchoredPosition = new Vector2(16f, -16f);

            var view = new PlayerView();
            view.Score = CreateTopText(panel, "Placar", 30, -40f, 50f);
            view.Clock = CreateTopText(panel, "Relógio", 30, -95f, 44f);
            view.Turn = CreateTopText(panel, "Vez", 26, -150f, 60f);
            view.Timer = CreateTopText(panel, "Timer", 26, -205f, 40f);
            view.Action = CreateButton(panel, "Ação", "Vai chutar", 26, Vector2.zero, new Vector2(240f, 56f), out view.ActionLabel);
            SetTop((RectTransform)view.Action.transform, -275f);
            view.Action.onClick.AddListener(() => OnAction(match.ActingSide));
            view.Pause = CreateButton(panel, "Pausa", "Pausa (Esc)", 26, Vector2.zero, new Vector2(240f, 56f), out _);
            SetTop((RectTransform)view.Pause.transform, -345f);
            view.Pause.onClick.AddListener(match.RequestPause);
            CreateTopText(panel, "Dicas", 18, -420f, 60f).text = "R: reiniciar\nBotão direito: cancelar mira";
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

        private Button CreateButton(Transform parent, string objectName, string label, int size, Vector2 position, Vector2 box, out Text labelText)
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

            labelText = CreateText(go.transform, "Texto", size, FontStyle.Bold, Vector2.zero, box);
            labelText.text = label;
            labelText.color = ButtonTextColor;
            return button;
        }

        private static string Name(TeamSide side) => side == TeamSide.Bottom ? "de baixo" : "de cima";
    }
}
