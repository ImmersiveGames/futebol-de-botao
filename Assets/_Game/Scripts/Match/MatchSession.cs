using UnityEngine;
using UnityEngine.InputSystem;

namespace FutebolDeBotao
{
    /// <summary>Placar final da última partida, lido pela tela de Resultado.</summary>
    public readonly struct MatchResult
    {
        public MatchResult(int bottom, int top, bool walkover = false, int penaltiesBottom = -1, int penaltiesTop = -1)
        {
            Bottom = bottom;
            Top = top;
            Walkover = walkover;
            PenaltiesBottom = penaltiesBottom;
            PenaltiesTop = penaltiesTop;
        }

        public int Bottom { get; }
        public int Top { get; }
        /// <summary>Acabou por W.O. (um time ficou sem botões por cartão vermelho).</summary>
        public bool Walkover { get; }
        /// <summary>Gols de cada time na disputa de pênaltis; -1 = não teve disputa.</summary>
        public int PenaltiesBottom { get; }
        public int PenaltiesTop { get; }
        public bool HasPenalties => PenaltiesBottom >= 0 && PenaltiesTop >= 0;
    }

    /// <summary>
    /// Dados que passam de uma tela para outra enquanto o jogo está aberto: as opções escolhidas no Menu,
    /// os times, esquemas e camisas da Seleção de times, o placar da última partida, o modo (contra a IA ou 2 jogadores) e o nível da IA. As opções são uma cópia em memória do asset padrão, que não é alterado.
    /// O <see cref="GameSave"/> carrega as últimas escolhas aqui quando o jogo abre e grava de volta.
    /// </summary>
    public static class MatchSession
    {
        private static MatchOptions options;

        public static MatchResult? LastResult { get; set; }

        /// <summary>
        /// Números dos dois times na última partida terminada (0 = baixo, 1 = cima), para a tela do pós-jogo. Ficam só na
        /// memória: o save guarda só os do jogador (<see cref="PlayerStats"/>).
        /// </summary>
        public static SaveData.MatchNumbers[] LastMatchNumbers { get; set; }

        /// <summary>Modo escolhido no Menu: true = contra a IA, false = 2 jogadores. Nulo: a partida abriu sem passar pelo Menu.</summary>
        public static bool? VsAi { get; set; }

        /// <summary>Dispositivo do Jogador 1 escolhido na tela Controles. Nulo: sem escolha (a Unity dá o primeiro livre).</summary>
        public static InputDevice Player1Device { get; set; }

        /// <summary>Dispositivo do Jogador 2 (só em Dois jogadores com dispositivos diferentes).</summary>
        public static InputDevice Player2Device { get; set; }

        /// <summary>Dois jogadores no mesmo dispositivo, revezando (a partida lê os dispositivos direto).</summary>
        public static bool SharedDevice { get; set; }

        /// <summary>Nível da IA escolhido no Menu.</summary>
        public static AiLevel AiLevel { get; set; } = AiLevel.Medium;

        /// <summary>Id do feltro da partida (<see cref="FeltTheme"/>); nulo = sortear. O Reiniciar e o Jogar de novo mantêm.</summary>
        public static string FeltId { get; set; }

        /// <summary>Id da mesa da partida (<see cref="TableTheme"/>); nulo = sortear de novo (com a cor das traves). O Reiniciar mantém.</summary>
        public static string TableId { get; set; }

        /// <summary>Id da bola da partida (<see cref="BallTheme"/>); nulo = sortear de novo. O Reiniciar mantém.</summary>
        public static string BallId { get; set; }

        /// <summary>Cor das traves da mesa da partida (índice nas cores da mesa).</summary>
        public static int GoalColor { get; set; }

        /// <summary>A partida é na mesa do visitante (sorteada); falso = na mesa de casa do jogador.</summary>
        public static bool Away { get; private set; }

        /// <summary>Mesa de casa (do Meu time) e a do visitante (sorteada) da partida; nulas antes da Seleção de times.</summary>
        public static VenueSetup HomeVenue { get; private set; }
        public static VenueSetup AwayVenue { get; private set; }

        /// <summary>
        /// Partida nova (Seleção de times aberta pelo Menu): pega a mesa de casa, sorteia a do visitante e começa em casa.
        /// O Reiniciar e o Jogar de novo não passam aqui: ficam com a mesma mesa, feltro, traves e bola.
        /// </summary>
        public static void PrepareVenues()
        {
            HomeVenue = Venue.Home();
            AwayVenue = Venue.RollAway(HomeVenue);
            SetAway(false);
        }

