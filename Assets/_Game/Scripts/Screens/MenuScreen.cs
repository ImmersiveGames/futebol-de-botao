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
        ExtraTime,
        Penalties,
        Music,
        /// <summary>Só nas Opções do torneio: quantos times na chave (um asset de torneio por tamanho).</summary>
        TournamentSize
    }

    /// <summary>
    /// Menu: a primeira tela tem Jogar, Meu time, Loja, Estatísticas e Sair. Jogar abre a escolha entre Torneio, Partida única
    /// (contra a IA) e Dois jogadores. O Torneio continua o guardado ou monta um novo (Controles, Opções do torneio e a
    /// chave, <see cref="TournamentScreen"/>); nas outras, escolhido o modo, abre os Controles (só com mais de um dispositivo), as Opções da partida (com a dificuldade da IA só em Um jogador), depois a Seleção de times, e
    /// "Jogar" põe os jogadores na sessão e pede a Route da Partida.
    /// As opções editam a cópia da sessão (MatchSession), não o asset padrão.
    /// </summary>
    public sealed class MenuScreen : MonoBehaviour
    {
        private static readonly int[] Durations = { 3, 5, 8, 10 };
        private static readonly int[] ExtraTimes = { 0, 50, 100 };
        /// <summary>Distância entre as linhas das Opções; sem a Dificuldade, as de baixo sobem uma linha.</summary>
        public const float OptionRowSpacing = 70f;

        [SerializeField] private MatchOptions defaults;
        [SerializeField] private RouteRequestTrigger matchRoute;
        [SerializeField] private GameObject modePanel;
        [SerializeField] private GameObject optionsPanel;
        [Tooltip("Escolha entre Um jogador e Dois jogadores (botão Jogar). Vazio: os botões ficam na primeira tela.")]
        [SerializeField] private GameObject playPanel;
        [Tooltip("Tela Meu time (botão na primeira tela do Menu).")]
        [SerializeField] private ProfileScreen profile;
        [Tooltip("Tela Loja (botão na primeira tela do Menu).")]
        [SerializeField] private ShopScreen shop;
        [SerializeField] private TeamSelectionScreen selection;
        [Tooltip("Tela Controles: cada jogador escolhe o dispositivo. Vazio: pula direto para as Opções.")]
        [SerializeField] private DeviceSelectionScreen devices;
        [Tooltip("Tela Estatísticas (botão na primeira tela do Menu).")]
        [SerializeField] private StatsScreen stats;
        [Tooltip("Tela do Torneio: a chave e o fim do torneio.")]
        [SerializeField] private TournamentScreen tournament;
        [Tooltip("Mostra nas Opções o modo escolhido.")]
        [SerializeField] private Text modeLabel;
        [Tooltip("Acerta quem está na sessão (Jogador 1 contra a IA). Vazio: a partida lê o mouse/teclado direto.")]
        [SerializeField] private MenuPlayers players;
        [Tooltip("Um texto de valor por opção, na ordem de MatchOptionKind.")]
        [SerializeField] private Text[] valueLabels = new Text[0];
        [Tooltip("Uma linha por opção, na ordem de MatchOptionKind.")]
        [SerializeField] private RectTransform[] optionRows = new RectTransform[0];

        [Tooltip("Pergunta \"Quer aprender a jogar?\" no primeiro Jogar (Sim abre o tutorial).")]
        [SerializeField] private GameObject tutorialAsk;

        private bool devicesShown;
        /// <summary>O caminho dos Controles termina no tutorial (sem Opções nem Seleção de times).</summary>
        private bool tutorialFlow;

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
            // Voltou de uma partida: as opções voltam a ser as da sessão; torneio que ficou no meio vira abandono.
            Tutorial.Leave();
            MatchSession.LeaveTournament();
            MatchSession.TournamentSetup = false;
            Tournament.ResolveLeftover();
            if (MatchSession.OpenTournament && tournament != null)
            {
                MatchSession.OpenTournament = false;
                ShowTournament();
            }
            else ShowMode();
            Refresh();
        }

        /// <summary>Partida única: contra a IA (nível nas Opções). Você joga com o time de baixo. Abre as Opções.</summary>
        public void PlayOnePlayer()
        {
            tutorialFlow = false;
            MatchSession.VsAi = true;
            MatchSession.TournamentSetup = false;
            OpenDevices();
        }

        /// <summary>Torneio: com um em andamento, a tela dele pergunta se continua; senão monta um novo.</summary>
        public void PlayTournament()
        {
            tutorialFlow = false;
            MatchSession.VsAi = true;
            if (Tournament.InProgress && tournament != null)
            {
                Show(tournament.gameObject);
                tournament.OpenAsk();
            }
            else NewTournament();
        }

        /// <summary>Torneio novo: Controles e as Opções do torneio; o "Avançar" sorteia a chave.</summary>
        public void NewTournament()
        {
            MatchSession.VsAi = true;
            MatchSession.TournamentSetup = true;
            OpenDevices();
        }

        /// <summary>A chave do torneio guardado (ou o fim dele).</summary>
        public void ShowTournament()
        {
            if (tournament == null) return;
            Show(tournament.gameObject);
            tournament.Open();
        }

        /// <summary>"Jogar" na chave: prepara o jogo do jogador e abre a Seleção de times (só esquema e camisas).</summary>
        public void PlayTournamentMatch()
        {
            if (!Tournament.PrepareMatch(defaults)) return;
            ShowSelection();
        }

        public void SetTournament(TournamentScreen tournamentScreen) => tournament = tournamentScreen;

        /// <summary>
        /// Fecha o aplicativo (no editor, para o Play). No Android também tira o jogo da lista de apps recentes
        /// (finishAndRemoveTask); sem isso o cartão ficava lá e, tocando nele, o jogo abria do zero.
        /// No navegador o botão nem aparece.
        /// </summary>
        public void QuitApp()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_ANDROID
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                activity.Call("finishAndRemoveTask");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Futebol de Botão] finishAndRemoveTask falhou: {e.Message}");
            }
            Application.Quit();
