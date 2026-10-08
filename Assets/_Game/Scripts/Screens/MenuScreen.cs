using Immersive.Framework.GameFlow;
using UnityEngine;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>Opções da partida que aparecem no Menu, na ordem das linhas da tela.</summary>
    public enum MatchOptionKind
    {
        Duration,
        Discs,
        Goalkeeper,
        Touches,
        GoalAfterWall,
        AimAssist
    }

    /// <summary>
    /// Menu: "Iniciar" abre a escolha do modo (contra a IA, com o nível, ou 2 jogadores) e então pede a Route da Partida;
    /// "Opções da partida" abre o painel com as opções do GDD.
    /// As opções editam a cópia da sessão (MatchSession), não o asset padrão.
    /// </summary>
    public sealed class MenuScreen : MonoBehaviour
    {
        private static readonly int[] Durations = { 3, 5, 8, 10 };

        [SerializeField] private MatchOptions defaults;
        [SerializeField] private RouteRequestTrigger matchRoute;
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private GameObject modePanel;
        [Tooltip("Um texto de valor por opção, na ordem de MatchOptionKind.")]
        [SerializeField] private Text[] valueLabels = new Text[0];

        public void Configure(MatchOptions defaultOptions, RouteRequestTrigger match, GameObject main, GameObject optionsRoot, GameObject modeRoot,
            Text[] values)
        {
            modePanel = modeRoot;
            defaults = defaultOptions;
            matchRoute = match;
            mainPanel = main;
            optionsPanel = optionsRoot;
            valueLabels = values;
        }

        private void Start()
        {
            ShowMain();
            Refresh();
        }

        /// <summary>"Iniciar": mostra a escolha do modo. Sem o painel de modo (cena antiga), entra direto.</summary>
        public void StartMatch()
        {
            if (modePanel == null)
            {
                RequestMatch();
                return;
            }
            Show(modePanel);
        }

        /// <summary>Contra a IA: 0 = Fácil, 1 = Médio, 2 = Difícil. Você joga com o Azul (baixo).</summary>
        public void PlayVsAi(int level)
        {
            MatchSession.VsAi = true;
            MatchSession.AiLevel = (AiLevel)Mathf.Clamp(level, 0, 2);
            RequestMatch();
        }

        public void PlayTwoPlayers()
        {
            MatchSession.VsAi = false;
            RequestMatch();
        }

        private void RequestMatch()
        {
            if (matchRoute != null) matchRoute.RequestRoute();
        }

        public void ShowOptions()
        {
            Show(optionsPanel);
            Refresh();
        }

        public void ShowMain() => Show(mainPanel);

        private void Show(GameObject panel)
        {
            foreach (var other in new[] { mainPanel, optionsPanel, modePanel })
                if (other != null) other.SetActive(other == panel);
        }

        public void Previous(int kind) => Change((MatchOptionKind)kind, -1);

        public void Next(int kind) => Change((MatchOptionKind)kind, 1);

        private void Change(MatchOptionKind kind, int step)
        {
            var options = MatchSession.Options(defaults);
            switch (kind)
            {
                case MatchOptionKind.Duration:
                    int index = System.Array.IndexOf(Durations, options.durationMinutes);
                    if (index < 0) index = 1;
                    options.durationMinutes = Durations[Wrap(index + step, Durations.Length)];
                    break;
                case MatchOptionKind.Discs:
                    options.discsPerTeam = options.discsPerTeam == 5 ? 3 : 5;
                    break;
                case MatchOptionKind.Goalkeeper:
                    options.hasGoalkeeper = !options.hasGoalkeeper;
                    break;
                case MatchOptionKind.Touches:
                    options.touchesPerTurn = Wrap(options.touchesPerTurn - 1 + step, 3) + 1;
                    break;
                case MatchOptionKind.GoalAfterWall:
                    options.goalAfterWallIsValid = !options.goalAfterWallIsValid;
                    break;
                case MatchOptionKind.AimAssist:
                    options.aimAssist = (AimAssistMode)Wrap((int)options.aimAssist + step, 3);
                    break;
            }
            Refresh();
        }

        private void Refresh()
        {
            var options = MatchSession.Options(defaults);
            for (int i = 0; i < valueLabels.Length; i++)
                if (valueLabels[i] != null) valueLabels[i].text = Value(options, (MatchOptionKind)i);
        }

        public static string Label(MatchOptionKind kind) => kind switch
        {
            MatchOptionKind.Duration => "Duração",
            MatchOptionKind.Discs => "Botões por time",
            MatchOptionKind.Goalkeeper => "Goleiro",
            MatchOptionKind.Touches => "Toques por vez",
            MatchOptionKind.GoalAfterWall => "Gol após parede",
            MatchOptionKind.AimAssist => "Ajuda de mira",
            _ => kind.ToString()
        };

        public static string Value(MatchOptions options, MatchOptionKind kind) => kind switch
        {
            MatchOptionKind.Duration => $"{options.durationMinutes} min",
            MatchOptionKind.Discs => options.discsPerTeam.ToString(),
            MatchOptionKind.Goalkeeper => options.hasGoalkeeper ? "Com" : "Sem",
            MatchOptionKind.Touches => options.touchesPerTurn.ToString(),
            MatchOptionKind.GoalAfterWall => options.goalAfterWallIsValid ? "Válido" : "Anulado",
            MatchOptionKind.AimAssist => options.aimAssist switch
            {
                AimAssistMode.Full => "Completa",
                AimAssistMode.Short => "Curta",
                _ => "Desligada"
            },
            _ => string.Empty
        };

        private static int Wrap(int value, int count) => ((value % count) + count) % count;
    }
}
