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
    /// Numa jogada ruim pode só reposicionar um botão. Também anuncia o "Vai chutar", posiciona o goleiro quando o
    /// adversário anuncia e escolhe onde pôr o batedor no tiro livre e no pênalti.
    /// O peteleco sai pelo AimController, o mesmo caminho do jogador, então as regras são as mesmas.
    /// </summary>
    public sealed class AiPlayer : MonoBehaviour
    {
        private const float FrameBudgetMs = 4f;
        private const float PickWindow = 30f;
        private const int RobustChecks = 3;
        private const int PositionSimulations = 6;
        private const int KickSimulationsPerAngle = 3;

        private struct Scored
        {
            public ShotCandidate Candidate;
            public float Score;
            /// <summary>A jogada simulada (sem erro) dá falta.</summary>
            public bool Foul;
            /// <summary>Com o erro de ângulo para um dos lados, dá falta.</summary>
            public bool FoulNearby;
        }

        private MatchController match;
        private AiDifficulty difficulty;
        private AimController aim;
        private MotionMonitor monitor;
        private ShotPlanner planner;
        private ShotSimulator simulator;
        private ShotPlanner attackerPlanner;
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
                    if (match.Turn == Side) StartCoroutine(SetUpKicker());
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
                var queue = Diversify(candidates, difficulty.simulations);
                var scored = new List<Scored>(queue.Count);

                simulator.Sync();
                int stepsBefore = simulator.TotalSteps;
                int fouls = 0;
                double computeMs = 0;
                var frame = Stopwatch.StartNew();

                foreach (var candidate in queue)
                {
                    var watch = Stopwatch.StartNew();
                    float score = SimulateAndScore(candidate.Disc, candidate.Direction, candidate.Power01, maxImpulse,
                        ballKick, goalsCount, touchesAfter, out bool foul);
                    computeMs += watch.Elapsed.TotalMilliseconds;
                    if (foul) fouls++;
                    scored.Add(new Scored { Candidate = candidate, Score = score, Foul = foul });

                    // Divide a conta em quadros para o jogo não travar.
                    if (frame.Elapsed.TotalMilliseconds > FrameBudgetMs)
                    {
                        yield return null;
                        if (!StillValid(version)) yield break;
                        frame.Restart();
                    }
                }

                // Robustez: as melhores também são testadas com o erro de ângulo para cada lado e ficam com a média.
                // Assim a IA foge de jogadas em que um errinho vira falta (bola de raspão com adversário atrás).
                scored.Sort((a, b) => b.Score.CompareTo(a.Score));
                float spread = difficulty.angleErrorDegrees * 0.75f;
                if (spread > 0.01f)
                {
                    for (int i = 0; i < Mathf.Min(RobustChecks, scored.Count); i++)
                    {
                        var entry = scored[i];
                        if (entry.Foul) continue;
                        float sum = entry.Score;
                        foreach (float sign in new[] { -1f, 1f })
                        {
                            var watch = Stopwatch.StartNew();
                            Vector2 tilted = Quaternion.Euler(0f, 0f, sign * spread) * entry.Candidate.Direction;
                            sum += SimulateAndScore(entry.Candidate.Disc, tilted, entry.Candidate.Power01, maxImpulse,
                                ballKick, goalsCount, touchesAfter, out bool foul);
                            computeMs += watch.Elapsed.TotalMilliseconds;
                            if (foul)
                            {
                                fouls++;
                                entry.FoulNearby = true;
                            }
                        }
                        entry.Score = sum / 3f;
                        scored[i] = entry;
                    }
                    scored.Sort((a, b) => b.Score.CompareTo(a.Score));
                }

                // Sorteio entre as melhores, mas uma jogada que a simulação já mostrou com falta só sai se for a melhor.
                float best = scored[0].Score;
                var pool = new List<Scored> { scored[0] };
                for (int i = 1; i < scored.Count && pool.Count < difficulty.pickAmongBest; i++)
                    if (!scored[i].Foul && scored[i].Score >= best - PickWindow) pool.Add(scored[i]);
                var pick = pool[Random.Range(0, pool.Count)];

                // Jogada ruim (falta provável ou nada de bom na bola): às vezes a IA só se posiciona. Mais no Difícil.
                bool poor = pick.Foul || pick.FoulNearby || pick.Score < difficulty.poorShotScore;
                if (poor && state == MatchState.Aim && match.FreeKickDisc == null && Random.value < difficulty.repositionChance &&
                    TryFindPosition(usable, maxImpulse, out var move, out float moveScore))
                {
                    Debug.Log($"[IA] {state}: jogada ruim (nota {pick.Score:0}{(pick.Foul ? ", com falta" : pick.FoulNearby ? ", falta se errar" : string.Empty)}); " +
                              $"posiciona {move.Disc.name} sem tocar na bola, nota {moveScore:0}.");
                    float moveError = Random.Range(-difficulty.angleErrorDegrees, difficulty.angleErrorDegrees) * 0.5f;
                    Vector2 moveDirection = Quaternion.Euler(0f, 0f, moveError) * move.Direction;
                    yield return ShowAimAndRelease(version, move.Disc, moveDirection, move.Power01);
                    yield break;
                }

                // Erro de execução conforme o nível: é ele que faz a IA errar e, às vezes, cometer falta.
                float angleError = Random.Range(-difficulty.angleErrorDegrees, difficulty.angleErrorDegrees);
                Vector2 direction = Quaternion.Euler(0f, 0f, angleError) * pick.Candidate.Direction;
                float power = Mathf.Clamp(pick.Candidate.Power01 * (1f + Random.Range(-difficulty.powerError, difficulty.powerError)), 0.1f, 1f);

                int discsTried = 0;
                var seen = new HashSet<Disc>();
                foreach (var entry in scored)
                    if (entry.Candidate.Disc != null && seen.Add(entry.Candidate.Disc)) discsTried++;
                Debug.Log($"[IA] {state}: pensou {computeMs:0.0} ms ({queue.Count} jogadas de {discsTried} botões, " +
                          $"{simulator.TotalSteps - stepsBefore} passos de física, {fouls} simulações com falta). " +
                          $"Escolhida: {(pick.Candidate.Disc != null ? pick.Candidate.Disc.name : "bola")}, nota {pick.Score:0}" +
                          $"{(pick.Foul ? " (com falta)" : pick.FoulNearby ? " (falta se errar)" : string.Empty)}; melhor {best:0}.");

                yield return ShowAimAndRelease(version, ballKick ? null : pick.Candidate.Disc, direction, power);
            }
            finally
            {
                busy = false;
            }
        }

        private float SimulateAndScore(Disc disc, Vector2 direction, float power01, float maxImpulse, bool ballKick,
            bool goalsCount, int touchesAfter, out bool foul)
        {
            var result = simulator.Simulate(disc != null ? disc.Body : null, direction * (power01 * maxImpulse), Side);
            foul = result.Foul;
            return planner.Score(result, ballKick, goalsCount, match.Options.goalAfterWallIsValid, touchesAfter, difficulty.foulCaution);
        }

        /// <summary>Melhor posicionamento que a simulação confirma (sem tocar na bola nem em adversário).</summary>
        private bool TryFindPosition(List<Disc> usable, float maxImpulse, out ShotCandidate best, out float bestScore)
        {
            best = default;
            bestScore = float.MinValue;
            var candidates = planner.PositionCandidates(usable, match.AllDiscs);
            for (int i = 0; i < Mathf.Min(PositionSimulations, candidates.Count); i++)
            {
                var candidate = candidates[i];
                var result = simulator.Simulate(candidate.Disc.Body, candidate.Direction * (candidate.Power01 * maxImpulse), Side);
                float? score = planner.ScorePosition(result, candidate);
                if (score == null || score.Value <= bestScore) continue;
                best = candidate;
                bestScore = score.Value;
            }
            return best.Disc != null;
        }

        /// <summary>
        /// Escolhe quem vai para a mesa simulada: primeiro a melhor jogada de cada botão (para a IA sempre comparar
        /// botões diferentes), depois as outras pela nota da geometria.
        /// </summary>
        private static List<ShotCandidate> Diversify(List<ShotCandidate> sorted, int count)
        {
            var queue = new List<ShotCandidate>();
            var taken = new bool[sorted.Count];
            var discs = new HashSet<Disc>();
            for (int i = 0; i < sorted.Count && queue.Count < count; i++)
            {
                var disc = sorted[i].Disc;
                if (disc == null || !discs.Add(disc)) continue;
                queue.Add(sorted[i]);
                taken[i] = true;
            }
            for (int i = 0; i < sorted.Count && queue.Count < count; i++)
                if (!taken[i]) queue.Add(sorted[i]);
            return queue;
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
                    float? bestX = null;
                    yield return BestKeeperX(keeper, version, x => bestX = x);
                    if (!StillValid(version)) yield break;
                    float targetX = (bestX ?? PredictShotX()) + Random.Range(-difficulty.keeperError, difficulty.keeperError);
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

        private const int KeeperPositions = 9;

        /// <summary>
        /// Onde o goleiro deixa passar menos gols: pega os chutes a gol mais prováveis do adversário (quantos, conforme
        /// o nível), simula cada um contra várias posições do goleiro e fica com a que sofre menos. Chutes mais fáceis
        /// pesam mais. Entre posições empatadas, fica no meio delas. Uma posição por quadro, para não travar.
        /// </summary>
        private IEnumerator BestKeeperX(Goalkeeper keeper, int version, System.Action<float?> done)
        {
            EnsureBrain();
            var attacker = Opponent(Side);
            var usable = new List<Disc>();
            foreach (var disc in match.AllDiscs)
                if (match.CanUse(disc)) usable.Add(disc);

            attackerPlanner ??= new ShotPlanner(attacker, aim.Tuning, match.Ball);
            var threats = attackerPlanner.GoalThreats(usable, difficulty.keeperShotsToConsider);
            if (threats.Count == 0)
            {
                done(null);
                yield break;
            }

            double computeMs = 0;
            int stepsBefore = simulator.TotalSteps;
            simulator.Sync();
            float maxImpulse = aim.Tuning.maxImpulse;
            var conceded = new float[KeeperPositions];
            var xs = new float[KeeperPositions];

            try
            {
                for (int p = 0; p < KeeperPositions; p++)
                {
                    var watch = Stopwatch.StartNew();
                    xs[p] = Mathf.Lerp(keeper.MinX, keeper.MaxX, p / (KeeperPositions - 1f));
                    simulator.OverrideKeeperX(Side, xs[p]);
                    foreach (var threat in threats)
                    {
                        var result = simulator.Simulate(threat.Disc.Body, threat.Direction * (threat.Power01 * maxImpulse), attacker);
                        if (result.GoalOf != Side) continue;
                        float weight = 1f / (0.5f + threat.Power01);
                        if (threat.BallBlocked) weight *= 0.7f;
                        conceded[p] += weight;
                    }

                    computeMs += watch.Elapsed.TotalMilliseconds;
                    yield return null;
                    if (!StillValid(version))
                    {
                        done(null);
                        yield break;
                    }
                }
            }
            finally
            {
                simulator.ClearKeeperOverride();
            }

            // Entre as posições que sofrem menos, fica no meio do maior bloco seguido (empate: o mais perto do centro).
            float least = Mathf.Min(conceded);
            float bestX = 0f;
            int bestLength = 0;
            float center = (keeper.MinX + keeper.MaxX) * 0.5f;
            for (int start = 0; start < KeeperPositions; start++)
            {
                if (conceded[start] > least + 0.001f || (start > 0 && conceded[start - 1] <= least + 0.001f)) continue;
                int end = start;
                while (end + 1 < KeeperPositions && conceded[end + 1] <= least + 0.001f) end++;
                float middle = (xs[start] + xs[end]) * 0.5f;
                int length = end - start + 1;
                if (length > bestLength || (length == bestLength && Mathf.Abs(middle - center) < Mathf.Abs(bestX - center)))
                {
                    bestLength = length;
                    bestX = middle;
                }
            }

            Debug.Log($"[IA] Goleiro: {threats.Count} chutes x {KeeperPositions} posições, pensou {computeMs:0.0} ms " +
                      $"({simulator.TotalSteps - stepsBefore} passos de física). Fica em x={bestX:0.00} (peso dos gols que ainda entram: {least:0.0}).");
            done(bestX);
        }

        private static TeamSide Opponent(TeamSide side) => side == TeamSide.Bottom ? TeamSide.Top : TeamSide.Bottom;

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

        // ---- Tiro livre e pênalti ----

        /// <summary>
        /// Posiciona o batedor: para cada alvo, põe o botão bem atrás da bola naquela direção (se o lugar estiver livre),
        /// simula os chutes e fica com o melhor ângulo. Um ângulo por quadro, então dá para ver o batedor girando.
        /// </summary>
        private IEnumerator SetUpKicker()
        {
            busy = true;
            try
            {
                int version = match.StateVersion;
                yield return new WaitForSeconds(difficulty.thinkSeconds);
                if (!StillValid(version)) yield break;
                EnsureBrain();

                var kicker = match.FreeKickDisc;
                if (kicker != null)
                {
                    Vector2 ballPosition = match.Ball.Body.position;
                    bool hasKeeper = match.Options.hasGoalkeeper;
                    // O chute do pênalti e do tiro livre no ataque é anunciado; sem goleiro todo gol vale.
                    bool scoring = !hasKeeper || match.IsPenaltySetup || FieldLayout.AttackDirection(Side).y * ballPosition.y > 0f;
                    bool calledShot = hasKeeper && scoring;
                    int touchesAfter = calledShot ? 0 : match.TouchesLeft - 1;
                    float maxImpulse = aim.Tuning.maxImpulse;
                    var usable = new List<Disc> { kicker };
                    var angles = new List<(float angle, float score)>();

                    simulator.Sync();
                    foreach (var target in planner.KickTargets(scoring))
                    {
                        Vector2 toTarget = target - ballPosition;
                        if (toTarget.sqrMagnitude < 0.01f) continue;
                        float angle = match.KickerAngleFor(toTarget.normalized);
                        if (!match.TryPlaceKicker(angle)) continue;

                        var candidates = planner.DiscCandidates(usable, scoring);
                        candidates.Sort((a, b) => b.PreScore.CompareTo(a.PreScore));
                        float best = float.MinValue;
                        for (int i = 0; i < Mathf.Min(KickSimulationsPerAngle, candidates.Count); i++)
                        {
                            var candidate = candidates[i];
                            best = Mathf.Max(best, SimulateAndScore(kicker, candidate.Direction, candidate.Power01, maxImpulse,
                                false, scoring, touchesAfter, out _));
                        }
                        if (candidates.Count > 0) angles.Add((angle, best));

                        yield return null;
                        if (!StillValid(version)) yield break;
                    }

                    if (angles.Count > 0)
                    {
                        angles.Sort((a, b) => b.score.CompareTo(a.score));
                        int pool = 1;
                        while (pool < angles.Count && pool < difficulty.pickAmongBest && angles[pool].score >= angles[0].score - PickWindow) pool++;
                        var pick = angles[Random.Range(0, pool)];
                        match.TryPlaceKicker(pick.angle);
                        Debug.Log($"[IA] {(match.IsPenaltySetup ? "Pênalti" : "Tiro livre")}: testou {angles.Count} posições do batedor, " +
                                  $"escolheu {pick.angle:0}° (nota {pick.score:0}; melhor {angles[0].score:0}).");
                    }
                }

                yield return new WaitForSeconds(0.6f);
                if (StillValid(version)) match.ConfirmReady();
            }
            finally
            {
                busy = false;
            }
        }
    }
}
