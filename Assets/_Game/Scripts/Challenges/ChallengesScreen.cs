using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Tela Desafios (botão laranja no Menu), por cima de tudo como as Configurações. Fixos: título, a sequência (7
    /// bolinhas do ciclo, dias seguidos e o que falta hoje), as abas Hoje e Semana, o tempo até virar e o Voltar. No meio,
    /// uma caixa de rolagem com os cartões: Hoje = os 3 desafios do dia (texto, barra, prêmio, "Pegar" ou "Trocar" com
    /// anúncio) e o bônus dos 3; Semana = os 3 semanais. Abre espaço para o banner (<see cref="BannerSpace"/>). Monta tudo
    /// por código; sequência e página são remontadas a cada mudança (<see cref="Challenges.Changed"/>).
    /// </summary>
    public sealed class ChallengesScreen : MonoBehaviour
    {
        private const float PanelWidth = 660f;
        private const float PanelHeight = 1160f;
        private const float CardWidth = 600f;
        private const float CardHeight = 150f;
        private const float StreakY = 375f;
        private const float ListTop = 150f;
        private const float ListBottom = -440f;
        private const float ListWidth = 620f;
        private static readonly Color CardColor = new(0f, 0f, 0f, 0.45f);
        private static readonly Color DoneCardColor = new(0.25f, 0.2f, 0f, 0.6f);
        private static readonly Color BarBack = new(0.1f, 0.1f, 0.1f, 1f);
        private static readonly Color BarFill = new(0.3f, 0.69f, 0.31f);
        private static readonly Color Gold = new(1f, 0.85f, 0.25f);
        private static readonly Color DoneDay = new(0.18f, 0.62f, 0.31f);
        private static readonly Color OpenDay = new(0.23f, 0.23f, 0.23f);
        private static readonly Color[] TierColors = { new(0.3f, 0.69f, 0.31f), new(0.94f, 0.63f, 0.13f), new(0.9f, 0.2f, 0.18f) };
        private static readonly string[] TierNames = { "Fácil", "Médio", "Difícil" };

        private static ChallengesScreen open;

        private readonly List<MenuNavigator> pausedNavigators = new();
        private Action onClose;
        private RectTransform panel;
        private RectTransform page;
        private RectTransform streak;
        /// <summary>Onde as peças são montadas agora (a sequência fixa ou a página da caixa de rolagem).</summary>
        private RectTransform layer;
        private RectTransform content;
        private ScrollRect scroll;
        private readonly Dictionary<MenuEntry, (float top, float bottom)> spans = new();
        private Text timerLabel;
        private Button todayTab, weekTab, back;
        private MenuNavigator navigator;
        private bool weekPage;
        private float nextTimerUpdate;
        private bool swapping;

        /// <summary>Abre a tela (se já estiver aberta, nada muda); <paramref name="closed"/> roda ao fechar.</summary>
        public static void Open(Action closed = null)
        {
            if (open != null) return;
            var go = new GameObject("Desafios", typeof(RectTransform));
            go.SetActive(false);
            open = go.AddComponent<ChallengesScreen>();
            open.onClose = closed;
            open.Build();
            go.SetActive(true);
            // Depois de ligar: com o objeto desligado o Attach não acha o Canvas (GetComponentInParent pula os desligados).
            BannerSpace.Attach(open.panel);
        }

        private void Close()
        {
            Challenges.Changed -= Rebuild;
            OverlayUi.ResumeNavigators(pausedNavigators);
            if (open == this) open = null;
            var closed = onClose;
            Destroy(gameObject);
            closed?.Invoke();
        }

        private void OnDestroy()
        {
            Challenges.Changed -= Rebuild;
            if (open == this) open = null;
        }

        private void Update()
        {
            // Teclado/controle: a lista rola até o cartão escolhido.
            if (PadMode.Active && navigator != null && navigator.Current != null && spans.TryGetValue(navigator.Current, out var span))
                ScrollTo(span.top, span.bottom);
            if (Time.unscaledTime < nextTimerUpdate) return;
            nextTimerUpdate = Time.unscaledTime + 1f;
            UpdateTimer();
        }

        private void Build()
        {
            var root = OverlayUi.CreateCanvas(gameObject, 500);
            panel = OverlayUi.CreatePanel(root, new Vector2(PanelWidth, PanelHeight));
            LocalizedText.Attach(OverlayUi.CreateText(panel, "Título", "Desafios", 48, FontStyle.Bold, Color.white,
                new Vector2(0f, 530f), new Vector2(600f, 70f)));
            todayTab = OverlayUi.CreateButton(panel, "Hoje", Txt.Get("Hoje"), new Vector2(-110f, 230f), new Vector2(200f, 64f),
                () => ShowPage(false));
            weekTab = OverlayUi.CreateButton(panel, "Semana", Txt.Get("Semana"), new Vector2(110f, 230f), new Vector2(200f, 64f),
                () => ShowPage(true));
            timerLabel = OverlayUi.CreateText(panel, "Tempo", string.Empty, 22, FontStyle.Normal, OverlayUi.LabelColor,
                new Vector2(0f, 175f), new Vector2(600f, 30f));
            BuildList();
            back = OverlayUi.CreateButton(panel, "Voltar", Txt.Get("Voltar"), new Vector2(0f, -500f), new Vector2(420f, 80f), Close,
                ButtonStyle.Red);

            pausedNavigators.AddRange(OverlayUi.PauseNavigators(gameObject));
            navigator = gameObject.AddComponent<MenuNavigator>();
            Challenges.Changed += Rebuild;
            ShowPage(false);
        }

        /// <summary>Caixa de rolagem entre o tempo e o Voltar; a página fica presa no topo do conteúdo.</summary>
        private void BuildList()
        {
            var viewport = OverlayUi.CreateRect(panel, "Lista", new Vector2(0f, (ListTop + ListBottom) * 0.5f),
                new Vector2(ListWidth, ListTop - ListBottom));
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = Color.clear;

            content = OverlayUi.CreateRect(viewport, "Conteúdo", Vector2.zero, Vector2.zero);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);

            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;
        }

        private void ShowPage(bool week)
        {
            bool changed = week != weekPage || page == null;
            weekPage = week;
            Rebuild();
            if (changed)
            {
                content.anchoredPosition = Vector2.zero;
                scroll.velocity = Vector2.zero;
            }
        }

        /// <summary>Rola o mínimo para mostrar a faixa [top, bottom] (distância do topo do conteúdo).</summary>
        private void ScrollTo(float top, float bottom)
        {
            float view = scroll.viewport.rect.height;
            float overflow = Mathf.Max(0f, content.rect.height - view);
            float y = content.anchoredPosition.y;
            if (top < y) y = top;
            else if (bottom > y + view) y = bottom - view;
            content.anchoredPosition = new Vector2(0f, Mathf.Clamp(y, 0f, overflow));
        }

        /// <summary>Monta de novo a página aberta (progresso, prêmios, troca).</summary>
        private void Rebuild()
        {
            if (this == null) return;
            if (streak != null) Destroy(streak.gameObject);
            streak = OverlayUi.CreateRect(panel, "Sequência", Vector2.zero, Vector2.zero);
            layer = streak;
            BuildStreak(StreakY);

            if (page != null) Destroy(page.gameObject);
            page = OverlayUi.CreateRect(content, weekPage ? "Página Semana" : "Página Hoje", Vector2.zero, Vector2.zero);
            page.anchorMin = page.anchorMax = new Vector2(0.5f, 1f);
            page.anchoredPosition = Vector2.zero;
            layer = page;
            spans.Clear();
            ButtonSkin.Apply(todayTab, weekPage ? ButtonStyle.Green : ButtonStyle.Orange);
            ButtonSkin.Apply(weekTab, weekPage ? ButtonStyle.Orange : ButtonStyle.Green);

            var entries = new List<MenuEntry> { Entry(todayTab), Entry(weekTab) };
            float height = weekPage ? BuildWeek(entries) : BuildToday(entries);
            content.sizeDelta = new Vector2(0f, height);
            entries.Add(Entry(back));
            navigator.Configure(entries.ToArray(), back, Mathf.Min(navigatorIndex, entries.Count - 1));
            UpdateTimer();
        }

        private int navigatorIndex => weekPage ? 1 : 0;

        private static MenuEntry Entry(Button button)
        {
            if (!button.TryGetComponent<MenuEntry>(out var entry)) entry = OverlayUi.Entry(button);
            return entry;
        }

        private void UpdateTimer()
        {
            if (timerLabel == null) return;
            if (weekPage) timerLabel.text = Txt.Format("A semana vira em {0}", Duration(WeeklyRanking.TimeToNextWeek()));
            else timerLabel.text = Txt.Format("Novos desafios em {0}", Duration(DateTime.Today.AddDays(1) - DateTime.Now));
        }

        private static string Duration(TimeSpan span)
        {
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;
            return span.TotalDays >= 1 ? $"{(int)span.TotalDays}d {span.Hours}h" : $"{span.Hours}h {span.Minutes:00}min";
        }

        // ---- Hoje ----

        /// <summary>Página Hoje (y a partir do topo da caixa, para baixo negativo). Devolve a altura usada.</summary>
        private float BuildToday(List<MenuEntry> entries)
        {
            var daily = Challenges.Daily;
            float top = 5f;
            for (int i = 0; i < daily.Count; i++)
            {
                BuildCard(daily[i], -(top + CardHeight * 0.5f), entries);
                top += CardHeight + 15f;
            }
            BuildBonus(-(top + 50f), entries);
            top += 100f;
            if (Ads.RewardedAvailable)
            {
                OverlayUi.CreateText(layer, "Trocar: explicação", Txt.Get("Trocar = assistir anúncio (1 troca por dia)"), 18,
                    FontStyle.Normal, OverlayUi.LabelColor, new Vector2(0f, -(top + 25f)), new Vector2(600f, 26f));
                top += 45f;
            }
            return top + 10f;
        }

        private void BuildStreak(float y)
        {
            Box(layer, "Sequência", new Vector2(0f, y), new Vector2(CardWidth, 190f), CardColor);
            int days = Challenges.StreakDays;
            string title = days == 1 ? Txt.Get("Sequência: 1 dia") : Txt.Format("Sequência: {0} dias seguidos", days);
            OverlayUi.CreateText(layer, "Dias seguidos", title, 28, FontStyle.Bold, Color.white, new Vector2(0f, y + 68f), new Vector2(560f, 36f));

            int today = Challenges.TodayInCycle;
            bool counted = Challenges.TodayCounted;
            int count = Challenges.StreakRewards.Length;
            for (int i = 0; i < count; i++)
            {
                float x = -255f + i * 85f;
                bool done = i < today || (i == today && counted);
                bool isToday = i == today && !counted;
                float size = i == count - 1 ? 68f : 60f;
                if (isToday)
                    OverlayUi.CreateImageOf(layer, "Hoje (anel)", UiKit.Circle, Gold, new Vector2(x, y + 8f), Vector2.one * (size + 8f));
                OverlayUi.CreateImageOf(layer, $"Dia {i + 1}", UiKit.Circle, done ? DoneDay : OpenDay, new Vector2(x, y + 8f), Vector2.one * size);
                OverlayUi.CreateText(layer, $"Dia {i + 1} prêmio", done ? "✓" : $"+{Challenges.StreakRewards[i]}", done ? 30 : 20,
                    FontStyle.Bold, done ? Color.white : Gold, new Vector2(x, y + 8f), new Vector2(size, size));
                OverlayUi.CreateText(layer, $"Dia {i + 1} nome", Txt.Format("Dia {0}", i + 1), 16, FontStyle.Normal,
                    OverlayUi.LabelColor, new Vector2(x, y - 40f), new Vector2(84f, 22f));
            }

            string line = counted
                ? Txt.Get("Hoje já conta na sequência!")
                : Txt.Format("Hoje: termine {0} partidas ({1}/{0})", Challenges.StreakMatches,
                    Mathf.Min(Challenges.MatchesToday, Challenges.StreakMatches));
            OverlayUi.CreateText(layer, "Hoje", line, 22, FontStyle.Bold, Gold, new Vector2(0f, y - 72f), new Vector2(560f, 30f));
        }

        private void BuildBonus(float y, List<MenuEntry> entries)
        {
            var daily = Challenges.Daily;
            int done = 0;
            foreach (var slot in daily)
                if (Challenges.IsComplete(slot)) done++;
            bool claimed = Challenges.DailyBonusClaimed;
            Box(layer, "Bônus", new Vector2(0f, y), new Vector2(CardWidth, 100f), Challenges.CanClaimBonus ? DoneCardColor : CardColor);
            var label = OverlayUi.CreateText(layer, "Bônus: texto", Txt.Get("Bônus: complete os 3"), 26, FontStyle.Bold, Color.white,
                new Vector2(-130f, y + 20f), new Vector2(330f, 34f));
            label.alignment = TextAnchor.MiddleLeft;
            Bar(new Vector2(-130f, y - 22f), 330f, done, daily.Count);
            Reward(new Vector2(205f, y + 22f), ChallengeCatalog.DailyBonus);
            if (claimed) Claimed(new Vector2(205f, y - 22f));
            else if (Challenges.CanClaimBonus)
            {
                var claim = OverlayUi.CreateButton(layer, "Pegar bônus", Txt.Get("Pegar"), new Vector2(205f, y - 20f), new Vector2(150f, 56f),
                    () => Challenges.ClaimBonus(), ButtonStyle.Yellow);
                var entry = Entry(claim);
                entries.Add(entry);
                Track(entry, y, 100f);
            }
        }

        // ---- Semana ----

        /// <summary>Página Semana (como a Hoje). Devolve a altura usada.</summary>
        private float BuildWeek(List<MenuEntry> entries)
        {
            var weekly = Challenges.Weekly;
            float top = 5f;
            for (int i = 0; i < weekly.Count; i++)
            {
                BuildCard(weekly[i], -(top + CardHeight * 0.5f), entries);
                top += CardHeight + 20f;
            }
            OverlayUi.CreateText(layer, "Semana: explicação", Txt.Get("Os desafios da semana não podem ser trocados."), 20,
                FontStyle.Normal, OverlayUi.LabelColor, new Vector2(0f, -(top + 20f)), new Vector2(600f, 30f));
            return top + 50f;
        }

        /// <summary>Guarda onde fica o cartão de <paramref name="entry"/> (para a rolagem do teclado/controle).</summary>
        private void Track(MenuEntry entry, float centerY, float height) =>
            spans[entry] = (-centerY - height * 0.5f, -centerY + height * 0.5f);

        // ---- Peças ----

        private void BuildCard(SaveData.ChallengeSlot slot, float y, List<MenuEntry> entries)
        {
            var def = ChallengeCatalog.Find(slot.id);
            if (def == null) return;
            bool complete = Challenges.IsComplete(slot);
            Box(layer, def.id, new Vector2(0f, y), new Vector2(CardWidth, CardHeight), complete && !slot.claimed ? DoneCardColor : CardColor);

            float left = -285f;
            string tag = def.weekly ? Txt.Get("Semana") : Txt.Get(TierNames[(int)def.tier]);
            var tagBox = Box(layer, "Etiqueta", new Vector2(left + 55f, y + 48f), new Vector2(110f, 30f), TierColors[(int)def.tier]);
            OverlayUi.CreateText(tagBox, "Texto", tag.ToUpperInvariant(), 18, FontStyle.Bold, Color.white, Vector2.zero, new Vector2(106f, 28f));

            var text = OverlayUi.CreateText(layer, "Desafio", Txt.Get(def.text), 26, FontStyle.Bold, Color.white,
                new Vector2(left + 195f, y + 6f), new Vector2(390f, 44f));
            text.alignment = TextAnchor.MiddleLeft;
            Bar(new Vector2(left + 165f, y - 45f), 330f, slot.progress, def.target);

            Reward(new Vector2(205f, y + 42f), def.reward);
            var at = new Vector2(205f, y - 28f);
            if (slot.claimed) Claimed(at);
            else if (complete)
            {
                var claim = OverlayUi.CreateButton(layer, "Pegar", Txt.Get("Pegar"), at, new Vector2(150f, 64f),
                    () => Challenges.Claim(slot), ButtonStyle.Yellow);
                var entry = Entry(claim);
                entries.Add(entry);
                Track(entry, y, CardHeight);
            }
            else if (!def.weekly && !Challenges.SwapUsed && Ads.RewardedAvailable)
            {
                Button swap = null;
                swap = OverlayUi.CreateButton(layer, "Trocar", Txt.Get("Trocar"), at, new Vector2(150f, 64f), () =>
                {
                    if (swapping) return;
                    swapping = true;
                    swap.interactable = false;
                    Challenges.Swap(slot, _ =>
                    {
                        swapping = false;
                        if (swap != null) swap.interactable = true;
                    });
                }, ButtonStyle.Orange);
                var entry = Entry(swap);
                entries.Add(entry);
                Track(entry, y, CardHeight);
            }
        }

        /// <summary>Barra de progresso com "2/3" à direita; <paramref name="center"/> é o meio da barra.</summary>
        private void Bar(Vector2 center, float width, int value, int target)
        {
            var background = Box(layer, "Barra", center, new Vector2(width, 20f), BarBack);
            float ratio = target > 0 ? Mathf.Clamp01((float)value / target) : 0f;
            if (ratio > 0f)
            {
                var fill = OverlayUi.CreateImageOf(background, "Cheio", null, ratio >= 1f ? Gold : BarFill, Vector2.zero, Vector2.zero);
                var rect = fill.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = new Vector2(ratio, 1f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            var label = OverlayUi.CreateText(layer, "Progresso", $"{Mathf.Min(value, target)}/{target}", 20, FontStyle.Bold, Color.white,
                new Vector2(center.x + width * 0.5f + 45f, center.y), new Vector2(80f, 26f));
            label.alignment = TextAnchor.MiddleLeft;
        }

        private void Reward(Vector2 at, int coins)
        {
            OverlayUi.CreateImageOf(layer, "Moeda", UiKit.Circle, Gold, at + new Vector2(-42f, 0f), Vector2.one * 26f);
            var label = OverlayUi.CreateText(layer, "Prêmio", $"+{coins}", 28, FontStyle.Bold, Gold, at + new Vector2(18f, 0f), new Vector2(90f, 36f));
            label.alignment = TextAnchor.MiddleLeft;
        }

        private void Claimed(Vector2 at) =>
            OverlayUi.CreateText(layer, "Pego", Txt.Get("Pego ✓"), 24, FontStyle.Bold, OverlayUi.LabelColor, at, new Vector2(150f, 40f));

        private static RectTransform Box(Transform parent, string objectName, Vector2 position, Vector2 size, Color color)
        {
            var image = OverlayUi.CreateImageOf(parent, objectName, null, color, position, size);
            return image.rectTransform;
        }
    }
}
