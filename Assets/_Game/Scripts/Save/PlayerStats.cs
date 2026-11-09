namespace FutebolDeBotao
{
    /// <summary>
    /// Estatísticas do jogador contra a IA, gravadas no <see cref="GameSave"/>: a partida marca no save que começou,
    /// e terminar grava o resultado e os números do jogador (o time de baixo). Sair no meio (menu, reiniciar ou fechar o
    /// jogo) conta como abandonada e zera a sequência de vitórias. Partidas de 2 jogadores não entram. Os números da IA
    /// ficam só em <see cref="MatchSession.LastMatchNumbers"/>, para a tela do pós-jogo.
    /// </summary>
    public static class PlayerStats
    {
        private static SaveData.StatsData Data => GameSave.Data.stats;

        /// <summary>
        /// A última partida bateu o recorde de sequência de vitórias (no Geral): o novo recorde; 0 = não bateu. Só a
        /// partir de 2 vitórias seguidas (a primeira vitória não é recorde). Para o aviso da tela de Resultado.
        /// </summary>
        public static int NewRecord { get; private set; }

        /// <summary>Moedas que a última partida pagou (para a tela de Resultado); 0 = nenhuma.</summary>
        public static int CoinsEarned { get; private set; }

        /// <summary>Começo de uma partida (também no Reiniciar e no Jogar de novo). Só conta contra a IA, vindo do Menu.</summary>
        public static void BeginMatch()
        {
            NewRecord = 0;
            CoinsEarned = 0;
            if (MatchSession.VsAi != true) return;
            CountLeftover();
            Data.inProgressLevel = (int)MatchSession.AiLevel;
            if (MatchSession.InTournamentMatch) Tournament.MarkPlaying();
            GameSave.Save("partida começou");
        }

        /// <summary>Saiu da partida no meio (menu ou reiniciar): abandonada. Partida já terminada não conta de novo.</summary>
        public static void Abandon()
        {
            // Torneio: sair no meio da partida é derrota e fim do torneio (grava sozinho).
            if (MatchSession.InTournamentMatch) Tournament.Forfeit();
            if (CountLeftover()) GameSave.Save("partida abandonada");
        }

        /// <summary>Conta como abandonada a partida marcada em andamento, sem gravar. Devolve se havia uma.</summary>
        public static bool CountLeftover()
        {
            int level = Data.inProgressLevel;
            if (level < 0) return false;
            Data.inProgressLevel = -1;
            if (level > 2) return true;
            foreach (var stats in new[] { Data.total, Data.levels[level] })
            {
                stats.abandoned++;
                stats.streak = 0;
            }
            return true;
        }

        /// <summary>
        /// Fim da partida (também W.O.): resultado, sequência, números do jogador e a Última partida. Com disputa de
        /// pênaltis (<paramref name="playerPenalties"/> 0 ou mais), quem venceu a disputa venceu a partida.
        /// </summary>
        public static void Finish(SaveData.MatchNumbers player, int playerScore, int aiScore, bool walkover,
            int playerPenalties = -1, int aiPenalties = -1)
        {
            int level = Data.inProgressLevel;
            if (level < 0 || level > 2) return;
            Data.inProgressLevel = -1;

            bool shootout = playerPenalties >= 0 && aiPenalties >= 0;
            int outcome = playerScore.CompareTo(aiScore);
            if (outcome == 0 && shootout) outcome = playerPenalties.CompareTo(aiPenalties);

            foreach (var stats in new[] { Data.total, Data.levels[level] })
            {
                if (shootout)
                {
                    if (playerPenalties > aiPenalties) stats.shootoutWins++;
                    else stats.shootoutLosses++;
                    stats.shootoutGoals += playerPenalties;
                    stats.shootoutConceded += aiPenalties;
                }
                if (outcome > 0)
                {
                    stats.wins++;
                    stats.streak++;
                    if (stats.streak > stats.bestStreak)
                    {
                        stats.bestStreak = stats.streak;
                        if (stats == Data.total && stats.streak > 1) NewRecord = stats.streak;
                    }
                }
                else
                {
                    if (outcome == 0) stats.draws++;
                    else stats.losses++;
                    stats.streak = 0;
                }
                stats.numbers.Add(player);
            }

            int coins = Shop.RewardFor(level, outcome);
            Data.levels[level].coins += coins;
            Data.total.coins += coins;
            Data.coinsEarned += coins;

            var last = new SaveData.LastMatchData
            {
                level = level,
                playerScore = playerScore,
                aiScore = aiScore,
                walkover = walkover,
                coins = coins,
                hasPenalties = shootout,
                playerPenalties = shootout ? playerPenalties : 0,
                aiPenalties = shootout ? aiPenalties : 0
            };
            last.numbers.Add(player);
            Data.last = last;
            Data.hasLast = true;
            CoinsEarned = coins;
            Shop.AddCoins(CoinsEarned);
            GameSave.Save("fim da partida");
        }

        /// <summary>"Zerar estatísticas": apaga tudo (todos os níveis, recordes e a Última partida).</summary>
        public static void ResetAll()
        {
            GameSave.Data.stats = new SaveData.StatsData();
            GameSave.Save("zerar estatísticas");
        }
    }
}
