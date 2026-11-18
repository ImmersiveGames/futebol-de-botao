using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>Mata-mata: fases, chave da fase seguinte, 3º lugar e quando o jogador sai. Parte do <see cref="Tournament"/>.</summary>
    public static partial class Tournament
    {
        /// <summary>Fase atual do mata-mata: a primeira com jogo por jogar (ou a última).</summary>
        public static int CurrentRound()
        {
            int round = Mathf.Max(0, Rounds - 1);
            foreach (var match in Run.matches)
                if (!match.groupStage && !match.played && match.round < round) round = match.round;
            return round;
        }

        /// <summary>Jogos de uma fase do mata-mata, de cima para baixo (na final: a final e depois o 3º lugar).</summary>
        public static List<SaveData.TournamentMatch> MatchesOf(int round)
        {
            var list = new List<SaveData.TournamentMatch>();
            foreach (var match in Run.matches)
                if (!match.groupStage && match.round == round) list.Add(match);
            list.Sort((a, b) => a.third != b.third ? a.third.CompareTo(b.third) : a.order.CompareTo(b.order));
            return list;
        }

        /// <summary>Nome da fase pela distância até a final: Final, Semifinal, Quartas de final, Oitavas de final...</summary>
        public static string RoundName(int round, int rounds)
        {
            return Txt.Get((rounds - 1 - round) switch
            {
                0 => "Final",
                1 => "Semifinal",
                2 => "Quartas de final",
                3 => "Oitavas de final",
                _ => "Primeira fase"
            });
        }

        /// <summary>Nome curto para a aba.</summary>
        public static string TabName(int round, int rounds)
        {
            return Txt.Get((rounds - 1 - round) switch
            {
                0 => "Final",
                1 => "Semi",
                2 => "Quartas",
                3 => "Oitavas",
                _ => "1ª fase"
            });
        }

        /// <summary>Mata-mata: sorteia os outros jogos da fase, monta as próximas fases e vê se o jogador terminou.</summary>
        private static void AdvanceKnockout()
        {
            var run = Run;
            int rounds = Rounds;
            CheckPlayerOut();
            for (int round = 0; round < rounds; round++)
            {
                var matches = MatchesOf(round);
                if (matches.Count == 0) break;
                // Os outros jogos da fase só saem depois do jogo do jogador nela.
                if (run.active && matches.Exists(m => !m.played && m.Has(Player))) return;
                foreach (var match in matches)
                {
                    // O jogo do jogador ainda ativo espera ele jogar.
                    if (match.played || (run.active && match.Has(Player))) continue;
                    Simulate(match);
                }
                if (!matches.TrueForAll(m => m.played)) return;
                if (round + 1 < rounds && MatchesOf(round + 1).Count == 0) BuildNextRound(round, rounds);
                CheckPlayerOut();
            }
        }

        /// <summary>Monta a fase seguinte pelos vencedores (e o 3º lugar pelos perdedores das semifinais).</summary>
        private static void BuildNextRound(int round, int rounds)
        {
            var previous = MatchesOf(round);
            int next = round + 1;
            var winners = new List<SaveData.TournamentMatch>();
            foreach (var match in previous)
                if (!match.third) winners.Add(match);
            for (int i = 0; i + 1 < winners.Count; i += 2)
                Run.matches.Add(new SaveData.TournamentMatch
                {
                    round = next, order = i / 2, home = winners[i].Winner, away = winners[i + 1].Winner
                });
            if (next == rounds - 1 && winners.Count == 2)
                Run.matches.Add(new SaveData.TournamentMatch
                {
                    round = next, order = 1, third = true, home = winners[0].Loser, away = winners[1].Loser
                });
        }

        /// <summary>O jogador perdeu fora da semifinal, ou jogou a final ou o 3º lugar: termina o torneio para ele.</summary>
        private static void CheckPlayerOut()
        {
            var run = Run;
            if (!run.active) return;
            int rounds = Rounds;
            SaveData.TournamentMatch last = null;
            foreach (var match in run.matches)
                if (!match.groupStage && match.played && match.Has(Player) && (last == null || match.round > last.round || match.third))
                    last = match;
            if (last == null) return;

            bool won = last.Winner == Player;
            int toFinal = rounds - 1 - last.round;
            int position;
            if (toFinal == 0) position = last.third ? (won ? 3 : 4) : (won ? 1 : 2);
            else if (won || toFinal == 1) return;     // ganhou, ou perdeu a semifinal (vai para o 3º lugar)
            else
            {
                position = 0;
                run.eliminatedRound = last.round;
            }
            Finish(position);
        }
    }
}