        /// <summary>Joga em casa ou fora: mesa, feltro, traves e bola daquele lado (a bola volta à do lado).</summary>
        public static void SetAway(bool away)
        {
            Away = away;
            var venue = away ? AwayVenue : HomeVenue;
            if (venue == null) return;
            TableId = venue.TableId;
            FeltId = venue.FeltId;
            GoalColor = venue.GoalColor;
            BallId = venue.BallId;
        }

        // Times e esquemas escolhidos na Seleção de times, por lado (0 = baixo, 1 = cima).
        private static readonly int[] teamIndex = { 0, 1 };
        private static readonly int[] scheme5 = { 0, 0 };
        private static readonly int[] scheme3 = { 0, 0 };
        private static readonly string[] discPattern = { null, null };
        private static readonly string[] keeperPattern = { null, null };
        /// <summary>Lado da IA com as camisas do time: vale qualquer padrão, mesmo travado na loja do jogador.</summary>
        private static readonly bool[] anyPattern = { false, false };
        private static readonly int[] secondaryIndex = { 0, 0 };

        /// <summary>Índice de time que quer dizer "Meu time" (<see cref="PlayerProfile"/>), só do Jogador 1.</summary>
        public const int ProfileTeam = -1;

        public static TeamData Team(TeamSide side) =>
            teamIndex[(int)side] < 0 ? PlayerProfile.Team : TeamCatalog.Load().Team(teamIndex[(int)side]);

        /// <summary>O lado está com o Meu time.</summary>
        public static bool IsProfile(TeamSide side) => teamIndex[(int)side] < 0;

        /// <summary>Põe o Meu time no lado, com a segunda cor e as camisas dele.</summary>
        public static void UseProfile(TeamSide side)
        {
            teamIndex[(int)side] = ProfileTeam;
            secondaryIndex[(int)side] = 0;
            discPattern[(int)side] = PlayerProfile.Data.discPatternId;
            keeperPattern[(int)side] = PlayerProfile.Data.keeperPatternId;
            anyPattern[(int)side] = false;
        }

        /// <summary>
        /// Camisas do time do catálogo no lado da IA: as escolhidas no asset do time (mesmo travadas na loja) ou
        /// sorteadas quando o time está em "Sortear".
        /// </summary>
        public static void UseTeamShirts(TeamSide side)
        {
            var team = Team(side);
            anyPattern[(int)side] = true;
            discPattern[(int)side] = ShirtPattern.FindOf(ShirtPattern.Kind.Botao, team.DiscPatternId)?.id
                                     ?? ShirtPattern.RandomId(ShirtPattern.Kind.Botao);
            keeperPattern[(int)side] = ShirtPattern.FindOf(ShirtPattern.Kind.Goleiro, team.KeeperPatternId)?.id
                                       ?? ShirtPattern.RandomId(ShirtPattern.Kind.Goleiro);
        }

        public static int TeamIndex(TeamSide side) => teamIndex[(int)side];

        public static void SetTeamIndex(TeamSide side, int index) => teamIndex[(int)side] = index;

        /// <summary>Segunda cor escolhida (0 = a do próprio time), entre as <see cref="TeamCatalog.SecondaryOptions"/>.</summary>
        public static int SecondaryIndex(TeamSide side) => secondaryIndex[(int)side];

        public static void SetSecondaryIndex(TeamSide side, int index) => secondaryIndex[(int)side] = index;

        /// <summary>Segunda cor do lado (padrão da camisa e anel), já com a escolha da Seleção de times.</summary>
        public static Color SecondaryColor(TeamSide side)
        {
            var options = TeamCatalog.Load().SecondaryOptions(Team(side));
            int count = options.Length;
            return options[((secondaryIndex[(int)side] % count) + count) % count];
        }

        /// <summary>Padrão da camisa dos botões do lado (o escolhido, se liberado; senão o primeiro liberado).</summary>
        public static ShirtPattern DiscPattern(TeamSide side) =>
            (anyPattern[(int)side] ? ShirtPattern.FindOf(ShirtPattern.Kind.Botao, discPattern[(int)side]) : null)
            ?? ShirtPattern.Resolve(ShirtPattern.Kind.Botao, discPattern[(int)side]);

