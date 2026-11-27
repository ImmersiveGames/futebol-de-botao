using System.Collections.Generic;

namespace FutebolDeBotao
{
    /// <summary>O que um desafio conta (cada fim de partida, lição ou moeda soma no que for do tipo).</summary>
    public enum ChallengeKind
    {
        /// <summary>Partidas terminadas.</summary>
        Matches,
        Wins,
        Goals,
        /// <summary>"Pro Gol!" avisados.</summary>
        ShotsCalled,
        /// <summary>"Pro Gol!" convertidos em gol.</summary>
        ShotGoals,
        /// <summary>Toques certos na bola.</summary>
        Touches,
        /// <summary>Partida terminada sem levar cartão.</summary>
        NoCardMatch,
        WinOrDraw,
        /// <summary>Partida que terminou empatada (mesmo indo aos pênaltis).</summary>
        Draw,
        TutorialLesson,
        /// <summary>Vitórias no nível <see cref="ChallengeDef.minLevel"/> ou acima.</summary>
        WinAtLevel,
        CleanSheetWin,
        /// <summary>Vitória numa partida de torneio ou de liga.</summary>
        CompetitionWin,
        /// <summary>Vitória depois de estar perdendo.</summary>
        ComebackWin,
        NoFoulWin,
        /// <summary>Gols de falta ou de pênalti (na partida, não na disputa).</summary>
        SetPieceGoals,
        /// <summary>Vitória por 3 gols ou mais.</summary>
        BigWin,
        /// <summary>Partida com 3 gols ou mais do jogador.</summary>
        HatTrick,
        /// <summary>Vitória no mata-mata de um torneio (passa de fase).</summary>
        KnockoutWin,
        ShootoutWin,
        ExtraTimeWin,
        /// <summary>Vitória jogando com 1 toque por vez.</summary>
        OneTouchWin,
        /// <summary>Partida de <see cref="ChallengeDef.minutes"/> minutos.</summary>
        MatchOfMinutes,
        NoKeeperMatch,
        FiveDiscMatch,
        /// <summary>Pontos na liga (3 a vitória, 1 o empate).</summary>
        LeaguePoints,
        LeagueMatches,
        /// <summary>Gols em que a bola tocou alguma parede antes de entrar.</summary>
        WallGoals,
        /// <summary>Moedas de partidas e torneios (inclusive o Dobrar).</summary>
        Coins,
        /// <summary>Torneio (não liga) ganho.</summary>
        TournamentTitle,
        /// <summary>Chegou à final de um torneio de 16 times ou mais.</summary>
        BigFinal,
        /// <summary>Terminou uma liga entre os 3 primeiros.</summary>
        LeagueTop3,
    }

    /// <summary>Dificuldade do desafio diário: 1 de cada por dia.</summary>
    public enum ChallengeTier
    {
        Easy,
        Medium,
        Hard,
    }

    /// <summary>Um desafio da lista: texto (id de tradução, em PT), meta, o que conta e o prêmio.</summary>
    public sealed class ChallengeDef
    {
        public string id;
        public string text;
        public int target;
        public ChallengeKind kind;
        public ChallengeTier tier;
        public int reward;
        public bool weekly;
        public AiLevel minLevel;
        public int minutes;
    }

    /// <summary>
    /// A lista dos desafios (doc "Desafios diários – Pro Gol!", 27/11/2026). Diários: F (fácil, 5), M (médio, 10),
    /// D (difícil, 15). Semanais: 25, 40 e 60, sorteados um de cada valor. Os textos são ids do textos.json.
    /// </summary>
    public static class ChallengeCatalog
    {
        public const int EasyReward = 5;
        public const int MediumReward = 10;
        public const int HardReward = 15;
        /// <summary>Bônus de completar os 3 do dia.</summary>
        public const int DailyBonus = 15;

