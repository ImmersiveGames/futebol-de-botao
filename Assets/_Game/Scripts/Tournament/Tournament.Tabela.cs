using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>Grupos da copa e liga: tabela de jogos (todos contra todos), classificação com desempates e passagem dos grupos para o mata-mata. Parte do <see cref="Tournament"/>.</summary>
    public static partial class Tournament
    {
        /// <summary>
        /// Jogos dos grupos (a liga é um grupo só): os times de <see cref="SaveData.TournamentRun.slots"/> em sequência
        /// formam os grupos; todos contra todos (método do círculo), no 2º turno com o mando invertido. Ficam na ordem das
        /// rodadas, para o próximo jogo do jogador ser sempre o primeiro que falta.
        /// </summary>
        private static void BuildGroupMatches(SaveData.TournamentRun run, TournamentConfig config)
        {
            int groups = config.GroupCount;
            int size = config.IsLeague ? run.slots.Count : config.GroupSize;
            var all = new List<SaveData.TournamentMatch>();
            for (int g = 0; g < groups; g++)
            {
                var teams = new List<int>();
                for (int i = g * size; i < (g + 1) * size && i < run.slots.Count; i++) teams.Add(run.slots[i]);
                all.AddRange(RoundRobin(teams, config.LegsFor(run.legs), g));
            }
            all.Sort((a, b) => a.round != b.round ? a.round.CompareTo(b.round)
                : a.group != b.group ? a.group.CompareTo(b.group) : a.order.CompareTo(b.order));
            run.matches.AddRange(all);
        }

        private static List<SaveData.TournamentMatch> RoundRobin(List<int> teams, int legs, int group)
        {
            var list = new List<SaveData.TournamentMatch>();
            var circle = new List<int>(teams);
            if (circle.Count % 2 == 1) circle.Add(Bye);
            int n = circle.Count;
            int roundsPerLeg = n - 1;
            for (int r = 0; r < roundsPerLeg; r++)
            {
                int order = 0;
                for (int i = 0; i < n / 2; i++)
                {
                    int a = circle[i], b = circle[n - 1 - i];
                    if (a == Bye || b == Bye) continue;
                    // Mando alternado: o fixo do círculo troca a cada rodada, os outros pela posição.
                    bool aHome = i == 0 ? r % 2 == 0 : (r + i) % 2 == 0;
                    int home = aHome ? a : b, away = aHome ? b : a;
                    for (int leg = 0; leg < legs; leg++)
                        list.Add(new SaveData.TournamentMatch
                        {
                            groupStage = true, group = group, round = r + leg * roundsPerLeg, order = order,
                            home = leg == 0 ? home : away, away = leg == 0 ? away : home
                        });
                    order++;
                }
                // Gira: o primeiro fica, o último passa a ser o segundo.
                int last = circle[n - 1];
                circle.RemoveAt(n - 1);
                circle.Insert(1, last);
            }
            return list;
        }

        /// <summary>Rodada atual dos grupos ou da liga: a primeira com jogo por jogar (ou a última).</summary>
        public static int CurrentMatchday()
        {
            int last = 0;
            foreach (var match in Run.matches)
            {
                if (!match.groupStage) continue;
                if (!match.played) return match.round;
                last = Mathf.Max(last, match.round);
            }
            return last;
        }

        /// <summary>Rodadas dos grupos ou da liga.</summary>
        public static int Matchdays()
        {
            int count = 0;
            foreach (var match in Run.matches)
                if (match.groupStage) count = Mathf.Max(count, match.round + 1);
            return count;
        }

        /// <summary>Jogos de um grupo (-1 = todos os grupos) numa rodada (-1 = todas), na ordem da tabela de jogos.</summary>
        public static List<SaveData.TournamentMatch> GroupMatches(int group, int matchday)
        {
            var list = new List<SaveData.TournamentMatch>();
            foreach (var match in Run.matches)
                if (match.groupStage && (group < 0 || match.group == group) && (matchday < 0 || match.round == matchday)) list.Add(match);
            return list;
        }

        /// <summary>Grupos guardados (liga = 1).</summary>
        public static int GroupCount()
        {
            int count = 0;
            foreach (var match in Run.matches)
                if (match.groupStage) count = Mathf.Max(count, match.group + 1);
            return count;
        }

        /// <summary>Grupo do jogador (-1: sem grupos).</summary>
        public static int PlayerGroup()
        {
            foreach (var match in Run.matches)
                if (match.groupStage && match.Has(Player)) return match.group;
            return -1;
        }

        public static string GroupName(int group) => Txt.Format("Grupo {0}", (char)('A' + Mathf.Clamp(group, 0, 25)));

        /// <summary>Uma linha da tabela.</summary>
        public sealed class Standing
        {
            public int team;
            public int played, won, drawn, lost, goalsFor, goalsAgainst;
            public int Points => won * 3 + drawn;
            public int GoalDifference => goalsFor - goalsAgainst;
        }

        /// <summary>Classificação de um grupo (na liga, o 0): pontos, saldo, gols pró, confronto direto e sorteio.</summary>
        public static List<Standing> Table(int group)
        {
            var rows = new Dictionary<int, Standing>();
            var matches = GroupMatches(group, -1);
            foreach (var match in matches)
            {
                foreach (int team in new[] { match.home, match.away })
                    if (!rows.ContainsKey(team)) rows[team] = new Standing { team = team };
                if (!match.played) continue;
                Count(rows[match.home], match.homeGoals, match.awayGoals);
                Count(rows[match.away], match.awayGoals, match.homeGoals);
            }
            var list = new List<Standing>(rows.Values);
            int seed = Run.seed;
            list.Sort((a, b) =>
            {
                if (a.Points != b.Points) return b.Points.CompareTo(a.Points);
                if (a.GoalDifference != b.GoalDifference) return b.GoalDifference.CompareTo(a.GoalDifference);
                if (a.goalsFor != b.goalsFor) return b.goalsFor.CompareTo(a.goalsFor);
                int headToHead = HeadToHead(matches, a.team, b.team);
                if (headToHead != 0) return -headToHead;
                return Draw(seed, a.team).CompareTo(Draw(seed, b.team));
            });
            return list;
        }

        private static void Count(Standing row, int goalsFor, int goalsAgainst)
        {
            row.played++;
            row.goalsFor += goalsFor;
            row.goalsAgainst += goalsAgainst;
            if (goalsFor > goalsAgainst) row.won++;
            else if (goalsFor == goalsAgainst) row.drawn++;
            else row.lost++;
        }

        /// <summary>Pontos de <paramref name="a"/> menos os de <paramref name="b"/> nos jogos entre os dois.</summary>
        private static int HeadToHead(List<SaveData.TournamentMatch> matches, int a, int b)
        {
            int points = 0;
            foreach (var match in matches)
            {
                if (!match.played || !match.Has(a) || !match.Has(b)) continue;
                int goalsA = match.home == a ? match.homeGoals : match.awayGoals;
                int goalsB = match.home == a ? match.awayGoals : match.homeGoals;
                points += goalsA > goalsB ? 3 : goalsA < goalsB ? -3 : 0;
            }
            return points;
        }

        /// <summary>Sorteio fixo do torneio (mesmo número toda vez para o mesmo time).</summary>
        private static int Draw(int seed, int team) => unchecked((team * 486187739) ^ seed) & int.MaxValue;

        /// <summary>
        /// Grupos e liga: sorteia os jogos das rodadas que o jogador já passou (todos, se ele não joga mais). Com todos
        /// jogados, a liga termina e a copa monta o mata-mata. Devolve se a fase de grupos acabou.
        /// </summary>
        private static bool AdvanceGroups()
        {
            var run = Run;
            var config = Config;
            var next = PlayerMatch();
            int upTo = next != null && next.groupStage ? next.round : int.MaxValue;
            bool done = true;
            foreach (var match in run.matches)
            {
                if (!match.groupStage || match.played) continue;
                if (match.round < upTo && !(run.active && match.Has(Player))) Simulate(match);
                if (!match.played) done = false;
            }
            if (!done) return false;

            if (config.IsLeague)
            {
                if (run.active) Finish(TablePosition(0, Player));
                return false;
            }
            if (MatchesOf(0).Count == 0) BuildKnockoutFromGroups(config);
            return true;
        }

        /// <summary>Posição (1 = primeiro) de um time na tabela do grupo.</summary>
        private static int TablePosition(int group, int team)
        {
            var table = Table(group);
            for (int i = 0; i < table.Count; i++)
                if (table[i].team == team) return i + 1;
            return table.Count;
        }

        /// <summary>
        /// 1ª fase do mata-mata da copa: os classificados de cada par de grupos se cruzam (1º A x 2º B, 1º B x 2º A),
        /// com os jogos dos grupos A/B, C/D... de um lado da chave e os cruzados do outro. Com mais de 2 classificados por
        /// grupo, o 1º pega o último classificado do grupo vizinho, o 2º o penúltimo... O jogador fora: caiu nos grupos.
        /// </summary>
        private static void BuildKnockoutFromGroups(TournamentConfig config)
        {
            var run = Run;
            int groups = GroupCount();
            int qualifiers = Mathf.Max(1, config.Qualifiers);
            var tables = new List<List<Standing>>();
            for (int g = 0; g < groups; g++) tables.Add(Table(g));

            var firstHalf = new List<(int, int)>();
            var secondHalf = new List<(int, int)>();
            if (groups == 1)
            {
                // Um grupo só: 1º x último classificado, 2º x penúltimo...
                var table = tables[0];
                int count = Mathf.Min(qualifiers, table.Count);
                for (int i = 0; i < count / 2; i++) firstHalf.Add((table[i].team, table[count - 1 - i].team));
            }
            else
            {
                for (int g = 0; g + 1 < groups; g += 2)
                {
                    var a = tables[g];
                    var b = tables[g + 1];
                    // 1º A x último classificado de B... (metade de cima) e 1º B x último de A... (metade de baixo).
                    for (int i = 0; i < qualifiers; i++)
                    {
                        int j = qualifiers - 1 - i;
                        if (i >= a.Count || j >= b.Count) continue;
                        if (i < (qualifiers + 1) / 2) firstHalf.Add((a[i].team, b[j].team));
                        else secondHalf.Add((b[j].team, a[i].team));
                    }
                }
            }
            var pairs = new List<(int home, int away)>(firstHalf);
            pairs.AddRange(secondHalf);
            int order = 0;
            var qualified = new HashSet<int>();
            foreach (var (home, away) in pairs)
            {
                run.matches.Add(new SaveData.TournamentMatch { round = 0, order = order++, home = home, away = away });
                qualified.Add(home);
                qualified.Add(away);
            }

            if (run.active && !qualified.Contains(Player))
            {
                run.eliminatedInGroups = true;
                Finish(0);
            }
        }
    }
}
