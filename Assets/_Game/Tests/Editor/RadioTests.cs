using Immersive.Audio.Authoring;
using NUnit.Framework;
using UnityEngine;

namespace FutebolDeBotao.Tests
{
    /// <summary>Rádio da partida: escolha, sorteio, Próxima, músicas trancadas e liberação pelos títulos. Save e lista de mentira.</summary>
    public class RadioTests
    {
        private RadioPlaylist playlist;

        [SetUp]
        public void SetUp()
        {
            playlist = ScriptableObject.CreateInstance<RadioPlaylist>();
            playlist.tracks.Add(Track("batucada", RadioUnlock.Free));
            playlist.tracks.Add(Track("rock", RadioUnlock.LeagueTitle));
            playlist.tracks.Add(Track("funk", RadioUnlock.Free));
            playlist.tracks.Add(Track("frevo", RadioUnlock.TournamentTitle));
            Radio.SetPlaylistForTests(playlist);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var track in playlist.tracks) Object.DestroyImmediate(track.cue);
            Object.DestroyImmediate(playlist);
            Radio.SetPlaylistForTests(Resources.Load<RadioPlaylist>(RadioPlaylist.ResourcePath));
        }

        private static RadioTrack Track(string id, RadioUnlock unlock) => new()
        {
            id = id, displayName = id, unlock = unlock, cue = ScriptableObject.CreateInstance<AudioBgmCueAsset>()
        };

        [Test]
        public void SavNovo_EAleatoria_ESoSorteiaLiberadas()
        {
            using (SaveSandbox.Use(new SaveData()))
            {
                Assert.IsTrue(Radio.IsRandom);
                for (int i = 0; i < 50; i++)
                {
                    var id = Radio.Pick().id;
                    Assert.That(id, Is.EqualTo("batucada").Or.EqualTo("funk"));
                }
            }
        }

        [Test]
        public void Aleatoria_NaoRepeteAAtual()
        {
            using (SaveSandbox.Use(new SaveData()))
            {
                var batucada = Radio.Find("batucada");
                for (int i = 0; i < 20; i++) Assert.AreEqual("funk", Radio.Pick(batucada).id);
            }
        }

        [Test]
        public void Desligada_NaoToca()
        {
            using (SaveSandbox.Use(new SaveData()))
            {
                Radio.Choice = Radio.Off;
                Assert.IsNull(Radio.Pick());
                Assert.IsNull(Radio.Next(null));
            }
        }

        [Test]
        public void Titulos_LiberamFrevoERock()
        {
            using (SaveSandbox.Use(new SaveData()))
            {
                Assert.IsTrue(Radio.IsLockedOption("frevo"));
                Assert.IsTrue(Radio.IsLockedOption("rock"));
                GameSave.Data.stats.total.tournamentTitles = 1;
                Assert.IsFalse(Radio.IsLockedOption("frevo"));
                Assert.IsTrue(Radio.IsLockedOption("rock"));
                GameSave.Data.stats.total.leagueTitles = 1;
                Assert.IsFalse(Radio.IsLockedOption("rock"));
            }
        }

        [Test]
        public void Fixa_Trancada_CaiNaAleatoria()
        {
            using (SaveSandbox.Use(new SaveData()))
            {
                Radio.Choice = "frevo";
                Assert.That(Radio.Pick().id, Is.EqualTo("batucada").Or.EqualTo("funk"));
                GameSave.Data.stats.total.tournamentTitles = 1;
                Assert.AreEqual("frevo", Radio.Pick().id);
            }
        }

        [Test]
        public void Proxima_ComFixa_PulaTrancadasEGravaAEscolha()
        {
            using (SaveSandbox.Use(new SaveData()))
            {
                Radio.Choice = "batucada";
                var next = Radio.Next(Radio.Find("batucada"));
                Assert.AreEqual("funk", next.id);
                Assert.AreEqual("funk", Radio.Choice);
                Assert.AreEqual("batucada", Radio.Next(next).id);
            }
        }

        [Test]
        public void Lista_VaiNaOrdemPulandoTrancadas_SemMudarAEscolha()
        {
            using (SaveSandbox.Use(new SaveData()))
            {
                Radio.Choice = Radio.List;
                Assert.That(Radio.Pick().id, Is.EqualTo("batucada").Or.EqualTo("funk"));
                Assert.AreEqual("funk", Radio.Next(Radio.Find("batucada")).id);
                Assert.AreEqual("batucada", Radio.Next(Radio.Find("funk")).id);
                Assert.AreEqual(Radio.List, Radio.Choice);
            }
        }

        [Test]
        public void Seletor_AleatoriaMusicasDesligada()
        {
            using (SaveSandbox.Use(new SaveData()))
            {
                CollectionAssert.AreEqual(new[] { "", Radio.List, "batucada", "rock", "funk", "frevo", Radio.Off }, Radio.Options());
            }
        }
    }
}
