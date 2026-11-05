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
                }
                else team.Set(data.teamName, data.abbreviation, data.primary, data.secondary);
                return team;
            }
        }

        public static ShirtPattern DiscPattern => ShirtPattern.Resolve(ShirtPattern.Kind.Botao, Data.discPatternId);

        public static ShirtPattern KeeperPattern => ShirtPattern.Resolve(ShirtPattern.Kind.Goleiro, Data.keeperPatternId);

        /// <summary>Grava o Meu time (nome e sigla já limpos; vazios voltam ao padrão).</summary>
        public static void Save(string teamName, string abbreviation, Color primary, Color secondary, string discPatternId, string keeperPatternId)
        {
            var data = Data;
            data.teamName = CleanName(teamName);
            data.abbreviation = CleanAbbreviation(abbreviation);
            data.primary = primary;
            data.secondary = secondary;
            data.discPatternId = discPatternId;
            data.keeperPatternId = keeperPatternId;
            // Quem está com o Meu time na sessão já sai com as camisas novas.
            if (MatchSession.TeamIndex(TeamSide.Bottom) == MatchSession.ProfileTeam) MatchSession.UseProfile(TeamSide.Bottom);
            GameSave.Save("meu time");
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
