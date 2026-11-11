using System.Collections.Generic;
using Immersive.Framework.ActivityFlow;
using Immersive.Framework.GameFlow;
using Immersive.Framework.Pause;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FutebolDeBotao
{
    /// <summary>
    /// Máquina de estados da partida: Saída, Mira, Vai chutar, Tiro de meta, Movimento, Gol e Fim.
    /// Cuida da vez, dos toques, da perda da vez, da falta e do pênalti, do relógio, do placar e do HUD provisório.
    /// A partida começa quando a Activity "Jogo" do framework entra (e recomeça a cada Activity Restart).
    /// </summary>
    public sealed class MatchController : MonoBehaviour, IActivityContentLifecycleReceiver
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

        [Tooltip("Multiplica o tamanho do HUD provisório.")]
        [SerializeField, Range(0.5f, 2f)] private float hudScale = 1f;

        private const float ReferenceHeight = 720f;
        private const float ReferenceMinWidth = 400f;

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
        private string message = string.Empty;
        private bool initialized;
        private bool resultRequested;
        private float holdTimer;
        private bool restoreControls;
        private TutorialDirector tutorial;

        public MatchState State { get; private set; }
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
        public string Message => message;
        /// <summary>Segundos que restam no timer da etapa atual (mira, goleiro, pênalti).</summary>
        public float StateSecondsLeft => Mathf.Max(0f, stateTimer);
        /// <summary>Timer de mira correndo (Mira, Vai chutar ou Tiro de meta).</summary>
        public bool IsAimTimerRunning => ClockRunning;
        /// <summary>Quem age agora: o atacante na mira e no pênalti, o defensor ajustando o goleiro.</summary>
        public TeamSide ActingSide => State == MatchState.ShotCall ? Opponent(Turn) : Turn;
        /// <summary>Há um "Pronto" esperando (goleiro no Vai chutar ou batedor no pênalti).</summary>
        public bool CanConfirmReady => !IsHolding && (State is (MatchState.ShotCall or MatchState.PenaltySetup) || IsPlacingGoalKick);
        /// <summary>Tiro de meta, primeiro passo: o time arrasta a bola para os lados e aperta "Pronto" antes de mirar.</summary>
        public bool IsPlacingGoalKick => State == MatchState.GoalKick && goalKickPlacing;
        /// <summary>Um aviso importante está na tela: controles travados, relógio e timer parados, a IA espera.</summary>
        public bool IsHolding => holdTimer > 0f;
        /// <summary>Com um HUD de verdade na cena, o HUD provisório (OnGUI) não é desenhado.</summary>
        public bool ExternalHud { get; set; }
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

        // ---- Activity do framework ----

        public void OnActivityContentEntered(ActivityContentLifecycleContext context)
        {
            EnsureInitialized();
            StartMatch();
        }

        public void OnActivityContentExited(ActivityContentLifecycleContext context)
        {
            if (!initialized) return;
            // Saiu antes do fim (menu ou reiniciar): partida abandonada. Terminada, já foi gravada e não conta de novo.
            PlayerStats.Abandon();
            EndShot();
            monitor.StopWatching();
            monitor.FreezeAll();
            message = string.Empty;
            Enter(MatchState.Waiting);
        }

        private void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;

            ball = FindAnyObjectByType<Ball>();
            keepers = FindObjectsByType<Goalkeeper>();
            aim = FindAnyObjectByType<AimController>();
            aimVisuals = FindAnyObjectByType<AimVisuals>();
            monitor = FindAnyObjectByType<MotionMonitor>();
            keeperControl = FindAnyObjectByType<GoalkeeperControl>();
            if (keeperControl == null) keeperControl = gameObject.AddComponent<GoalkeeperControl>();

            CollectDiscs();

            aim.Flicked += OnFlicked;
            aim.BallKicked += OnBallKicked;
            monitor.Settled += OnSettled;
            foreach (var goal in FindObjectsByType<GoalTrigger>()) goal.BallEntered += OnBallEntered;
            foreach (var list in discs.Values)
                foreach (var disc in list)
                {
                    disc.Fouled += OnFouled;
                    disc.HitOpponent += OnHitOpponent;
                    disc.HitOpponentKeeper += OnHitOpponentKeeper;
                }

            Enter(MatchState.Waiting);
        }

        private void CollectDiscs()
        {
            discs[TeamSide.Bottom] = new List<Disc>();
            discs[TeamSide.Top] = new List<Disc>();
            foreach (var disc in FindObjectsByType<Disc>()) discs[disc.Side].Add(disc);
            foreach (var list in discs.Values) list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            allDiscs.Clear();
            allDiscs.AddRange(discs[TeamSide.Bottom]);
            allDiscs.AddRange(discs[TeamSide.Top]);
        }

        private void ApplyOptions()
        {
            foreach (var list in discs.Values)
                for (int i = 0; i < list.Count; i++)
                    list[i].gameObject.SetActive(i < options.discsPerTeam);

            foreach (var keeper in keepers) keeper.gameObject.SetActive(options.hasGoalkeeper);
            ApplyGoalWidth();
            if (options.hasGoalkeeper)
                foreach (var keeper in keepers) keeper.SetWidth(options.keeperWidth, GoalWidth);
            if (aimVisuals != null) aimVisuals.Mode = options.aimAssist;
            PaintTeams();
        }

        /// <summary>Largura da boca do gol nesta partida (menor sem goleiro).</summary>
        public float GoalWidth { get; private set; } = FieldLayout.GoalWidth;

        private readonly List<GameObject> goalPosts = new();

        /// <summary>
        /// Sem goleiro o gol fica menor: dois blocos de parede, um de cada lado, da linha de fundo até a rede de trás
        /// (copiados da parede do fundo).
        /// </summary>
        private void ApplyGoalWidth()
        {
            GoalWidth = options.hasGoalkeeper
                ? FieldLayout.GoalWidth
                : Mathf.Clamp(options.goalWidthWithoutKeeper, 0.6f, FieldLayout.GoalWidth);
            float post = (FieldLayout.GoalWidth - GoalWidth) * 0.5f;

            if (goalPosts.Count == 0 && post > 0.001f)
            {
                foreach (var wall in FindObjectsByType<Wall>())
                {
                    // As paredes do fundo, ao lado do gol: viram o molde das traves daquele lado do campo.
                    var position = wall.transform.position;
                    if (!wall.PushesBack || Mathf.Abs(position.y) < FieldLayout.HalfHeight || Mathf.Abs(position.x) > FieldLayout.HalfWidth) continue;
                    if (position.x > 0f) continue;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var copy = Instantiate(wall.gameObject, wall.transform.parent);
                        copy.name = $"Trave sem goleiro {(position.y < 0f ? "baixo" : "cima")} {(side < 0f ? "esquerda" : "direita")}";
                        copy.SetActive(false);
                        copy.transform.position = new Vector3(side, Mathf.Sign(position.y), position.z);
                        goalPosts.Add(copy);
                    }
                }
            }

            foreach (var copy in goalPosts)
            {
                var position = copy.transform.position;
                float side = Mathf.Sign(position.x);
                float end = Mathf.Sign(position.y);
                copy.transform.position = new Vector3(side * (GoalWidth * 0.5f + post * 0.5f),
                    end * (FieldLayout.HalfHeight + FieldLayout.GoalDepth * 0.5f), position.z);
                var scale = copy.transform.localScale;
                copy.transform.localScale = new Vector3(Mathf.Max(post, 0.001f), FieldLayout.GoalDepth, scale.z);
                copy.SetActive(post > 0.001f);
            }

            var repulsion = FindAnyObjectByType<WallRepulsion>();
            if (repulsion != null) repulsion.RefreshWalls();

            var art = FindAnyObjectByType<TableArt>();
            if (art != null) art.SetSmallGoals(!options.hasGoalkeeper);
        }

        public void StartMatch()
        {
            options = MatchSession.Options(options);
            ApplyOptions();
            ConfigureAi();
            resultRequested = false;
            holdTimer = 0f;
            restoreControls = false;
            score[0] = score[1] = 0;
            farFouls[0] = farFouls[1] = 0;
            numbers[0] = new SaveData.MatchNumbers();
            numbers[1] = new SaveData.MatchNumbers();
            touchPending = false;
            period = Period.Regular;
            foreach (var list in shootoutKicks) list.Clear();
            clock = options.DurationSeconds;
            ConfigureTutorial();
            if (tutorial != null)
            {
                // Tutorial: sem relógio e sem estatísticas; a lição monta a jogada.
                tutorial.Begin();
                return;
            }
            PlayerStats.BeginMatch();
            // Início: time sorteado dá a saída.
            KickOff(Random.value < 0.5f ? TeamSide.Bottom : TeamSide.Top);
        }

        /// <summary>Partida do tutorial (aberta pelo Menu): liga o <see cref="TutorialDirector"/>; senão tira.</summary>
        private void ConfigureTutorial()
        {
            if (!FutebolDeBotao.Tutorial.Active)
            {
                if (tutorial != null) Destroy(tutorial);
                tutorial = null;
                return;
            }
            if (tutorial == null) tutorial = gameObject.AddComponent<TutorialDirector>();
            tutorial.Configure(this);
        }

        /// <summary>Contra a IA, o time de cima joga sozinho no nível do Menu; em 2 jogadores, ninguém.</summary>
        private void ConfigureAi()
        {
            bool fromMenu = MatchSession.VsAi.HasValue;
            bool vsAi = MatchSession.VsAi ?? aiPlaysTop;
            if (!vsAi)
            {
                if (ai != null) Destroy(ai);
                ai = null;
                return;
            }

            // No tutorial a IA é sempre a do Fácil.
            var difficulty = FutebolDeBotao.Tutorial.Active ? AiDifficulty.Load(AiLevel.Easy)
                : fromMenu || aiDifficulty == null ? AiDifficulty.Load(MatchSession.AiLevel) : aiDifficulty;
            if (ai == null) ai = gameObject.AddComponent<AiPlayer>();
            ai.Configure(this, TeamSide.Top, difficulty);
        }

        private void KickOff(TeamSide side)
        {
            Enter(MatchState.KickOff);
            EndShot();
            monitor.StopWatching();

            foreach (var pair in discs)
                for (int i = 0; i < pair.Value.Count && i < options.discsPerTeam; i++)
                    pair.Value[i].PlaceAt(FormationPosition(i, pair.Key));
            foreach (var keeper in keepers)
                if (keeper.isActiveAndEnabled) keeper.ResetToCenter();
            ball.ResetTo(Vector2.zero);
            KeepOutOfCenterCircle(Opponent(side));

            GiveTurn(side);
            message = Txt.Format("Saída do {0}.", TeamName(side));
            Noticed?.Invoke(MatchNotice.KickOff);
            Enter(MatchState.Aim);
        }

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
                    else if (stateTimer <= 0f) PassTurn("Tempo de mira esgotado.");
                    else if (!IsAi(Turn) && MatchInput.For(Turn).ShotCallPressedThisFrame) TryCallShot();
                    break;

                case MatchState.ShotAim:
                    if (stateTimer <= 0f) PassTurn("Tempo de mira esgotado.");
                    break;

                case MatchState.GoalKick:
                    if (stateTimer <= 0f) PassTurn("Tempo de mira esgotado.");
                    else if (goalKickPlacing && !IsAi(Turn))
                    {
                        UpdateGoalKickPlacement();
                        if (MatchInput.For(Turn).SelectPressedThisFrame) FinishGoalKickPlacement();
                    }
                    break;

                case MatchState.ShotCall:
                    bool ready = !IsAi(Opponent(Turn)) && MatchInput.For(Opponent(Turn)).SelectPressedThisFrame;
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

            keeperControl.ControlledSide = !holding && state == MatchState.ShotCall && !IsAi(Opponent(Turn)) ? Opponent(Turn) : null;
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

        // ---- Vai chutar ----

        public bool CanCallShot
        {
            get
            {
                // Sem goleiro não existe "Vai chutar": entrou, é gol.
                // Disponível quando a bola está no campo de ataque do time da vez; qualquer botão pode chutar.
                return !IsHolding && !TutorialLocked && State == MatchState.Aim && !aim.IsAiming && options.hasGoalkeeper &&
                       InAttackHalf(ball.Body.position, Turn);
            }
        }

        /// <summary>"Pronto": encerra o ajuste do goleiro ou o posicionamento do batedor do pênalti.</summary>
        public void ConfirmReady()
        {
            if (IsHolding || TutorialLocked) return;
            if (State == MatchState.ShotCall) FinishShotCall();
            else if (State == MatchState.PenaltySetup) FinishPenaltySetup();
            else if (IsPlacingGoalKick) FinishGoalKickPlacement();
        }

        /// <summary>Botão de pausa do HUD: o menu fica com o dono do mouse/toque que clicou.</summary>
        public void RequestPause()
        {
            DeviceChoice.PausedBy = DeviceChoice.PointerOwner(Pointer.current);
            if (pauseTrigger != null) pauseTrigger.RequestPause();
        }

        public void TryCallShot()
        {
            if (!CanCallShot) return;
            message = Txt.Format("{0}: Pro Gol!", TeamName(Turn));
            BeginShotCall();
        }

        /// <summary>
        /// Chute anunciado (falta no ataque, pênalti ou "Vai chutar"): o defensor ganha os segundos para ajustar o goleiro.
        /// Sem goleiro vira um chute normal de quem vai bater.
        /// </summary>
        private void BeginShotCall()
        {
            if (!options.hasGoalkeeper)
            {
                Enter(MatchState.Aim);
                return;
            }

            message += " " + Txt.Format("{0} ajusta o goleiro.", TeamName(Opponent(Turn)));
            Enter(MatchState.ShotCall);
        }

        /// <summary>
        /// Goleiro pronto: com a bola encostada nele ou atrás dele (área do goleiro), é bola do goleiro, porque o atacante
        /// só chegaria nela fazendo falta no goleiro. Senão, o atacante chuta.
        /// </summary>
        private void FinishShotCall()
        {
            if (HiddenPenalty)
            {
                FinishPenaltyKeeper();
                return;
            }
            if (TryKeeperZoneGoalKick()) return;
            EnterShotAim();
        }

        private void EnterShotAim()
        {
            message = Txt.Format("{0}: chute!", TeamName(Turn));
            Enter(MatchState.ShotAim);
        }

        /// <summary>
        /// Um gol nesta jogada valeria: num chute anunciado ou, sem goleiro, com a bola no campo de ataque do time da vez.
        /// </summary>
        public bool GoalWouldCount => State == MatchState.ShotAim ||
                                      (!options.hasGoalkeeper && InAttackHalf(ball.Body.position, Turn));

        private static bool InAttackHalf(Vector2 position, TeamSide side) =>
            side == TeamSide.Bottom ? position.y > 0f : position.y < 0f;

        /// <summary>Botão do time da vez; no tiro livre, só quem sofreu a falta.</summary>
        public bool CanUse(Disc disc)
        {
            if (disc == null || !disc.isActiveAndEnabled || disc.Side != Turn) return false;
            return freeKickDisc == null || disc == freeKickDisc;
        }

        // ---- Movimento ----

        private void OnBallKicked(Vector2 impulse)
        {
            if (State != MatchState.GoalKick) return;
            ball.MarkTouchedBy(Turn);
            Count(Turn).touches++;
            shotWasCalled = false;
            shotFromAttackHalf = InAttackHalf(ball.Body.position, Turn);
            shooter = null;
            touchesLeft--;
            message = string.Empty;
            Enter(MatchState.Moving);
        }

        private void OnFlicked(Disc disc, Vector2 impulse)
        {
            if (State != MatchState.Aim && State != MatchState.ShotAim) return;
            shotWasCalled = State == MatchState.ShotAim;
            if (shotWasCalled) Count(Turn).shots++;
            touchPending = true;
            touchSide = Turn;
            shooter = disc;
            shotBallStart = ball.Body.position;
            shotFromAttackHalf = InAttackHalf(shotBallStart, Turn);
            shooter.BeginShot();
            freeKickDisc = null;
            touchesLeft--;
            message = string.Empty;
            Enter(MatchState.Moving);
        }

        private void OnSettled()
        {
            if (State != MatchState.Moving) return;
            EndShot();

            // Disputa de pênaltis: a bola parou sem entrar.
            if (IsShootout)
            {
                monitor.FreezeAll();
                RecordShootoutKick(false);
                return;
            }
            if (tutorial != null)
            {
                // Tutorial: a lição decide se acertou, continua ou repete.
                if (TryKeeperZoneGoalKick()) return;
                tutorial.OnPlayEnded(ball.LastTouchSide == null ? TutorialPlay.Missed
                    : ball.LastTouchSide != Turn ? TutorialPlay.OpponentLast
                    : TutorialPlay.Touched);
                return;
            }
            if (clock <= 0f) EndMatch();
            else if (TryKeeperZoneGoalKick()) return;
            else if (ball.LastTouchSide == null) PassTurn("Errou a bola.");
            else if (ball.LastTouchSide != Turn) PassTurn(shotWasCalled ? "Chute defendido." : "Último toque do adversário.");
            else if (shotWasCalled) PassTurn("Chute para fora.");
            else if (touchesLeft > 0) Enter(MatchState.Aim);
            else PassTurn("Acabaram os toques.");
        }

        /// <summary>Bola do goleiro (parada na área em volta dele) vira tiro de meta para o time dele, não importa quem jogou.</summary>
        private bool TryKeeperZoneGoalKick()
        {
            if (IsShootout) return false;
            var owner = KeeperBall.Owner(ball.Body.position, ball.Radius, keepers, options.hasGoalkeeper);
            if (owner == null) return false;
            if (tutorial != null)
            {
                monitor.FreezeAll();
                Enter(MatchState.Moving);
                tutorial.OnPlayEnded(TutorialPlay.KeeperBall);
                return true;
            }
            message = Txt.Get("Bola do goleiro.");
            SetupGoalKick(owner.Value);
            Hold();
            return true;
        }

        private void EndShot()
        {
            if (shooter != null) shooter.EndShot();
            shooter = null;
            CountTouch();
        }

        private SaveData.MatchNumbers Count(TeamSide side) => numbers[(int)side];

        /// <summary>Fim do peteleco: algum botão do time tocou na bola (toque) ou nenhum (toque errado).</summary>
        private void CountTouch()
        {
            if (!touchPending) return;
            touchPending = false;
            bool hit = false;
            foreach (var touch in ball.Touches)
                if (touch.Side == touchSide && touch.What != "goleiro") hit = true;
            if (hit) Count(touchSide).touches++;
            else Count(touchSide).misses++;
        }

        /// <summary>Passa a vez para <paramref name="side"/> com os toques cheios.</summary>
        private void GiveTurn(TeamSide side)
        {
            Turn = side;
            touchesLeft = options.touchesPerTurn;
            freeKickDisc = null;
        }

        private void PassTurn(string reason)
        {
            if (tutorial != null)
            {
                tutorial.OnPlayEnded(TutorialPlay.Missed);
                return;
            }
            // Na disputa de pênaltis, perder a vez (tempo de mira esgotado) é pênalti perdido.
            if (IsShootout)
            {
                RecordShootoutKick(false);
                return;
            }
            GiveTurn(Opponent(Turn));
            string turnText = Txt.Format("Vez do {0}.", TeamName(Turn));
            message = string.IsNullOrEmpty(reason) ? turnText : $"{Txt.Get(reason)} {turnText}";
            Enter(MatchState.Aim);
        }

        // ---- Falta ----

        private void OnFouled(Disc offender, Disc victim, Vector2 victimPosition)
        {
            if (State != MatchState.Moving || offender != shooter || !options.fouls || IsShootout) return;
            CommitFoul(offender, victim, victimPosition);
        }

        /// <summary>
        /// Companheiro empurrado pelo botão do peteleco acertou um adversário antes de alguém tocar a bola: também é
        /// falta (não vale usar um botão para empurrar o outro em cima do adversário).
        /// </summary>
        private void OnHitOpponent(Disc disc, Disc victim, Vector2 victimPosition)
        {
            if (State != MatchState.Moving || shooter == null || !options.fouls || IsShootout) return;
            if (disc == shooter || disc.Side != shooter.Side || ball.LastTouchSide != null) return;
            CommitFoul(disc, victim, victimPosition);
        }

        /// <summary>
        /// Botão do peteleco (ou companheiro empurrado por ele) acertou o goleiro adversário antes de alguém tocar a
        /// bola: falta no goleiro, tiro de meta para o time dele.
        /// </summary>
        private void OnHitOpponentKeeper(Disc disc, Goalkeeper keeper, bool asShooter)
        {
            if (State != MatchState.Moving || shooter == null || !options.fouls || IsShootout) return;
            if (asShooter ? disc != shooter : disc == shooter || disc.Side != shooter.Side || ball.LastTouchSide != null) return;

            Debug.Log($"[Falta] {disc.name} ({TeamName(disc.Side)}) acertou o goleiro do {TeamName(keeper.Side)}.");
            Count(disc.Side).fouls++;
            EndShot();
            monitor.StopWatching();
            monitor.FreezeAll();
            if (tutorial != null)
            {
                tutorial.OnFoul(disc.Side);
                return;
            }

            bool far = Vector2.Distance(keeper.transform.position, shotBallStart) > options.farFoulDistance;
            message = Txt.Format("Falta no goleiro do {0}!", TeamName(keeper.Side));
            SetupGoalKick(keeper.Side);
            Noticed?.Invoke(MatchNotice.Foul);
            if (far) GiveCard(disc);
            Hold();
        }

        private void CommitFoul(Disc offender, Disc victim, Vector2 victimPosition)
        {
            // Para investigar falta marcada errado: mostra o que o jogo sabia no momento.
            Debug.Log($"[Falta] {offender.name} ({TeamName(offender.Side)}) acertou {victim.name}. " +
                      $"Último toque na bola neste peteleco: {(ball.LastDiscTouch != null ? ball.LastDiscTouch.name : "ninguém")}.");

            Count(offender.Side).fouls++;
            EndShot();
            monitor.StopWatching();
            monitor.FreezeAll();
            if (tutorial != null)
            {
                tutorial.OnFoul(offender.Side);
                return;
            }

            // Falta longe do lance (para afastar a bola, por exemplo) vale cartão para o jogador, não para o botão.
            bool far = Vector2.Distance(victimPosition, shotBallStart) > options.farFoulDistance;

            if (FieldLayout.InArea(victimPosition, offender.Side)) SetupPenalty(offender, victim);
            else SetupFreeKick(offender, victim, victimPosition);

            Noticed?.Invoke(setupIsPenalty ? MatchNotice.Penalty : MatchNotice.Foul);
            if (far) GiveCard(offender);
            Hold();
        }

        /// <summary>1ª falta longe do lance: amarelo. Depois, cada uma é vermelho e o botão que fez a falta sai.</summary>
        private void GiveCard(Disc offender)
        {
            var side = offender.Side;
            int count = ++farFouls[(int)side];
            if (count == 1) Count(side).yellow++;
            else Count(side).red++;
            if (count == 1)
            {
                Noticed?.Invoke(MatchNotice.YellowCard);
                message += " " + Txt.Format("Cartão amarelo para o {0}!", TeamName(side));
                return;
            }

            int onField = 0;
            foreach (var disc in discs[side])
                if (disc.isActiveAndEnabled) onField++;
            offender.gameObject.SetActive(false);
            Noticed?.Invoke(MatchNotice.RedCard);
            if (onField > 1)
            {
                message += " " + Txt.Format("Cartão vermelho para o {0}: {1} expulso!", TeamName(side), offender.name);
                return;
            }
            Walkover(side);
        }

        /// <summary>W.O.: o time ficou sem botões. Quem foi expulso fica com 0 e o adversário ganha 3 gols a mais.</summary>
        private void Walkover(TeamSide loser)
        {
            var winner = Opponent(loser);
            score[(int)loser] = 0;
            score[(int)winner] += 3;
            EndShot();
            monitor.StopWatching();
            monitor.FreezeAll();
            message = Txt.Format("W.O.! O {0} ficou sem botões. Vitória do {1}!", TeamName(loser), TeamName(winner));
            Noticed?.Invoke(MatchNotice.FinalWhistle);
            MatchSession.LastResult = new MatchResult(Score(TeamSide.Bottom), Score(TeamSide.Top), true);
            RecordStats(walkover: true);
            Enter(MatchState.End);
        }

        /// <summary>
        /// Tiro livre: a bola vai para <paramref name="foulSpot"/> e quem sofreu a falta vira o batedor, posicionado em
        /// volta da bola. Quem fez a falta volta para a formação e ninguém fica na roda em que o batedor gira.
        /// </summary>
        private void SetupFreeKick(Disc offender, Disc victim, Vector2 foulSpot)
        {
            float margin = ball.Radius + 0.02f;
            Vector2 spot = new(
                Mathf.Clamp(foulSpot.x, -FieldLayout.HalfWidth + margin, FieldLayout.HalfWidth - margin),
                Mathf.Clamp(foulSpot.y, -FieldLayout.HalfHeight + margin, FieldLayout.HalfHeight - margin));
            ball.ResetTo(spot);

            // A roda: o batedor gira a esta distância da bola, então os outros ficam do lado de fora dela.
            float clearance = KickerDistance(victim) + victim.Radius * 2f + 0.05f;

            int index = discs[offender.Side].IndexOf(offender);
            offender.PlaceAt(FindFreeSpot(FormationPosition(index, offender.Side), offender, victim, clearance));

            foreach (var disc in allDiscs)
            {
                if (disc == victim || disc == offender || !disc.isActiveAndEnabled) continue;
                Vector2 offset = disc.Body.position - spot;
                if (offset.sqrMagnitude >= clearance * clearance) continue;
                Vector2 away = offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector2.right;
                disc.PlaceAt(FindFreeSpot(spot + away * clearance, disc, victim, clearance));
            }

            GiveTurn(victim.Side);
            freeKickDisc = victim;
            setupIsPenalty = false;
            PlaceKickerNearest(victim, 0f);

            message = Txt.Format("Falta do {0}! Tiro livre para o {1}: posicione o batedor.", TeamName(offender.Side), TeamName(victim.Side));
            Enter(MatchState.PenaltySetup);
        }

        /// <summary>
        /// Pênalti: falta dentro da área de quem defende. Bola na marca, quem sofreu bate; os outros atacantes voltam
        /// para a formação e os defensores ficam espalhados na linha do meio-campo.
        /// </summary>
        private void SetupPenalty(Disc offender, Disc victim)
        {
            var defending = offender.Side;
            var attacking = victim.Side;
            Vector2 spot = FieldLayout.PenaltySpot(defending);
            Vector2 attack = FieldLayout.AttackDirection(attacking);

            var attackers = discs[attacking];
            for (int i = 0; i < attackers.Count && i < options.discsPerTeam; i++)
                if (attackers[i] != victim) attackers[i].PlaceAt(FormationPosition(i, attacking));

            var defenders = new List<Disc>();
            foreach (var disc in discs[defending])
                if (disc.isActiveAndEnabled) defenders.Add(disc);
            float spacing = FieldLayout.Width / (defenders.Count + 1);
            for (int i = 0; i < defenders.Count; i++)
                defenders[i].PlaceAt(new Vector2(-FieldLayout.HalfWidth + spacing * (i + 1), 0f));

            foreach (var keeper in keepers)
                if (keeper.isActiveAndEnabled) keeper.ResetToCenter();
            ball.ResetTo(spot);
            setupIsPenalty = true;
            PlaceKickerNearest(victim, 0f);

            GiveTurn(attacking);
            freeKickDisc = victim;
            BeginPenaltyChoice(Txt.Format("Pênalti para o {0}! Posicione o botão.", TeamName(attacking)));
        }

        /// <summary>
        /// Começo do pênalti (de falta ou da disputa). Com goleiro, o primeiro a escolher fica escondido: contra a IA
        /// ela escolhe primeiro (o goleiro dela, com o jogador batendo); senão, o batedor.
        /// </summary>
        private void BeginPenaltyChoice(string kickerMessage)
        {
            kickerHidden = keeperHidden = false;
            penaltyKeeperFirst = false;
            if (HiddenPenalty && IsAi(Opponent(Turn)) && !IsAi(Turn))
            {
                penaltyKeeperFirst = true;
                keeperHidden = true;
                message = Txt.Format("Pênalti do {0}! O goleiro do {1} escolhe o canto.", TeamName(Turn), TeamName(Opponent(Turn)));
                Enter(MatchState.ShotCall);
                return;
            }
            kickerHidden = HiddenPenalty && IsAi(Turn);
            message = kickerMessage;
            Enter(MatchState.PenaltySetup);
        }

        /// <summary>Goleiro do pênalti pronto: tem que estar de um lado. Depois, o batedor escolhe ou a faixa sai e ele chuta.</summary>
        private void FinishPenaltyKeeper()
        {
            var keeper = DefendingKeeper();
            if (keeper != null && !KeeperSideChosen(keeper))
            {
                if (stateTimer > 0f && !IsAi(Opponent(Turn)))
                {
                    message = Txt.Get("Escolha um lado: o goleiro não pode ficar no meio.");
                    return;
                }
                ForceKeeperSide(keeper);
            }
            if (penaltyKeeperFirst)
            {
                message = Txt.Format("{0}: posicione o batedor (o goleiro já escolheu o canto).", TeamName(Turn));
                Enter(MatchState.PenaltySetup);
                return;
            }
            EnterShotAim();
        }

        private Goalkeeper DefendingKeeper()
        {
            foreach (var keeper in keepers)
                if (keeper != null && keeper.isActiveAndEnabled && keeper.Side == Opponent(Turn)) return keeper;
            return null;
        }

        private bool KickerSideChosen() =>
            freeKickDisc == null || Mathf.Abs(PenaltyAngle(freeKickDisc)) >= options.penaltyKickerMinDegrees - 0.5f;

        private bool KeeperSideChosen(Goalkeeper keeper) =>
            Mathf.Abs(keeper.SideFraction) >= options.penaltyKeeperMinSide - 0.01f;

        /// <summary>Tempo esgotado com o batedor no meio: vai para o lado mais perto (no meio exato, sorteado).</summary>
        private void ForceKickerSide()
        {
            var disc = freeKickDisc;
            if (disc == null) return;
            float angle = PenaltyAngle(disc);
            float sign = Mathf.Abs(angle) < 0.5f ? (Random.value < 0.5f ? -1f : 1f) : Mathf.Sign(angle);
            float min = options.penaltyKickerMinDegrees;
            if (!TryPlaceKicker(disc, sign * min)) TryPlaceKicker(disc, -sign * min);
        }

        /// <summary>Tempo esgotado com o goleiro no meio: vai para o lado mais perto (no meio exato, sorteado).</summary>
        private static void ForceKeeperSide(Goalkeeper keeper, float minSide)
        {
            float side = keeper.SideFraction;
            float sign = Mathf.Abs(side) < 0.01f ? (Random.value < 0.5f ? -1f : 1f) : Mathf.Sign(side);
            float center = (keeper.MinX + keeper.MaxX) * 0.5f;
            float half = (keeper.MaxX - keeper.MinX) * 0.5f;
            keeper.SnapTo(center + sign * half * minSide);
        }

        private void ForceKeeperSide(Goalkeeper keeper) => ForceKeeperSide(keeper, options.penaltyKeeperMinSide);

        /// <summary>
        /// Faixa preta de lado a lado: cobre o batedor e a bola (ou o goleiro e o gol) de quem já escolheu.
        /// </summary>
        private void UpdatePenaltyCover()
        {
            bool show = kickerHidden || keeperHidden;
            if (!show)
            {
                if (penaltyCover != null) penaltyCover.enabled = false;
                return;
            }

            float minY, maxY;
            if (kickerHidden)
            {
                Vector2 ballPosition = ball.Body.position;
                float up = FieldLayout.AttackDirection(Turn).y;
                float discRadius = freeKickDisc != null ? freeKickDisc.Radius : ball.Radius;
                float back = freeKickDisc != null ? KickerDistance(freeKickDisc) + discRadius : discRadius;
                float a = ballPosition.y - up * (back + PenaltyCoverMargin);
                float b = ballPosition.y + up * (ball.Radius + PenaltyCoverMargin);
                minY = Mathf.Min(a, b);
                maxY = Mathf.Max(a, b);
            }
            else
            {
                var keeper = DefendingKeeper();
                float goalY = FieldLayout.GoalLineY(Opponent(Turn));
                float sign = Mathf.Sign(goalY);
                float front = goalY - sign * 0.6f;
                if (keeper != null && keeper.TryGetComponent(out Collider2D box))
                    front = sign > 0f ? box.bounds.min.y : box.bounds.max.y;
                float a = goalY + sign * FieldLayout.GoalDepth;
                float b = front - sign * PenaltyCoverMargin;
                minY = Mathf.Min(a, b);
                maxY = Mathf.Max(a, b);
            }

            if (penaltyCover == null)
            {
                var go = new GameObject("Faixa do pênalti");
                go.transform.SetParent(transform, false);
                penaltyCover = go.AddComponent<SpriteRenderer>();
                var texture = Texture2D.whiteTexture;
                penaltyCover.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), texture.width);
                penaltyCover.color = Color.black;
                penaltyCover.sortingOrder = PenaltyCoverOrder;
            }
            penaltyCover.enabled = true;
            var t = penaltyCover.transform;
            t.position = new Vector3(0f, (minY + maxY) * 0.5f, 0f);
            Vector3 parentScale = transform.lossyScale;
            float width = FieldLayout.Width + 1f;
            t.localScale = new Vector3(width / Mathf.Max(parentScale.x, 0.0001f), (maxY - minY) / Mathf.Max(parentScale.y, 0.0001f), 1f);
        }

        private float KickerDistance(Disc disc) => disc.Radius + ball.Radius + options.penaltyDiscGap;

        /// <summary>
        /// Põe o batedor em volta da bola. <paramref name="angleDegrees"/> = 0 é bem atrás (para o lado do próprio gol),
        /// sinal = lado. No pênalti fica preso ao arco; no tiro livre dá a volta inteira, mas não entra em cima de
        /// outro botão, do goleiro ou fora do campo (aí ele fica onde estava e devolve false).
        /// </summary>
        private bool TryPlaceKicker(Disc disc, float angleDegrees)
        {
            float angle = setupIsPenalty
                ? Mathf.Clamp(angleDegrees, -options.penaltyArcDegrees, options.penaltyArcDegrees)
                : Mathf.DeltaAngle(0f, angleDegrees);
            Vector2 back = -FieldLayout.AttackDirection(disc.Side);
            Vector2 dir = (Vector2)(Quaternion.Euler(0f, 0f, angle) * back);
            Vector2 position = ball.Body.position + dir * KickerDistance(disc);
            if (!IsFreeSpot(position, disc, null, disc.Radius + ball.Radius + 0.05f)) return false;
            disc.PlaceAt(position);
            return true;
        }

        /// <summary>Batedor no ângulo livre mais perto de <paramref name="angleDegrees"/>; sem nenhum, no lugar livre mais perto.</summary>
        private void PlaceKickerNearest(Disc disc, float angleDegrees)
        {
            for (int step = 0; step <= 12; step++)
            {
                if (TryPlaceKicker(disc, angleDegrees + step * 15f)) return;
                if (step > 0 && step < 12 && TryPlaceKicker(disc, angleDegrees - step * 15f)) return;
            }
            Vector2 back = -FieldLayout.AttackDirection(disc.Side);
            disc.PlaceAt(FindFreeSpot(ball.Body.position + back * KickerDistance(disc), disc, null, disc.Radius + ball.Radius + 0.05f));
        }

        /// <summary>A IA posiciona o batedor (mesmas regras do jogador). Devolve false se o lugar não serve.</summary>
        public bool TryPlaceKicker(float angleDegrees) =>
            State == MatchState.PenaltySetup && freeKickDisc != null && TryPlaceKicker(freeKickDisc, angleDegrees);

        /// <summary>Ângulo do batedor para bater reto na bola na direção <paramref name="shotDirection"/>.</summary>
        public float KickerAngleFor(Vector2 shotDirection) =>
            Vector2.SignedAngle(-FieldLayout.AttackDirection(Turn), -shotDirection);

        private float PenaltyAngle(Disc disc)
        {
            Vector2 back = -FieldLayout.AttackDirection(disc.Side);
            return Vector2.SignedAngle(back, disc.Body.position - ball.Body.position);
        }

        /// <summary>Arrastar (ou A/D, setas, analógico, D-pad) gira o batedor em volta da bola (no pênalti, dentro do arco).</summary>
        private void UpdatePenaltySetup()
        {
            var disc = freeKickDisc;
            if (disc == null || IsAi(Turn)) return;

            var input = MatchInput.For(Turn);
            if (input.PointerHeld && input.TryGetPointer(out var screen) && !UiPointer.IsOverUi())
            {
                worldCamera = WorldCamera.Resolve(worldCamera);
                if (worldCamera != null)
                {
                    Vector2 world = WorldCamera.ScreenToWorld(worldCamera, screen);
                    Vector2 offset = world - ball.Body.position;
                    // Toques longe da bola (no HUD, por exemplo) não mexem o botão.
                    if (offset.sqrMagnitude > 0.01f && offset.magnitude < 3f)
                    {
                        Vector2 back = -FieldLayout.AttackDirection(disc.Side);
                        TryPlaceKicker(disc, Vector2.SignedAngle(back, offset));
                        return;
                    }
                }
            }

            // Direita leva o batedor (atrás da bola) para a direita de quem joga.
            float axis = input.SideAxis * MatchInput.TurnSign(Turn);
            if (axis != 0f) TryPlaceKicker(disc, PenaltyAngle(disc) + axis * 90f * Time.deltaTime);
        }

        private void FinishPenaltySetup()
        {
            if (HiddenPenalty)
            {
                if (!KickerSideChosen())
                {
                    if (stateTimer > 0f && !IsAi(Turn))
                    {
                        message = Txt.Get("Escolha um lado: o batedor não pode ficar no meio.");
                        return;
                    }
                    ForceKickerSide();
                }
                if (penaltyKeeperFirst)
                {
                    // O goleiro já escolheu: a faixa sai e o batedor chuta.
                    EnterShotAim();
                    return;
                }
                kickerHidden = true;
                message = Txt.Format("{0}: escolha o canto do goleiro.", TeamName(Opponent(Turn)));
                Enter(MatchState.ShotCall);
            }
            else if (setupIsPenalty)
            {
                message = Txt.Format("Pênalti: {0} vai chutar!", TeamName(Turn));
                BeginShotCall();
            }
            else
            {
                // Tiro livre: a vez começa normal; no campo de ataque o "Vai chutar" fica disponível como sempre.
                message = Txt.Format("Tiro livre do {0}.", TeamName(Turn));
                Enter(MatchState.Aim);
            }
        }

        /// <summary>
        /// Lugar livre para um botão: dentro do campo, longe de <paramref name="ballClearance"/> do centro da bola,
        /// sem encostar em outro botão (menos <paramref name="ignore"/>) nem no goleiro.
        /// </summary>
        private bool IsFreeSpot(Vector2 position, Disc disc, Disc ignore, float ballClearance)
        {
            float radius = disc.Radius;
            if (Mathf.Abs(position.x) > FieldLayout.HalfWidth - radius - 0.02f) return false;
            if (Mathf.Abs(position.y) > FieldLayout.HalfHeight - radius - 0.02f) return false;
            if ((position - ball.Body.position).sqrMagnitude < ballClearance * ballClearance) return false;

            foreach (var other in allDiscs)
            {
                if (other == disc || other == ignore || !other.isActiveAndEnabled) continue;
                float minDistance = radius + other.Radius + 0.05f;
                if ((position - other.Body.position).sqrMagnitude < minDistance * minDistance) return false;
            }

            foreach (var keeper in keepers)
            {
                if (!keeper.isActiveAndEnabled || !keeper.TryGetComponent(out Collider2D keeperCollider)) continue;
                var bounds = keeperCollider.bounds;
                float gap = radius + 0.05f;
                if (bounds.SqrDistance(new Vector3(position.x, position.y, bounds.center.z)) < gap * gap) return false;
            }
            return true;
        }

        /// <summary>O lugar livre mais perto de <paramref name="desired"/>, procurando em anéis cada vez maiores.</summary>
        private Vector2 FindFreeSpot(Vector2 desired, Disc disc, Disc ignore, float ballClearance)
        {
            if (IsFreeSpot(desired, disc, ignore, ballClearance)) return desired;
            const int directions = 24;
            for (float distance = 0.15f; distance <= 8f; distance += 0.15f)
            for (int i = 0; i < directions; i++)
            {
                float angle = i * Mathf.PI * 2f / directions;
                Vector2 candidate = desired + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                if (IsFreeSpot(candidate, disc, ignore, ballClearance)) return candidate;
            }
            return desired;
        }

        // ---- Gol ----

        private void OnBallEntered(GoalTrigger goal, Ball enteredBall)
        {
            if (State != MatchState.Moving) return;

            EndShot();
            monitor.StopWatching();
            enteredBall.StopInGoal();

            // Disputa de pênaltis: entrou no gol de quem defende = gol.
            if (IsShootout)
            {
                RecordShootoutKick(goal.DefendingSide == Opponent(Turn));
                return;
            }
            pendingGoal = goal;

            // Gol contra: a jogada era de quem defende este gol (o chute saiu de um botão dele), mesmo que a bola tenha
            // batido num adversário. Chute do atacante que desvia num defensor é gol normal do atacante.
            // Gol normal só vale num "Pro Gol!" (sem goleiro: chute do campo de ataque). Com "Gol após parede: Anulado",
            // a bola tocar a parede lateral anula qualquer gol, contra ou não.
            bool ownGoal = Turn == goal.DefendingSide;
            bool wallOk = options.goalAfterWallIsValid || !enteredBall.TouchedWallSinceShot;
            bool called = options.hasGoalkeeper ? shotWasCalled : shotFromAttackHalf;
            pendingGoalValid = wallOk && (ownGoal || called);

            var scorer = Opponent(goal.DefendingSide);
            if (pendingGoalValid)
            {
                score[(int)scorer]++;
                Count(scorer).goals++;
                Count(goal.DefendingSide).conceded++;
                if (ownGoal) Count(Turn).ownGoals++;
                else if (shotWasCalled) Count(scorer).shotGoals++;
                message = ownGoal ? Txt.Format("Gol contra! Ponto do {0}.", TeamName(scorer)) : Txt.Format("GOL do {0}!", TeamName(scorer));
            }
            else
            {
                message = Txt.Get(!wallOk ? "Gol anulado: a bola tocou a parede lateral."
                    : options.hasGoalkeeper ? "Gol anulado: não avisou o \"Pro Gol!\"."
                    : "Gol anulado: chute do campo de defesa.");
            }

            Noticed?.Invoke(pendingGoalValid ? MatchNotice.Goal : MatchNotice.GoalAnnulled);
            Enter(MatchState.Goal);
            // O aviso já é a pausa do gol: acabou, segue direto para a saída ou o tiro de meta.
            if (options.noticeHoldSeconds > 0f) stateTimer = 0f;
            Hold();
        }

        private void ResolveGoal()
        {
            if (IsShootout)
            {
                ContinueShootout();
                return;
            }
            var defending = pendingGoal.DefendingSide;
            pendingGoal = null;

            if (tutorial != null)
            {
                // Sai do estado de gol (senão o gol seria resolvido de novo a cada quadro); a lição monta a próxima jogada.
                Enter(MatchState.Moving);
                tutorial.OnGoal(Opponent(defending), pendingGoalValid);
                return;
            }

            if (clock <= 0f)
            {
                EndMatch();
                return;
            }

            if (pendingGoalValid)
            {
                // Quem levou o gol dá a saída.
                KickOff(defending);
                return;
            }

            SetupGoalKick(defending);
        }

        /// <summary>
        /// Tiro de meta: todos os botões voltam para a formação, os goleiros para o centro e a bola vai para a frente do
        /// goleiro de quem defendeu. Ele chuta direto na bola.
        /// </summary>
        private void SetupGoalKick(TeamSide defending)
        {
            monitor.FreezeAll();
            Vector2 towardCenter = FieldLayout.AttackDirection(defending);
            foreach (var other in keepers)
                if (other.isActiveAndEnabled) other.ResetToCenter();
            var keeper = FindKeeper(defending);
            Vector2 spot = keeper != null
                ? (Vector2)keeper.transform.position + towardCenter * 0.8f
                : new Vector2(0f, FieldLayout.GoalLineY(defending)) + towardCenter * 1f;
            ball.ResetTo(spot);
            ResetFormation();

            GiveTurn(defending);
            message += " " + Txt.Format("Tiro de meta do {0}: posicione a bola e aperte Pronto.", TeamName(defending));
            goalKickPlacing = true;
            draggingGoalKickBall = false;
            Enter(MatchState.GoalKick);
        }

        // ---- Tiro de meta: posicionar a bola ----

        /// <summary>Até onde a bola anda para cada lado no tiro de meta: a largura da área.</summary>
        public float GoalKickHalfRange => FieldLayout.AreaWidth * 0.5f - ball.Radius;

        /// <summary>Arrastar a bola (começando nela) leva ela para os lados; A/D, setas, analógico e D-pad também.</summary>
        private void UpdateGoalKickPlacement()
        {
            var input = MatchInput.For(Turn);
            if (!input.PointerHeld) draggingGoalKickBall = false;
            else if (input.TryGetPointer(out var screen))
            {
                worldCamera = WorldCamera.Resolve(worldCamera);
                if (worldCamera != null)
                {
                    Vector2 world = WorldCamera.ScreenToWorld(worldCamera, screen);
                    // Área de toque um pouco maior que a bola, que é pequena (igual à mira do tiro de meta).
                    if (input.PointerPressedThisFrame && !UiPointer.IsOverUi() &&
                        Vector2.Distance(world, ball.Body.position) <= ball.Radius + 0.3f)
                        draggingGoalKickBall = true;
                    if (draggingGoalKickBall)
                    {
                        MoveGoalKickBall(world.x);
                        return;
                    }
                }
            }

            float axis = input.SideAxis * MatchInput.ScreenSign(Turn);
            if (axis != 0f) MoveGoalKickBall(ball.Body.position.x + axis * 3f * Time.deltaTime);
        }

        /// <summary>
        /// Leva a bola do tiro de meta até <paramref name="targetX"/> (dentro da largura da área), parando antes de
        /// encostar num botão. Usada também pela IA.
        /// </summary>
        public void MoveGoalKickBall(float targetX)
        {
            if (!IsPlacingGoalKick) return;
            float range = GoalKickHalfRange;
            targetX = Mathf.Clamp(targetX, -range, range);
            Vector2 position = ball.Body.position;
            float step = Mathf.Sign(targetX - position.x) * 0.02f;
            while (Mathf.Abs(targetX - position.x) > 0.001f)
            {
                var next = new Vector2(Mathf.Abs(targetX - position.x) <= Mathf.Abs(step) ? targetX : position.x + step, position.y);
                if (!BallSpotFree(next)) break;
                position = next;
            }
            if (position != ball.Body.position) ball.ResetTo(position);
        }

        private bool BallSpotFree(Vector2 position)
        {
            foreach (var other in allDiscs)
            {
                if (!other.isActiveAndEnabled) continue;
                float minDistance = ball.Radius + other.Radius + 0.02f;
                if ((position - other.Body.position).sqrMagnitude < minDistance * minDistance) return false;
            }
            return true;
        }

        /// <summary>"Pronto" no tiro de meta: a bola fica onde está e começa a mira (no mesmo tempo).</summary>
        private void FinishGoalKickPlacement()
        {
            if (!IsPlacingGoalKick) return;
            goalKickPlacing = false;
            draggingGoalKickBall = false;
            message = Txt.Format("Tiro de meta do {0}: arraste a bola para chutar.", TeamName(Turn));
            StateVersion++;
            aim.Cancel();
            ApplyControls();
        }

        /// <summary>Todos os botões na formação inicial; quem cair em cima da bola vai para o lugar livre mais perto.</summary>
        /// <summary>
        /// Na saída, os botões de quem não sai que caem dentro do círculo central vão para a linha dele (o centro do
        /// botão em cima da linha), na mesma direção. Quem dá a saída pode ficar dentro.
        /// </summary>
        private void KeepOutOfCenterCircle(TeamSide side)
        {
            if (!discs.TryGetValue(side, out var team)) return;
            float radius = FieldLayout.CenterCircleRadius;
            foreach (var disc in team)
            {
                if (!disc.isActiveAndEnabled) continue;
                Vector2 position = disc.Body.position;
                if (position.sqrMagnitude >= radius * radius) continue;
                // No meio exato, recua para o próprio campo.
                Vector2 direction = position.sqrMagnitude > 0.0001f ? position.normalized : -FieldLayout.AttackDirection(side);
                Vector2 target = direction * radius;
                disc.PlaceAt(IsFreeSpot(target, disc, null, disc.Radius + ball.Radius + 0.05f)
                    ? target
                    : FindFreeSpot(direction * (radius + disc.Radius), disc, null, disc.Radius + ball.Radius + 0.05f));
            }
        }

        private void ResetFormation()
        {
            foreach (var pair in discs)
                for (int i = 0; i < pair.Value.Count && i < options.discsPerTeam; i++)
                    pair.Value[i].PlaceAt(FormationPosition(i, pair.Key));

            foreach (var disc in allDiscs)
            {
                if (!disc.isActiveAndEnabled) continue;
                float clearance = disc.Radius + ball.Radius + 0.3f;
                if ((disc.Body.position - ball.Body.position).sqrMagnitude >= clearance * clearance) continue;
                disc.PlaceAt(FindFreeSpot(disc.Body.position, disc, null, clearance));
            }
        }

        /// <summary>
        /// Fim do tempo. Empatado: prorrogação (se ligada e ainda não jogada); depois, disputa de pênaltis (se ligada e
        /// com goleiro). Senão termina como está.
        /// </summary>
        private void EndMatch()
        {
            if (Score(TeamSide.Bottom) == Score(TeamSide.Top))
            {
                if (period == Period.Regular && options.ExtraTimeSeconds > 0f)
                {
                    StartExtraTime();
                    return;
                }
                if (period != Period.Shootout && options.PenaltiesEnabled)
                {
                    StartShootout();
                    return;
                }
            }
            FinishMatch();
        }

        private void StartExtraTime()
        {
            period = Period.ExtraTime;
            clock = options.ExtraTimeSeconds;
            KickOff(Random.value < 0.5f ? TeamSide.Bottom : TeamSide.Top);
            message = Txt.Format("Prorrogação! Saída do {0}.", TeamName(Turn));
            Hold();
        }

        // ---- Disputa de pênaltis ----

        /// <summary>
        /// Começa a disputa: um time sorteado bate primeiro, cada um no seu gol de ataque, revezando os botões que
        /// estavam em campo. Em campo ficam só o batedor e o goleiro de quem defende.
        /// </summary>
        private void StartShootout()
        {
            period = Period.Shootout;
            EndShot();
            monitor.StopWatching();
            monitor.FreezeAll();
            for (int i = 0; i < 2; i++)
            {
                shootoutKicks[i].Clear();
                shootoutNext[i] = 0;
                shootoutTakers[i].Clear();
                foreach (var disc in discs[(TeamSide)i])
                    if (disc.isActiveAndEnabled) shootoutTakers[i].Add(disc);
            }
            shootoutFirst = Random.value < 0.5f ? TeamSide.Bottom : TeamSide.Top;
            Noticed?.Invoke(MatchNotice.FinalWhistle);
            NextShootoutKick();
            message = $"{Txt.Get("Disputa de pênaltis!")} {message}";
            Hold();
        }

        /// <summary>Prepara a próxima cobrança: bate quem cobrou menos (empatados, quem começou).</summary>
        private void NextShootoutKick()
        {
            int bottomKicks = shootoutKicks[(int)TeamSide.Bottom].Count;
            int topKicks = shootoutKicks[(int)TeamSide.Top].Count;
            var side = bottomKicks == topKicks ? shootoutFirst : bottomKicks < topKicks ? TeamSide.Bottom : TeamSide.Top;
            var defending = Opponent(side);

            var takers = shootoutTakers[(int)side];
            Disc kicker = takers.Count > 0 ? takers[shootoutNext[(int)side]++ % takers.Count] : null;
            foreach (var disc in allDiscs) disc.gameObject.SetActive(disc == kicker);
            foreach (var keeper in keepers)
            {
                keeper.gameObject.SetActive(keeper.Side == defending);
                if (keeper.Side == defending) keeper.ResetToCenter();
            }

            ball.ResetTo(FieldLayout.PenaltySpot(defending));
            GiveTurn(side);
            touchesLeft = 1;
            setupIsPenalty = true;
            freeKickDisc = kicker;
            if (kicker != null) PlaceKickerNearest(kicker, 0f);
            BeginPenaltyChoice(Txt.Format("Pênalti do {0}: posicione o batedor.", TeamName(side)));
        }

        /// <summary>Fim de uma cobrança: marca no placar da disputa e para um pouco para todo mundo ver.</summary>
        private void RecordShootoutKick(bool scored)
        {
            shootoutKicks[(int)Turn].Add(scored);
            message = scored ? Txt.Format("GOL do {0}!", TeamName(Turn)) : Txt.Format("{0} perdeu o pênalti!", TeamName(Turn));
            Noticed?.Invoke(scored ? MatchNotice.Goal : MatchNotice.GoalAnnulled);
            pendingGoal = null;
            Enter(MatchState.Goal);
            if (options.noticeHoldSeconds > 0f) stateTimer = 0f;
            Hold();
        }

        private void ContinueShootout()
        {
            var winner = ShootoutWinner();
            if (winner == null)
            {
                NextShootoutKick();
                return;
            }

            message = Txt.Format("Fim de jogo: {0} vence nos pênaltis!", TeamName(winner.Value));
            MatchSession.LastResult = new MatchResult(Score(TeamSide.Bottom), Score(TeamSide.Top), false,
                ShootoutGoals(TeamSide.Bottom), ShootoutGoals(TeamSide.Top));
            RecordStats(walkover: false);
            Noticed?.Invoke(MatchNotice.FinalWhistle);
            Enter(MatchState.End);
        }

        /// <summary>
        /// Quem já ganhou a disputa: nas 5 cobranças, quando o outro não alcança mais; depois (morte súbita), quando
        /// os dois bateram o mesmo número de vezes e um fez mais. Nulo: continua.
        /// </summary>
        private TeamSide? ShootoutWinner()
        {
            int bottomKicks = shootoutKicks[(int)TeamSide.Bottom].Count, topKicks = shootoutKicks[(int)TeamSide.Top].Count;
            int bottomGoals = ShootoutGoals(TeamSide.Bottom), topGoals = ShootoutGoals(TeamSide.Top);
            if (bottomKicks <= ShootoutRounds && topKicks <= ShootoutRounds)
            {
                if (bottomGoals + (ShootoutRounds - bottomKicks) < topGoals) return TeamSide.Top;
                if (topGoals + (ShootoutRounds - topKicks) < bottomGoals) return TeamSide.Bottom;
            }
            if (bottomKicks == topKicks && bottomKicks >= ShootoutRounds && bottomGoals != topGoals)
                return bottomGoals > topGoals ? TeamSide.Bottom : TeamSide.Top;
            return null;
        }

        // ---- Tutorial ----

        /// <summary>Botões do time (inclusive os desligados), na ordem do nome.</summary>
        public IReadOnlyList<Disc> TeamDiscs(TeamSide side) => discs[side];

        /// <summary>
        /// Tutorial: monta a jogada da lição. Botões nas posições dadas (os que faltarem, na formação), goleiros no centro,
        /// a bola no lugar, a vez de <paramref name="turn"/> com os toques cheios e o placar zerado.
        /// </summary>
        public void TutorialSetup(TeamSide turn, Vector2 ballPosition, Vector2[] bottom, Vector2[] top)
        {
            EndShot();
            monitor.StopWatching();
            monitor.FreezeAll();
            holdTimer = 0f;
            restoreControls = false;
            score[0] = score[1] = 0;
            farFouls[0] = farFouls[1] = 0;
            kickerHidden = keeperHidden = false;
            penaltyKeeperFirst = false;
            setupIsPenalty = false;
            goalKickPlacing = false;
            shotWasCalled = false;
            pendingGoal = null;

            foreach (var pair in discs)
            {
                var positions = pair.Key == TeamSide.Bottom ? bottom : top;
                for (int i = 0; i < pair.Value.Count && i < options.discsPerTeam; i++)
                {
                    pair.Value[i].gameObject.SetActive(true);
                    pair.Value[i].PlaceAt(positions != null && i < positions.Length ? positions[i] : FormationPosition(i, pair.Key));
                }
            }
            foreach (var keeper in keepers)
                if (keeper.isActiveAndEnabled) keeper.ResetToCenter();
            ball.ResetTo(ballPosition);

            GiveTurn(turn);
            message = string.Empty;
            Enter(MatchState.Aim);
        }

        /// <summary>Tutorial: segue a vez com os toques que sobraram.</summary>
        public void TutorialContinue()
        {
            Enter(MatchState.Aim);
        }

        /// <summary>Tutorial: o time da vez avisa "Pro Gol!" (a IA, na lição de defender).</summary>
        public void TutorialCallShot()
        {
            message = Txt.Format("{0}: Pro Gol!", TeamName(Turn));
            BeginShotCall();
        }

        /// <summary>Tutorial: falta de <paramref name="offender"/> em <paramref name="victim"/>, tiro livre onde a vítima está.</summary>
        public void TutorialFreeKick(Disc offender, Disc victim)
        {
            SetupFreeKick(offender, victim, victim.Body.position);
            Noticed?.Invoke(MatchNotice.Foul);
        }

        /// <summary>Tutorial: pênalti para o time de <paramref name="victim"/>.</summary>
        public void TutorialPenalty(Disc offender, Disc victim)
        {
            SetupPenalty(offender, victim);
            Noticed?.Invoke(MatchNotice.Penalty);
        }

        /// <summary>Tutorial: mostra um aviso no meio do campo; com <paramref name="hold"/>, para o jogo enquanto ele aparece.</summary>
        public void TutorialMessage(string text, bool hold)
        {
            message = text;
            if (hold) Hold();
        }

        /// <summary>Tutorial: o balão travou ou destravou os controles.</summary>
        public void TutorialRefreshControls()
        {
            aim.Cancel();
            ApplyControls();
        }

        // ---- Fim ----

        /// <summary>Menu de teste: zera o relógio (a partida acaba no próximo lance, como no fim do tempo).</summary>
        public void DebugEndTime()
        {
            if (IsShootout || State is MatchState.Waiting or MatchState.End) return;
            clock = 0f;
        }

        /// <summary>Menu de teste: iguala o placar (o time com menos gols fica com os do outro).</summary>
        public void DebugTieScore()
        {
            int most = Mathf.Max(score[0], score[1]);
            score[0] = score[1] = most;
        }

        private void FinishMatch()
        {
            int bottom = Score(TeamSide.Bottom);
            int top = Score(TeamSide.Top);
            message = bottom == top ? Txt.Get("Fim de jogo: empate!") : Txt.Format("Fim de jogo: vitória do {0}!", TeamName(bottom > top ? TeamSide.Bottom : TeamSide.Top));
            MatchSession.LastResult = new MatchResult(bottom, top);
            RecordStats(walkover: false);
            Noticed?.Invoke(MatchNotice.FinalWhistle);
            Enter(MatchState.End);
        }

        /// <summary>Fim da partida: os números dos dois times vão para o pós-jogo; os do jogador (baixo), para o save.</summary>
        private void RecordStats(bool walkover)
        {
            MatchSession.LastMatchNumbers = new[] { numbers[0], numbers[1] };
            int bottomPenalties = IsShootout ? ShootoutGoals(TeamSide.Bottom) : -1;
            int topPenalties = IsShootout ? ShootoutGoals(TeamSide.Top) : -1;
            PlayerStats.Finish(numbers[(int)TeamSide.Bottom], Score(TeamSide.Bottom), Score(TeamSide.Top), walkover,
                bottomPenalties, topPenalties);
            if (MatchSession.InTournamentMatch)
                Tournament.RecordPlayerMatch(Score(TeamSide.Bottom), Score(TeamSide.Top), bottomPenalties, topPenalties, walkover,
                    PlayerStats.CoinsEarned);
        }

        private void RequestResult()
        {
            resultRequested = true;
            MatchPlayers.ReleaseBeforeLeaving("fim de jogo");
            if (resultRoute != null) resultRoute.RequestRoute();
            else Debug.LogWarning("[Futebol de Botão] MatchController sem Route Request Trigger do Resultado. Rode \"Futebol de Botão/Criar telas\".");
        }

        // ---- Utilidades ----

        private Goalkeeper FindKeeper(TeamSide side)
        {
            foreach (var keeper in keepers)
                if (keeper.isActiveAndEnabled && keeper.Side == side) return keeper;
            return null;
        }

        private static TeamSide Opponent(TeamSide side) => side == TeamSide.Bottom ? TeamSide.Top : TeamSide.Bottom;

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

        // ---- HUD provisório ----

        private void OnGUI()
        {
            // Na pausa só a tela de pausa do framework aparece; esperando a Activity, nada.
            if (ExternalHud || Paused || State == MatchState.Waiting) return;

            // O HUD é desenhado numa tela de referência de 720 px de altura e escalado para a tela real.
            float scale = Mathf.Max(0.5f, Mathf.Min(Screen.height / ReferenceHeight, Screen.width / ReferenceMinWidth)) * hudScale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float screenHeight = Screen.height / scale;

            var big = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            var small = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true, normal = { textColor = Color.white } };
            var button = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };

            int minutes = Mathf.FloorToInt(clock / 60f);
            int seconds = Mathf.CeilToInt(clock % 60f);
            if (seconds == 60) { minutes++; seconds = 0; }

            const float x = 20f;
            const float width = 340f;
            var lines = new List<(string text, GUIStyle style, float height)>
            {
                ($"{TeamName(TeamSide.Bottom)} {Score(TeamSide.Bottom)} x {Score(TeamSide.Top)} {TeamName(TeamSide.Top)}", big, 40f),
                ($"Tempo {minutes}:{seconds:00}", small, 28f)
            };
            if (State is MatchState.Aim or MatchState.ShotAim or MatchState.GoalKick or MatchState.Moving)
                lines.Add(($"Vez: {TeamName(Turn)}  |  toques: {touchesLeft}", small, 28f));
            if (State is MatchState.Aim or MatchState.ShotAim or MatchState.GoalKick)
                lines.Add(($"Mira: {Mathf.CeilToInt(Mathf.Max(0f, stateTimer))} s", small, 28f));
            if (State == MatchState.PenaltySetup)
                lines.Add(($"Batedor: arraste em volta da bola ({Mathf.CeilToInt(stateTimer)} s)", small, 56f));
            if (State == MatchState.ShotCall)
                lines.Add(($"Goleiro: arraste para os lados ({Mathf.CeilToInt(stateTimer)} s)", small, 56f));
            if (!string.IsNullOrEmpty(message))
                lines.Add((message, small, small.CalcHeight(new GUIContent(message), width)));

            bool showButton = CanCallShot || State is MatchState.ShotCall or MatchState.PenaltySetup;
            float panelHeight = 16f;
            foreach (var line in lines) panelHeight += line.height;
            if (showButton) panelHeight += 52f;

            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(x - 10f, 10f, width + 20f, panelHeight), Texture2D.whiteTexture);
            GUI.color = previous;

            float y = 16f;
            foreach (var line in lines)
            {
                GUI.Label(new Rect(x, y, width, line.height), line.text, line.style);
                y += line.height;
            }

            var buttonRect = new Rect(x, y + 6f, 220f, 42f);
            if (CanCallShot && GUI.Button(buttonRect, "Pro Gol! (V)", button)) TryCallShot();
            if (State == MatchState.ShotCall && GUI.Button(buttonRect, "Pronto (Espaço)", button)) FinishShotCall();
            if (State == MatchState.PenaltySetup && GUI.Button(buttonRect, "Pronto (Espaço)", button)) FinishPenaltySetup();

            float screenWidth = Screen.width / scale;
            if (pauseTrigger != null && GUI.Button(new Rect(screenWidth - 150f, 16f, 130f, 42f), "Pausa (Esc)", button))
                pauseTrigger.RequestPause();

            GUI.Label(new Rect(x, screenHeight - 34f, 600f, 28f), "R: reiniciar  |  Botão direito: cancelar mira", small);
            GUI.matrix = Matrix4x4.identity;
        }
    }
}
