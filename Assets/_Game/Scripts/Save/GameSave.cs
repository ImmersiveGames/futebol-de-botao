using System;
using System.Text;
using Immersive.Framework.ProgressionSave;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Save do jogo: um slot do backend JSON do Immersive Framework (gravação segura, não corrompe se o aparelho
    /// desligar no meio). No framework 1.1.0 o jogo ainda não alcança o Progression Save da aplicação, então o
    /// backend é usado direto pela interface Stable <see cref="IProgressionSaveStore"/>; quando o framework liberar o
    /// acesso, só a criação da loja muda. Carrega antes da primeira cena e aplica no <see cref="MatchSession"/>.
    /// </summary>
    public static class GameSave
    {
        private const string ProductFolder = "ProGol";
        private const string MediaType = "application/json";
        private static readonly ProgressionSaveSlotId Slot = ProgressionSaveSlotId.From("progol.principal");
        private static readonly ProgressionSaveRecordId Record = ProgressionSaveRecordId.From("progol.save");

        private static IProgressionSaveStore store;
        private static long createdTicks;

        /// <summary>Dados carregados (ou o padrão, se não havia save ou ele não pôde ser lido).</summary>
        public static SaveData Data { get; private set; } = new();

        /// <summary>Havia um save no aparelho quando o jogo abriu.</summary>
        public static bool Existed { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Load()
        {
            Data = new SaveData();
            Existed = false;
            dirty = false;
            pendingReason = null;
            createdTicks = 0;
            store = null;
            try
            {
                store = JsonProgressionSaveStore.CreateDefault(ProductFolder);
                var result = store.ReadSlot(Slot);
                if (result.Status == ProgressionSaveReadStatus.Found && result.HasRecord)
                {
                    var bytes = new byte[result.Record.Payload.ByteCount];
                    for (int i = 0; i < bytes.Length; i++) bytes[i] = result.Record.Payload.Bytes[i];
                    var loaded = JsonUtility.FromJson<SaveData>(Encoding.UTF8.GetString(bytes));
                    if (loaded != null)
                    {
                        Data = Upgrade(loaded);
                        Existed = true;
                        createdTicks = result.Record.CreatedUtcTicks;
                    }
                }
                else if (result.Status != ProgressionSaveReadStatus.Missing)
                    Debug.LogWarning($"[Save] Não consegui ler o save ({result.Status}): {result.Message}. Começando do padrão.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Save] Save indisponível, começando do padrão: {exception.Message}");
            }
            ApplyToSession();
            // Partida contra a IA que ficou no meio quando o jogo fechou: conta como abandonada.
            if (PlayerStats.CountLeftover()) Save("partida abandonada");
        }

        /// <summary>
        /// Junta o estado atual (MatchSession, opções) no save e marca para gravar. A gravação no disco acontece uma vez só,
        /// no fim do quadro (<see cref="Flush"/>), mesmo que vários sistemas peçam no mesmo quadro.
        /// </summary>
        public static void Save(string reason)
        {
            Capture();
            MarkDirty(reason);
        }

        /// <summary>
        /// Marca para gravar o save como está, sem juntar as escolhas da sessão (o tutorial usa: a partida dele troca os
        /// times da sessão e isso não pode virar a última escolha do jogador).
        /// </summary>
        public static void WriteOnly(string reason) => MarkDirty(reason);

        // ---- Gravar uma vez por quadro ----

        private static bool dirty;
        private static string pendingReason;
        private static SaveFlusher flusher;

        /// <summary>Tem mudança marcada que ainda não foi para o disco.</summary>
        public static bool IsDirty => dirty;

        private static void MarkDirty(string reason)
        {
            if (!Application.isPlaying)
            {
                // Fora do Play (menus do editor): grava na hora, não há quadro para esperar.
                Write(reason);
                return;
            }
            pendingReason = dirty ? pendingReason + ", " + reason : reason;
            dirty = true;
            if (flusher == null) flusher = SaveFlusher.Create();
        }

        /// <summary>Grava agora o que estiver marcado. Chamado no fim do quadro, ao pausar o app e ao fechar o jogo.</summary>
        public static void Flush()
        {
            if (!dirty) return;
            dirty = false;
            string reason = pendingReason;
            pendingReason = null;
            Write(reason);
        }

        /// <summary>Opções salvas por cima da cópia da sessão (chamado quando o MatchSession cria a cópia).</summary>
        public static void ApplyOptions(MatchOptions options)
        {
            var saved = Data.options;
            if (options == null || !saved.saved) return;
            options.durationMinutes = saved.durationMinutes;
            options.touchesPerTurn = Mathf.Clamp(saved.touchesPerTurn, 1, 3);
            options.goalAfterWallIsValid = saved.goalAfterWallIsValid;
            options.fouls = saved.fouls;
            options.discsPerTeam = saved.discsPerTeam == 5 ? 5 : 3;
            options.hasGoalkeeper = saved.hasGoalkeeper;
            options.aimAssist = (AimAssistMode)Mathf.Clamp(saved.aimAssist, 0, 2);
            options.extraTimePercent = saved.extraTimePercent >= 100 ? 100 : saved.extraTimePercent >= 50 ? 50 : 0;
            options.penalties = saved.penalties;
        }

        private static void ApplyToSession()
        {
            MatchSession.AiLevel = (AiLevel)Mathf.Clamp(Data.settings.aiLevel, 0, AiDifficulty.LevelCount - 1);
            MatchSession.TournamentId = Data.settings.tournamentId;
            MatchSession.LeagueId = Data.settings.leagueId;
            MatchSession.LeagueLegs = Mathf.Clamp(Data.settings.leagueLegs, 0, 2);
            for (int i = 0; i < 2; i++)
            {
                var side = (TeamSide)i;
                var choices = Data.players[i];
                // O Jogador 1 pode estar com o Meu time (-1).
                MatchSession.SetTeamIndex(side, Mathf.Max(side == TeamSide.Bottom ? MatchSession.ProfileTeam : 0, choices.team));
                // As camisas vêm do uniforme do time (versão 8); o save guarda só qual uniforme.
                MatchSession.SetUniform(side, choices.uniform);
                MatchSession.SetSchemeIndex(side, 3, Mathf.Max(0, choices.scheme3));
                MatchSession.SetSchemeIndex(side, 5, Mathf.Max(0, choices.scheme5));
            }
        }

        private static void Capture()
        {
            Data.settings.aiLevel = (int)MatchSession.AiLevel;
            Data.settings.tournamentId = MatchSession.TournamentId;
            Data.settings.leagueId = MatchSession.LeagueId;
            Data.settings.leagueLegs = MatchSession.LeagueLegs;

            var options = MatchSession.Current;
            if (options != null)
            {
                var saved = Data.options;
                saved.saved = true;
                saved.durationMinutes = options.durationMinutes;
                saved.touchesPerTurn = options.touchesPerTurn;
                saved.goalAfterWallIsValid = options.goalAfterWallIsValid;
                saved.fouls = options.fouls;
                saved.discsPerTeam = options.discsPerTeam;
                saved.hasGoalkeeper = options.hasGoalkeeper;
                saved.aimAssist = (int)options.aimAssist;
                saved.extraTimePercent = options.extraTimePercent;
                saved.penalties = options.penalties;
            }

            // Contra a IA o time de cima é dela (sorteado): guarda só o do Jogador 1 e mantém o que o Jogador 2 usou.
            int sides = MatchSession.VsAi == true ? 1 : 2;
            for (int i = 0; i < sides; i++)
            {
                var side = (TeamSide)i;
                var choices = Data.players[i];
                choices.team = MatchSession.TeamIndex(side);
                choices.uniform = MatchSession.UniformChoice(side);
                choices.scheme3 = MatchSession.SchemeIndex(side, 3);
                choices.scheme5 = MatchSession.SchemeIndex(side, 5);
            }
        }

        private static void Write(string reason)
        {
            WriteRaw(reason);
            PlayGames.SaveChanged();
        }

        /// <summary>Grava sem contar como mudança para a nuvem (usado pela própria sincronização).</summary>
        internal static void WriteRaw(string reason)
        {
            if (store == null) return;
            try
            {
                Data.version = SaveData.CurrentVersion;
                var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(Data));
                long now = DateTime.UtcNow.Ticks;
                if (createdTicks == 0) createdTicks = now;
                var payload = new ProgressionSavePayload(ProgressionSavePayloadFormat.Structured, bytes, MediaType);
                var record = new ProgressionSaveSlotRecord(Slot, Record, payload, createdTicks, now, "Pro Gol!", "futebol.save", reason);
                var result = store.WriteSlot(record);
                if (!result.Written) Debug.LogWarning($"[Save] Não consegui gravar ({reason}): {result.Status} {result.Message}");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Save] Não consegui gravar ({reason}): {exception.Message}");
            }
        }

        // ---- Save na nuvem (Play Games) ----

        /// <summary>
        /// Cópia do save para a nuvem, sem o que é só deste aparelho (volumes, idioma, opções da partida, escolhas da
        /// Seleção, idade e anúncios). <paramref name="uploadId"/> e a data vão junto para o outro aparelho reconhecer.
        /// </summary>
        internal static string CloudJson(string uploadId, long savedAtTicks)
        {
            Data.version = SaveData.CurrentVersion;
            var copy = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(Data));
            copy.settings = new SaveData.SettingsData();
            copy.options = new SaveData.OptionsData();
            copy.players = new[] { new SaveData.PlayerChoices { team = 0 }, new SaveData.PlayerChoices { team = 1 } };
            copy.ads = new SaveData.AdsData();
            copy.cloud = new SaveData.CloudData { uploadId = uploadId, savedAtTicks = savedAtTicks };
            return JsonUtility.ToJson(copy);
        }

        /// <summary>Resumo do que vai para a nuvem (sem identificador e data), para saber se mudou desde o último envio.</summary>
        internal static string CloudHash() => Hash128.Compute(CloudJson(string.Empty, 0)).ToString();

        /// <summary>Lê um save que veio da nuvem (convertido para a versão atual); nulo se não deu para ler.</summary>
        internal static SaveData ParseCloud(string json)
        {
            try
            {
                var loaded = JsonUtility.FromJson<SaveData>(json);
                return loaded != null ? Upgrade(loaded) : null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Save] Save da nuvem ilegível: {exception.Message}");
                return null;
            }
        }

        /// <summary>Troca o progresso deste aparelho pelo da nuvem, mantendo o que é só do aparelho.</summary>
        internal static void ApplyCloud(SaveData cloud)
        {
            cloud.settings = Data.settings;
            cloud.options = Data.options;
            cloud.players = Data.players;
            // Ano de nascimento e anúncios ficam do aparelho; a compra "remover anúncios" a loja devolve sozinha.
            cloud.ads = Data.ads;
            var meta = cloud.cloud ?? new SaveData.CloudData();
            cloud.cloud = new SaveData.CloudData { uploadId = meta.uploadId, savedAtTicks = meta.savedAtTicks };
            Data = cloud;
            Data.cloud.syncedHash = CloudHash();
            WriteRaw("save da nuvem");
            ApplyToSession();
        }

        /// <summary>O save já tem algum progresso do jogador (partidas, tutorial, moedas, compras, Meu time).</summary>
        internal static bool HasProgress(SaveData data)
        {
            if (data == null) return false;
            var stats = data.stats;
            if (stats != null && stats.total != null && (stats.total.Played > 0 || stats.total.tournaments > 0)) return true;
            if (data.tutorial != null && data.tutorial.done != null && data.tutorial.done.Count > 0) return true;
            if (data.unlocked != null && data.unlocked.Count > 0) return true;
            if (data.tournament != null && data.tournament.active) return true;
            if (data.league != null && data.league.active) return true;
            var profile = data.profile;
            return profile != null && (profile.coins > 0 || profile.teamName != PlayerProfile.DefaultName);
        }

        /// <summary>
        /// Converte um save de versão antiga para a de hoje: primeiro completa o que faltar (campos novos ficam no padrão),
        /// depois roda, em ordem, cada passo de versão mais nova que a do save (<see cref="Migrations"/>).
        /// </summary>
        private static SaveData Upgrade(SaveData data)
        {
            FillMissing(data);
            foreach (var (version, step) in Migrations)
                if (data.version < version) step(data);
            // Uniforme 2 zerado (save sem perfil): as cores do 1 invertidas.
            if (data.profile.primary2.a <= 0f) data.profile.primary2 = data.profile.secondary;
            if (data.profile.secondary2.a <= 0f) data.profile.secondary2 = data.profile.primary;
            data.version = SaveData.CurrentVersion;
            return data;
        }

        /// <summary>
        /// Passos de conversão: cada um leva o save para a versão do número (só roda se o save for mais velho que ela).
        /// Versão nova com conversão = um passo novo no fim da lista; versões sem passo só ganharam campos com padrão.
        /// </summary>
        private static readonly (int version, Action<SaveData> step)[] Migrations =
        {
            (2, ToVersion2),
            (4, ToVersion4),
            (8, ToVersion8),
            (9, ToVersion9),
            (10, ToVersion10),
            (13, ToVersion13),
        };

        /// <summary>
        /// Versão 13: as cores da mesa passaram a vir da Color Palette (a do time), com os nomes no masculino; o nome
        /// salvo da cor das traves acompanha.
        /// </summary>
        private static void ToVersion13(SaveData data)
        {
            data.profile.goalColorName = data.profile.goalColorName switch
            {
                "Branca" => "Branco",
                "Vermelha" => "Vermelho",
                "Amarela" => "Amarelo",
                var name => name,
            };
        }

        /// <summary>Versão 1 não tinha a marca de partida em andamento (o campo vinha 0 = "Fácil em andamento").</summary>
        private static void ToVersion2(SaveData data) => data.stats.inProgressLevel = -1;

        /// <summary>Versão 4 passou a contar moedas ganhas e gastas: gastos pelo preço do que já foi comprado, ganhos = saldo + gastos.</summary>
        private static void ToVersion4(SaveData data)
        {
            int spent = 0, bought = 0;
            foreach (var key in data.unlocked)
            {
                int price = Shop.PriceOf(key);
                if (price < 0) continue;
                spent += price;
                bought++;
            }
            data.stats.coinsSpent = spent;
            data.stats.itemsBought = bought;
            data.stats.coinsEarned = data.profile.coins + spent;
        }

        /// <summary>Versão 8 trocou a cor reserva pelo Uniforme 2: a reserva vira o Uniforme 2; sem reserva, as cores invertidas.</summary>
        private static void ToVersion8(SaveData data)
        {
            var profile = data.profile;
            profile.discPatternId2 = profile.discPatternId;
            profile.keeperPatternId2 = profile.keeperPatternId;
            if (profile.hasReserve && profile.reserve.a > 0f)
            {
                profile.primary2 = profile.reserveReplacesSecondary ? profile.primary : profile.reserve;
                profile.secondary2 = profile.reserveReplacesSecondary ? profile.reserve : profile.secondary;
            }
            else
            {
                profile.primary2 = profile.secondary;
                profile.secondary2 = profile.primary;
            }
            profile.hasReserve = false;
        }

        /// <summary>
        /// Versão 9 criou o Financeiro: os totais vêm das estatísticas; do ganho, o tutorial é exato (lições e bônus) e
        /// o resto vai para Partidas (antes não se separava partida de torneio). O extrato começa vazio.
        /// </summary>
        private static void ToVersion9(SaveData data)
        {
            var finance = data.finance;
            finance.earned = data.stats.coinsEarned;
            finance.spent = data.stats.coinsSpent;
            int tutorial = data.tutorial.done.Count * Tutorial.LessonReward + (data.tutorial.bonusPaid ? Tutorial.CompleteBonus : 0);
            finance.fromTutorial = Math.Min(tutorial, Math.Max(0, finance.earned));
            finance.fromMatches = Math.Max(0, finance.earned - finance.fromTutorial);
        }

        /// <summary>Versão 10 trocou a Música Ligado/Desligado pelos volumes: música desligada = volume da música no 0.</summary>
        private static void ToVersion10(SaveData data)
        {
            if (!data.settings.music) data.settings.musicVolume = 0;
        }

        /// <summary>Completa o que o save não tem (listas nulas, níveis faltando, perfil sem nome ou cor).</summary>
        private static void FillMissing(SaveData data)
        {
            data.settings ??= new SaveData.SettingsData();
            data.options ??= new SaveData.OptionsData();
            if (data.players == null || data.players.Length < 2)
            {
                var players = new[] { new SaveData.PlayerChoices { team = 0 }, new SaveData.PlayerChoices { team = 1 } };
                for (int i = 0; data.players != null && i < data.players.Length; i++) players[i] = data.players[i] ?? players[i];
                data.players = players;
            }
            for (int i = 0; i < data.players.Length; i++) data.players[i] ??= new SaveData.PlayerChoices { team = i };
            data.stats ??= new SaveData.StatsData();
            var stats = data.stats;
            stats.total ??= new SaveData.LevelStats();
            // Versão 6 ganhou o nível Pro: saves antigos têm 3 níveis, o Pro entra zerado.
            int levelCount = AiDifficulty.LevelCount;
            if (stats.levels == null || stats.levels.Length != levelCount)
            {
                var levels = new SaveData.LevelStats[levelCount];
                for (int i = 0; stats.levels != null && i < stats.levels.Length && i < levelCount; i++) levels[i] = stats.levels[i];
                stats.levels = levels;
            }
            for (int i = 0; i < levelCount; i++) stats.levels[i] ??= new SaveData.LevelStats();
            stats.total.numbers ??= new SaveData.MatchNumbers();
            foreach (var level in stats.levels) level.numbers ??= new SaveData.MatchNumbers();
            stats.last ??= new SaveData.LastMatchData();
            stats.last.numbers ??= new SaveData.MatchNumbers();
            data.profile ??= new SaveData.ProfileData();
            if (string.IsNullOrWhiteSpace(data.profile.teamName)) data.profile.teamName = PlayerProfile.DefaultName;
            if (string.IsNullOrWhiteSpace(data.profile.abbreviation)) data.profile.abbreviation = PlayerProfile.DefaultAbbreviation;
            // Save sem perfil (JsonUtility deixa a cor zerada): cores padrão.
            if (data.profile.primary.a <= 0f) data.profile.primary = PlayerProfile.DefaultPrimary;
            if (data.profile.secondary.a <= 0f) data.profile.secondary = PlayerProfile.DefaultSecondary;
            data.unlocked ??= new System.Collections.Generic.List<string>();
            data.tournament ??= new SaveData.TournamentRun();
            data.tournament.slots ??= new System.Collections.Generic.List<int>();
            data.tournament.matches ??= new System.Collections.Generic.List<SaveData.TournamentMatch>();
            data.league ??= new SaveData.TournamentRun();
            data.league.slots ??= new System.Collections.Generic.List<int>();
            data.league.matches ??= new System.Collections.Generic.List<SaveData.TournamentMatch>();
            data.tutorial ??= new SaveData.TutorialData();
            data.tutorial.done ??= new System.Collections.Generic.List<string>();
            data.finance ??= new SaveData.FinanceData();
            data.finance.entries ??= new System.Collections.Generic.List<SaveData.FinanceEntry>();
            data.ads ??= new SaveData.AdsData();
            data.cloud ??= new SaveData.CloudData();
            data.achievements ??= new System.Collections.Generic.List<string>();
            data.weekly ??= new SaveData.WeeklyData();
            data.weekly.points ??= new System.Collections.Generic.List<int>();
        }
    }
}