#else
            Application.Quit();
#endif
        }

        /// <summary>2 jogadores: cada um no seu dispositivo ou revezando o mesmo. Abre os Controles ou as Opções.</summary>
        public void PlayTwoPlayers()
        {
            tutorialFlow = false;
            MatchSession.VsAi = false;
            MatchSession.TournamentSetup = false;
            OpenDevices();
        }

        /// <summary>Controles, quando há mais de um dispositivo; senão o único fica escolhido e vai para as Opções.</summary>
        private void OpenDevices()
        {
            devicesShown = devices != null && devices.Open();
            if (devicesShown) Show(devices.gameObject);
            else if (tutorialFlow) StartTutorial();
            else ShowOptions();
        }

        /// <summary>Os Controles terminaram: segue para as Opções (ou abre o tutorial).</summary>
        public void ShowOptionsFromDevices()
        {
            if (tutorialFlow) StartTutorial();
            else ShowOptions();
        }

        /// <summary>
        /// "Tutorial" no Jogar (ou "Sim" na pergunta): Controles, se houver mais de um dispositivo, e a partida do
        /// tutorial na primeira lição que falta.
        /// </summary>
        public void PlayTutorial()
        {
            Tutorial.MarkAsked();
            MatchSession.VsAi = true;
            MatchSession.TournamentSetup = false;
            tutorialFlow = true;
            OpenDevices();
        }

        /// <summary>"Agora não" na pergunta do tutorial: não pergunta mais e mostra o Jogar.</summary>
        public void SkipTutorialAsk()
        {
            Tutorial.MarkAsked();
            Show(playPanel != null ? playPanel : modePanel);
        }

        public void SetTutorialAsk(GameObject panel) => tutorialAsk = panel;

        private void StartTutorial()
        {
            tutorialFlow = false;
            Tutorial.Prepare(Tutorial.FirstPending());
            Play();
        }

        /// <summary>"Voltar" nas Opções: volta aos Controles se eles apareceram, senão à escolha do modo.</summary>
        public void BackFromOptions()
        {
            if (devicesShown) OpenDevices();
            else ShowPlay();
        }

        /// <summary>"Jogar" na Seleção de times: põe os jogadores na sessão (com os dispositivos) e vai para a Partida.</summary>
        public void Play()
        {
            if (players != null) players.Prepare(RequestMatch);
            else RequestMatch();
        }

        public void SetDevices(DeviceSelectionScreen deviceSelection) => devices = deviceSelection;

        public void SetStats(StatsScreen statsScreen) => stats = statsScreen;

        public void SetPlayPanel(GameObject panel) => playPanel = panel;

        public void SetProfile(ProfileScreen profileScreen) => profile = profileScreen;

        public void SetShop(ShopScreen shopScreen) => shop = shopScreen;

        /// <summary>"Loja" na primeira tela.</summary>
        public void ShowShop()
        {
            if (shop == null) return;
            Show(shop.gameObject);
            shop.Open();
        }

        /// <summary>"Jogar" na primeira tela: a escolha entre Torneio, Partida única e Dois jogadores.</summary>
        public void ShowPlay()
        {
            MatchSession.TournamentSetup = false;
            // Primeira vez: pergunta se quer aprender a jogar.
            if (tutorialAsk != null && Tutorial.ShouldAsk)
            {
                Show(tutorialAsk);
                return;
            }
            Show(playPanel != null ? playPanel : modePanel);
        }

        /// <summary>"Meu time" na primeira tela.</summary>
        public void ShowProfile()
        {
            if (profile == null) return;
            Show(profile.gameObject);
            profile.Open();
        }

        /// <summary>"Estatísticas" na primeira tela do Menu.</summary>
        public void ShowStats()
        {
            if (stats == null) return;
            Show(stats.gameObject);
            stats.Open();
        }

        public void SetPlayers(MenuPlayers menuPlayers) => players = menuPlayers;

        public void SetSelection(TeamSelectionScreen teamSelection) => selection = teamSelection;

        /// <summary>"Avançar" nas Opções: abre a Seleção de times (sem ela, vai direto para a Partida).</summary>
        public void ShowSelection()
        {
            // Opções do torneio novo: sorteia a chave e mostra.
            if (MatchSession.TournamentSetup)
            {
                MatchSession.TournamentSetup = false;
                GameSave.Save("opcoes");
                Tournament.StartNew(TournamentConfig.Find(MatchSession.TournamentId), MatchSession.Options(defaults), MatchSession.AiLevel);
                ShowTournament();
                return;
            }
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
            if (modeLabel != null)
                modeLabel.text = MatchSession.TournamentSetup ? "Torneio"
                    : MatchSession.VsAi == true ? "Partida única" : "Dois jogadores";
            LayoutRows();
            Refresh();
        }

        /// <summary>"Como jogar": a primeira tela do Menu.</summary>
        public void ShowMode() => Show(modePanel);

        private void Show(GameObject panel)
        {
            foreach (var other in new[] { modePanel, playPanel, optionsPanel, selection != null ? selection.gameObject : null,
                         devices != null ? devices.gameObject : null, stats != null ? stats.gameObject : null,
                         profile != null ? profile.gameObject : null, shop != null ? shop.gameObject : null,
                         tournament != null ? tournament.gameObject : null, tutorialAsk })
                if (other != null) other.SetActive(other == panel);
        }

        /// <summary>A Dificuldade só aparece contra a IA; as linhas visíveis ficam uma embaixo da outra.</summary>
        private void LayoutRows()
        {
            bool vsAi = MatchSession.VsAi == true;
            bool tournamentSetup = MatchSession.TournamentSetup;
            int slot = 0;
            for (int i = 0; i < optionRows.Length; i++)
            {
                var row = optionRows[i];
                if (row == null) continue;
                var kind = (MatchOptionKind)i;
                // Dificuldade só contra a IA; Pênaltis só com goleiro.
                // Torneio: goleiro, prorrogação e pênaltis sempre ligados (não aparecem).
                bool shown = (vsAi || kind != MatchOptionKind.Difficulty) &&
                             (tournamentSetup || kind != MatchOptionKind.TournamentSize) &&
                             (kind != MatchOptionKind.Penalties || MatchSession.Options(defaults).hasGoalkeeper) &&
                             !(tournamentSetup && kind is MatchOptionKind.Goalkeeper or MatchOptionKind.ExtraTime or MatchOptionKind.Penalties);
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
                    MatchSession.AiLevel = (AiLevel)Wrap((int)MatchSession.AiLevel + step, AiDifficulty.LevelCount);
                    break;
                case MatchOptionKind.Discs:
                    options.discsPerTeam = options.discsPerTeam == 5 ? 3 : 5;
                    break;
                case MatchOptionKind.Goalkeeper:
                    options.hasGoalkeeper = !options.hasGoalkeeper;
                    LayoutRows();
                    break;
                case MatchOptionKind.ExtraTime:
                    int extra = System.Array.IndexOf(ExtraTimes, options.extraTimePercent);
                    if (extra < 0) extra = 0;
                    options.extraTimePercent = ExtraTimes[Wrap(extra + step, ExtraTimes.Length)];
                    break;
                case MatchOptionKind.Penalties:
                    options.penalties = !options.penalties;
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
                case MatchOptionKind.TournamentSize:
                    var sizes = TournamentConfig.All();
                    if (sizes.Length > 0)
                    {
                        int current = System.Array.IndexOf(sizes, TournamentConfig.Find(MatchSession.TournamentId));
                        MatchSession.TournamentId = sizes[Wrap(Mathf.Max(0, current) + step, sizes.Length)].id;
                    }
                    break;
                case MatchOptionKind.Music:
                    // A música grava sozinha.
                    MusicSetting.Enabled = !MusicSetting.Enabled;
                    Refresh();
                    return;
            }
            GameSave.Save("opcoes");
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
            MatchOptionKind.ExtraTime => "Prorrogação",
            MatchOptionKind.Penalties => "Pênaltis",
            MatchOptionKind.Music => "Música",
            MatchOptionKind.TournamentSize => "Times",
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
            MatchOptionKind.ExtraTime => options.extraTimePercent > 0 ? $"{options.extraTimePercent}%" : "Desligado",
            MatchOptionKind.Penalties => OnOff(options.penalties),
            MatchOptionKind.Music => OnOff(MusicSetting.Enabled),
            MatchOptionKind.TournamentSize => TournamentConfig.Find(MatchSession.TournamentId).TeamCount.ToString(),
            _ => string.Empty
        };

        public static string OnOff(bool value) => value ? "Ligado" : "Desligado";

        private static int Wrap(int value, int count) => ((value % count) + count) % count;
    }
}
