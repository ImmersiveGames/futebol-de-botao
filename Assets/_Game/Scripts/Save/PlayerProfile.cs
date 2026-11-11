using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// O Meu time do jogador, guardado no save (<see cref="SaveData.ProfileData"/>): nome, sigla de 3 letras, cor principal,
    /// segunda cor, camisas do botão e do goleiro e o saldo de moedas. Na Seleção de times ele é a primeira opção do
    /// Jogador 1 (<see cref="MatchSession.ProfileTeam"/>) e vem escolhido sempre que a tela abre.
    /// </summary>
    public static class PlayerProfile
    {
        public const string DefaultName = "Meu Time";
        public const string DefaultAbbreviation = "MEU";
        public const int NameLength = 16;
        public const int AbbreviationLength = 3;
        public static readonly Color DefaultPrimary = new(0.165f, 0.373f, 0.851f);
        public static readonly Color DefaultSecondary = Color.white;

        private static TeamData team;

        public static SaveData.ProfileData Data => GameSave.Data.profile ??= new SaveData.ProfileData();

        public static int Coins => Data.coins;

        /// <summary>O Meu time como um time do catálogo (nome, sigla e cores), para a Partida, o placar e o Resultado.</summary>
        public static TeamData Team
        {
            get
            {
                var data = Data;
                if (team == null)
                {
                    team = TeamData.Create(data.teamName, data.abbreviation, data.primary, data.secondary);
                    team.hideFlags = HideFlags.DontSave;
                    team.FromPlayer = true;
                }
                else team.Set(data.teamName, data.abbreviation, data.primary, data.secondary);
                team.SetShirts(data.discPatternId, data.keeperPatternId);
                team.SetUniform2(Uniform(1));
                return team;
            }
        }

        /// <summary>Uniforme 1 (0) ou 2 (1) do Meu time, como está no save.</summary>
        public static TeamUniform Uniform(int index)
        {
            var data = Data;
            return index <= 0
                ? new TeamUniform(data.primary, data.secondary, data.discPatternId, data.keeperPatternId)
                : new TeamUniform(data.primary2, data.secondary2, data.discPatternId2, data.keeperPatternId2);
        }

        /// <summary>Grava o Meu time (nome e sigla já limpos; vazios voltam ao padrão) com os dois uniformes.</summary>
        public static void Save(string teamName, string abbreviation, TeamUniform first, TeamUniform second)
        {
            var data = Data;
            data.teamName = CleanName(teamName);
            data.abbreviation = CleanAbbreviation(abbreviation);
            data.primary = first.primary;
            data.secondary = first.secondary;
            data.discPatternId = first.discPatternId;
            data.keeperPatternId = first.keeperPatternId;
            data.primary2 = second.primary;
            data.secondary2 = second.secondary;
            data.discPatternId2 = second.discPatternId;
            data.keeperPatternId2 = second.keeperPatternId;
            GameSave.Save("meu time");
        }

        /// <summary>Grava a mesa de casa (aba Mesa do Meu time).</summary>
        public static void SaveHome(VenueSetup home)
        {
            var data = Data;
            data.tableId = home.TableId;
            data.feltId = home.FeltId;
            data.goalColorName = home.Table != null ? home.Table.Goal(home.GoalColor)?.name : null;
            data.ballId = home.BallId;
            GameSave.Save("mesa de casa");
        }

        public static string CleanName(string value)
        {
            value = value?.Trim() ?? string.Empty;
            if (value.Length > NameLength) value = value.Substring(0, NameLength).Trim();
            return value.Length > 0 ? value : DefaultName;
        }

        public static string CleanAbbreviation(string value)
        {
            var text = new System.Text.StringBuilder();
            foreach (char c in value ?? string.Empty)
                if (char.IsLetterOrDigit(c) && text.Length < AbbreviationLength) text.Append(char.ToUpperInvariant(c));
            return text.Length > 0 ? text.ToString() : DefaultAbbreviation;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => team = null;
    }
}
