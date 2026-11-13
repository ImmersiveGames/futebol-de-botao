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

        /// <summary>As moedas da última partida já foram dobradas com o anúncio (só uma vez por partida).</summary>
        public static bool CoinsDoubled { get; private set; }

        /// <summary>
        /// "Dobrar moedas" no Resultado (o jogador assistiu ao anúncio): paga de novo o que a partida pagou, no
        /// Financeiro (partida ou torneio), nas estatísticas e nas moedas das partidas do torneio. Grava.
        /// </summary>
        public static void DoubleCoins()
        {
            int coins = CoinsEarned;
            if (coins <= 0 || CoinsDoubled) return;
            CoinsDoubled = true;
            int level = Data.hasLast ? UnityEngine.Mathf.Clamp(Data.last.level, 0, AiDifficulty.LevelCount - 1) : 0;
            Data.levels[level].coins += coins;
            Data.total.coins += coins;
            if (Data.hasLast) Data.last.coins += coins;
            CoinsEarned += coins;
            bool tournament = MatchSession.InTournamentMatch;
            if (tournament && Tournament.Run != null) Tournament.Run.matchCoins += coins;
            Finance.Earn(tournament ? FinanceSource.Tournament : FinanceSource.Match, Txt.Get("Moedas em dobro (anúncio)"), coins);
            GameSave.WriteOnly("moedas em dobro");
        }

        /// <summary>Começo de uma partida (também no Reiniciar e no Jogar de novo). Só conta contra a IA, vindo do Menu.</summary>
        public static void BeginMatch()
        {
            NewRecord = 0;
            CoinsEarned = 0;
            CoinsDoubled = false;
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
            if (level >= AiDifficulty.LevelCount) return true;
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
            if (level < 0 || level >= AiDifficulty.LevelCount) return;
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
            bool tournament = MatchSession.InTournamentMatch;
            Finance.Earn(tournament ? FinanceSource.Tournament : FinanceSource.Match,
                Finance.MatchText(level, outcome, tournament, walkover), coins);
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
