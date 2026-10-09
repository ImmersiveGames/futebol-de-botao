using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>Placar final da última partida, lido pela tela de Resultado.</summary>
    public readonly struct MatchResult
    {
        public MatchResult(int bottom, int top, bool walkover = false)
        {
            Bottom = bottom;
            Top = top;
            Walkover = walkover;
        }

        public int Bottom { get; }
        public int Top { get; }
        /// <summary>Acabou por W.O. (um time ficou sem botões por cartão vermelho).</summary>
        public bool Walkover { get; }
    }

    /// <summary>
    /// Dados que passam de uma tela para outra enquanto o jogo está aberto: as opções escolhidas no Menu,
    /// os times e esquemas da Seleção de times, o placar da última partida, o modo (contra a IA ou 2 jogadores) e o nível da IA. As opções são uma cópia em memória do asset padrão, que não é alterado.
    /// Salvar entre sessões fica para a fase 4 (Progression Save).
    /// </summary>
    public static class MatchSession
    {
        private static MatchOptions options;

        public static MatchResult? LastResult { get; set; }

        /// <summary>Modo escolhido no Menu: true = contra a IA, false = 2 jogadores. Nulo: a partida abriu sem passar pelo Menu.</summary>
        public static bool? VsAi { get; set; }

        /// <summary>Nível da IA escolhido no Menu.</summary>
        public static AiLevel AiLevel { get; set; } = AiLevel.Medium;

        // Times e esquemas escolhidos na Seleção de times, por lado (0 = baixo, 1 = cima).
        private static readonly int[] teamIndex = { 0, 1 };
        private static readonly int[] scheme5 = { 0, 0 };
        private static readonly int[] scheme3 = { 0, 0 };

        public static TeamData Team(TeamSide side) => TeamCatalog.Load().Team(teamIndex[(int)side]);

        public static int TeamIndex(TeamSide side) => teamIndex[(int)side];

        public static void SetTeamIndex(TeamSide side, int index) => teamIndex[(int)side] = index;

        /// <summary>Índice do esquema do lado <paramref name="side"/> para 3 ou 5 botões.</summary>
        public static int SchemeIndex(TeamSide side, int discs) => (discs == 3 ? scheme3 : scheme5)[(int)side];

        public static void SetSchemeIndex(TeamSide side, int discs, int index) => (discs == 3 ? scheme3 : scheme5)[(int)side] = index;

        /// <summary>Esquema tático escolhido para o lado, com 3 ou 5 botões.</summary>
        public static Formation Scheme(TeamSide side, int discs) => TeamCatalog.Load().Scheme(discs, SchemeIndex(side, discs));

        /// <summary>Opções da sessão; na primeira vez copia <paramref name="defaults"/> (ou o padrão do código).</summary>
        public static MatchOptions Options(MatchOptions defaults)
        {
            if (options != null) return options;
            options = defaults != null ? Object.Instantiate(defaults) : MatchOptions.CreateDefault();
            options.name = "Opções da sessão";
            options.hideFlags = HideFlags.DontSave;
            return options;
        }

        // Com o "Enter Play Mode" sem recarregar o domínio, os estáticos sobrevivem entre execuções no editor.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            options = null;
            LastResult = null;
            VsAi = null;
            AiLevel = AiLevel.Medium;
            teamIndex[0] = 0;
            teamIndex[1] = 1;
            scheme5[0] = scheme5[1] = 0;
            scheme3[0] = scheme3[1] = 0;
        }
    }
}
