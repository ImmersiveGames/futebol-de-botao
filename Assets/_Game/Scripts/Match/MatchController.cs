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
        [Tooltip("Multiplica o tamanho do HUD provisório.")]
        [SerializeField, Range(0.5f, 2f)] private float hudScale = 1f;

        private const float ReferenceHeight = 720f;
        private const float ReferenceMinWidth = 400f;

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
            keepers = FindObjectsByType<Goalkeeper>();
            aim = FindAnyObjectByType<AimController>();
            aimVisuals = FindAnyObjectByType<AimVisuals>();
            monitor = FindAnyObjectByType<MotionMonitor>();
            keeperControl = FindAnyObjectByType<GoalkeeperControl>();
            if (keeperControl == null) keeperControl = gameObject.AddComponent<GoalkeeperControl>();

            CollectDiscs();
            ApplyOptions();

            aim.Flicked += OnFlicked;
            monitor.Settled += OnSettled;
            foreach (var goal in FindObjectsByType<GoalTrigger>()) goal.BallEntered += OnBallEntered;

            StartMatch();
        }

        private void CollectDiscs()
        {
            discs[TeamSide.Bottom] = new List<Disc>();
            discs[TeamSide.Top] = new List<Disc>();
            foreach (var disc in FindObjectsByType<Disc>()) discs[disc.Side].Add(disc);
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
            if (State is MatchState.Aim or MatchState.ShotAim or MatchState.Moving)
                lines.Add(($"Vez: {TeamName(Turn)}  |  toques: {touchesLeft}", small, 28f));
            if (State is MatchState.Aim or MatchState.ShotAim)
                lines.Add(($"Mira: {Mathf.CeilToInt(Mathf.Max(0f, stateTimer))} s", small, 28f));
            if (State == MatchState.ShotCall)
                lines.Add(($"Goleiro: arraste para os lados ({Mathf.CeilToInt(stateTimer)} s)", small, 56f));
            if (!string.IsNullOrEmpty(message))
                lines.Add((message, small, small.CalcHeight(new GUIContent(message), width)));

            bool showButton = CanCallShot || State == MatchState.ShotCall || State == MatchState.End;
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
            if (State == MatchState.End && GUI.Button(buttonRect, "Jogar de novo", button)) StartMatch();

            GUI.Label(new Rect(x, screenHeight - 34f, 600f, 28f), "R: reiniciar  |  Botão direito: cancelar mira", small);
            GUI.matrix = Matrix4x4.identity;
        }
    }
}
