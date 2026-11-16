using System.Collections;
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
    ///
    /// Partida que pagou moedas (contra a IA): o Resultado tem só "Avançar", que troca para a página da Premiação na mesma
    /// cena: as moedas grandes subindo, "Dobrar moedas" (anúncio com prêmio) em destaque e os botões de seguir. O anúncio
    /// entre partidas (<see cref="Ads.AfterMatch"/>) vem ao sair da Premiação.
    /// </summary>
    public sealed class ResultScreen : MonoBehaviour
    {
        private const float ScoreY = 350f;
        private const float DiscX = 230f;
        private const float DiscSize = 160f;
        private const float NameY = 245f;
        private const float RecordY = 188f;
        private const float PrizeAmountY = 300f;
        private const float PrizeWordY = 185f;
        private const float PrizeBalanceY = 120f;
        private const float DoubleY = -60f;
        private const float DoubleHintY = -160f;
        private const float CountSeconds = 0.8f;
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
        private RectTransform resultPage;
        private RectTransform prizePage;
        private Text title;
        private Button againButton;
        private Button menuButton;
        private MenuNavigator navigator;
        /// <summary>O "Avançar" do Resultado abre a Premiação (ainda não aberta).</summary>
        private bool prizeNext;
        private Text prizeAmount;
        private Text prizeBalance;
        private Text doubleHint;
        private int shownCoins;
        private Coroutine counting;
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
            title = FindChild<Text>(root, "Título");
            againButton = FindChild<Button>(root, "Jogar de novo");
            menuButton = FindChild<Button>(root, "Menu");
            navigator = GetComponent<MenuNavigator>();
            var result = MatchSession.LastResult;
            if (result == null)
            {
                CreateText(root, "Sem partida", Txt.Get("Sem partida"), 40, FontStyle.Bold, Color.white, new Vector2(0f, ScoreY), new Vector2(680f, 90f));
                return;
            }

            // Páginas no primeiro nível do Canvas (o BannerSpace sobe as duas quando há banner).
            resultPage = CreateRect(root, "Página Resultado", Vector2.zero, Vector2.zero);
            BuildScore(resultPage, result.Value);
            if (MatchSession.VsAi == true && PlayerStats.NewRecord > 0)
                CreateText(resultPage, "Recorde", Txt.Format("Novo recorde de sequência: {0} vitórias!", PlayerStats.NewRecord), 30, FontStyle.Bold,
                    HighlightColor, new Vector2(0f, RecordY), new Vector2(680f, 44f));
            var numbers = MatchSession.LastMatchNumbers;
            if (numbers != null && numbers.Length == 2) BuildTable(resultPage, numbers[0], numbers[1]);

            prizeNext = MatchSession.VsAi == true && PlayerStats.CoinsEarned > 0;
            if (prizeNext)
            {
                BuildPrize(root);
                prizePage.gameObject.SetActive(false);
                // Só "Avançar" embaixo; ele leva à Premiação.
                SetLabel(againButton, "Avançar");
                if (menuButton != null) menuButton.gameObject.SetActive(false);
                SetNavigation(null, againButton);
            }
            else if (MatchSession.InTournamentMatch) PrepareTournamentButtons();
            if (MatchSession.InTournamentMatch) BuildTournamentStatus(prizeNext ? prizePage : resultPage);
            BannerSpace.Attach(this);
        }

        private static T FindChild<T>(Transform root, string objectName) where T : Component
        {
            var child = root.Find(objectName);
            return child != null ? child.GetComponent<T>() : null;
        }

        private static void SetLabel(Button button, string label)
        {
            var text = OverlayUi.Label(button);
            if (text != null) text.text = Txt.Get(label);
        }

        /// <summary>Teclado/controle: a lista de itens da página aberta (Voltar = Menu/Sair quando existe).</summary>
        private void SetNavigation(Button first, Button second, Button back = null)
        {
            if (navigator == null) return;
            var entries = new List<MenuEntry>();
            foreach (var button in new[] { first, second, back })
            {
                if (button == null) continue;
                if (!button.TryGetComponent<MenuEntry>(out var entry)) entry = OverlayUi.Entry(button);
                entries.Add(entry);
            }
            navigator.Configure(entries.ToArray(), back, 0);
        }

        /// <summary>Torneio: Avançar e Sair no lugar de Jogar de novo e Menu.</summary>
        private void PrepareTournamentButtons()
        {
            SetLabel(againButton, "Avançar");
            SetLabel(menuButton, "Sair");
        }

        /// <summary>Torneio: como o jogador está (próximo jogo ou colocação final).</summary>
        private void BuildTournamentStatus(RectTransform page)
        {
            var run = Tournament.Run;
            string status;
            var next = Tournament.PlayerMatch();
            if (next != null)
                status = Txt.Format("Torneio: próximo jogo na {0}",
                    next.third ? Txt.Get("disputa de 3º lugar") : Tournament.RoundName(next.round, Tournament.Rounds).ToLowerInvariant());
            else status = Txt.Format("Torneio: {0}", Tournament.PositionText(run));
            CreateText(page, "Torneio", status, 30, FontStyle.Bold, HighlightColor, new Vector2(0f, TournamentY), new Vector2(680f, 50f));
        }

        // ---- Premiação ----

        /// <summary>Página da Premiação: moedas grandes, saldo e, com anúncio disponível, "Dobrar moedas" em destaque.</summary>
        private void BuildPrize(RectTransform root)
        {
            prizePage = CreateRect(root, "Página Premiação", Vector2.zero, Vector2.zero);
            prizeAmount = CreateText(prizePage, "Moedas", "+0", 120, FontStyle.Bold, HighlightColor, new Vector2(0f, PrizeAmountY), new Vector2(680f, 160f));
            CreateText(prizePage, "Palavra moedas", Txt.Get("moedas"), 40, FontStyle.Bold, HighlightColor, new Vector2(0f, PrizeWordY), new Vector2(680f, 60f));
            prizeBalance = CreateText(prizePage, "Saldo", string.Empty, 30, FontStyle.Normal, Color.white, new Vector2(0f, PrizeBalanceY), new Vector2(680f, 44f));
            if (!Ads.RewardedAvailable || PlayerStats.CoinsDoubled) return;
            doubleButton = OverlayUi.CreateButton(prizePage, "Dobrar moedas", "Dobrar moedas", new Vector2(0f, DoubleY), new Vector2(520f, 120f),
                WatchDouble, ButtonStyle.Yellow);
            var label = OverlayUi.Label(doubleButton);
            if (label != null)
            {
                label.fontSize = 44;
                label.resizeTextMaxSize = 44;
                LocalizedText.Attach(label);
            }
            doubleHint = CreateText(prizePage, "Dobrar explicação",
                Txt.Format("Assista a um anúncio e ganhe mais {0} moedas, o dobro do que esta partida pagou.", PlayerStats.CoinsEarned),
                26, FontStyle.Normal, Color.white, new Vector2(0f, DoubleHintY), new Vector2(600f, 80f));
        }

        /// <summary>"Avançar" do Resultado: troca para a Premiação com os botões de seguir.</summary>
        private void ShowPrize()
        {
            prizeNext = false;
            resultPage.gameObject.SetActive(false);
            prizePage.gameObject.SetActive(true);
            if (title != null) title.text = Txt.Get("Premiação");
            if (menuButton != null) menuButton.gameObject.SetActive(true);
            if (MatchSession.InTournamentMatch) PrepareTournamentButtons();
            else SetLabel(againButton, "Jogar de novo");
            // Com o Dobrar na tela, ele é o destaque (amarelo) e o seguir fica verde.
            if (doubleButton != null && againButton != null) ButtonSkin.Apply(againButton, ButtonStyle.Green);
            SetNavigation(doubleButton, againButton, menuButton);
            CountTo(PlayerStats.CoinsEarned);
        }

        private void CountTo(int target)
        {
            if (counting != null) StopCoroutine(counting);
            counting = StartCoroutine(Count(shownCoins, target));
        }

        private IEnumerator Count(int from, int to)
        {
            // O saldo já tem as moedas; mostra o de antes subindo junto com o número.
            int balanceAtEnd = Shop.Coins;
            for (float t = 0f; t < CountSeconds; t += Time.unscaledDeltaTime)
            {
                SetCoins(Mathf.RoundToInt(Mathf.Lerp(from, to, t / CountSeconds)), balanceAtEnd - to);
                yield return null;
            }
            SetCoins(to, balanceAtEnd - to);
            counting = null;
        }

        private void SetCoins(int coins, int balanceBefore)
        {
            shownCoins = coins;
            if (prizeAmount != null) prizeAmount.text = $"+{coins}";
            if (prizeBalance != null) prizeBalance.text = Txt.Format("Saldo: {0}", balanceBefore + coins);
        }

        /// <summary>"Dobrar moedas": o anúncio com prêmio; assistido, o número sobe até o dobro e o botão some.</summary>
        private void WatchDouble()
        {
            if (leaving || doubleButton == null) return;
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
                doubleButton.gameObject.SetActive(false);
                if (doubleHint != null) doubleHint.gameObject.SetActive(false);
                if (againButton != null) ButtonSkin.Apply(againButton, ButtonStyle.Yellow);
                SetNavigation(null, againButton, menuButton);
                CountTo(PlayerStats.CoinsEarned);
            }, "dobrar");
        }

        public void PlayAgain()
        {
            if (leaving) return;
            if (prizeNext)
            {
                ShowPrize();
                return;
            }
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
