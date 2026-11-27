using System;
using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>O que aconteceu numa partida terminada (contra a IA, partida única, torneio ou liga), para os desafios.</summary>
    public struct ChallengeMatch
    {
        public int goals;
        public int conceded;
        /// <summary>Maior que 0 = vitória (também nos pênaltis), 0 = empate, menor = derrota.</summary>
        public int outcome;
        public bool walkover;
        public bool shootoutWon;
        public bool extraTimeWin;
        public bool comeback;
        public AiLevel level;
        public SaveData.MatchNumbers numbers;
        public int minutes;
        public bool hasGoalkeeper;
        public int discs;
        public int touchesPerTurn;
        /// <summary>Partida de torneio ou de liga.</summary>
        public bool competition;
        public bool league;
        /// <summary>Jogo de mata-mata de torneio (não liga, não fase de grupos).</summary>
        public bool knockout;
        public int setPieceGoals;
        public int wallGoals;
    }

    /// <summary>
    /// Desafios do dia (3: fácil, médio e difícil, viram à meia-noite do aparelho), da semana (3: 25, 40 e 60 moedas,
    /// viram com o ranking Pontos da semana) e a sequência de dias (o dia conta com <see cref="StreakMatches"/> partidas
    /// terminadas; prêmio crescente num ciclo de 7 dias). Só partidas contra a IA (partida única, torneio e liga) contam.
    /// O prêmio de cada desafio se pega na tela Desafios ("Pegar"); o da sequência entra sozinho. Um desafio do dia pode
    /// ser trocado por dia, com anúncio. Ver o doc "Desafios diários – Pro Gol!".
    /// </summary>
    public static class Challenges
    {
        public const int DailyCount = 3;
        public const int StreakMatches = 2;
        public static readonly int[] StreakRewards = { 5, 5, 10, 10, 15, 15, 30 };

        /// <summary>Desafios completados desde o começo da última partida (para a Premiação).</summary>
        public static readonly List<ChallengeDef> JustCompleted = new();

        /// <summary>A última partida fechou o dia da sequência: quantos dias seguidos (0 = não fechou).</summary>
        public static int StreakJustReached { get; private set; }

        /// <summary>Moedas que a sequência pagou ao fechar o dia (0 = nada).</summary>
        public static int StreakJustPaid { get; private set; }

        /// <summary>Algo mudou (progresso, prêmio, troca): a tela e o aviso do Menu se atualizam.</summary>
        public static event Action Changed;

        private static SaveData.ChallengesData Data => GameSave.Data.challenges ??= new SaveData.ChallengesData();

        private static string Today => DateTime.Now.ToString("yyyy-MM-dd");
        private static string Yesterday => DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");

        /// <summary>Os 3 do dia (sorteia se o dia virou).</summary>
        public static List<SaveData.ChallengeSlot> Daily
        {
            get
            {
                Refresh();
                return Data.daily;
            }
        }

        /// <summary>Os 3 da semana (sorteia se a semana virou).</summary>
        public static List<SaveData.ChallengeSlot> Weekly
        {
            get
            {
                Refresh();
                return Data.weekly;
            }
        }

        public static bool IsComplete(SaveData.ChallengeSlot slot)
        {
            var def = ChallengeCatalog.Find(slot?.id);
            return def != null && slot.progress >= def.target;
        }

        public static bool AllDailyComplete
        {
            get
            {
                var daily = Daily;
                if (daily.Count == 0) return false;
                foreach (var slot in daily)
                    if (!IsComplete(slot)) return false;
                return true;
            }
        }

        public static bool DailyBonusClaimed => Data.dailyBonusClaimed;

        public static bool CanClaimBonus => AllDailyComplete && !Data.dailyBonusClaimed;

        /// <summary>Há prêmio esperando o "Pegar" (bolinha no botão do Menu).</summary>
        public static bool HasRewardToClaim
        {
            get
            {
                foreach (var slot in Daily)
                    if (IsComplete(slot) && !slot.claimed) return true;
                foreach (var slot in Weekly)
                    if (IsComplete(slot) && !slot.claimed) return true;
                return CanClaimBonus;
            }
        }

        /// <summary>A troca de hoje ainda está livre e há anúncio com prêmio.</summary>
        public static bool CanSwap => !Data.swapUsed && Ads.RewardedAvailable;

        public static bool SwapUsed => Data.swapUsed;

        // ---- Sequência ----

        /// <summary>Dias seguidos valendo agora (a sequência cai se ontem não entrou).</summary>
        public static int StreakDays
        {
            get
            {
                var data = Data;
                return data.streakDay == Today || data.streakDay == Yesterday ? data.streakDays : 0;
            }
        }

        public static int BestStreak => Data.bestStreak;

        /// <summary>Hoje já entrou na sequência.</summary>
        public static bool TodayCounted => Data.streakDay == Today;

        /// <summary>Partidas terminadas hoje (para "termine 2 partidas (1/2)").</summary>
        public static int MatchesToday => Data.matchesDay == Today ? Data.matchesToday : 0;

        /// <summary>Posição de hoje no ciclo de 7 dias (0 a 6); os anteriores do ciclo já foram feitos.</summary>
        public static int TodayInCycle
        {
            get
            {
                int days = StreakDays;
                return TodayCounted ? (days - 1) % StreakRewards.Length : days % StreakRewards.Length;
            }
        }

        // ---- Sorteio ----

        /// <summary>Sorteia de novo o que virou (dia ou semana). Grava sem esperar quando mudou.</summary>
        public static void Refresh()
        {
            var data = Data;
            bool changed = false;
            string today = Today;
            if (data.day != today || data.daily.Count != DailyCount)
            {
                data.previousDaily.Clear();
                foreach (var slot in data.daily) data.previousDaily.Add(slot.id);
                data.daily.Clear();
                foreach (ChallengeTier tier in Enum.GetValues(typeof(ChallengeTier)))
                    data.daily.Add(new SaveData.ChallengeSlot { id = Pick(ChallengeCatalog.Daily, d => d.tier == tier, data.previousDaily) });
                data.day = today;
                data.swapUsed = false;
                data.dailyBonusClaimed = false;
                changed = true;
            }
            string week = WeeklyRanking.CurrentWeek();
            if (data.week != week || data.weekly.Count != ChallengeCatalog.WeeklyRewards.Length)
            {
                data.previousWeekly.Clear();
                foreach (var slot in data.weekly) data.previousWeekly.Add(slot.id);
                data.weekly.Clear();
                foreach (int reward in ChallengeCatalog.WeeklyRewards)
                    data.weekly.Add(new SaveData.ChallengeSlot { id = Pick(ChallengeCatalog.Weekly, d => d.reward == reward, data.previousWeekly) });
                data.week = week;
                changed = true;
            }
            if (changed) GameSave.WriteOnly("desafios novos");
        }

        /// <summary>Um da lista que passa no filtro, evitando os de <paramref name="avoid"/> (se sobrar algum).</summary>
        private static string Pick(ChallengeDef[] list, Func<ChallengeDef, bool> filter, ICollection<string> avoid)
        {
            var options = new List<ChallengeDef>();
            foreach (var def in list)
                if (filter(def) && !avoid.Contains(def.id)) options.Add(def);
            if (options.Count == 0)
                foreach (var def in list)
                    if (filter(def)) options.Add(def);
            return options.Count > 0 ? options[UnityEngine.Random.Range(0, options.Count)].id : null;
        }

        // ---- Progresso ----

        /// <summary>Começo de partida: limpa o que a Premiação da anterior mostrou.</summary>
        public static void BeginMatch()
        {
            JustCompleted.Clear();
            StreakJustReached = 0;
            StreakJustPaid = 0;
        }

        /// <summary>Fim de partida que entrou nas estatísticas: desafios e sequência. Grava.</summary>
        public static void MatchFinished(ChallengeMatch match)
        {
            Refresh();
            Advance(def => Amount(def, match));
            CountStreakMatch();
            GameSave.WriteOnly("desafios");
            Changed?.Invoke();
        }

        /// <summary>Lição do tutorial completada (mesmo repetida).</summary>
        public static void TutorialLesson() => AddAndSave(ChallengeKind.TutorialLesson, 1);

        /// <summary>Moedas ganhas em partidas e torneios (o Financeiro chama; não grava, quem pagou grava).</summary>
        public static void CoinsEarned(int amount)
        {
            if (amount <= 0) return;
            Refresh();
            Advance(def => def.kind == ChallengeKind.Coins ? amount : 0);
            Changed?.Invoke();
        }

        /// <summary>Torneio ou liga terminados (abandonado não conta).</summary>
        public static void TournamentFinished(TournamentConfig config, int position, bool abandoned)
        {
            if (config == null || abandoned || position <= 0) return;
            Refresh();
            bool league = config.IsLeague;
            Advance(def => def.kind switch
            {
                ChallengeKind.TournamentTitle => !league && position == 1 ? 1 : 0,
                ChallengeKind.BigFinal => !league && config.TeamCount >= 16 && position <= 2 ? 1 : 0,
                ChallengeKind.LeagueTop3 => league && position <= 3 ? 1 : 0,
                _ => 0,
            });
            GameSave.WriteOnly("desafios (torneio)");
            Changed?.Invoke();
        }

        private static void AddAndSave(ChallengeKind kind, int amount)
        {
            Refresh();
            Advance(def => def.kind == kind ? amount : 0);
            GameSave.WriteOnly("desafios");
            Changed?.Invoke();
        }

        /// <summary>Soma em cada desafio (dia e semana) o que <paramref name="amount"/> disser; avisa os que fecharam.</summary>
        private static void Advance(Func<ChallengeDef, int> amount)
        {
            foreach (var slot in Data.daily) Advance(slot, amount);
            foreach (var slot in Data.weekly) Advance(slot, amount);
        }

        private static void Advance(SaveData.ChallengeSlot slot, Func<ChallengeDef, int> amount)
        {
            var def = ChallengeCatalog.Find(slot.id);
            if (def == null || slot.progress >= def.target) return;
            int add = amount(def);
            if (add <= 0) return;
            slot.progress = Mathf.Min(def.target, slot.progress + add);
            if (slot.progress < def.target) return;
            JustCompleted.Add(def);
            GameAnalytics.Log("challenge_complete", ("challenge", def.id), ("weekly", def.weekly ? "yes" : "no"));
        }

        /// <summary>Quanto a partida soma num desafio.</summary>
        private static int Amount(ChallengeDef def, ChallengeMatch m)
        {
            var n = m.numbers ?? new SaveData.MatchNumbers();
            bool win = m.outcome > 0;
            bool draw = m.goals == m.conceded;
            return def.kind switch
            {
                ChallengeKind.Matches => 1,
                ChallengeKind.Wins => win ? 1 : 0,
                ChallengeKind.Goals => m.goals,
                ChallengeKind.ShotsCalled => n.shots,
                ChallengeKind.ShotGoals => n.shotGoals,
                ChallengeKind.Touches => n.touches,
                ChallengeKind.NoCardMatch => !m.walkover && n.yellow + n.red == 0 ? 1 : 0,
                ChallengeKind.WinOrDraw => win || draw ? 1 : 0,
                ChallengeKind.Draw => draw ? 1 : 0,
                ChallengeKind.WinAtLevel => win && m.level >= def.minLevel ? 1 : 0,
                ChallengeKind.CleanSheetWin => win && m.conceded == 0 ? 1 : 0,
                ChallengeKind.CompetitionWin => win && m.competition ? 1 : 0,
                ChallengeKind.ComebackWin => win && m.comeback ? 1 : 0,
                ChallengeKind.NoFoulWin => win && !m.walkover && n.fouls == 0 ? 1 : 0,
                ChallengeKind.SetPieceGoals => m.setPieceGoals,
                ChallengeKind.BigWin => win && m.goals - m.conceded >= 3 ? 1 : 0,
                ChallengeKind.HatTrick => m.goals >= 3 ? 1 : 0,
                ChallengeKind.KnockoutWin => win && m.knockout ? 1 : 0,
                ChallengeKind.ShootoutWin => m.shootoutWon ? 1 : 0,
                ChallengeKind.ExtraTimeWin => m.extraTimeWin ? 1 : 0,
                ChallengeKind.OneTouchWin => win && m.touchesPerTurn == 1 ? 1 : 0,
                ChallengeKind.MatchOfMinutes => !m.walkover && m.minutes == def.minutes ? 1 : 0,
                ChallengeKind.NoKeeperMatch => !m.walkover && !m.hasGoalkeeper ? 1 : 0,
                ChallengeKind.FiveDiscMatch => !m.walkover && m.discs == 5 ? 1 : 0,
                ChallengeKind.LeaguePoints => m.league ? win ? 3 : draw ? 1 : 0 : 0,
                ChallengeKind.LeagueMatches => m.league ? 1 : 0,
                ChallengeKind.WallGoals => m.wallGoals,
                _ => 0,
            };
        }

        /// <summary>Mais uma partida hoje; a 2ª fecha o dia da sequência e paga o prêmio do dia do ciclo.</summary>
        private static void CountStreakMatch()
        {
            var data = Data;
            string today = Today;
            if (data.matchesDay != today)
            {
                data.matchesDay = today;
                data.matchesToday = 0;
            }
            data.matchesToday++;
            if (data.matchesToday < StreakMatches || data.streakDay == today) return;
            data.streakDays = data.streakDay == Yesterday ? data.streakDays + 1 : 1;
            data.streakDay = today;
            data.bestStreak = Mathf.Max(data.bestStreak, data.streakDays);
            int reward = StreakRewards[(data.streakDays - 1) % StreakRewards.Length];
            StreakJustReached = data.streakDays;
            StreakJustPaid = reward;
            Finance.Earn(FinanceSource.Challenge, Txt.Format("Sequência: {0} dias seguidos", data.streakDays), reward);
            GameAnalytics.Log("streak_day", ("days", (long)data.streakDays));
        }

        // ---- Prêmios e troca ----

        /// <summary>"Pegar" num desafio completo: paga e grava. Devolve as moedas (0 = nada a pegar).</summary>
        public static int Claim(SaveData.ChallengeSlot slot)
        {
            var def = ChallengeCatalog.Find(slot?.id);
            if (def == null || slot.claimed || slot.progress < def.target) return 0;
            slot.claimed = true;
            Finance.Earn(FinanceSource.Challenge, Txt.Get(def.weekly ? "Desafio semanal" : "Desafio do dia"), def.reward);
            GameSave.Save("prêmio de desafio");
            Changed?.Invoke();
            return def.reward;
        }

        /// <summary>"Pegar" o bônus dos 3 do dia.</summary>
        public static int ClaimBonus()
        {
            if (!CanClaimBonus) return 0;
            Data.dailyBonusClaimed = true;
            Finance.Earn(FinanceSource.Challenge, Txt.Get("Bônus dos desafios do dia"), ChallengeCatalog.DailyBonus);
            GameSave.Save("bônus dos desafios");
            Changed?.Invoke();
            return ChallengeCatalog.DailyBonus;
        }

        /// <summary>"Trocar" um desafio do dia ainda não completo: anúncio com prêmio e, assistido, outro da mesma dificuldade.</summary>
        public static void Swap(SaveData.ChallengeSlot slot, Action<bool> done)
        {
            var def = ChallengeCatalog.Find(slot?.id);
            if (def == null || def.weekly || IsComplete(slot) || !CanSwap)
            {
                done?.Invoke(false);
                return;
            }
            Ads.ShowRewarded(rewarded =>
            {
                if (rewarded && !Data.swapUsed && Data.daily.Contains(slot))
                {
                    var avoid = new List<string>(Data.previousDaily);
                    foreach (var other in Data.daily) avoid.Add(other.id);
                    slot.id = Pick(ChallengeCatalog.Daily, d => d.tier == def.tier, avoid);
                    slot.progress = 0;
                    slot.claimed = false;
                    Data.swapUsed = true;
                    GameAnalytics.Log("challenge_swap", ("from", def.id), ("to", slot.id ?? "-"));
                    GameSave.Save("troca de desafio");
                    Changed?.Invoke();
                }
                done?.Invoke(rewarded);
            }, "trocar_desafio");
        }

        /// <summary>Texto do desafio no idioma em uso.</summary>
        public static string Text(SaveData.ChallengeSlot slot)
        {
            var def = ChallengeCatalog.Find(slot?.id);
            return def != null ? Txt.Get(def.text) : string.Empty;
        }

        // ---- Teste (menu do editor) ----

        /// <summary>Completa todos (dia e semana), para testar os prêmios.</summary>
        public static void DebugCompleteAll()
        {
            Refresh();
            Advance(def => def.target);
            GameSave.Save("teste: desafios completos");
            Changed?.Invoke();
        }

        /// <summary>Esquece o dia e a semana: sorteia de novo na hora.</summary>
        public static void DebugNewDraw()
        {
            Data.day = null;
            Data.week = null;
            Refresh();
            GameSave.Save("teste: desafios novos");
            Changed?.Invoke();
        }

        // Com o "Enter Play Mode" sem recarregar o domínio, os estáticos sobrevivem entre execuções no editor.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            JustCompleted.Clear();
            StreakJustReached = 0;
            StreakJustPaid = 0;
            Changed = null;
        }
    }
}
