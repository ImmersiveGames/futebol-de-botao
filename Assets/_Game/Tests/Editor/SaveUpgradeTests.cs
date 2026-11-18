using NUnit.Framework;
using UnityEngine;

namespace FutebolDeBotao.Tests
{
    /// <summary>
    /// Saves de versões antigas abrem certo na versão de hoje (GameSave.Upgrade). Os saves são textos JSON escritos aqui;
    /// o save de verdade do jogo não é lido nem gravado.
    /// </summary>
    public class SaveUpgradeTests
    {
        private static SaveData Open(string json) =>
            SaveSandbox.Call<SaveData>(typeof(GameSave), "Upgrade", JsonUtility.FromJson<SaveData>(json));

        private static string ColorJson(float r, float g, float b) => $"{{\"r\":{r},\"g\":{g},\"b\":{b},\"a\":1}}";

        [Test]
        public void Versao1_ZeraPartidaEmAndamento_E_CompletaNiveis()
        {
            var data = Open("{\"version\":1,\"stats\":{\"inProgressLevel\":0,\"levels\":[{\"wins\":2},{\"wins\":1},{}]}," +
                            "\"profile\":{\"teamName\":\"\",\"coins\":30}}");

            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(-1, data.stats.inProgressLevel, "versão 1 não tinha a marca: 0 viraria 'Fácil abandonada'");
            Assert.AreEqual(AiDifficulty.LevelCount, data.stats.levels.Length, "o nível Pro entra zerado");
            Assert.AreEqual(2, data.stats.levels[0].wins);
            Assert.AreEqual(1, data.stats.levels[1].wins);
            Assert.AreEqual(0, data.stats.levels[3].wins);
            foreach (var level in data.stats.levels) Assert.IsNotNull(level.numbers);
            Assert.AreEqual(PlayerProfile.DefaultName, data.profile.teamName);
            Assert.Greater(data.profile.primary.a, 0f);
            Assert.Greater(data.profile.secondary.a, 0f);
        }

        [Test]
        public void Versao2_MantemPartidaEmAndamento()
        {
            var data = Open("{\"version\":2,\"stats\":{\"inProgressLevel\":0}}");
            Assert.AreEqual(0, data.stats.inProgressLevel);
        }

        [Test]
        public void Versao3_ContaMoedasGanhasPeloSaldoMaisCompras()
        {
            // Um item que existe na loja hoje (se houver) e um que não existe mais (não conta).
            string key = null;
            int price = 0;
            foreach (var item in ShirtPattern.All())
            {
                if (item.price <= 0) continue;
                key = item.ShopKey;
                price = item.price;
                break;
            }
            string unlocked = key != null ? $"[\"{key}\",\"camisa:nao-existe\"]" : "[\"camisa:nao-existe\"]";
            var data = Open($"{{\"version\":3,\"profile\":{{\"coins\":40}},\"unlocked\":{unlocked}}}");

            Assert.AreEqual(price, data.stats.coinsSpent);
            Assert.AreEqual(key != null ? 1 : 0, data.stats.itemsBought);
            Assert.AreEqual(40 + price, data.stats.coinsEarned);
            // E o Financeiro (versão 9) sai dessas contas.
            Assert.AreEqual(40 + price, data.finance.earned);
            Assert.AreEqual(price, data.finance.spent);
            Assert.AreEqual(40 + price, data.finance.fromMatches);
        }

        [Test]
        public void Versao5_CorReservaViraUniforme2()
        {
            var data = Open("{\"version\":5,\"profile\":{\"primary\":" + ColorJson(1, 0, 0) + ",\"secondary\":" + ColorJson(1, 1, 1) +
                            ",\"hasReserve\":true,\"reserve\":" + ColorJson(0, 0, 1) +
                            ",\"reserveReplacesSecondary\":false,\"discPatternId\":\"listrada\"}}");

            Assert.AreEqual(new Color(0, 0, 1), data.profile.primary2, "a reserva troca a cor principal");
            Assert.AreEqual(new Color(1, 1, 1), data.profile.secondary2);
            Assert.AreEqual("listrada", data.profile.discPatternId2, "o Uniforme 2 começa com as camisas do 1");
            Assert.IsFalse(data.profile.hasReserve);
        }

        [Test]
        public void Versao5_ReservaNaSegundaCor()
        {
            var data = Open("{\"version\":5,\"profile\":{\"primary\":" + ColorJson(1, 0, 0) + ",\"secondary\":" + ColorJson(1, 1, 1) +
                            ",\"hasReserve\":true,\"reserve\":" + ColorJson(0, 0, 1) + ",\"reserveReplacesSecondary\":true}}");

            Assert.AreEqual(new Color(1, 0, 0), data.profile.primary2);
            Assert.AreEqual(new Color(0, 0, 1), data.profile.secondary2);
        }

        [Test]
        public void Versao5_SemReserva_Uniforme2ComCoresInvertidas()
        {
            var data = Open("{\"version\":5,\"profile\":{\"primary\":" + ColorJson(1, 0, 0) + ",\"secondary\":" + ColorJson(1, 1, 1) + "}}");

            Assert.AreEqual(new Color(1, 1, 1), data.profile.primary2);
            Assert.AreEqual(new Color(1, 0, 0), data.profile.secondary2);
        }

