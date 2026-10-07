using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FutebolDeBotao
{
    /// <summary>
    /// Máquina de estados da partida: Saída, Mira, Vai chutar, Movimento, Gol e Fim.
    /// Cuida da vez, dos toques, do relógio, do placar e do HUD provisório.
    /// Perda da vez por falta e por último toque do adversário entram no próximo passo da fase 2.
    /// </summary>
    public sealed class MatchController : MonoBehaviour
    {
        [SerializeField] private MatchOptions options;
        [SerializeField] private float goalPauseSeconds = 1.5f;

        private readonly Dictionary<TeamSide, List<Disc>> discs = new();
        private readonly int[] score = new int[2];
        private Ball ball;
        private Goalkeeper[] keepers;
        private AimController aim;
        private AimVisuals aimVisuals;
        private MotionMonitor monitor;
        private GoalkeeperControl keeperControl;

        private float clock;
        private float stateTimer;
        private int touchesLeft;
        private bool shotWasCalled;
        private GoalTrigger pendingGoal;
        private bool pendingGoalValid;
        private string message = string.Empty;

        public MatchState State { get; private set; }
        public TeamSide Turn { get; private set; }
        public int TouchesLeft => touchesLeft;
        public float ClockSeconds => clock;
        public int Score(TeamSide side) => score[(int)side];

        public void Configure(MatchOptions matchOptions) => options = matchOptions;

        private void Start()
        {
            if (options == null) options = MatchOptions.CreateDefault();

            ball = FindAnyObjectByType<Ball>();
            keepers = FindObjectsByType<Goalkeeper>(FindObjectsSortMode.None);
            aim = FindAnyObjectByType<AimController>();
            aimVisuals = FindAnyObjectByType<AimVisuals>();
            monitor = FindAnyObjectByType<MotionMonitor>();
            keeperControl = FindAnyObjectByType<GoalkeeperControl>();
            if (keeperControl == null) keeperControl = gameObject.AddComponent<GoalkeeperControl>();

            CollectDiscs();
            ApplyOptions();

            aim.Flicked += OnFlicked;
            monitor.Settled += OnSettled;
            foreach (var goal in FindObjectsByType<GoalTrigger>(FindObjectsSortMode.None)) goal.BallEntered += OnBallEntered;

            StartMatch();
        }

        private void CollectDiscs()
        {
            discs[TeamSide.Bottom] = new List<Disc>();
            discs[TeamSide.Top] = new List<Disc>();
            foreach (var disc in FindObjectsByType<Disc>(FindObjectsSortMode.None)) discs[disc.Side].Add(disc);
            foreach (var list in discs.Values) list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }

        private void ApplyOptions()
        {
            foreach (var list in discs.Values)
                for (int i = 0; i < list.Count; i++)
                    list[i].gameObject.SetActive(i < options.discsPerTeam);

            foreach (var keeper in keepers) keeper.gameObject.SetActive(options.hasGoalkeeper);
            if (aimVisuals != null) aimVisuals.Mode = options.aimAssist;
        }

        public void StartMatch()
        {
            score[0] = score[1] = 0;
            clock = options.DurationSeconds;
            KickOff(TeamSide.Bottom);
        }

        private void KickOff(TeamSide side)
        {
            Enter(MatchState.KickOff);
            monitor.StopWatching();

            foreach (var pair in discs)
                for (int i = 0; i < pair.Value.Count && i < options.discsPerTeam; i++)
                    pair.Value[i].PlaceAt(options.FormationPosition(i, pair.Key));
            foreach (var keeper in keepers)
                if (keeper.isActiveAndEnabled) keeper.ResetToCenter();
            ball.ResetTo(Vector2.zero);

            Turn = side;
            touchesLeft = options.touchesPerTurn;
            message = $"Saída do {TeamName(side)}.";
            Enter(MatchState.Aim);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                StartMatch();
                return;
            }

            if (ClockRunning) clock = Mathf.Max(0f, clock - Time.deltaTime);
            stateTimer -= Time.deltaTime;

            switch (State)
            {
                case MatchState.Aim:
                    if (clock <= 0f) EndMatch();
                    else if (stateTimer <= 0f) PassTurn("Tempo de mira esgotado.");
                    else if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame) TryCallShot();
                    break;

                case MatchState.ShotAim:
                    if (stateTimer <= 0f) PassTurn("Tempo de mira esgotado.");
                    break;

                case MatchState.ShotCall:
                    bool ready = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
                    if (stateTimer <= 0f || ready) EnterShotAim();
                    break;

                case MatchState.Goal:
                    if (stateTimer <= 0f) ResolveGoal();
                    break;
            }
        }

        private bool ClockRunning =>
            State is MatchState.Aim or MatchState.ShotCall or MatchState.ShotAim or MatchState.Moving;

        private void Enter(MatchState state)
        {
            State = state;

            aim.Cancel();
            aim.InputEnabled = state is MatchState.Aim or MatchState.ShotAim;
            aim.CanSelect = state == MatchState.ShotAim
                ? disc => disc.Side == Turn && InAttackHalf(disc)
                : disc => disc.Side == Turn;

            keeperControl.ControlledSide = state == MatchState.ShotCall ? Opponent(Turn) : null;

            stateTimer = state switch
            {
                MatchState.Aim or MatchState.ShotAim => options.aimTimeSeconds,
                MatchState.ShotCall => options.shotCallSeconds,
                MatchState.Goal => goalPauseSeconds,
                _ => 0f
            };
        }

        // ---- Vai chutar ----

        public bool CanCallShot
        {
            get
            {
                if (State != MatchState.Aim || aim.IsAiming || !options.hasGoalkeeper) return false;
                foreach (var disc in discs[Turn])
                    if (disc.isActiveAndEnabled && InAttackHalf(disc)) return true;
                return false;
            }
        }

        public void TryCallShot()
        {
            if (!CanCallShot) return;
            message = $"{TeamName(Turn)}: vai chutar! {TeamName(Opponent(Turn))} ajusta o goleiro.";
            Enter(MatchState.ShotCall);
        }

        private void EnterShotAim()
        {
            message = $"{TeamName(Turn)}: chute com um botão do campo de ataque.";
            Enter(MatchState.ShotAim);
        }

        private bool InAttackHalf(Disc disc)
        {
            float y = disc.Body.position.y;
            return disc.Side == TeamSide.Bottom ? y > 0f : y < 0f;
        }

        // ---- Movimento ----

        private void OnFlicked(Disc disc, Vector2 impulse)
        {
            if (State != MatchState.Aim && State != MatchState.ShotAim) return;
            shotWasCalled = State == MatchState.ShotAim;
            touchesLeft--;
            message = string.Empty;
            Enter(MatchState.Moving);
        }

        private void OnSettled()
        {
            if (State != MatchState.Moving) return;

            if (clock <= 0f) EndMatch();
            else if (shotWasCalled) PassTurn("Chute defendido.");
            else if (touchesLeft > 0) Enter(MatchState.Aim);
            else PassTurn(null);
        }

        private void PassTurn(string reason)
        {
            Turn = Opponent(Turn);
            touchesLeft = options.touchesPerTurn;
            message = string.IsNullOrEmpty(reason) ? $"Vez do {TeamName(Turn)}." : $"{reason} Vez do {TeamName(Turn)}.";
            Enter(MatchState.Aim);
        }

        // ---- Gol ----

        private void OnBallEntered(GoalTrigger goal, Ball enteredBall)
        {
            if (State != MatchState.Moving) return;

            monitor.StopWatching();
            pendingGoal = goal;
            pendingGoalValid = options.goalAfterWallIsValid || !enteredBall.TouchedWallSinceShot;

            if (pendingGoalValid)
            {
                score[(int)Opponent(goal.DefendingSide)]++;
                message = $"GOL do {TeamName(Opponent(goal.DefendingSide))}!";
            }
            else
            {
                message = "Gol anulado: a bola tocou a parede.";
            }

            Enter(MatchState.Goal);
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

            // Gol anulado: bola na frente do gol de quem defendeu, e a vez é dele.
            monitor.FreezeAll();
            Vector2 towardCenter = defending == TeamSide.Bottom ? Vector2.up : Vector2.down;
            var keeper = FindKeeper(defending);
            Vector2 goalMouth = keeper != null
                ? (Vector2)keeper.transform.position
                : new Vector2(0f, ball.Body.position.y);
            ball.ResetTo(goalMouth + towardCenter * (keeper != null ? 0.8f : 1.6f));

            Turn = defending;
            touchesLeft = options.touchesPerTurn;
            message += $" Bola do {TeamName(defending)}.";
            Enter(MatchState.Aim);
        }

        private void EndMatch()
        {
            int bottom = Score(TeamSide.Bottom);
            int top = Score(TeamSide.Top);
            message = bottom == top ? "Fim de jogo: empate!" : $"Fim de jogo: vitória do {TeamName(bottom > top ? TeamSide.Bottom : TeamSide.Top)}!";
            Enter(MatchState.End);
        }

        // ---- Utilidades ----

        private Goalkeeper FindKeeper(TeamSide side)
        {
            foreach (var keeper in keepers)
                if (keeper.isActiveAndEnabled && keeper.Side == side) return keeper;
            return null;
        }

        private static TeamSide Opponent(TeamSide side) => side == TeamSide.Bottom ? TeamSide.Top : TeamSide.Bottom;

        private static string TeamName(TeamSide side) => side == TeamSide.Bottom ? "Azul" : "Vermelho";

        // ---- HUD provisório ----

        private void OnGUI()
        {
            var big = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            var small = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };

            int minutes = Mathf.FloorToInt(clock / 60f);
            int seconds = Mathf.CeilToInt(clock % 60f);
            if (seconds == 60) { minutes++; seconds = 0; }

            float y = 12f;
            GUI.Label(new Rect(16, y, 400, 32), $"Azul {Score(TeamSide.Bottom)} x {Score(TeamSide.Top)} Vermelho", big); y += 32;
            GUI.Label(new Rect(16, y, 400, 26), $"Tempo {minutes}:{seconds:00}", small); y += 26;

            if (State is MatchState.Aim or MatchState.ShotAim or MatchState.Moving)
            {
                GUI.Label(new Rect(16, y, 400, 26), $"Vez: {TeamName(Turn)}  |  toques restantes: {touchesLeft}", small); y += 26;
            }

            if (State is MatchState.Aim or MatchState.ShotAim)
            {
                GUI.Label(new Rect(16, y, 400, 26), $"Mira: {Mathf.CeilToInt(Mathf.Max(0f, stateTimer))} s", small); y += 26;
            }

            if (!string.IsNullOrEmpty(message))
            {
                GUI.Label(new Rect(16, y, 320, 48), message, small); y += 50;
            }

            if (CanCallShot && GUI.Button(new Rect(16, y, 160, 34), "Vai chutar (V)")) TryCallShot();

            if (State == MatchState.ShotCall)
            {
                GUI.Label(new Rect(16, y, 320, 26), $"Goleiro: arraste para os lados ({Mathf.CeilToInt(stateTimer)} s)", small); y += 28;
                if (GUI.Button(new Rect(16, y, 160, 34), "Pronto (Espaço)")) EnterShotAim();
            }

            if (State == MatchState.End && GUI.Button(new Rect(16, y, 160, 34), "Jogar de novo")) StartMatch();

            GUI.Label(new Rect(16, Screen.height - 34, 600, 26), "R: reiniciar  |  Botão direito: cancelar mira", small);
        }
    }
}
