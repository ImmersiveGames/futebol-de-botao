using Immersive.Framework.GameFlow;
using UnityEngine;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>Opções da partida que aparecem no Menu, na ordem das linhas da tela.</summary>
    public enum MatchOptionKind
    {
        Duration,
        Difficulty,
        Discs,
        Goalkeeper,
        Touches,
        GoalAfterWall,
        AimAssist,
        Fouls,
        Music
    }

    /// <summary>
    /// Menu: começa com Um jogador (contra a IA) ou Dois jogadores; escolhido o modo, abre os Controles (só com mais de
    /// um dispositivo), as Opções da partida (com a dificuldade da IA só em Um jogador), depois a Seleção de times, e
    /// "Jogar" põe os jogadores na sessão e pede a Route da Partida.
    /// As opções editam a cópia da sessão (MatchSession), não o asset padrão.
    /// </summary>
    public sealed class MenuScreen : MonoBehaviour
    {
        private static readonly int[] Durations = { 3, 5, 8, 10 };
        /// <summary>Distância entre as linhas das Opções; sem a Dificuldade, as de baixo sobem uma linha.</summary>
        public const float OptionRowSpacing = 80f;

        [SerializeField] private MatchOptions defaults;
        [SerializeField] private RouteRequestTrigger matchRoute;
        [SerializeField] private GameObject modePanel;
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private TeamSelectionScreen selection;
        [Tooltip("Tela Controles: cada jogador escolhe o dispositivo. Vazio: pula direto para as Opções.")]
        [SerializeField] private DeviceSelectionScreen devices;
        [Tooltip("Mostra nas Opções o modo escolhido.")]
        [SerializeField] private Text modeLabel;
        [Tooltip("Acerta quem está na sessão (Jogador 1 contra a IA). Vazio: a partida lê o mouse/teclado direto.")]
        [SerializeField] private MenuPlayers players;
        [Tooltip("Um texto de valor por opção, na ordem de MatchOptionKind.")]
        [SerializeField] private Text[] valueLabels = new Text[0];
        [Tooltip("Uma linha por opção, na ordem de MatchOptionKind.")]
        [SerializeField] private RectTransform[] optionRows = new RectTransform[0];

        private bool devicesShown;

        public void Configure(MatchOptions defaultOptions, RouteRequestTrigger match, GameObject modeRoot, GameObject optionsRoot,
            Text mode, Text[] values, RectTransform[] rows)
        {
            optionRows = rows;
            defaults = defaultOptions;
            matchRoute = match;
            modePanel = modeRoot;
            optionsPanel = optionsRoot;
            modeLabel = mode;
            valueLabels = values;
        }

        private void Start()
        {
            ShowMode();
            Refresh();
        }

        /// <summary>Um jogador: contra a IA (nível nas Opções). Você joga com o time de baixo. Abre as Opções.</summary>
        public void PlayOnePlayer()
        {
            MatchSession.VsAi = true;
            OpenDevices();
        }

        /// <summary>2 jogadores: cada um no seu dispositivo ou revezando o mesmo. Abre os Controles ou as Opções.</summary>
        public void PlayTwoPlayers()
        {
            MatchSession.VsAi = false;
            OpenDevices();
        }

        /// <summary>Controles, quando há mais de um dispositivo; senão o único fica escolhido e vai para as Opções.</summary>
        private void OpenDevices()
        {
            devicesShown = devices != null && devices.Open();
            if (devicesShown) Show(devices.gameObject);
            else ShowOptions();
        }

        /// <summary>Os Controles terminaram: segue para as Opções.</summary>
        public void ShowOptionsFromDevices() => ShowOptions();

        /// <summary>"Voltar" nas Opções: volta aos Controles se eles apareceram, senão à primeira tela.</summary>
        public void BackFromOptions()
        {
            if (devicesShown) OpenDevices();
            else ShowMode();
        }

        /// <summary>"Jogar" na Seleção de times: põe os jogadores na sessão (com os dispositivos) e vai para a Partida.</summary>
        public void Play()
        {
            if (players != null) players.Prepare(RequestMatch);
            else RequestMatch();
        }

        public void SetDevices(DeviceSelectionScreen deviceSelection) => devices = deviceSelection;

        public void SetPlayers(MenuPlayers menuPlayers) => players = menuPlayers;

        public void SetSelection(TeamSelectionScreen teamSelection) => selection = teamSelection;

        /// <summary>"Avançar" nas Opções: abre a Seleção de times (sem ela, vai direto para a Partida).</summary>
        public void ShowSelection()
        {
            if (selection == null)
            {
                Play();
                return;
            }
            Show(selection.gameObject);
            selection.Open();
        }

        private void RequestMatch()
        {
            if (matchRoute != null) matchRoute.RequestRoute();
        }

        public void ShowOptions()
        {
            Show(optionsPanel);
            if (modeLabel != null) modeLabel.text = MatchSession.VsAi == true ? "Um jogador" : "Dois jogadores";
            LayoutRows();
            Refresh();
        }

        /// <summary>"Como jogar": a primeira tela do Menu.</summary>
        public void ShowMode() => Show(modePanel);

        private void Show(GameObject panel)
        {
            foreach (var other in new[] { modePanel, optionsPanel, selection != null ? selection.gameObject : null,
                         devices != null ? devices.gameObject : null })
                if (other != null) other.SetActive(other == panel);
        }

        /// <summary>A Dificuldade só aparece contra a IA; as linhas visíveis ficam uma embaixo da outra.</summary>
        private void LayoutRows()
        {
            bool vsAi = MatchSession.VsAi == true;
            int slot = 0;
            for (int i = 0; i < optionRows.Length; i++)
            {
                var row = optionRows[i];
                if (row == null) continue;
                bool shown = vsAi || (MatchOptionKind)i != MatchOptionKind.Difficulty;
                row.gameObject.SetActive(shown);
                if (!shown) continue;
                row.anchoredPosition = new Vector2(0f, (i - slot) * OptionRowSpacing);
                slot++;
            }
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
                case MatchOptionKind.Difficulty:
                    MatchSession.AiLevel = (AiLevel)Wrap((int)MatchSession.AiLevel + step, 3);
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
                case MatchOptionKind.Fouls:
                    options.fouls = !options.fouls;
                    break;
                case MatchOptionKind.Music:
                    MusicSetting.Enabled = !MusicSetting.Enabled;
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
            MatchOptionKind.Difficulty => "Dificuldade",
            MatchOptionKind.Discs => "Botões por time",
            MatchOptionKind.Goalkeeper => "Goleiro",
            MatchOptionKind.Touches => "Toques por vez",
            MatchOptionKind.GoalAfterWall => "Gol após parede",
            MatchOptionKind.AimAssist => "Ajuda de mira",
            MatchOptionKind.Fouls => "Faltas",
            MatchOptionKind.Music => "Música",
            _ => kind.ToString()
        };

        public static string Value(MatchOptions options, MatchOptionKind kind) => kind switch
        {
            MatchOptionKind.Duration => $"{options.durationMinutes} min",
            MatchOptionKind.Difficulty => AiDifficulty.DisplayName(MatchSession.AiLevel),
            MatchOptionKind.Discs => options.discsPerTeam.ToString(),
            MatchOptionKind.Goalkeeper => OnOff(options.hasGoalkeeper),
            MatchOptionKind.Touches => options.touchesPerTurn.ToString(),
            MatchOptionKind.GoalAfterWall => options.goalAfterWallIsValid ? "Válido" : "Anulado",
            MatchOptionKind.AimAssist => options.aimAssist switch
            {
                AimAssistMode.Full => "Completa",
                AimAssistMode.Short => "Curta",
                _ => "Desligada"
            },
            MatchOptionKind.Fouls => OnOff(options.fouls),
            MatchOptionKind.Music => OnOff(MusicSetting.Enabled),
            _ => string.Empty
        };

        public static string OnOff(bool value) => value ? "Ligado" : "Desligado";

        private static int Wrap(int value, int count) => ((value % count) + count) % count;
    }
}
