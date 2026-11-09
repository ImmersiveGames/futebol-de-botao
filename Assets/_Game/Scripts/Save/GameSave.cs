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

        /// <summary>Junta o estado atual (MatchSession, opções, música) e grava.</summary>
        public static void Save(string reason)
        {
            Capture();
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
            MatchSession.AiLevel = (AiLevel)Mathf.Clamp(Data.settings.aiLevel, 0, 2);
            for (int i = 0; i < 2; i++)
            {
                var side = (TeamSide)i;
                var choices = Data.players[i];
                // O Jogador 1 pode estar com o Meu time (-1).
                MatchSession.SetTeamIndex(side, Mathf.Max(side == TeamSide.Bottom ? MatchSession.ProfileTeam : 0, choices.team));
                MatchSession.SetSecondaryIndex(side, Mathf.Max(0, choices.secondary));
                // Save antigo (sem id): converte pela linha da folha de onde o padrão veio.
                MatchSession.SetDiscPattern(side, !string.IsNullOrEmpty(choices.discPatternId)
                    ? choices.discPatternId
                    : ShirtPattern.FromLegacyRow(ShirtPattern.Kind.Botao, Mathf.Max(0, choices.discPattern)));
                MatchSession.SetKeeperPattern(side, !string.IsNullOrEmpty(choices.keeperPatternId)
                    ? choices.keeperPatternId
                    : ShirtPattern.FromLegacyRow(ShirtPattern.Kind.Goleiro, Mathf.Max(0, choices.keeperPattern)));
                MatchSession.SetSchemeIndex(side, 3, Mathf.Max(0, choices.scheme3));
                MatchSession.SetSchemeIndex(side, 5, Mathf.Max(0, choices.scheme5));
            }
        }

        private static void Capture()
        {
            Data.settings.music = MusicSetting.Enabled;
            Data.settings.aiLevel = (int)MatchSession.AiLevel;

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
                choices.secondary = MatchSession.SecondaryIndex(side);
                choices.discPatternId = MatchSession.DiscPatternId(side);
                choices.keeperPatternId = MatchSession.KeeperPatternId(side);
                choices.scheme3 = MatchSession.SchemeIndex(side, 3);
                choices.scheme5 = MatchSession.SchemeIndex(side, 5);
            }
        }

        private static void Write(string reason)
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

        /// <summary>Converte saves de versões antigas e completa o que faltar (campos novos ficam no padrão).</summary>
        private static SaveData Upgrade(SaveData data)
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
            if (stats.levels == null || stats.levels.Length != 3)
            {
                var levels = new SaveData.LevelStats[3];
                for (int i = 0; stats.levels != null && i < stats.levels.Length && i < 3; i++) levels[i] = stats.levels[i];
                stats.levels = levels;
            }
            for (int i = 0; i < 3; i++) stats.levels[i] ??= new SaveData.LevelStats();
            stats.total.numbers ??= new SaveData.MatchNumbers();
            foreach (var level in stats.levels) level.numbers ??= new SaveData.MatchNumbers();
            stats.last ??= new SaveData.LastMatchData();
            stats.last.numbers ??= new SaveData.MatchNumbers();
            // Versão 1 não tinha a marca de partida em andamento (o campo vinha 0 = "Fácil em andamento").
            if (data.version < 2) stats.inProgressLevel = -1;
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
            // Versão 4 passou a contar moedas ganhas e gastas: gastos pelo preço do que já foi comprado, ganhos = saldo + gastos.
            if (data.version < 4)
            {
                int spent = 0, bought = 0;
                foreach (var key in data.unlocked)
                {
                    int price = Shop.PriceOf(key);
                    if (price < 0) continue;
                    spent += price;
                    bought++;
                }
                stats.coinsSpent = spent;
                stats.itemsBought = bought;
                stats.coinsEarned = data.profile.coins + spent;
            }
            data.version = SaveData.CurrentVersion;
            return data;
        }
    }
}
