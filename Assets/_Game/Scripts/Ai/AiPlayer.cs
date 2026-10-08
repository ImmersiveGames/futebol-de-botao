using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FutebolDeBotao
{
    /// <summary>
    /// Joga um time pela IA. Na vez dela: escolhe os botões perto da bola, gera jogadas por geometria, simula as
    /// melhores na mesa invisível, dá nota, escolhe com um erro conforme o nível e mostra a mira antes de soltar.
    /// Também anuncia o "Vai chutar", posiciona o goleiro quando o adversário anuncia e confirma o pênalti.
    /// O peteleco sai pelo AimController, o mesmo caminho do jogador, então as regras são as mesmas.
    /// </summary>
    public sealed class AiPlayer : MonoBehaviour
    {
        private const float FrameBudgetMs = 4f;
        private const float PickWindow = 30f;

        private MatchController match;
        private AiDifficulty difficulty;
        private AimController aim;
        private MotionMonitor monitor;
        private ShotPlanner planner;
        private ShotSimulator simulator;
        private bool busy;

        public TeamSide Side { get; private set; }

        public void Configure(MatchController matchController, TeamSide side, AiDifficulty aiDifficulty)
        {
            match = matchController;
            Side = side;
            difficulty = aiDifficulty;
        }

        private void Start()
        {
            aim = FindAnyObjectByType<AimController>();
            monitor = FindAnyObjectByType<MotionMonitor>();
        }

        private void OnDestroy()
        {
            simulator?.Dispose();
            simulator = null;
        }

        private void Update()
        {
            if (busy || match == null || aim == null || difficulty == null || Time.timeScale <= 0f) return;
            if (monitor != null && monitor.IsMoving) return;

            switch (match.State)
            {
                case MatchState.Aim:
                case MatchState.ShotAim:
                case MatchState.GoalKick:
                    if (match.Turn == Side) StartCoroutine(PlayTouch());
                    break;
                case MatchState.ShotCall:
                    if (match.ActingSide == Side) StartCoroutine(PlaceKeeper());
                    break;
                case MatchState.PenaltySetup:
                    if (match.Turn == Side) StartCoroutine(ConfirmPenalty());
                    break;
            }
        }

        private bool StillValid(int version) => match != null && match.StateVersion == version;

        // ---- Toque ----

        private IEnumerator PlayTouch()
        {
            busy = true;
            try
            {
                int version = match.StateVersion;
                var state = match.State;
                yield return new WaitForSeconds(difficulty.thinkSeconds);
                if (!StillValid(version)) yield break;
                EnsureBrain();

                bool ballKick = state == MatchState.GoalKick;
                var usable = UsableDiscs();
                if (!ballKick && usable.Count == 0)
                {
                    yield return new WaitForSeconds(0.5f);
                    yield break;
                }

                if (state == MatchState.Aim && match.CanCallShot && planner.ShouldCallShot(usable, difficulty.shotCallDistance))
                {
                    Debug.Log("[IA] Anunciou o \"Vai chutar\".");
                    match.TryCallShot();
                    yield break;
                }

                // Gol só vale num chute anunciado (ou num jogo sem goleiro).
                bool goalsCount = state == MatchState.ShotAim || !match.Options.hasGoalkeeper;
                var candidates = ballKick ? planner.BallKickCandidates() : planner.DiscCandidates(usable, goalsCount);
                if (candidates.Count == 0) candidates.Add(Fallback(usable, ballKick));
                candidates.Sort((a, b) => b.PreScore.CompareTo(a.PreScore));

                var tuning = aim.Tuning;
                float maxImpulse = ballKick ? tuning.ballKickMaxImpulse : tuning.maxImpulse;
                int touchesAfter = match.TouchesLeft - 1;
                int simulations = Mathf.Min(difficulty.simulations, candidates.Count);
                var scored = new List<(ShotCandidate candidate, float score)>(simulations);

                simulator.Sync();
                int stepsBefore = simulator.TotalSteps;
                double computeMs = 0;
                var frame = Stopwatch.StartNew();

                for (int i = 0; i < simulations; i++)
                {
                    var candidate = candidates[i];
                    var watch = Stopwatch.StartNew();
                    var result = simulator.Simulate(candidate.Disc != null ? candidate.Disc.Body : null,
                        candidate.Direction * (candidate.Power01 * maxImpulse), Side);
                    float score = planner.Score(result, ballKick, goalsCount, match.Options.goalAfterWallIsValid,
                        touchesAfter, difficulty.foulCaution);
                    computeMs += watch.Elapsed.TotalMilliseconds;
                    scored.Add((candidate, score));

                    // Divide a conta em quadros para o jogo não travar.
                    if (frame.Elapsed.TotalMilliseconds > FrameBudgetMs)
                    {
                        yield return null;
                        if (!StillValid(version)) yield break;
                        frame.Restart();
                    }
                }

                scored.Sort((a, b) => b.score.CompareTo(a.score));
                float best = scored[0].score;
                int pool = 1;
                while (pool < scored.Count && pool < difficulty.pickAmongBest && scored[pool].score >= best - PickWindow) pool++;
                var pick = scored[Random.Range(0, pool)];

                // Erro de execução conforme o nível: é ele que faz a IA errar e, às vezes, cometer falta.
                float angleError = Random.Range(-difficulty.angleErrorDegrees, difficulty.angleErrorDegrees);
                Vector2 direction = Quaternion.Euler(0f, 0f, angleError) * pick.candidate.Direction;
                float power = Mathf.Clamp(pick.candidate.Power01 * (1f + Random.Range(-difficulty.powerError, difficulty.powerError)), 0.1f, 1f);

                Debug.Log($"[IA] {state}: pensou {computeMs:0.0} ms ({simulations} jogadas simuladas, " +
                          $"{simulator.TotalSteps - stepsBefore} passos de física). Nota da escolhida {pick.score:0} (melhor {best:0}).");

                yield return ShowAimAndRelease(version, ballKick ? null : pick.candidate.Disc, direction, power);
            }
            finally
            {
                busy = false;
            }
        }

        /// <summary>Mostra a mira e a barra de força enchendo, como um jogador arrastando, e solta.</summary>
        private IEnumerator ShowAimAndRelease(int version, Disc disc, Vector2 direction, float power)
        {
            aim.BeginScripted(disc);
            float duration = Mathf.Max(difficulty.aimSeconds, 0.01f);
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / duration);
                aim.SetScripted(direction, power * Mathf.SmoothStep(0f, 1f, t));
                yield return null;
                // Se o estado mudou (pausa com reinício, fim do tempo), a partida já cancelou a mira.
                if (!StillValid(version)) yield break;
            }
            aim.ReleaseScripted();
        }

        private List<Disc> UsableDiscs()
        {
            Vector2 ballPosition = match.Ball.Body.position;
            var usable = new List<Disc>();
            foreach (var disc in match.AllDiscs)
                if (match.CanUse(disc)) usable.Add(disc);
            usable.Sort((a, b) => (a.Body.position - ballPosition).sqrMagnitude.CompareTo((b.Body.position - ballPosition).sqrMagnitude));
            if (usable.Count > difficulty.discsToTest) usable.RemoveRange(difficulty.discsToTest, usable.Count - difficulty.discsToTest);
            return usable;
        }

        /// <summary>Nenhuma jogada passou na geometria: bate reto na bola com força média.</summary>
        private ShotCandidate Fallback(List<Disc> usable, bool ballKick)
        {
            if (ballKick || usable.Count == 0)
                return new ShotCandidate { Direction = FieldLayout.AttackDirection(Side), Power01 = 0.6f };

            var disc = usable[0];
            var toBall = match.Ball.Body.position - disc.Body.position;
            return new ShotCandidate { Disc = disc, Direction = toBall.normalized, Power01 = 0.5f };
        }

        private void EnsureBrain()
        {
            planner ??= new ShotPlanner(Side, aim.Tuning, match.Ball);
            simulator ??= new ShotSimulator(aim.Tuning, match.AllDiscs, match.Ball, match.Keepers);
        }

        // ---- Goleiro ----

        /// <summary>O adversário anunciou o chute: põe o goleiro onde a bola deve passar, com erro conforme o nível.</summary>
        private IEnumerator PlaceKeeper()
        {
            busy = true;
            try
            {
                int version = match.StateVersion;
                yield return new WaitForSeconds(0.4f);
                if (!StillValid(version)) yield break;

                var keeper = FindKeeper();
                float travelSeconds = 0f;
                if (keeper != null)
                {
                    float targetX = PredictShotX() + Random.Range(-difficulty.keeperError, difficulty.keeperError);
                    keeper.SetTargetX(targetX);
                    float speed = aim.Tuning != null ? Mathf.Max(aim.Tuning.goalkeeperSpeed, 0.1f) : 3f;
                    travelSeconds = Mathf.Min(Mathf.Abs(targetX - keeper.transform.position.x) / speed, 2.5f);
                }

                yield return new WaitForSeconds(travelSeconds + 0.4f);
                if (StillValid(version)) match.ConfirmReady();
            }
            finally
            {
                busy = false;
            }
        }

        /// <summary>Onde a bola cruza a linha do gol se o batedor bater reto nela.</summary>
        private float PredictShotX()
        {
            Vector2 ballPosition = match.Ball.Body.position;
            var shooter = match.FreeKickDisc;
            if (shooter == null)
            {
                float bestDistance = float.MaxValue;
                foreach (var disc in match.AllDiscs)
                {
                    if (!match.CanUse(disc)) continue;
                    float distance = (disc.Body.position - ballPosition).sqrMagnitude;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        shooter = disc;
                    }
                }
            }
            if (shooter == null) return ballPosition.x;

            Vector2 direction = (ballPosition - shooter.Body.position).normalized;
            float goalY = FieldLayout.GoalLineY(Side);
            float toGoal = goalY - ballPosition.y;
            if (Mathf.Abs(direction.y) < 0.1f || Mathf.Sign(direction.y) != Mathf.Sign(toGoal)) return ballPosition.x;

            float x = ballPosition.x + direction.x * (toGoal / direction.y);
            return Mathf.Clamp(x, -FieldLayout.GoalWidth * 0.5f, FieldLayout.GoalWidth * 0.5f);
        }

        private Goalkeeper FindKeeper()
        {
            foreach (var keeper in match.Keepers)
                if (keeper != null && keeper.isActiveAndEnabled && keeper.Side == Side) return keeper;
            return null;
        }

        // ---- Pênalti ----

        /// <summary>Por enquanto a IA bate o pênalti com o botão bem atrás da bola.</summary>
        private IEnumerator ConfirmPenalty()
        {
            busy = true;
            try
            {
                int version = match.StateVersion;
                yield return new WaitForSeconds(0.8f);
                if (StillValid(version)) match.ConfirmReady();
            }
            finally
            {
                busy = false;
            }
        }
    }
}
