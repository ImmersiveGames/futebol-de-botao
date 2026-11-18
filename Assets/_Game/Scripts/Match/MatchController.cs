using System.Collections.Generic;
using Immersive.Framework.ActivityFlow;
using Immersive.Framework.GameFlow;
using Immersive.Framework.Pause;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FutebolDeBotao
{
    /// <summary>
    /// Máquina de estados da partida: Saída, Mira, Pro Gol!, Tiro de meta, Movimento, Gol e Fim.
    /// Cuida da vez, dos toques, da perda da vez, da falta e do pênalti, do relógio e do placar (o HUD é o MatchHud).
    /// A partida começa quando a Activity "Jogo" do framework entra (e recomeça a cada Activity Restart).
    /// </summary>
    public sealed partial class MatchController : MonoBehaviour, IActivityContentLifecycleReceiver
    {
        [Tooltip("Opções padrão. O Menu edita uma cópia delas (MatchSession); este asset não muda.")]
        [SerializeField] private MatchOptions options;
        [SerializeField] private float goalPauseSeconds = 1.5f;
        [Tooltip("Tempo mostrando o placar final antes de ir para a tela de Resultado.")]
        [SerializeField] private float endPauseSeconds = 2.5f;

        [Header("Framework")]
        [Tooltip("Route Request Trigger que leva à tela de Resultado no fim do tempo.")]
        [SerializeField] private RouteRequestTrigger resultRoute;
        [Tooltip("Pause Request Trigger usado pelo botão Pausa e por Esc/Start.")]
        [SerializeField] private PauseRequestTrigger pauseTrigger;
        [Header("IA")]
        [Tooltip("Só quando a partida abre direto, sem passar pelo Menu (teste no editor): o time de cima joga pela IA. " +
                 "Pelo Menu vale o modo escolhido lá.")]
        [SerializeField] private bool aiPlaysTop = true;
        [Tooltip("Nível da IA quando a partida abre direto, sem passar pelo Menu. Vazio usa o Médio.")]
        [SerializeField] private AiDifficulty aiDifficulty;

        private readonly Dictionary<TeamSide, List<Disc>> discs = new();
        private readonly int[] score = new int[2];
        /// <summary>Faltas longe do lance de cada jogador (1 = amarelo; a partir de 2, cada uma é vermelho).</summary>
        private readonly int[] farFouls = new int[2];
        private Vector2 shotBallStart;
        /// <summary>A bola estava no campo de ataque de quem jogou no começo da jogada (sem goleiro, só assim o gol vale).</summary>
        private bool shotFromAttackHalf;
        private Ball ball;
        private Goalkeeper[] keepers;
        private AimController aim;
        private AimVisuals aimVisuals;
        private MotionMonitor monitor;
        private GoalkeeperControl keeperControl;
        private AiPlayer ai;
        private readonly List<Disc> allDiscs = new();

        private float clock;
        private float stateTimer;
        private int touchesLeft;
        private bool shotWasCalled;
        private Disc shooter;
        private Disc freeKickDisc;
        private bool setupIsPenalty;
        // Pênalti com goleiro: cada lado escolhe o canto sem ver o outro. Quem escolheu primeiro fica debaixo da faixa
        // preta até o outro dar Pronto. Contra a IA ela sempre escolhe primeiro (com o jogador batendo, o goleiro dela).
        private bool kickerHidden;
        private bool keeperHidden;
        private bool penaltyKeeperFirst;
        private SpriteRenderer penaltyCover;
        private const int PenaltyCoverOrder = 90;
        private const float PenaltyCoverMargin = 0.25f;
        private Camera worldCamera;
        private GoalTrigger pendingGoal;
        private bool pendingGoalValid;
        /// <summary>Números da partida por lado (0 = baixo, 1 = cima), para as estatísticas.</summary>
        private readonly SaveData.MatchNumbers[] numbers = { new(), new() };
        /// <summary>Peteleco em andamento ainda não contado como toque na bola ou toque errado.</summary>
        private bool touchPending;
        private TeamSide touchSide;
        private string messageText = string.Empty;
        /// <summary>Mensagem do meio do campo; cada troca conta como aviso novo (mesmo texto repetido aparece de novo).</summary>
        private string message
        {
            get => messageText;
            set
            {
                messageText = value ?? string.Empty;
                MessageVersion++;
            }
        }
        private bool initialized;
        /// <summary>Partida (fora do tutorial) começou e ainda não terminou: sair agora conta como abandono no Analytics.</summary>
        private bool analyticsMatchOpen;
        private float analyticsStartTime;
        private bool resultRequested;
        private float holdTimer;
        private bool restoreControls;
        private TutorialDirector tutorial;

        public MatchState State { get; private set; }
        /// <summary>Time que marcou o último gol (do aviso <see cref="MatchNotice.Goal"/>), contando gol contra e pênalti da disputa.</summary>
        public TeamSide LastScorer { get; private set; }
        public TeamSide Turn { get; private set; }
        public int TouchesLeft => touchesLeft;
        public float ClockSeconds => clock;
        public int Score(TeamSide side) => score[(int)side];
        /// <summary>O jogador de <paramref name="side"/> já levou amarelo.</summary>
        public bool HasYellow(TeamSide side) => farFouls[(int)side] >= 1;
        /// <summary>Vermelhos que o jogador de <paramref name="side"/> levou.</summary>
        public int RedCards(TeamSide side) => Mathf.Max(0, farFouls[(int)side] - 1);
        /// <summary>Lances que têm som (apito, gol, cartão...). Quem toca é o <c>MatchSounds</c>.</summary>
        public event System.Action<MatchNotice> Noticed;

        /// <summary>Última mensagem da partida (falta, gol, vez...). O HUD mostra quando ela muda.</summary>
        public string Message => messageText;
        public int MessageVersion { get; private set; }
        /// <summary>Segundos que restam no timer da etapa atual (mira, goleiro, pênalti).</summary>
        public float StateSecondsLeft => Mathf.Max(0f, stateTimer);
        /// <summary>Timer de mira correndo (Mira, Pro Gol! ou Tiro de meta).</summary>
        public bool IsAimTimerRunning => ClockRunning;
        /// <summary>Quem age agora: o atacante na mira e no pênalti, o defensor ajustando o goleiro.</summary>
        public TeamSide ActingSide => State == MatchState.ShotCall ? Turn.Opponent() : Turn;
        /// <summary>Há um "Pronto" esperando (goleiro no Pro Gol! ou batedor no pênalti).</summary>
        public bool CanConfirmReady => !IsHolding && (State is (MatchState.ShotCall or MatchState.PenaltySetup) || IsPlacingGoalKick);
        /// <summary>Tiro de meta, primeiro passo: o time arrasta a bola para os lados e aperta "Pronto" antes de mirar.</summary>
        public bool IsPlacingGoalKick => State == MatchState.GoalKick && goalKickPlacing;
        /// <summary>Um aviso importante está na tela: controles travados, relógio e timer parados, a IA espera.</summary>
        public bool IsHolding => holdTimer > 0f;
        public PauseRequestTrigger PauseTrigger => pauseTrigger;
        /// <summary>Quem conduz as lições quando a partida é o tutorial; nulo numa partida normal.</summary>
        public TutorialDirector Tutorial => tutorial;
        // Tutorial explicando (balão com Continuar) ou com a lição concluída na tela: ninguém joga.
        private bool TutorialLocked => tutorial != null && tutorial.LocksControls;
        /// <summary>A jogada em andamento (ou a última) foi um chute anunciado.</summary>
        public bool ShotWasCalled => shotWasCalled;
        public MatchOptions Options => options;
        /// <summary>Muda a cada troca de estado, mesmo para o mesmo estado (a IA usa para saber se a jogada dela ainda vale).</summary>
        public int StateVersion { get; private set; }
        /// <summary>Todos os botões da mesa, inclusive os desligados no modo de 3 botões.</summary>
        public IReadOnlyList<Disc> AllDiscs => allDiscs;
        public IReadOnlyList<Goalkeeper> Keepers => keepers;
        public Ball Ball => ball;
        /// <summary>Este time é jogado pela IA.</summary>
        public bool IsAi(TeamSide side) => ai != null && ai.Side == side;
        /// <summary>Botão do tiro livre ou do pênalti (só ele pode jogar). Nulo fora disso.</summary>
        public Disc FreeKickDisc => freeKickDisc;
        /// <summary>No posicionamento do batedor: true = pênalti (arco atrás da bola), false = tiro livre (volta inteira).</summary>
        public bool IsPenaltySetup => setupIsPenalty;
        /// <summary>Cobrança de pênalti em andamento (de falta ou da disputa): do posicionamento até o chute.</summary>
        public bool IsPenaltyKick => setupIsPenalty && freeKickDisc != null;
        /// <summary>O batedor do pênalti está escondido pela faixa preta (o goleiro ainda escolhe o canto).</summary>
        public bool KickerHidden => kickerHidden;
        /// <summary>O goleiro do pênalti está escondido pela faixa preta (o batedor ainda escolhe o lado).</summary>
        public bool KeeperHidden => keeperHidden;
        // Pênalti com goleiro: os dois escolhem um lado escondidos.
        private bool HiddenPenalty => IsPenaltyKick && options.hasGoalkeeper;

        private bool goalKickPlacing;
        private bool draggingGoalKickBall;

        // ---- Prorrogação e pênaltis ----

        private enum Period { Regular, ExtraTime, Shootout }

        /// <summary>Cobranças de cada lado na disputa antes da morte súbita.</summary>
        public const int ShootoutRounds = 5;

        private Period period;
        /// <summary>Resultado de cada cobrança da disputa, por lado (true = gol).</summary>
        private readonly List<bool>[] shootoutKicks = { new(), new() };
        /// <summary>Botões de cada lado que batem na disputa (os que estavam em campo), em revezamento.</summary>
        private readonly List<Disc>[] shootoutTakers = { new(), new() };
        private readonly int[] shootoutNext = new int[2];
        private TeamSide shootoutFirst;

        /// <summary>A partida está na prorrogação.</summary>
        public bool IsExtraTime => period == Period.ExtraTime;
        /// <summary>A partida está na disputa de pênaltis (continua true no Fim depois dela).</summary>
        public bool IsShootout => period == Period.Shootout;
        /// <summary>Cobranças do lado na disputa, em ordem (true = gol).</summary>
        public IReadOnlyList<bool> ShootoutKicks(TeamSide side) => shootoutKicks[(int)side];
        /// <summary>Gols do lado na disputa.</summary>
        public int ShootoutGoals(TeamSide side) => shootoutKicks[(int)side].FindAll(scored => scored).Count;
        /// <summary>Já passaram as 5 cobranças de cada lado e segue empatado.</summary>
        public bool IsSuddenDeath => IsShootout && Mathf.Min(shootoutKicks[0].Count, shootoutKicks[1].Count) >= ShootoutRounds;

        public void Configure(MatchOptions matchOptions) => options = matchOptions;

        public void ConfigureFramework(RouteRequestTrigger result, PauseRequestTrigger pause)
        {
            resultRoute = result;
            pauseTrigger = pause;
        }

        private static bool Paused => Time.timeScale <= 0f;

        private void Start() => EnsureInitialized();

        private void OnEnable() => MobileSettings.KeepScreenOn(true);

        private void OnDisable() => MobileSettings.KeepScreenOn(false);

        private void Update()
        {
            // Esc/Start de cada jogador, no próprio dispositivo: quem pausou mexe no menu e só ele tira a pausa por aqui.
            // Reiniciar fica só no menu de pausa.
            if (pauseTrigger != null && TryGetPausePress(out int pressedBy))
            {
                if (!Paused)
                {
                    DeviceChoice.PausedBy = pressedBy;
                    pauseTrigger.RequestPause();
                }
                else if (DeviceChoice.PausedBy == 0 || pressedBy == 0 || pressedBy == DeviceChoice.PausedBy)
                    pauseTrigger.RequestResume();
            }
            if (Paused) return;

            if (IsHolding)
            {
                UpdateHold();
                return;
            }

            // Um dedo já apertado durante o aviso não vira mira: os controles voltam no quadro seguinte.
            if (restoreControls)
            {
                restoreControls = false;
                ApplyControls();
            }

            if (ClockRunning) clock = Mathf.Max(0f, clock - Time.deltaTime);
            stateTimer -= Time.deltaTime;

            // Tutorial com o pop-up aberto (ou fechado neste quadro, pelo mesmo E/A): ninguém posiciona nem confirma.
            if (tutorial != null && (TutorialLocked || tutorial.UnlockedThisFrame) && State is not (MatchState.Goal or MatchState.End))
                return;

            switch (State)
            {
                case MatchState.Aim:
                    if (clock <= 0f) EndMatch();
                    else if (stateTimer <= 0f) PassTurn("Tempo esgotado!");
                    else if (!IsAi(Turn) && MatchInput.For(Turn).ShotCallPressedThisFrame) TryCallShot();
                    break;

                case MatchState.ShotAim:
                    if (stateTimer <= 0f) PassTurn("Tempo esgotado!");
                    break;

                case MatchState.GoalKick:
                    if (stateTimer <= 0f) PassTurn("Tempo esgotado!");
                    else if (goalKickPlacing && !IsAi(Turn))
                    {
                        UpdateGoalKickPlacement();
                        if (MatchInput.For(Turn).SelectPressedThisFrame) FinishGoalKickPlacement();
                    }
                    break;

                case MatchState.ShotCall:
                    bool ready = !IsAi(Turn.Opponent()) && MatchInput.For(Turn.Opponent()).SelectPressedThisFrame;
                    if (stateTimer <= 0f || ready) FinishShotCall();
                    break;

                case MatchState.PenaltySetup:
                    UpdatePenaltySetup();
                    bool placed = !IsAi(Turn) && MatchInput.For(Turn).SelectPressedThisFrame;
                    if (stateTimer <= 0f || placed) FinishPenaltySetup();
                    break;

                case MatchState.Goal:
                    if (stateTimer <= 0f) ResolveGoal();
                    break;

                case MatchState.End:
                    if (stateTimer <= 0f && !resultRequested) RequestResult();
                    break;
            }
        }

        /// <summary>Esc, Start ou o Voltar do Android apertado agora num dispositivo de jogador; <paramref name="player"/> = dono (0 = qualquer um).</summary>
        private static bool TryGetPausePress(out int player)
        {
            player = 0;
            // Voltar do Android pausa; com a pausa aberta quem usa é o menu de pausa (Continuar).
            if (!Paused && AndroidBack.TryUse()) return true;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && TryOwner(keyboard, out player)) return true;
            foreach (var gamepad in Gamepad.all)
                if (gamepad.startButton.wasPressedThisFrame && TryOwner(gamepad, out player)) return true;
            return false;
        }

        // Dispositivo que ninguém escolheu na tela Controles não pausa.
        private static bool TryOwner(InputDevice device, out int player)
        {
            player = DeviceChoice.OwnerOf(device);
            return player >= 0;
        }

        // GDD: o relógio corre só durante a mira.
        // No tutorial não tem relógio.
        private bool ClockRunning => tutorial == null && State is MatchState.Aim or MatchState.ShotAim or MatchState.GoalKick;

        private void Enter(MatchState state)
        {
            State = state;
            StateVersion++;

            aim.Cancel();
            ApplyControls();

            // A faixa do pênalti só existe enquanto alguém escolhe o canto.
            if (state is not (MatchState.PenaltySetup or MatchState.ShotCall)) kickerHidden = keeperHidden = false;
            UpdatePenaltyCover();

            stateTimer = state switch
            {
                MatchState.Aim or MatchState.ShotAim or MatchState.GoalKick => options.aimTimeSeconds,
                MatchState.ShotCall => options.shotCallSeconds,
                MatchState.PenaltySetup => options.penaltySetupSeconds,
                MatchState.Goal => goalPauseSeconds,
                MatchState.End => endPauseSeconds,
                _ => 0f
            };
            // No tutorial ninguém tem pressa: sem timer de mira, de goleiro nem de batedor.
            if (tutorial != null && state is MatchState.Aim or MatchState.ShotAim or MatchState.GoalKick or MatchState.ShotCall
                    or MatchState.PenaltySetup)
                stateTimer = float.PositiveInfinity;
        }

        /// <summary>Liga a mira e o goleiro de quem age no estado atual (nada durante um aviso).</summary>
        private void ApplyControls()
        {
            var state = State;
            bool holding = IsHolding || restoreControls || TutorialLocked;
            // Na vez da IA o ponteiro não mira.
            aim.InputEnabled = !holding && (state is MatchState.Aim or MatchState.ShotAim || state == MatchState.GoalKick && !goalKickPlacing) && !IsAi(Turn);
            aim.InputSide = Turn;
            aim.BallKickMode = state == MatchState.GoalKick;
            aim.CanSelect = CanUse;

            keeperControl.ControlledSide = !holding && state == MatchState.ShotCall && !IsAi(Turn.Opponent()) ? Turn.Opponent() : null;
        }

        // ---- Avisos ----

        /// <summary>Aviso importante: para o jogo por alguns segundos para todo mundo ler. A mensagem atual é o aviso.</summary>
        private void Hold()
        {
            if (options.noticeHoldSeconds <= 0f) return;
            holdTimer = options.noticeHoldSeconds;
            aim.Cancel();
            ApplyControls();
        }

        private void UpdateHold()
        {
            holdTimer -= Time.deltaTime;
            if (holdTimer > 0f) return;
            holdTimer = 0f;
            restoreControls = true;
        }

        // ---- Utilidades ----

        private Goalkeeper FindKeeper(TeamSide side)
        {
            foreach (var keeper in keepers)
                if (keeper.isActiveAndEnabled && keeper.Side == side) return keeper;
            return null;
        }


        public static string TeamName(TeamSide side) => MatchSession.Team(side).TeamName;

        /// <summary>Posição inicial do botão <paramref name="index"/> no esquema escolhido na Seleção de times.</summary>
        private Vector2 FormationPosition(int index, TeamSide side)
        {
            var scheme = MatchSession.Scheme(side, options.discsPerTeam);
            if (scheme != null && index < scheme.Count) return scheme.PositionFor(index, side);
            return options.FormationPosition(index, side);
        }

        /// <summary>
        /// Pinta botões e goleiros com as cores e camisas escolhidas na Seleção de times. Sem as camadas do "Aplicar artes",
        /// pinta os círculos do protótipo (botão na cor principal, anel na segunda cor).
        /// </summary>
        private void PaintTeams()
        {
            foreach (var pair in discs)
            {
                var primary = MatchSession.PrimaryColor(pair.Key);
                var secondary = MatchSession.SecondaryColor(pair.Key);
                foreach (var disc in pair.Value)
                {
                    var art = disc.GetComponentInChildren<PieceArt>(true);
                    if (art != null)
                    {
                        art.Paint(primary, secondary, MatchSession.DiscPattern(pair.Key));
                        continue;
                    }
                    if (disc.TryGetComponent(out SpriteRenderer body)) body.color = primary;
                    var ring = disc.transform.Find("Anel");
                    if (ring != null && ring.TryGetComponent(out SpriteRenderer ringRenderer)) ringRenderer.color = secondary;
                    var center = disc.transform.Find("Centro");
                    if (center != null && center.TryGetComponent(out SpriteRenderer centerRenderer)) centerRenderer.color = primary;
                }
            }

            foreach (var keeper in keepers)
            {
                var primary = MatchSession.PrimaryColor(keeper.Side);
                var art = keeper.GetComponentInChildren<PieceArt>(true);
                if (art != null) art.Paint(primary, MatchSession.SecondaryColor(keeper.Side), MatchSession.KeeperPattern(keeper.Side));
                else if (keeper.TryGetComponent(out SpriteRenderer renderer))
                    renderer.color = Color.Lerp(primary, Color.black, 0.25f);
            }
        }
    }
}
