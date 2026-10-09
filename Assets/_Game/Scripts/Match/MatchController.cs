using System.Collections.Generic;
using Immersive.Framework.ActivityFlow;
using Immersive.Framework.ActivityRestart;
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
        [Tooltip("Activity Restart Trigger usado pela tecla R e pelo Reiniciar da pausa.")]
        [SerializeField] private ActivityRestartTrigger restartTrigger;
        [Tooltip("Pause Request Trigger usado pelo botão Pausa e pela tecla Esc.")]
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
        private Camera worldCamera;
        private GoalTrigger pendingGoal;
        private bool pendingGoalValid;
        private string message = string.Empty;
        private bool initialized;
        private bool resultRequested;
        private float holdTimer;
        private bool restoreControls;

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
        public bool CanConfirmReady => !IsHolding && State is (MatchState.ShotCall or MatchState.PenaltySetup);
        /// <summary>Um aviso importante está na tela: controles travados, relógio e timer parados, a IA espera.</summary>
        public bool IsHolding => holdTimer > 0f;
        /// <summary>Com um HUD de verdade na cena, o HUD provisório (OnGUI) não é desenhado.</summary>
        public bool ExternalHud { get; set; }
        public PauseRequestTrigger PauseTrigger => pauseTrigger;
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

        public void Configure(MatchOptions matchOptions) => options = matchOptions;

        public void ConfigureFramework(RouteRequestTrigger result, ActivityRestartTrigger restart, PauseRequestTrigger pause)
        {
            resultRoute = result;
            restartTrigger = restart;
            pauseTrigger = pause;
        }

        private static bool Paused => Time.timeScale <= 0f;

        private void Start() => EnsureInitialized();

        // ---- Activity do framework ----

        public void OnActivityContentEntered(ActivityContentLifecycleContext context)
        {
            EnsureInitialized();
            StartMatch();
        }

        public void OnActivityContentExited(ActivityContentLifecycleContext context)
        {
            if (!initialized) return;
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
            if (aimVisuals != null) aimVisuals.Mode = options.aimAssist;
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
            clock = options.DurationSeconds;
            // Início: time sorteado dá a saída.
            KickOff(Random.value < 0.5f ? TeamSide.Bottom : TeamSide.Top);
        }

        /// <summary>Contra a IA, o time de cima (Vermelho) joga sozinho no nível do Menu; em 2 jogadores, ninguém.</summary>
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

            var difficulty = fromMenu || aiDifficulty == null ? AiDifficulty.Load(MatchSession.AiLevel) : aiDifficulty;
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
                    pair.Value[i].PlaceAt(options.FormationPosition(i, pair.Key));
            foreach (var keeper in keepers)
                if (keeper.isActiveAndEnabled) keeper.ResetToCenter();
            ball.ResetTo(Vector2.zero);

            GiveTurn(side);
            message = $"Saída do {TeamName(side)}.";
            Noticed?.Invoke(MatchNotice.KickOff);
            Enter(MatchState.Aim);
        }

        private void Update()
        {
            // Com o Jogador 1 na sessão, o Esc é dele (Pause PlayerInput Binding do framework); sem ninguém, lê o teclado direto.
            var keyboard = Keyboard.current;
            if (!CoachActor.AnyPauseBound && keyboard != null && keyboard.escapeKey.wasPressedThisFrame && pauseTrigger != null)
                pauseTrigger.TogglePause();
            if (Paused) return;

            // R: só quem age agora (na vez da IA, o Reiniciar fica na pausa).
            if (!IsAi(ActingSide) && MatchInput.For(ActingSide).RestartPressedThisFrame && restartTrigger != null)
            {
                restartTrigger.RequestActivityRestart();
                return;
            }

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

            switch (State)
            {
                case MatchState.Aim:
                    if (clock <= 0f) EndMatch();
                    else if (stateTimer <= 0f) PassTurn("Tempo de mira esgotado.");
                    else if (!IsAi(Turn) && MatchInput.For(Turn).ShotCallPressedThisFrame) TryCallShot();
                    break;

                case MatchState.ShotAim:
                case MatchState.GoalKick:
                    if (stateTimer <= 0f) PassTurn("Tempo de mira esgotado.");
                    break;

                case MatchState.ShotCall:
                    bool ready = !IsAi(Opponent(Turn)) && MatchInput.For(Opponent(Turn)).ConfirmPressedThisFrame;
                    if (stateTimer <= 0f || ready) EnterShotAim();
                    break;

                case MatchState.PenaltySetup:
                    UpdatePenaltySetup();
                    bool placed = !IsAi(Turn) && MatchInput.For(Turn).ConfirmPressedThisFrame;
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

        // GDD: o relógio corre só durante a mira.
        private bool ClockRunning => State is MatchState.Aim or MatchState.ShotAim or MatchState.GoalKick;

        private void Enter(MatchState state)
        {
            State = state;
            StateVersion++;

            aim.Cancel();
            ApplyControls();

            stateTimer = state switch
            {
                MatchState.Aim or MatchState.ShotAim or MatchState.GoalKick => options.aimTimeSeconds,
                MatchState.ShotCall => options.shotCallSeconds,
                MatchState.PenaltySetup => options.penaltySetupSeconds,
                MatchState.Goal => goalPauseSeconds,
                MatchState.End => endPauseSeconds,
                _ => 0f
            };
        }

        /// <summary>Liga a mira e o goleiro de quem age no estado atual (nada durante um aviso).</summary>
        private void ApplyControls()
        {
            var state = State;
            bool holding = IsHolding || restoreControls;
            // Na vez da IA o ponteiro não mira.
            aim.InputEnabled = !holding && (state is MatchState.Aim or MatchState.ShotAim or MatchState.GoalKick) && !IsAi(Turn);
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
                return !IsHolding && State == MatchState.Aim && !aim.IsAiming && options.hasGoalkeeper &&
                       InAttackHalf(ball.Body.position, Turn);
            }
        }

        /// <summary>"Pronto": encerra o ajuste do goleiro ou o posicionamento do batedor do pênalti.</summary>
        public void ConfirmReady()
        {
            if (IsHolding) return;
            if (State == MatchState.ShotCall) EnterShotAim();
            else if (State == MatchState.PenaltySetup) FinishPenaltySetup();
        }

        public void RequestPause()
        {
            if (pauseTrigger != null) pauseTrigger.RequestPause();
        }

        public void TryCallShot()
        {
            if (!CanCallShot) return;
            message = $"{TeamName(Turn)}: vai chutar!";
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

            message += $" {TeamName(Opponent(Turn))} ajusta o goleiro.";
            Enter(MatchState.ShotCall);
        }

        private void EnterShotAim()
        {
            message = $"{TeamName(Turn)}: chute!";
            Enter(MatchState.ShotAim);
        }

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
            shotWasCalled = false;
            shooter = null;
            touchesLeft--;
            message = string.Empty;
            Enter(MatchState.Moving);
        }

        private void OnFlicked(Disc disc, Vector2 impulse)
        {
            if (State != MatchState.Aim && State != MatchState.ShotAim) return;
            shotWasCalled = State == MatchState.ShotAim;
            shooter = disc;
            shotBallStart = ball.Body.position;
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
            var owner = KeeperBall.Owner(ball.Body.position, ball.Radius, keepers, options.hasGoalkeeper);
            if (owner == null) return false;
            message = "Bola do goleiro.";
            SetupGoalKick(owner.Value);
            Hold();
            return true;
        }

        private void EndShot()
        {
            if (shooter != null) shooter.EndShot();
            shooter = null;
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
            GiveTurn(Opponent(Turn));
            message = string.IsNullOrEmpty(reason) ? $"Vez do {TeamName(Turn)}." : $"{reason} Vez do {TeamName(Turn)}.";
            Enter(MatchState.Aim);
        }

        // ---- Falta ----

        private void OnFouled(Disc offender, Disc victim, Vector2 victimPosition)
        {
            if (State != MatchState.Moving || offender != shooter || !options.fouls) return;
            CommitFoul(offender, victim, victimPosition);
        }

        /// <summary>
        /// Companheiro empurrado pelo botão do peteleco acertou um adversário antes de alguém tocar a bola: também é
        /// falta (não vale usar um botão para empurrar o outro em cima do adversário).
        /// </summary>
        private void OnHitOpponent(Disc disc, Disc victim, Vector2 victimPosition)
        {
            if (State != MatchState.Moving || shooter == null || !options.fouls) return;
            if (disc == shooter || disc.Side != shooter.Side || ball.LastTouchSide != null) return;
            CommitFoul(disc, victim, victimPosition);
        }

        private void CommitFoul(Disc offender, Disc victim, Vector2 victimPosition)
        {
            // Para investigar falta marcada errado: mostra o que o jogo sabia no momento.
            Debug.Log($"[Falta] {offender.name} ({TeamName(offender.Side)}) acertou {victim.name}. " +
                      $"Último toque na bola neste peteleco: {(ball.LastDiscTouch != null ? ball.LastDiscTouch.name : "ninguém")}.");

            EndShot();
            monitor.StopWatching();
            monitor.FreezeAll();

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
            if (count == 1)
            {
                Noticed?.Invoke(MatchNotice.YellowCard);
                message += $" Cartão amarelo para o {TeamName(side)}!";
                return;
            }

            int onField = 0;
            foreach (var disc in discs[side])
                if (disc.isActiveAndEnabled) onField++;
            offender.gameObject.SetActive(false);
            Noticed?.Invoke(MatchNotice.RedCard);
            if (onField > 1)
            {
                message += $" Cartão vermelho para o {TeamName(side)}: {offender.name} expulso!";
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
            message = $"W.O.! O {TeamName(loser)} ficou sem botões. Vitória do {TeamName(winner)}!";
            Noticed?.Invoke(MatchNotice.FinalWhistle);
            MatchSession.LastResult = new MatchResult(Score(TeamSide.Bottom), Score(TeamSide.Top), true);
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
            offender.PlaceAt(FindFreeSpot(options.FormationPosition(index, offender.Side), offender, victim, clearance));

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

            message = $"Falta do {TeamName(offender.Side)}! Tiro livre para o {TeamName(victim.Side)}: posicione o batedor.";
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
                if (attackers[i] != victim) attackers[i].PlaceAt(options.FormationPosition(i, attacking));

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
            message = $"Pênalti para o {TeamName(attacking)}! Posicione o botão.";
            Enter(MatchState.PenaltySetup);
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

        /// <summary>Arrastar (ou setas/A-D) gira o batedor em volta da bola (no pênalti, dentro do arco).</summary>
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

            float axis = input.KeeperAxis;
            if (axis != 0f) TryPlaceKicker(disc, PenaltyAngle(disc) + axis * 90f * Time.deltaTime);
        }

        private void FinishPenaltySetup()
        {
            if (setupIsPenalty)
            {
                message = $"Pênalti: {TeamName(Turn)} vai chutar!";
                BeginShotCall();
            }
            else if (InAttackHalf(ball.Body.position, Turn))
            {
                // GDD: tiro livre no campo de ataque é chute anunciado.
                message = $"Tiro livre: {TeamName(Turn)} vai chutar!";
                BeginShotCall();
            }
            else
            {
                message = $"Tiro livre do {TeamName(Turn)}.";
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
            pendingGoal = goal;

            // Gol contra (último toque de um botão de quem defende) vale sempre.
            // Os outros só valem num "Vai chutar" (sem goleiro não precisa) e, por padrão, sem a bola tocar a parede.
            var lastDisc = enteredBall.LastDiscTouch;
            bool ownGoal = lastDisc != null && lastDisc.Side == goal.DefendingSide;
            bool wallOk = options.goalAfterWallIsValid || !enteredBall.TouchedWallSinceShot;
            bool called = shotWasCalled || !options.hasGoalkeeper;
            pendingGoalValid = ownGoal || (called && wallOk);

            var scorer = Opponent(goal.DefendingSide);
            if (pendingGoalValid)
            {
                score[(int)scorer]++;
                message = ownGoal ? $"Gol contra! Ponto do {TeamName(scorer)}." : $"GOL do {TeamName(scorer)}!";
            }
            else
            {
                message = called
                    ? "Gol anulado: a bola tocou a parede lateral."
                    : "Gol anulado: não avisou o \"Vai chutar\".";
            }

            Noticed?.Invoke(pendingGoalValid ? MatchNotice.Goal : MatchNotice.GoalAnnulled);
            Enter(MatchState.Goal);
            // O aviso já é a pausa do gol: acabou, segue direto para a saída ou o tiro de meta.
            if (options.noticeHoldSeconds > 0f) stateTimer = 0f;
            Hold();
        }

        private void ResolveGoal()
        {
            var defending = pendingGoal.DefendingSide;
            pendingGoal = null;

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
            message += $" Tiro de meta do {TeamName(defending)}: arraste a bola.";
            Enter(MatchState.GoalKick);
        }

        /// <summary>Todos os botões na formação inicial; quem cair em cima da bola vai para o lugar livre mais perto.</summary>
        private void ResetFormation()
        {
            foreach (var pair in discs)
                for (int i = 0; i < pair.Value.Count && i < options.discsPerTeam; i++)
                    pair.Value[i].PlaceAt(options.FormationPosition(i, pair.Key));

            foreach (var disc in allDiscs)
            {
                if (!disc.isActiveAndEnabled) continue;
                float clearance = disc.Radius + ball.Radius + 0.3f;
                if ((disc.Body.position - ball.Body.position).sqrMagnitude >= clearance * clearance) continue;
                disc.PlaceAt(FindFreeSpot(disc.Body.position, disc, null, clearance));
            }
        }

        private void EndMatch()
        {
            int bottom = Score(TeamSide.Bottom);
            int top = Score(TeamSide.Top);
            message = bottom == top ? "Fim de jogo: empate!" : $"Fim de jogo: vitória do {TeamName(bottom > top ? TeamSide.Bottom : TeamSide.Top)}!";
            MatchSession.LastResult = new MatchResult(bottom, top);
            Noticed?.Invoke(MatchNotice.FinalWhistle);
            Enter(MatchState.End);
        }

        private void RequestResult()
        {
            resultRequested = true;
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

        public static string TeamName(TeamSide side) => side == TeamSide.Bottom ? "Azul" : "Vermelho";

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
                ($"Azul {Score(TeamSide.Bottom)} x {Score(TeamSide.Top)} Vermelho", big, 40f),
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
            if (CanCallShot && GUI.Button(buttonRect, "Vai chutar (V)", button)) TryCallShot();
            if (State == MatchState.ShotCall && GUI.Button(buttonRect, "Pronto (Espaço)", button)) EnterShotAim();
            if (State == MatchState.PenaltySetup && GUI.Button(buttonRect, "Pronto (Espaço)", button)) FinishPenaltySetup();

            float screenWidth = Screen.width / scale;
            if (pauseTrigger != null && GUI.Button(new Rect(screenWidth - 150f, 16f, 130f, 42f), "Pausa (Esc)", button))
                pauseTrigger.RequestPause();

            GUI.Label(new Rect(x, screenHeight - 34f, 600f, 28f), "R: reiniciar  |  Botão direito: cancelar mira", small);
            GUI.matrix = Matrix4x4.identity;
        }
    }
}
