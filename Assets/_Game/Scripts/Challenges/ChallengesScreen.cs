using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Tela Desafios (botão laranja no Menu), por cima de tudo como as Configurações: abas Hoje e Semana. Hoje: a
    /// sequência (7 bolinhas do ciclo, dias seguidos e o que falta hoje), os 3 desafios do dia (texto, barra, prêmio,
    /// "Pegar" ou "Trocar" com anúncio) e o bônus dos 3. Semana: os 3 semanais. Monta tudo por código; a página aberta é
    /// remontada a cada mudança (<see cref="Challenges.Changed"/>).
    /// </summary>
    public sealed class ChallengesScreen : MonoBehaviour
    {
        private const float PanelWidth = 660f;
        private const float PanelHeight = 1160f;
        private const float CardWidth = 600f;
        private const float CardHeight = 150f;
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
            todayTab = OverlayUi.CreateButton(panel, "Hoje", Txt.Get("Hoje"), new Vector2(-110f, 455f), new Vector2(200f, 64f),
                () => ShowPage(false));
            weekTab = OverlayUi.CreateButton(panel, "Semana", Txt.Get("Semana"), new Vector2(110f, 455f), new Vector2(200f, 64f),
                () => ShowPage(true));
            timerLabel = OverlayUi.CreateText(panel, "Tempo", string.Empty, 22, FontStyle.Normal, OverlayUi.LabelColor,
                new Vector2(0f, 400f), new Vector2(600f, 30f));
            back = OverlayUi.CreateButton(panel, "Voltar", Txt.Get("Voltar"), new Vector2(0f, -500f), new Vector2(420f, 80f), Close,
                ButtonStyle.Red);

            pausedNavigators.AddRange(OverlayUi.PauseNavigators(gameObject));
            navigator = gameObject.AddComponent<MenuNavigator>();
            Challenges.Changed += Rebuild;
            ShowPage(false);
        }

        private void ShowPage(bool week)
        {
            weekPage = week;
            Rebuild();
        }

        /// <summary>Monta de novo a página aberta (progresso, prêmios, troca).</summary>
        private void Rebuild()
        {
            if (this == null) return;
            if (page != null) Destroy(page.gameObject);
            page = OverlayUi.CreateRect(panel, weekPage ? "Página Semana" : "Página Hoje", Vector2.zero, Vector2.zero);
            ButtonSkin.Apply(todayTab, weekPage ? ButtonStyle.Green : ButtonStyle.Orange);
            ButtonSkin.Apply(weekTab, weekPage ? ButtonStyle.Orange : ButtonStyle.Green);

            var entries = new List<MenuEntry> { Entry(todayTab), Entry(weekTab) };
            if (weekPage) BuildWeek(entries);
            else BuildToday(entries);
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

        private void BuildToday(List<MenuEntry> entries)
        {
            BuildStreak(292f);
            var daily = Challenges.Daily;
            for (int i = 0; i < daily.Count; i++) BuildCard(daily[i], 115f - i * 165f, entries);
            BuildBonus(-370f, entries);
            if (Ads.RewardedAvailable)
                OverlayUi.CreateText(page, "Trocar: explicação", Txt.Get("Trocar = assistir anúncio (1 troca por dia)"), 18,
                    FontStyle.Normal, OverlayUi.LabelColor, new Vector2(0f, -440f), new Vector2(600f, 26f));
        }

        private void BuildStreak(float y)
        {
            Box(page, "Sequência", new Vector2(0f, y), new Vector2(CardWidth, 190f), CardColor);
            int days = Challenges.StreakDays;
            string title = days == 1 ? Txt.Get("Sequência: 1 dia") : Txt.Format("Sequência: {0} dias seguidos", days);
            OverlayUi.CreateText(page, "Dias seguidos", title, 28, FontStyle.Bold, Color.white, new Vector2(0f, y + 68f), new Vector2(560f, 36f));

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
                    OverlayUi.CreateImageOf(page, "Hoje (anel)", UiKit.Circle, Gold, new Vector2(x, y + 8f), Vector2.one * (size + 8f));
                OverlayUi.CreateImageOf(page, $"Dia {i + 1}", UiKit.Circle, done ? DoneDay : OpenDay, new Vector2(x, y + 8f), Vector2.one * size);
                OverlayUi.CreateText(page, $"Dia {i + 1} prêmio", done ? "✓" : $"+{Challenges.StreakRewards[i]}", done ? 30 : 20,
                    FontStyle.Bold, done ? Color.white : Gold, new Vector2(x, y + 8f), new Vector2(size, size));
                OverlayUi.CreateText(page, $"Dia {i + 1} nome", Txt.Format("Dia {0}", i + 1), 16, FontStyle.Normal,
                    OverlayUi.LabelColor, new Vector2(x, y - 40f), new Vector2(84f, 22f));
            }

            string line = counted
                ? Txt.Get("Hoje já conta na sequência!")
                : Txt.Format("Hoje: termine {0} partidas ({1}/{0})", Challenges.StreakMatches,
                    Mathf.Min(Challenges.MatchesToday, Challenges.StreakMatches));
            OverlayUi.CreateText(page, "Hoje", line, 22, FontStyle.Bold, Gold, new Vector2(0f, y - 72f), new Vector2(560f, 30f));
        }

        private void BuildBonus(float y, List<MenuEntry> entries)
        {
            var daily = Challenges.Daily;
            int done = 0;
            foreach (var slot in daily)
                if (Challenges.IsComplete(slot)) done++;
            bool claimed = Challenges.DailyBonusClaimed;
            Box(page, "Bônus", new Vector2(0f, y), new Vector2(CardWidth, 100f), Challenges.CanClaimBonus ? DoneCardColor : CardColor);
            var label = OverlayUi.CreateText(page, "Bônus: texto", Txt.Get("Bônus: complete os 3"), 26, FontStyle.Bold, Color.white,
                new Vector2(-130f, y + 20f), new Vector2(330f, 34f));
            label.alignment = TextAnchor.MiddleLeft;
            Bar(new Vector2(-130f, y - 22f), 330f, done, daily.Count);
            Reward(new Vector2(205f, y + 22f), ChallengeCatalog.DailyBonus);
            if (claimed) Claimed(new Vector2(205f, y - 22f));
            else if (Challenges.CanClaimBonus)
            {
                var claim = OverlayUi.CreateButton(page, "Pegar bônus", Txt.Get("Pegar"), new Vector2(205f, y - 20f), new Vector2(150f, 56f),
                    () => Challenges.ClaimBonus(), ButtonStyle.Yellow);
                entries.Add(Entry(claim));
            }
        }

        // ---- Semana ----

        private void BuildWeek(List<MenuEntry> entries)
        {
            var weekly = Challenges.Weekly;
            for (int i = 0; i < weekly.Count; i++) BuildCard(weekly[i], 280f - i * 170f, entries);
            OverlayUi.CreateText(page, "Semana: explicação", Txt.Get("Os desafios da semana não podem ser trocados."), 20,
                FontStyle.Normal, OverlayUi.LabelColor, new Vector2(0f, -150f), new Vector2(600f, 30f));
        }

        // ---- Peças ----

        private void BuildCard(SaveData.ChallengeSlot slot, float y, List<MenuEntry> entries)
        {
            var def = ChallengeCatalog.Find(slot.id);
            if (def == null) return;
            bool complete = Challenges.IsComplete(slot);
            Box(page, def.id, new Vector2(0f, y), new Vector2(CardWidth, CardHeight), complete && !slot.claimed ? DoneCardColor : CardColor);

            float left = -285f;
            string tag = def.weekly ? Txt.Get("Semana") : Txt.Get(TierNames[(int)def.tier]);
            var tagBox = Box(page, "Etiqueta", new Vector2(left + 55f, y + 48f), new Vector2(110f, 30f), TierColors[(int)def.tier]);
            OverlayUi.CreateText(tagBox, "Texto", tag.ToUpperInvariant(), 18, FontStyle.Bold, Color.white, Vector2.zero, new Vector2(106f, 28f));

            var text = OverlayUi.CreateText(page, "Desafio", Txt.Get(def.text), 26, FontStyle.Bold, Color.white,
                new Vector2(left + 195f, y + 6f), new Vector2(390f, 44f));
            text.alignment = TextAnchor.MiddleLeft;
            Bar(new Vector2(left + 165f, y - 45f), 330f, slot.progress, def.target);

            Reward(new Vector2(205f, y + 42f), def.reward);
            var at = new Vector2(205f, y - 28f);
            if (slot.claimed) Claimed(at);
            else if (complete)
            {
                var claim = OverlayUi.CreateButton(page, "Pegar", Txt.Get("Pegar"), at, new Vector2(150f, 64f),
                    () => Challenges.Claim(slot), ButtonStyle.Yellow);
                entries.Add(Entry(claim));
            }
            else if (!def.weekly && !Challenges.SwapUsed && Ads.RewardedAvailable)
            {
                Button swap = null;
                swap = OverlayUi.CreateButton(page, "Trocar", Txt.Get("Trocar"), at, new Vector2(150f, 64f), () =>
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
                entries.Add(Entry(swap));
            }
        }

        /// <summary>Barra de progresso com "2/3" à direita; <paramref name="center"/> é o meio da barra.</summary>
        private void Bar(Vector2 center, float width, int value, int target)
        {
            var background = Box(page, "Barra", center, new Vector2(width, 20f), BarBack);
            float ratio = target > 0 ? Mathf.Clamp01((float)value / target) : 0f;
            if (ratio > 0f)
            {
                var fill = OverlayUi.CreateImageOf(background, "Cheio", null, ratio >= 1f ? Gold : BarFill, Vector2.zero, Vector2.zero);
                var rect = fill.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = new Vector2(ratio, 1f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            var label = OverlayUi.CreateText(page, "Progresso", $"{Mathf.Min(value, target)}/{target}", 20, FontStyle.Bold, Color.white,
                new Vector2(center.x + width * 0.5f + 45f, center.y), new Vector2(80f, 26f));
            label.alignment = TextAnchor.MiddleLeft;
        }

        private void Reward(Vector2 at, int coins)
        {
            OverlayUi.CreateImageOf(page, "Moeda", UiKit.Circle, Gold, at + new Vector2(-42f, 0f), Vector2.one * 26f);
            var label = OverlayUi.CreateText(page, "Prêmio", $"+{coins}", 28, FontStyle.Bold, Gold, at + new Vector2(18f, 0f), new Vector2(90f, 36f));
            label.alignment = TextAnchor.MiddleLeft;
        }

        private void Claimed(Vector2 at) =>
            OverlayUi.CreateText(page, "Pego", Txt.Get("Pego ✓"), 24, FontStyle.Bold, OverlayUi.LabelColor, at, new Vector2(150f, 40f));

        private static RectTransform Box(Transform parent, string objectName, Vector2 position, Vector2 size, Color color)
        {
            var image = OverlayUi.CreateImageOf(parent, objectName, null, color, position, size);
            return image.rectTransform;
        }
    }
}
