using NUnit.Framework;

namespace FutebolDeBotao.Tests
{
    /// <summary>Moedas por partida (GDD), Financeiro e extrato. Tudo num save de mentira: o de verdade não muda.</summary>
    public class EconomyTests
    {
        [TestCase(0, 5, 3, 1)]
        [TestCase(1, 10, 5, 2)]
        [TestCase(2, 12, 6, 3)]
        [TestCase(3, 15, 8, 3)]
        public void MoedasPorNivelEResultado(int level, int win, int draw, int loss)
        {
            Assert.AreEqual(win, Shop.Reward(level, 2, 1));
            Assert.AreEqual(draw, Shop.Reward(level, 1, 1));
            Assert.AreEqual(loss, Shop.Reward(level, 0, 3));
        }

        [Test]
        public void NivelForaDaLista_NaoPaga()
        {
            Assert.AreEqual(0, Shop.RewardFor(-1, 1));
            Assert.AreEqual(0, Shop.RewardFor(AiDifficulty.LevelCount, 1));
        }

        [Test]
        public void Ganhar_SomaNoSaldoENaOrigem()
        {
            using (SaveSandbox.Use(new SaveData()))
            {
                Finance.Earn(FinanceSource.Match, "partida", 10);
                Finance.Earn(FinanceSource.Tournament, "torneio", 50);
                Finance.Earn(FinanceSource.Tutorial, "lição", 15);
                Finance.Earn(FinanceSource.Sponsor, "patrocínio", 5);
                Finance.Earn(FinanceSource.Match, "nada", 0);

                Assert.AreEqual(80, Shop.Coins);
                Assert.AreEqual(80, Finance.Data.earned);
                Assert.AreEqual(10, Finance.Data.fromMatches);
                Assert.AreEqual(50, Finance.Data.fromTournaments);
                Assert.AreEqual(15, Finance.Data.fromTutorial);
                Assert.AreEqual(5, Finance.Data.fromSponsors);
                Assert.AreEqual(4, Finance.Data.entries.Count, "ganho de 0 não entra no extrato");
            }
        }

        [Test]
        public void Gastar_TiraDoSaldo_E_SaldoNuncaFicaNegativo()
        {
            using (SaveSandbox.Use(new SaveData()))
            {
                Finance.Earn(FinanceSource.Match, "partida", 10);
                Finance.Spend("camisa", 4);
                Assert.AreEqual(6, Shop.Coins);
                Assert.AreEqual(4, Finance.Data.spent);
                Assert.AreEqual(-4, Finance.Data.entries[^1].amount);

                Shop.AddCoins(-100);
                Assert.AreEqual(0, Shop.Coins);
            }
        }

        [Test]
        public void Extrato_GuardaSoAsUltimas()
        {
            using (SaveSandbox.Use(new SaveData()))
            {
                for (int i = 1; i <= Finance.StatementSize + 5; i++) Finance.Earn(FinanceSource.Match, "partida " + i, i);

                var entries = Finance.Data.entries;
                Assert.AreEqual(Finance.StatementSize, entries.Count);
                Assert.AreEqual(6, entries[0].amount, "as 5 mais antigas saíram");
                Assert.AreEqual(Finance.StatementSize + 5, entries[^1].amount);
            }
        }

        [Test]
        public void Comprado_SoOQueEstaNaLista()
        {
            var data = new SaveData();
            data.unlocked.Add("bola:teste");
            using (SaveSandbox.Use(data))
            {
                Assert.IsTrue(Shop.Owns("bola:teste"));
                Assert.IsFalse(Shop.Owns("bola:outra"));
            }
        }

        [Test]
        public void ItensDaLoja_PrecoNaoNegativo_E_IdsSemRepetir()
        {
            var keys = new System.Collections.Generic.HashSet<string>();
            void Check(string key, int price)
            {
                Assert.IsTrue(keys.Add(key), $"id repetido na loja: {key}");
                Assert.GreaterOrEqual(price, 0, key);
                Assert.AreEqual(price, Shop.PriceOf(key), key);
            }
            foreach (var item in ShirtPattern.All()) Check(item.ShopKey, item.price);
            foreach (var item in BallTheme.All()) Check(item.ShopKey, item.price);
            foreach (var item in TableTheme.All()) Check(item.ShopKey, item.price);
            foreach (var item in FeltTheme.All()) Check(item.ShopKey, item.price);
        }
    }
}
