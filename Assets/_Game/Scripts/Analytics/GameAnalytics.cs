using System;
using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Eventos do jogo para o Analytics. Quem envia é o <see cref="Sink"/>, registrado pelo Firebase
    /// (Assets/_Game/Lojas/FirebaseServices.cs, só no aparelho e com PROGOL_FIREBASE); sem ele, nada sai do jogo.
    /// Nomes e parâmetros em inglês, minúsculos com _ (regra do Firebase: até 40 letras, sem espaço).
    /// </summary>
    public static class GameAnalytics
    {
        /// <summary>Envia um evento: nome + parâmetros (string, long ou double).</summary>
        public static Action<string, IReadOnlyList<KeyValuePair<string, object>>> Sink;

        public static void Log(string name, params (string key, object value)[] parameters)
        {
            if (Sink == null) return;
            var list = new List<KeyValuePair<string, object>>(parameters.Length);
            foreach (var (key, value) in parameters) list.Add(new KeyValuePair<string, object>(key, value));
            try
            {
                Sink(name, list);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Analytics] {name}: {exception.Message}");
            }
        }

        /// <summary>"tutorial", "torneio", "ia" ou "2j".</summary>
        public static string MatchMode =>
            Tutorial.Active ? "tutorial" : MatchSession.InTournamentMatch ? "torneio" : MatchSession.VsAi == true ? "ia" : "2j";

        public static string AiLevelName => MatchSession.VsAi == true ? MatchSession.AiLevel.ToString().ToLowerInvariant() : "-";

        /// <summary>Começo da partida, com as opções escolhidas.</summary>
        public static void MatchStart(MatchOptions options) => Log("match_start", ("mode", MatchMode), ("level", AiLevelName),
            ("discs", (long)options.discsPerTeam), ("goalkeeper", YesNo(options.hasGoalkeeper)),
            ("minutes", (long)options.durationMinutes), ("touches", (long)options.touchesPerTurn),
            ("fouls_on", YesNo(options.fouls)), ("extra_time_pct", (long)options.extraTimePercent),
            ("penalties_on", YesNo(options.PenaltiesEnabled)), ("first_ai_match", YesNo(FirstAiMatch)));

        /// <summary>Primeira partida contra a IA desta instalação (nada ganho, empatado, perdido ou abandonado ainda).</summary>
        private static bool FirstAiMatch
        {
            get
            {
                if (MatchSession.VsAi != true) return false;
                var total = GameSave.Data.stats?.total;
                return total == null || total.wins + total.draws + total.losses + total.abandoned == 0;
            }
        }

        /// <summary>Fim da partida. "Nós" = time de baixo (o jogador, ou o Jogador 1 no 2J).</summary>
        public static void MatchEnd(int bottom, int top, bool walkover, int bottomPenalties, int topPenalties, bool extraTime,
            float realSeconds, SaveData.MatchNumbers us, SaveData.MatchNumbers them)
        {
            bool shootout = bottomPenalties >= 0 && topPenalties >= 0;
            int outcome = bottom.CompareTo(top);
            if (outcome == 0 && shootout) outcome = bottomPenalties.CompareTo(topPenalties);
            us ??= new SaveData.MatchNumbers();
            them ??= new SaveData.MatchNumbers();
            Log("match_end", ("mode", MatchMode), ("level", AiLevelName),
                ("result", outcome > 0 ? "win" : outcome < 0 ? "loss" : "draw"),
                ("goals_for", (long)bottom), ("goals_against", (long)top),
                ("extra_time", YesNo(extraTime)),
                ("shootout", !shootout ? "no" : bottomPenalties > topPenalties ? "won" : "lost"),
                ("walkover", YesNo(walkover)), ("real_seconds", (long)realSeconds),
                ("progol_called", (long)us.shots), ("progol_scored", (long)us.shotGoals),
                ("opp_progol_called", (long)them.shots), ("opp_progol_scored", (long)them.shotGoals),
                ("fouls", (long)us.fouls), ("opp_fouls", (long)them.fouls),
                ("cards", (long)(us.yellow + us.red)), ("opp_cards", (long)(them.yellow + them.red)));
        }

        private static string YesNo(bool value) => value ? "yes" : "no";

        /// <summary>Anúncios entre partidas mostrados nesta sessão (desde que o jogo abriu).</summary>
        public static int InterstitialsThisSession;

        /// <summary>Saiu no meio: em que parte ("regular", "extra_time", "shootout"), em que minuto dela e com que placar.</summary>
        public static void MatchAbandon(string period, int minute, int bottom, int top) => Log("match_abandon",
            ("mode", MatchMode), ("level", AiLevelName), ("period", period), ("minute", (long)minute),
            ("goals_for", (long)bottom), ("goals_against", (long)top));
    }
}