        /// <summary>Id do padrão dos botões escolhido (pode estar vazio ou travado; <see cref="DiscPattern"/> resolve).</summary>
        public static string DiscPatternId(TeamSide side) => DiscPattern(side)?.id ?? discPattern[(int)side];

        public static void SetDiscPattern(TeamSide side, string patternId) => discPattern[(int)side] = patternId;

        /// <summary>Padrão da camisa do goleiro do lado (o escolhido, se liberado; senão o primeiro liberado).</summary>
        public static ShirtPattern KeeperPattern(TeamSide side) =>
            (anyPattern[(int)side] ? ShirtPattern.FindOf(ShirtPattern.Kind.Goleiro, keeperPattern[(int)side]) : null)
            ?? ShirtPattern.Resolve(ShirtPattern.Kind.Goleiro, keeperPattern[(int)side]);

        public static string KeeperPatternId(TeamSide side) => KeeperPattern(side)?.id ?? keeperPattern[(int)side];

        public static void SetKeeperPattern(TeamSide side, string patternId) => keeperPattern[(int)side] = patternId;

        /// <summary>Índice do esquema do lado <paramref name="side"/> para 3 ou 5 botões.</summary>
        public static int SchemeIndex(TeamSide side, int discs) => (discs == 3 ? scheme3 : scheme5)[(int)side];

        public static void SetSchemeIndex(TeamSide side, int discs, int index) => (discs == 3 ? scheme3 : scheme5)[(int)side] = index;

        /// <summary>Esquema tático escolhido para o lado, com 3 ou 5 botões.</summary>
        public static Formation Scheme(TeamSide side, int discs) => TeamCatalog.Load().Scheme(discs, SchemeIndex(side, discs));

        /// <summary>Torneio: o Menu está montando um torneio novo (Opções do torneio).</summary>
        public static bool TournamentSetup { get; set; }

        /// <summary>A partida é do torneio (<see cref="Tournament"/>): as opções são as dele.</summary>
        public static bool InTournamentMatch { get; private set; }

        /// <summary>Ao abrir, o Menu vai direto para a chave do torneio (Avançar no Resultado).</summary>
        public static bool OpenTournament { get; set; }

        private static MatchOptions tournamentOptions;

        /// <summary>Partida do torneio com estas opções (o <see cref="Options"/> passa a devolvê-las).</summary>
        public static void UseTournamentOptions(MatchOptions matchOptions)
        {
            tournamentOptions = matchOptions;
            InTournamentMatch = matchOptions != null;
        }

        /// <summary>Fora do torneio: volta às opções da sessão.</summary>
        public static void LeaveTournament()
        {
            tournamentOptions = null;
            InTournamentMatch = false;
        }

        /// <summary>Opções da sessão; na primeira vez copia <paramref name="defaults"/> (ou o padrão do código).</summary>
        public static MatchOptions Options(MatchOptions defaults)
        {
            if (InTournamentMatch && tournamentOptions != null) return tournamentOptions;
            if (options != null) return options;
            options = defaults != null ? Object.Instantiate(defaults) : MatchOptions.CreateDefault();
            options.name = "Opções da sessão";
            options.hideFlags = HideFlags.DontSave;
            GameSave.ApplyOptions(options);
            return options;
        }

        /// <summary>Opções da sessão já criadas, ou nulo se ainda ninguém pediu.</summary>
        public static MatchOptions Current => options;

        // Com o "Enter Play Mode" sem recarregar o domínio, os estáticos sobrevivem entre execuções no editor.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            options = null;
            tournamentOptions = null;
            InTournamentMatch = false;
            TournamentSetup = false;
            OpenTournament = false;
            LastResult = null;
            LastMatchNumbers = null;
            FeltId = null;
            TableId = null;
            BallId = null;
            GoalColor = 0;
            Away = false;
            HomeVenue = null;
            AwayVenue = null;
            VsAi = null;
            AiLevel = AiLevel.Medium;
            teamIndex[0] = 0;
            teamIndex[1] = 1;
            scheme5[0] = scheme5[1] = 0;
            scheme3[0] = scheme3[1] = 0;
            discPattern[0] = discPattern[1] = null;
            keeperPattern[0] = keeperPattern[1] = null;
            anyPattern[0] = anyPattern[1] = false;
            secondaryIndex[0] = secondaryIndex[1] = 0;
        }
    }
}
