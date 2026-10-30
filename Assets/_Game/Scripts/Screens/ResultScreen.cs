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
    /// para o Menu. O título e os botões vêm do "Criar telas"; o placar e a tabela são montados aqui.
    /// </summary>
    public sealed class ResultScreen : MonoBehaviour
    {
        private const float ScoreY = 350f;
        private const float DiscX = 230f;
        private const float DiscSize = 160f;
        private const float NameY = 245f;
        private const float RecordY = 180f;
        private static readonly Vector2 TableCenter = new(0f, -95f);
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

        public void Configure(RouteRequestTrigger match, RouteRequestTrigger menu)
        {
            matchRoute = match;
            menuRoute = menu;
        }

        private void Start()
        {
            font = GameFont.Get();
            var root = (RectTransform)transform;
            var result = MatchSession.LastResult;
            if (result == null)
            {
                CreateText(root, "Sem partida", "Sem partida", 40, FontStyle.Bold, Color.white, new Vector2(0f, ScoreY), new Vector2(680f, 90f));
                return;
            }

            BuildScore(root, result.Value);
            if (MatchSession.VsAi == true && PlayerStats.NewRecord > 0)
                CreateText(root, "Recorde", $"Novo recorde de sequência: {PlayerStats.NewRecord} vitórias!", 30, FontStyle.Bold,
                    HighlightColor, new Vector2(0f, RecordY), new Vector2(680f, 60f));
            var numbers = MatchSession.LastMatchNumbers;
            if (numbers != null && numbers.Length == 2) BuildTable(root, numbers[0], numbers[1]);
        }

        public void PlayAgain()
        {
            // Partida nova: sorteia o feltro de novo (só o Reiniciar mantém).
            MatchSession.GradientFelt = null;
            if (matchRoute != null) matchRoute.RequestRoute();
        }

        public void BackToMenu()
        {
            if (menuRoute != null) menuRoute.RequestRoute();
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
            if (result.Walkover)
                CreateText(root, "W.O.", "W.O.", 30, FontStyle.Bold, HighlightColor, new Vector2(0f, NameY), new Vector2(200f, 50f));
        }

        /// <summary>O botão do time com as camadas da camisa (as cores e o padrão da Seleção de times).</summary>
        private static void BuildDisc(RectTransform root, TeamSide side, Vector2 position)
        {
            var piece = CreateRect(root, $"Botão {side}", position, Vector2.one * DiscSize);
            var team = MatchSession.Team(side);
            var secondary = MatchSession.SecondaryColor(side);
            var shirts = ShirtArt.Load();
            if (shirts == null)
            {
                // Sem a arte: um quadrado na cor do time.
                var plain = piece.gameObject.AddComponent<Image>();
                plain.color = team.PrimaryColor;
                plain.raycastTarget = false;
                piece.sizeDelta = Vector2.one * DiscSize * 0.5f;
                return;
            }

            for (int i = 0; i < ShirtArt.LayerCount; i++)
            {
                var layer = (ShirtLayer)i;
                var sprite = shirts.disc.Get(layer, MatchSession.DiscPattern(side));
                if (sprite == null) continue;
                var image = CreateRect(piece, layer.ToString(), Vector2.zero, Vector2.one * DiscSize).gameObject.AddComponent<Image>();
                image.sprite = sprite;
                image.color = ShirtArt.Tint(layer, team.PrimaryColor, secondary);
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
            var panel = CreateRect(root, "Números", TableCenter, new Vector2(TableWidth, height)).gameObject.AddComponent<Image>();
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
                CreateText(row, "Nome", label, 24, FontStyle.Normal, Color.white, Vector2.zero, new Vector2(inner - 2f * ValueWidth, RowHeight));
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
