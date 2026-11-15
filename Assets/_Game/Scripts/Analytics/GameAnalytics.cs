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

        public static void MatchStart() => Log("match_start", ("mode", MatchMode), ("level", AiLevelName));

        public static void MatchEnd(int bottom, int top, bool walkover, int bottomPenalties, int topPenalties)
        {
            int outcome = bottom.CompareTo(top);
            if (outcome == 0 && bottomPenalties >= 0 && topPenalties >= 0) outcome = bottomPenalties.CompareTo(topPenalties);
            Log("match_end", ("mode", MatchMode), ("level", AiLevelName),
                ("result", outcome > 0 ? "win" : outcome < 0 ? "loss" : "draw"),
                ("goals_for", (long)bottom), ("goals_against", (long)top),
                ("penalties", bottomPenalties >= 0 ? "yes" : "no"), ("walkover", walkover ? "yes" : "no"));
        }

        public static void MatchAbandon() => Log("match_abandon", ("mode", MatchMode), ("level", AiLevelName));
    }
}
