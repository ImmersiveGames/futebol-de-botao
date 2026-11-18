using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace FutebolDeBotao.Tests
{
    /// <summary>Tabela de jogos (todos contra todos), classificação com desempates, prêmios e os torneios em Resources/Torneios.</summary>
    public class TournamentTests
    {
        private static List<SaveData.TournamentMatch> RoundRobin(int teamCount, int legs)
        {
            var teams = new List<int>();
            for (int i = 0; i < teamCount; i++) teams.Add(i);
            return SaveSandbox.Call<List<SaveData.TournamentMatch>>(typeof(Tournament), "RoundRobin", teams, legs, 0);
        }

        [TestCase(4, 1)]
        [TestCase(5, 1)]
        [TestCase(6, 1)]
        [TestCase(10, 2)]
        [TestCase(20, 1)]
        public void TodosContraTodos_CadaParUmaVezPorTurno_UmJogoPorRodada(int teamCount, int legs)
        {
            var matches = RoundRobin(teamCount, legs);

            Assert.AreEqual(teamCount * (teamCount - 1) / 2 * legs, matches.Count);
            var pairs = new Dictionary<(int, int), int>();
            var homeGames = new Dictionary<(int, int), int>();
            var perRound = new Dictionary<(int, int), int>();
            foreach (var match in matches)
            {
                Assert.AreNotEqual(match.home, match.away);
                Assert.IsTrue(match.groupStage);
                var pair = (Mathf.Min(match.home, match.away), Mathf.Max(match.home, match.away));
                pairs[pair] = pairs.TryGetValue(pair, out int n) ? n + 1 : 1;
                homeGames[(match.home, match.away)] = homeGames.TryGetValue((match.home, match.away), out int h) ? h + 1 : 1;
                foreach (int team in new[] { match.home, match.away })
                {
                    var key = (match.round, team);
                    perRound[key] = perRound.TryGetValue(key, out int r) ? r + 1 : 1;
                    Assert.AreEqual(1, perRound[key], $"time {team} joga duas vezes na rodada {match.round}");
                }
            }
            Assert.AreEqual(teamCount * (teamCount - 1) / 2, pairs.Count, "algum par não se enfrenta");
            foreach (var count in pairs.Values) Assert.AreEqual(legs, count);
            if (legs == 2)
                foreach (var count in homeGames.Values) Assert.AreEqual(1, count, "no returno o mando troca");
        }

        private static SaveData.TournamentMatch Game(int home, int away, int homeGoals, int awayGoals, bool played = true) => new()
        {
            groupStage = true, home = home, away = away, homeGoals = homeGoals, awayGoals = awayGoals, played = played
        };

        private static List<int> Order(params SaveData.TournamentMatch[] matches)
        {
            var data = new SaveData();
            data.tournament.matches.AddRange(matches);
            data.tournament.seed = 12345;
            using (SaveSandbox.Use(data))
            {
                Tournament.LeagueMode = false;
                var order = new List<int>();
                foreach (var row in Tournament.Table(0)) order.Add(row.team);
                return order;
            }
        }

        [Test]
        public void Tabela_PontosPrimeiro()
        {
            // 0 vence os dois; 1 empata com 2; 2 perde de 0 e empata.
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, Order(Game(0, 1, 1, 0), Game(0, 2, 3, 0), Game(1, 2, 1, 1)));
        }

        [Test]
        public void Tabela_EmpateEmPontos_SaldoDeGols()
        {
            // 1 e 2 vencem 0 (3 pontos cada); 2 com saldo maior.
            CollectionAssert.AreEqual(new[] { 2, 1, 0 }, Order(Game(1, 0, 1, 0), Game(2, 0, 3, 0)));
        }

        [Test]
        public void Tabela_EmpateNoSaldo_GolsPro()
        {
            // 1 vence 0 por 3 a 2; 2 vence 0 por 1 a 0: mesmo saldo (+1), 1 fez mais gols.
            CollectionAssert.AreEqual(new[] { 1, 2, 0 }, Order(Game(1, 0, 3, 2), Game(2, 0, 1, 0)));
        }

        [Test]
        public void Tabela_EmpateEmTudo_ConfrontoDireto()
        {
            // 0 venceu 1 por 2 a 1. 0: 3 pontos, 2 pró, 2 contra. 1: 3 pontos, 2 pró, 2 contra. 2 tem saldo +1. 3 não pontua.
            var order = Order(Game(0, 1, 2, 1), Game(0, 2, 0, 1), Game(1, 3, 1, 0), Game(2, 3, 0, 0, false));
            CollectionAssert.AreEqual(new[] { 2, 0, 1, 3 }, order);
        }

        [Test]
        public void Tabela_JogoNaoJogadoNaoConta_MasTimeAparece()
        {
            var order = Order(Game(0, 1, 9, 0, false), Game(2, 3, 1, 0));
            Assert.AreEqual(4, order.Count);
            Assert.AreEqual(2, order[0]);
            Assert.AreEqual(3, order[3]);
        }

        [Test]
        public void Tabela_SorteioDoDesempateNaoMudaEntreOlhadas()
        {
            var matches = new[] { Game(0, 1, 0, 0), Game(2, 3, 0, 0), Game(0, 2, 0, 0, false) };
            CollectionAssert.AreEqual(Order(matches), Order(matches));
        }

        [Test]
        public void Premio_PorColocacao()
        {
            var config = ScriptableObject.CreateInstance<TournamentConfig>();
            try
            {
                config.prizes = new[] { 50, 25, 10 };
                Assert.AreEqual(50, config.PrizeFor(1));
                Assert.AreEqual(25, config.PrizeFor(2));
                Assert.AreEqual(10, config.PrizeFor(3));
                Assert.AreEqual(0, config.PrizeFor(4));
                Assert.AreEqual(0, config.PrizeFor(0));
                Assert.AreEqual(50, config.PrizeFor(1, 2), "mata-mata não multiplica pelos turnos");

                config.format = TournamentFormat.Liga;
                config.twoLegsPrizeMultiplier = 2f;
                Assert.AreEqual(50, config.PrizeFor(1, 1));
                Assert.AreEqual(100, config.PrizeFor(1, 2), "liga em 2 turnos paga em dobro");
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void Torneios_DosAssets_Coerentes()
        {
            var all = TournamentConfig.All();
            Assert.IsNotEmpty(all, "nenhum torneio em Resources/Torneios");
            int catalogTeams = TeamCatalog.Load().TeamCount;
            var ids = new HashSet<string>();
            foreach (var config in all)
            {
                Assert.IsTrue(ids.Add(config.id), $"id repetido: {config.id}");
                Assert.LessOrEqual(config.TeamCount - 1, catalogTeams, $"{config.id}: mais times do que o catálogo");
                Assert.IsNotNull(config.prizes, config.id);
                for (int i = 0; i < config.prizes.Length; i++)
                {
                    Assert.GreaterOrEqual(config.prizes[i], 0, $"{config.id}: prêmio negativo");
                    if (i > 0) Assert.LessOrEqual(config.prizes[i], config.prizes[i - 1], $"{config.id}: prêmio maior para pior colocação");
                }
                if (config.IsLeague)
                {
                    Assert.AreEqual(0, config.Rounds, $"{config.id}: liga não tem mata-mata");
                    continue;
                }
                int knockout = config.KnockoutTeams;
                Assert.IsTrue(Mathf.IsPowerOfTwo(knockout), $"{config.id}: mata-mata com {knockout} times");
                Assert.AreEqual(knockout, 1 << config.Rounds, $"{config.id}: fases não fecham com os times");
                if (config.HasGroups)
                    Assert.AreEqual(knockout, config.GroupCount * config.Qualifiers, $"{config.id}: classificados não fecham a chave");
            }
        }
    }
}
