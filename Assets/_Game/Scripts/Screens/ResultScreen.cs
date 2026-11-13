using System.Collections.Generic;
using System.Globalization;
using Immersive.Framework.GameFlow;
using UnityEngine;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Resultado: "Fim de Jogo", o placar com o botão e o nome de cada time (baixo à esquerda, cima à direita) e uma
    /// tabela com os números da partida dos dois times (<see cref="MatchSession.LastMatchNumbers"/>, só na memória).
    /// Contra a IA, avisa quando bateu o recorde de sequência de vitórias. "Jogar de novo" volta para a Partida e "Menu"
    /// para o Menu. No torneio, os botões viram "Avançar" (a chave, ou o fim do torneio) e "Sair" (Menu; o torneio fica
    /// guardado) e aparece como o jogador está no torneio. O título e os botões vêm do "Criar telas"; o placar e a tabela são montados aqui.
    /// </summary>
    public sealed class ResultScreen : MonoBehaviour
    {
        private const float ScoreY = 350f;
        private const float DiscX = 230f;
        private const float DiscSize = 160f;
        private const float NameY = 245f;
        private const float RecordY = 188f;
        private const float CoinsY = 462f;
        private const float TournamentY = -345f;
        /// <summary>Gols da disputa de pênaltis, entre parênteses, embaixo de cada número do placar.</summary>
        private const float PenaltyX = 82f;
        private const float PenaltyY = 272f;
        /// <summary>Alto do quadro: logo abaixo dos nomes (e do aviso de recorde); ele cresce para baixo.</summary>
        private const float TableTop = 155f;
        private const float TableWidth = 680f;
        private const float TablePadding = 16f;
        private const float RowHeight = 50f;
        private const float ValueWidth = 150f;
        private static readonly Color HighlightColor = new(1f, 0.85f, 0.25f);
        private static readonly Color StripeColor = new(1f, 1f, 1f, 0.07f);
        private static readonly Color PanelColor = new(0f, 0f, 0f, 0.55f);
        private static readonly Color ShadowColor = new(0f, 0f, 0f, 0.6f);
        private static readonly Vector2 ShadowDistance = new(3f, -3f);

        [SerializeField] private RouteRequestTrigger matchRoute;
        [SerializeField] private RouteRequestTrigger menuRoute;

        private Font font;
        private Text coinsLabel;
        private Button doubleButton;
        /// <summary>Assistiu a um anúncio com prêmio nesta tela: não leva o anúncio entre partidas ao sair.</summary>
        private bool watchedReward;
        private bool leaving;

        public void Configure(RouteRequestTrigger match, RouteRequestTrigger menu)
        {
            matchRoute = match;
            menuRoute = menu;
        }

        private void OnDestroy() => Ads.HideBanner(this);

        private void Start()
        {
            Ads.ShowBanner(this);
            font = GameFont.Get();
            var root = (RectTransform)transform;
            var result = MatchSession.LastResult;
            if (result == null)
            {
                CreateText(root, "Sem partida", Txt.Get("Sem partida"), 40, FontStyle.Bold, Color.white, new Vector2(0f, ScoreY), new Vector2(680f, 90f));
                return;
            }

            BuildScore(root, result.Value);
            if (MatchSession.VsAi == true && PlayerStats.NewRecord > 0)
                CreateText(root, "Recorde", Txt.Format("Novo recorde de sequência: {0} vitórias!", PlayerStats.NewRecord), 30, FontStyle.Bold,
                    HighlightColor, new Vector2(0f, RecordY), new Vector2(680f, 44f));
            if (MatchSession.VsAi == true && PlayerStats.CoinsEarned > 0) BuildCoins(root);
            var numbers = MatchSession.LastMatchNumbers;
            if (numbers != null && numbers.Length == 2) BuildTable(root, numbers[0], numbers[1]);
            if (MatchSession.InTournamentMatch) BuildTournament(root);
        }

        /// <summary>Torneio: Avançar e Sair no lugar de Jogar de novo e Menu, e a situação do jogador.</summary>
        private void BuildTournament(RectTransform root)
        {
            foreach (var (objectName, label) in new[] { ("Jogar de novo", "Avançar"), ("Menu", "Sair") })
            {
                var button = root.Find(objectName);
                var text = button != null ? button.GetComponentInChildren<Text>() : null;
                if (text != null) text.text = Txt.Get(label);
            }

            var run = Tournament.Run;
            string status;
            var next = Tournament.PlayerMatch();
            if (next != null)
                status = Txt.Format("Torneio: próximo jogo na {0}",
                    next.third ? Txt.Get("disputa de 3º lugar") : Tournament.RoundName(next.round, Tournament.Rounds).ToLowerInvariant());
            else status = Txt.Format("Torneio: {0}", Tournament.PositionText(run));
            CreateText(root, "Torneio", status, 30, FontStyle.Bold, HighlightColor, new Vector2(0f, TournamentY), new Vector2(680f, 50f));
        }

        /// <summary>Moedas ganhas e, com anúncio disponível, "Dobrar moedas" ao lado (some depois de usar).</summary>
        private void BuildCoins(RectTransform root)
        {
            bool canDouble = Ads.RewardedAvailable && !PlayerStats.CoinsDoubled;
            coinsLabel = CreateText(root, "Moedas", string.Empty, 30, FontStyle.Bold, HighlightColor,
                new Vector2(canDouble ? -115f : 0f, CoinsY), new Vector2(canDouble ? 440f : 680f, 44f));
            RefreshCoins();
            if (!canDouble) return;
            doubleButton = OverlayUi.CreateButton(root, "Dobrar moedas", "Dobrar moedas", new Vector2(225f, CoinsY), new Vector2(230f, 62f),
                DoubleCoins, ButtonStyle.Yellow);
            LocalizedText.Attach(OverlayUi.Label(doubleButton));
            // Entra no começo da lista do teclado/controle.
            var navigator = GetComponent<MenuNavigator>();
            if (navigator != null) navigator.Prepend(OverlayUi.Entry(doubleButton));
        }

        private void RefreshCoins()
        {
            if (coinsLabel != null) coinsLabel.text = Txt.Format("+{0} moedas (saldo: {1})", PlayerStats.CoinsEarned, Shop.Coins);
        }

        private void DoubleCoins()
        {
            doubleButton.interactable = false;
            Ads.ShowRewarded(rewarded =>
            {
                if (this == null) return;
                if (!rewarded)
                {
                    doubleButton.interactable = true;
                    return;
                }
                watchedReward = true;
                PlayerStats.DoubleCoins();
                RefreshCoins();
                doubleButton.gameObject.SetActive(false);
            });
        }

        public void PlayAgain()
        {
            if (leaving) return;
            leaving = true;
            Ads.AfterMatch(PlayAgainNow, watchedReward);
        }

        private void PlayAgainNow()
        {
            // Torneio: "Avançar" volta ao Menu já na chave (ou no fim do torneio).
            if (MatchSession.InTournamentMatch)
            {
                MatchSession.OpenTournament = true;
                if (menuRoute != null) menuRoute.RequestRoute();
                return;
            }
            // Mesma mesa, feltro, traves e bola (só uma partida nova pelo Menu muda).
            if (matchRoute != null) matchRoute.RequestRoute();
        }

        public void BackToMenu()
        {
            if (leaving) return;
            leaving = true;
            Ads.AfterMatch(() =>
            {
                if (menuRoute != null) menuRoute.RequestRoute();
            }, watchedReward);
        }

        // ---- Placar ----

        private void BuildScore(RectTransform root, MatchResult result)
        {
            foreach (var side in new[] { TeamSide.Bottom, TeamSide.Top })
            {
                float x = side == TeamSide.Bottom ? -DiscX : DiscX;
                BuildDisc(root, side, new Vector2(x, ScoreY));
                CreateText(root, $"Nome {side}", MatchController.TeamName(side), 30, FontStyle.Bold, Color.white,
                    new Vector2(x, NameY), new Vector2(260f, 50f));
            }
            CreateText(root, "Placar", $"{result.Bottom} x {result.Top}", 96, FontStyle.Bold, Color.white,
                new Vector2(0f, ScoreY), new Vector2(280f, 130f));
            if (result.HasPenalties)
                foreach (var (side, goals) in new[] { (-1f, result.PenaltiesBottom), (1f, result.PenaltiesTop) })
                    CreateText(root, side < 0f ? "Pênaltis (baixo)" : "Pênaltis (cima)", $"({goals})", 40, FontStyle.Bold, HighlightColor,
                        new Vector2(side * PenaltyX, PenaltyY), new Vector2(110f, 56f));
            if (result.Walkover)
                CreateText(root, "W.O.", "W.O.", 30, FontStyle.Bold, HighlightColor, new Vector2(0f, NameY), new Vector2(200f, 50f));
        }

        /// <summary>O botão do time com as camadas da camisa (as cores e o padrão da Seleção de times).</summary>
        private static void BuildDisc(RectTransform root, TeamSide side, Vector2 position)
        {
            var piece = CreateRect(root, $"Botão {side}", position, Vector2.one * DiscSize);
            var primary = MatchSession.PrimaryColor(side);
            var secondary = MatchSession.SecondaryColor(side);
            var shirt = MatchSession.DiscPattern(side);
            if (shirt == null)
            {
                // Sem a arte: um quadrado na cor do time.
                var plain = piece.gameObject.AddComponent<Image>();
                plain.color = primary;
                plain.raycastTarget = false;
                piece.sizeDelta = Vector2.one * DiscSize * 0.5f;
                return;
            }

            for (int i = 0; i < ShirtArt.LayerCount; i++)
            {
                var layer = (ShirtLayer)i;
                var sprite = shirt.Get(layer);
                if (sprite == null) continue;
                var image = CreateRect(piece, layer.ToString(), Vector2.zero, Vector2.one * DiscSize).gameObject.AddComponent<Image>();
                image.sprite = sprite;
                image.color = ShirtArt.Tint(layer, primary, secondary);
                image.raycastTarget = false;
            }
        }

        // ---- Tabela ----

        private void BuildTable(RectTransform root, SaveData.MatchNumbers bottom, SaveData.MatchNumbers top)
        {
            var rows = new List<(string label, string left, string right)>
            {
                ("Chutes a gol (Pro Gol!)", bottom.shots.ToString(), top.shots.ToString()),
                ("Conversão", Conversion(bottom), Conversion(top)),
                ("Posse de bola", Share(bottom.touches, top.touches), Share(top.touches, bottom.touches)),
                ("Toques na bola", bottom.touches.ToString(), top.touches.ToString()),
                ("Toques errados", bottom.misses.ToString(), top.misses.ToString()),
                ("Faltas", bottom.fouls.ToString(), top.fouls.ToString()),
                ("Cartões (amarelo / vermelho)", $"{bottom.yellow} / {bottom.red}", $"{top.yellow} / {top.red}"),
            };
            if (bottom.ownGoals > 0 || top.ownGoals > 0)
                rows.Add(("Gols contra", bottom.ownGoals.ToString(), top.ownGoals.ToString()));

            float height = rows.Count * RowHeight + 2f * TablePadding;
            var panel = CreateRect(root, "Números", new Vector2(0f, TableTop - height * 0.5f), new Vector2(TableWidth, height)).gameObject.AddComponent<Image>();
            panel.raycastTarget = false;
            var sprite = ButtonArt.Find(ButtonSkin.HudPanelName);
            if (sprite != null)
            {
                panel.sprite = sprite;
                panel.type = Image.Type.Sliced;
            }
            else panel.color = PanelColor;

            float inner = TableWidth - 2f * TablePadding;
            for (int i = 0; i < rows.Count; i++)
            {
                var (label, left, right) = rows[i];
                float y = height * 0.5f - TablePadding - (i + 0.5f) * RowHeight;
                var row = CreateRect(panel.transform, label, new Vector2(0f, y), new Vector2(inner, RowHeight));
                if (i % 2 == 1)
                {
                    var band = row.gameObject.AddComponent<Image>();
                    band.color = StripeColor;
                    band.raycastTarget = false;
                }
                float valueX = (inner - ValueWidth) * 0.5f;
                CreateText(row, "Baixo", left, 28, FontStyle.Bold, Color.white, new Vector2(-valueX, 0f), new Vector2(ValueWidth, RowHeight));
                CreateText(row, "Nome", Txt.Get(label), 24, FontStyle.Normal, Color.white, Vector2.zero, new Vector2(inner - 2f * ValueWidth, RowHeight));
                CreateText(row, "Cima", right, 28, FontStyle.Bold, Color.white, new Vector2(valueX, 0f), new Vector2(ValueWidth, RowHeight));
            }
        }

        private static string Percent(float value) => value.ToString("0", CultureInfo.InvariantCulture) + "%";

        /// <summary>Gols que saíram de um "Pro Gol!" sobre os "Pro Gol!" avisados.</summary>
        private static string Conversion(SaveData.MatchNumbers n) => n.shots > 0 ? Percent(100f * n.shotGoals / n.shots) : "–";

        /// <summary>Posse de bola: a parte dos toques na bola que foi deste time.</summary>
        private static string Share(int own, int other) => own + other > 0 ? Percent(100f * own / (own + other)) : "–";

        // ---- UI ----

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
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 12;
            text.resizeTextMaxSize = size;
            text.raycastTarget = false;
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = ShadowColor;
            shadow.effectDistance = ShadowDistance;
            return text;
        }
    }
}
