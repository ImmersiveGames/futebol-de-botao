using System;
using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Ranking "Pontos da semana" (decidido em 16/11): cada torneio terminado vale
    /// colocação × nível × tamanho da copa + saldo de gols (no máximo +3 por partida, nunca negativo no torneio)
    /// + 10 por título logo depois de outro título na mesma semana. A pontuação da semana é a soma dos
    /// <see cref="BestCount"/> melhores torneios, então nunca diminui e o Google (que guarda só o maior valor da semana)
    /// mostra o certo. A semana vira de sábado para domingo no horário do Pacífico, igual ao ranking semanal do Google.
    /// </summary>
    public static class WeeklyRanking
    {
        public const int BestCount = 10;
        private const int TitleStreakBonus = 10;
        private const int MaxGoalDifferencePerMatch = 3;

        private static readonly float[] LevelFactor = { 0.5f, 1f, 1.5f, 2f };

        private static SaveData.WeeklyData Data => GameSave.Data.weekly;

        /// <summary>Pontos da semana atual (soma dos melhores torneios).</summary>
        public static int Points
        {
            get
            {
                Roll();
                var points = new List<int>(Data.points);
                points.Sort((a, b) => b.CompareTo(a));
                int sum = 0;
                for (int i = 0; i < points.Count && i < BestCount; i++) sum += points[i];
                return sum;
            }
        }

        /// <summary>Torneios terminados na semana atual.</summary>
        public static int Tournaments
        {
            get
            {
                Roll();
                return Data.points.Count;
            }
        }

        /// <summary>Pontos do último torneio registrado (para a tela do fim do torneio).</summary>
        public static int LastTournamentPoints { get; private set; }

        /// <summary>
        /// Torneio terminado: calcula os pontos, guarda na semana e manda a soma ao Google. <paramref name="stageFromFinal"/>
        /// só vale para quem foi eliminado: 0 = final... 2 = quartas, 3 = oitavas.
        /// </summary>
        public static void TournamentFinished(int position, int stageFromFinal, bool abandoned, int level, int teams, int goalDifference)
        {
            Roll();
            int points = 0;
            bool title = !abandoned && position == 1;
            if (!abandoned)
            {
                int placement = position switch
                {
                    1 => 100,
                    2 => 60,
                    3 => 40,
                    4 => 30,
                    _ => stageFromFinal <= 2 ? 15 : 8
                };
                float factor = LevelFactor[Mathf.Clamp(level, 0, LevelFactor.Length - 1)] * (teams >= 16 ? 1.5f : 1f);
                points = Mathf.RoundToInt(placement * factor) + Mathf.Max(0, goalDifference);
                if (title && Data.lastTitle) points += TitleStreakBonus;
            }
            Data.lastTitle = title;
            Data.points.Add(points);
            LastTournamentPoints = points;
            GameSave.WriteOnly("ranking semanal");
            Report();
        }

        /// <summary>Saldo de gols do jogador no torneio, com no máximo +3 por partida (pênaltis não contam).</summary>
        public static int GoalDifference(IEnumerable<(int goalsFor, int goalsAgainst)> matches)
        {
            int total = 0;
            foreach (var (goalsFor, goalsAgainst) in matches)
                total += Mathf.Min(goalsFor - goalsAgainst, MaxGoalDifferencePerMatch);
            return Mathf.Max(0, total);
        }

        /// <summary>Manda a pontuação da semana ao Google (com login e algum ponto).</summary>
        public static void Report()
        {
            int points = Points;
            if (points > 0) PlayGames.ReportScore(points);
        }

        // Semana nova: começa do zero.
        private static void Roll()
        {
            string week = CurrentWeek();
            if (Data.week == week) return;
            Data.week = week;
            Data.points.Clear();
            Data.lastTitle = false;
        }

        private static string CurrentWeek()
        {
            DateTime pacific = ToPacific(DateTime.UtcNow);
            return pacific.Date.AddDays(-(int)pacific.DayOfWeek).ToString("yyyy-MM-dd");
        }

        private static DateTime ToPacific(DateTime utc)
        {
            foreach (var id in new[] { "America/Los_Angeles", "Pacific Standard Time" })
            {
                try
                {
                    return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById(id));
                }
                catch (Exception)
                {
                    // Tenta o próximo nome (Android/Linux usam o do IANA, Windows o outro).
                }
            }
            // Sem fuso: horário de verão do Pacífico vai de março a novembro.
            bool summer = utc.Month > 3 && utc.Month < 11;
            return utc.AddHours(summer ? -7 : -8);
        }
    }
}