        public static readonly ChallengeDef[] Daily =
        {
            D("F1", ChallengeTier.Easy, "Jogue 2 partidas", 2, ChallengeKind.Matches),
            D("F2", ChallengeTier.Easy, "Marque 3 gols", 3, ChallengeKind.Goals),
            D("F3", ChallengeTier.Easy, "Chame o Pro Gol! 5 vezes", 5, ChallengeKind.ShotsCalled),
            D("F4", ChallengeTier.Easy, "Faça 30 toques na bola", 30, ChallengeKind.Touches),
            D("F5", ChallengeTier.Easy, "Termine uma partida sem cartão", 1, ChallengeKind.NoCardMatch),
            D("F6", ChallengeTier.Easy, "Empate ou vença uma partida", 1, ChallengeKind.WinOrDraw),
            D("F7", ChallengeTier.Easy, "Jogue uma lição do tutorial", 1, ChallengeKind.TutorialLesson),
            D("F8", ChallengeTier.Easy, "Jogue uma partida de 5 minutos", 1, ChallengeKind.MatchOfMinutes, minutes: 5),
            D("F9", ChallengeTier.Easy, "Jogue uma partida de 10 minutos", 1, ChallengeKind.MatchOfMinutes, minutes: 10),
            D("F10", ChallengeTier.Easy, "Jogue uma partida sem goleiro", 1, ChallengeKind.NoKeeperMatch),
            D("F11", ChallengeTier.Easy, "Jogue uma partida com 5 botões", 1, ChallengeKind.FiveDiscMatch),
            D("F12", ChallengeTier.Easy, "Faça 1 ponto na liga", 1, ChallengeKind.LeaguePoints),
            D("F13", ChallengeTier.Easy, "Faça 3 pontos na liga", 3, ChallengeKind.LeaguePoints),

            D("M1", ChallengeTier.Medium, "Vença no Médio ou acima", 1, ChallengeKind.WinAtLevel, AiLevel.Medium),
            D("M2", ChallengeTier.Medium, "Vença sem sofrer gol", 1, ChallengeKind.CleanSheetWin),
            D("M3", ChallengeTier.Medium, "Marque 6 gols", 6, ChallengeKind.Goals),
            D("M4", ChallengeTier.Medium, "Converta 3 Pro Gol! em gol", 3, ChallengeKind.ShotGoals),
            D("M5", ChallengeTier.Medium, "Vença uma partida de torneio ou liga", 1, ChallengeKind.CompetitionWin),
            D("M6", ChallengeTier.Medium, "Vire um jogo", 1, ChallengeKind.ComebackWin),
            D("M7", ChallengeTier.Medium, "Vença sem cometer falta", 1, ChallengeKind.NoFoulWin),
            D("M8", ChallengeTier.Medium, "Marque um gol de falta ou pênalti", 1, ChallengeKind.SetPieceGoals),
            D("M9", ChallengeTier.Medium, "Empate uma partida", 1, ChallengeKind.Draw),
            D("M10", ChallengeTier.Medium, "Faça 6 pontos na liga", 6, ChallengeKind.LeaguePoints),
            D("M11", ChallengeTier.Medium, "Ganhe 30 moedas em partidas", 30, ChallengeKind.Coins),
            D("M12", ChallengeTier.Medium, "Faça um gol de rebote na parede", 1, ChallengeKind.WallGoals),

            D("D1", ChallengeTier.Hard, "Vença no Difícil ou no Pro", 1, ChallengeKind.WinAtLevel, AiLevel.Hard),
            D("D2", ChallengeTier.Hard, "Vença por 3 gols de diferença", 1, ChallengeKind.BigWin),
            D("D3", ChallengeTier.Hard, "Vença 3 partidas", 3, ChallengeKind.Wins),
            D("D4", ChallengeTier.Hard, "Marque 3 gols numa partida", 1, ChallengeKind.HatTrick),
            D("D5", ChallengeTier.Hard, "Passe de fase num torneio", 1, ChallengeKind.KnockoutWin),
            D("D6", ChallengeTier.Hard, "Vença uma disputa de pênaltis", 1, ChallengeKind.ShootoutWin),
            D("D7", ChallengeTier.Hard, "Vença jogando com 1 toque por vez", 1, ChallengeKind.OneTouchWin),
            D("D8", ChallengeTier.Hard, "Vença na prorrogação", 1, ChallengeKind.ExtraTimeWin),
            D("D9", ChallengeTier.Hard, "Faça 9 pontos na liga", 9, ChallengeKind.LeaguePoints),
        };

        public static readonly ChallengeDef[] Weekly =
        {
            W("S1", 25, "Vença 8 partidas", 8, ChallengeKind.Wins),
            W("S2", 25, "Marque 25 gols", 25, ChallengeKind.Goals),
            W("S3", 25, "Jogue 15 partidas", 15, ChallengeKind.Matches),
            W("S4", 40, "Vença 3 partidas no Difícil ou no Pro", 3, ChallengeKind.WinAtLevel, AiLevel.Hard),
            W("S5", 40, "Converta 15 Pro Gol! em gol", 15, ChallengeKind.ShotGoals),
            W("S6", 40, "Vença 4 partidas sem sofrer gol", 4, ChallengeKind.CleanSheetWin),
            W("S7", 40, "Faça 15 pontos na liga", 15, ChallengeKind.LeaguePoints),
            W("S11", 40, "Ganhe 250 moedas em partidas e torneios", 250, ChallengeKind.Coins),
            W("S12", 40, "Jogue 10 partidas de liga", 10, ChallengeKind.LeagueMatches),
            W("S8", 60, "Ganhe um torneio", 1, ChallengeKind.TournamentTitle),
            W("S9", 60, "Chegue à final de um torneio de 16 ou mais", 1, ChallengeKind.BigFinal),
            W("S10", 60, "Termine uma liga entre os 3 primeiros", 1, ChallengeKind.LeagueTop3),
            W("S13", 60, "Faça 3 gols de rebote na parede", 3, ChallengeKind.WallGoals),
        };

        /// <summary>Prêmios dos semanais, um desafio de cada.</summary>
        public static readonly int[] WeeklyRewards = { 25, 40, 60 };

        private static Dictionary<string, ChallengeDef> byId;

        public static ChallengeDef Find(string id)
        {
            if (byId == null)
            {
                byId = new Dictionary<string, ChallengeDef>();
                foreach (var def in Daily) byId[def.id] = def;
                foreach (var def in Weekly) byId[def.id] = def;
            }
            return id != null && byId.TryGetValue(id, out var found) ? found : null;
        }

        public static int RewardOf(ChallengeTier tier) => tier switch
        {
            ChallengeTier.Easy => EasyReward,
            ChallengeTier.Medium => MediumReward,
            _ => HardReward,
        };

        private static ChallengeDef D(string id, ChallengeTier tier, string text, int target, ChallengeKind kind,
            AiLevel minLevel = AiLevel.Easy, int minutes = 0) => new()
        {
            id = id, tier = tier, text = text, target = target, kind = kind, reward = RewardOf(tier), minLevel = minLevel,
            minutes = minutes,
        };

        private static ChallengeDef W(string id, int reward, string text, int target, ChallengeKind kind,
            AiLevel minLevel = AiLevel.Easy) => new()
        {
            id = id, weekly = true, reward = reward, text = text, target = target, kind = kind, minLevel = minLevel,
            tier = reward <= 25 ? ChallengeTier.Easy : reward <= 40 ? ChallengeTier.Medium : ChallengeTier.Hard,
        };
    }
}