        [Test]
        public void Versao8_FinanceiroSeparaTutorialDePartidas()
        {
            var data = Open("{\"version\":8,\"stats\":{\"coinsEarned\":100,\"coinsSpent\":20}," +
                            "\"tutorial\":{\"done\":[\"a\",\"b\"],\"bonusPaid\":false}}");

            int tutorial = 2 * Tutorial.LessonReward;
            Assert.AreEqual(100, data.finance.earned);
            Assert.AreEqual(20, data.finance.spent);
            Assert.AreEqual(tutorial, data.finance.fromTutorial);
            Assert.AreEqual(100 - tutorial, data.finance.fromMatches);
            Assert.IsNotNull(data.finance.entries);
        }

        [Test]
        public void Versao8_TutorialNuncaPassaDoQueFoiGanho()
        {
            var data = Open("{\"version\":8,\"stats\":{\"coinsEarned\":10},\"tutorial\":{\"done\":[\"a\",\"b\",\"c\"],\"bonusPaid\":true}}");

            Assert.AreEqual(10, data.finance.fromTutorial);
            Assert.AreEqual(0, data.finance.fromMatches);
        }

        [Test]
        public void Versao9_MusicaDesligadaViraVolumeZero()
        {
            var data = Open("{\"version\":9,\"settings\":{\"music\":false,\"musicVolume\":7}}");
            Assert.AreEqual(0, data.settings.musicVolume);
        }

        [Test]
        public void VersaoAtual_NaoMexeNoVolume()
        {
            var data = Open($"{{\"version\":{SaveData.CurrentVersion},\"settings\":{{\"music\":false,\"musicVolume\":7}}}}");
            Assert.AreEqual(7, data.settings.musicVolume);
        }

        [Test]
        public void SaveVazio_TudoPreenchido()
        {
            var data = Open("{}");

            Assert.IsNotNull(data.settings);
            Assert.IsNotNull(data.options);
            Assert.AreEqual(2, data.players.Length);
            Assert.IsNotNull(data.players[0]);
            Assert.IsNotNull(data.players[1]);
            Assert.IsNotNull(data.stats.total.numbers);
            Assert.IsNotNull(data.stats.last.numbers);
            Assert.IsNotNull(data.unlocked);
            Assert.IsNotNull(data.tournament.matches);
            Assert.IsNotNull(data.league.slots);
            Assert.IsNotNull(data.tutorial.done);
            Assert.IsNotNull(data.ads);
            Assert.IsNotNull(data.cloud);
            Assert.IsNotNull(data.achievements);
            Assert.IsNotNull(data.weekly.points);
        }

        [Test]
        public void SaveAtual_IdaEVolta_NaoPerdeNada()
        {
            var original = new SaveData();
            original.profile.teamName = "Teste FC";
            original.profile.coins = 123;
            original.unlocked.Add("bola:teste");
            original.stats.levels[3].wins = 4;
            original.stats.total.numbers.goals = 9;
            original.tutorial.done.Add("peteleco");
            original.tournament.exists = true;
            original.tournament.matches.Add(new SaveData.TournamentMatch { home = -1, away = 3, played = true, homeGoals = 2 });
            original.finance.entries.Add(new SaveData.FinanceEntry { text = "x", amount = 5 });

            var copy = SaveSandbox.Call<SaveData>(typeof(GameSave), "ParseCloud", JsonUtility.ToJson(original));

            Assert.AreEqual(SaveData.CurrentVersion, copy.version);
            Assert.AreEqual("Teste FC", copy.profile.teamName);
            Assert.AreEqual(123, copy.profile.coins);
            CollectionAssert.AreEqual(original.unlocked, copy.unlocked);
            Assert.AreEqual(4, copy.stats.levels[3].wins);
            Assert.AreEqual(9, copy.stats.total.numbers.goals);
            CollectionAssert.AreEqual(original.tutorial.done, copy.tutorial.done);
            Assert.IsTrue(copy.tournament.exists);
            Assert.AreEqual(1, copy.tournament.matches.Count);
            Assert.AreEqual(-1, copy.tournament.matches[0].home);
            Assert.AreEqual(2, copy.tournament.matches[0].homeGoals);
            Assert.AreEqual(1, copy.finance.entries.Count);
        }

        [Test]
        public void SaveIlegivel_DevolveNulo()
        {
            Assert.IsNull(SaveSandbox.Call<SaveData>(typeof(GameSave), "ParseCloud", "{isso não é um save"));
        }

        [Test]
        public void TemProgresso()
        {
            var fresh = new SaveData();
            Assert.IsFalse(SaveSandbox.Call<bool>(typeof(GameSave), "HasProgress", fresh));
            fresh.profile.coins = 1;
            Assert.IsTrue(SaveSandbox.Call<bool>(typeof(GameSave), "HasProgress", fresh));
        }

        [Test]
        public void PassosDeVersao_EmOrdem_E_NenhumDepoisDaAtual()
        {
            var field = typeof(GameSave).GetField("Migrations", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, "lista de passos de versão não encontrada");
            var steps = ((int version, System.Action<SaveData> step)[])field.GetValue(null);
            int previous = 1;
            foreach (var (version, step) in steps)
            {
                Assert.Greater(version, previous, "passos fora de ordem ou repetidos");
                Assert.LessOrEqual(version, SaveData.CurrentVersion, "passo para uma versão que ainda não existe");
                Assert.IsNotNull(step);
                previous = version;
            }
        }
    }
}
