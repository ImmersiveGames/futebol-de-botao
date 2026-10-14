using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FutebolDeBotao
{
    /// <summary>
    /// Joga um time pela IA. Na vez dela: lê a situação (AiSituation), o cérebro (AiBrain) escolhe pelos objetivos
    /// (marcar, tirar do perigo, fechar o gol, avançar, toque seguro) a ação e a jogada simulada, e a IA executa com
    /// um erro conforme o nível, mostrando a mira antes de soltar. Também posiciona o goleiro quando o adversário
    /// anuncia e escolhe onde pôr o batedor no tiro livre, no pênalti e a bola no tiro de meta.
    /// O peteleco sai pelo AimController, o mesmo caminho do jogador, então as regras são as mesmas.
    /// </summary>
    public sealed class AiPlayer : MonoBehaviour
    {
        private const float PickWindow = 30f;
        private const int KickSimulationsPerAngle = 3;

        private MatchController match;
        /// <summary>O nível como veio do Menu (asset) e a cópia ajustada pela personalidade a cada jogada.</summary>
        private AiDifficulty levelDifficulty;
        private AiDifficulty difficulty;
        /// <summary>Perfil do esquema da IA e a mistura com o ajuste pelo placar que vale nesta jogada.</summary>
        private AiProfile baseProfile;
        private AiProfile profile;
        private string profileLine = string.Empty;
        private AimController aim;
        private MotionMonitor monitor;
        private ShotPlanner planner;
        private ShotSimulator simulator;
        private ShotPlanner attackerPlanner;
        private AiBrain brain;
        private AiLog log;
        private bool busy;

        public TeamSide Side { get; private set; }

        public void Configure(MatchController matchController, TeamSide side, AiDifficulty aiDifficulty)
        {
            // Nova partida: um log novo (o arquivo é por partida).
            if (match != null && log != null) match.Noticed -= log.Noticed;
            log?.Dispose();
            match = matchController;
            Side = side;
            levelDifficulty = aiDifficulty;
            if (difficulty == null)
            {
                difficulty = ScriptableObject.CreateInstance<AiDifficulty>();
                difficulty.hideFlags = HideFlags.DontSave;
            }
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<AiProfile>();
                profile.hideFlags = HideFlags.DontSave;
            }
            baseProfile = AiProfile.Load(AiProfile.StyleOf(MatchSession.Scheme(side, match.Options.discsPerTeam)));
            UpdateProfile();
            log = new AiLog(side, aiDifficulty);
            match.Noticed += log.Noticed;
        }

        private void Start()
        {
            aim = FindAnyObjectByType<AimController>();
            monitor = FindAnyObjectByType<MotionMonitor>();
            if (monitor != null) monitor.Settled += OnSettled;
            if (aim != null)
            {
                aim.Flicked += OnFlicked;
                aim.BallKicked += OnBallKicked;
            }
        }

        private void OnDestroy()
        {
            if (monitor != null) monitor.Settled -= OnSettled;
            if (aim != null)
            {
                aim.Flicked -= OnFlicked;
                aim.BallKicked -= OnBallKicked;
            }
            if (match != null && log != null) match.Noticed -= log.Noticed;
            log?.Dispose();
            log = null;
            simulator?.Dispose();
            simulator = null;
            if (difficulty != null) Destroy(difficulty);
            if (profile != null) Destroy(profile);
        }

        // Jogadas do jogador também vão para o log (a IA solta pelo mesmo AimController, mas na vez dela).
        private void OnFlicked(Disc disc, Vector2 impulse)
        {
            if (match != null && log != null && match.Turn != Side && aim != null)
                log.PlayerShot(disc, impulse, aim.Tuning.maxImpulse, match);
        }

        private void OnBallKicked(Vector2 impulse)
        {
            if (match != null && log != null && match.Turn != Side && aim != null)
                log.PlayerShot(null, impulse, aim.Tuning.ballKickMaxImpulse, match);
        }

        /// <summary>Tudo parou: se era a jogada da IA ou do jogador, o log anota onde a bola ficou.</summary>
        private void OnSettled()
        {
            if (match != null) log?.Finish(match);
        }

        private void Update()
        {
            if (busy || match == null || aim == null || difficulty == null || Time.timeScale <= 0f) return;
            // Aviso na tela: a IA espera como todo mundo.
            if (match.IsHolding) return;
            if (monitor != null && monitor.IsMoving) return;

            switch (match.State)
            {
                case MatchState.Aim:
                case MatchState.ShotAim:
                case MatchState.GoalKick:
                    if (match.Turn != Side) break;
                    UpdateProfile();
                    StartCoroutine(match.IsPlacingGoalKick ? PlaceGoalKickBall() : PlayTouch());
                    break;
                case MatchState.ShotCall:
                    if (match.ActingSide != Side) break;
                    UpdateProfile();
                    StartCoroutine(PlaceKeeper());
                    break;
                case MatchState.PenaltySetup:
                    if (match.Turn != Side) break;
                    UpdateProfile();
                    StartCoroutine(SetUpKicker());
                    break;
            }
        }

        private bool StillValid(int version) => match != null && match.StateVersion == version && !match.IsHolding;

        // ---- Toque ----

        /// <summary>
        /// Um toque: lê a situação, o cérebro (AiBrain) escolhe o objetivo e a jogada, o log registra e a IA executa
        /// com o erro do nível.
        /// </summary>
        private IEnumerator PlayTouch()
        {
            busy = true;
            try
            {
                int version = match.StateVersion;
                yield return new WaitForSeconds(difficulty.thinkSeconds);
                if (!StillValid(version)) yield break;
                EnsureBrain();

                var allUsable = UsableDiscs();
                var usable = allUsable.GetRange(0, Mathf.Min(difficulty.discsToTest, allUsable.Count));
                if (match.State != MatchState.GoalKick && usable.Count == 0)
                {
                    yield return new WaitForSeconds(0.5f);
                    yield break;
                }

                var situation = AiSituation.Read(match, Side, planner, OpponentPlanner(), difficulty, usable, allUsable);
                situation.Profile = profileLine;
                AiDecision decision = null;
                yield return brain.Decide(situation, match, aim.Tuning, difficulty, profile, () => StillValid(version),
                    d => decision = d);
                if (decision == null || !StillValid(version)) yield break;
                log.Decided(decision, situation, match);

                if (decision.Action == AiAction.CallShot)
                {
                    match.TryCallShot();
                    yield break;
                }

                // Erro de execução conforme o nível: é ele que faz a IA errar e, às vezes, cometer falta. No
                // posicionamento só metade do erro de mira.
                var pick = decision.Candidate;
                bool placing = decision.Action != null && !decision.Action.TouchesBall;
                float angleError = Random.Range(-difficulty.angleErrorDegrees, difficulty.angleErrorDegrees) * (placing ? 0.5f : 1f);
                Vector2 direction = Quaternion.Euler(0f, 0f, angleError) * pick.Direction;
                float power = placing
                    ? pick.Power01
                    : Mathf.Clamp(pick.Power01 * (1f + Random.Range(-difficulty.powerError, difficulty.powerError)), 0.1f, 1f);

                yield return ShowAimAndRelease(version, situation.IsBallKick ? null : pick.Disc, direction, power);
            }
            finally
            {
                busy = false;
            }
        }

        private float SimulateAndScore(Disc disc, Vector2 direction, float power01, float maxImpulse, bool ballKick,
            bool goalsCount, int touchesAfter, out bool foul, out bool ownGoal)
        {
            var result = simulator.Simulate(disc != null ? disc.Body : null, direction * (power01 * maxImpulse), Side);
            foul = result.Foul;
            ownGoal = result.GoalOf == Side;
            return planner.Score(result, ballKick, goalsCount, match.Options.goalAfterWallIsValid, touchesAfter, difficulty.foulCaution);
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
            return usable;
        }

        private void EnsureBrain()
        {
            planner ??= new ShotPlanner(Side, aim.Tuning, match.Ball);
            simulator ??= new ShotSimulator(aim.Tuning, match.AllDiscs, match.Ball, match.Keepers);
            planner.FoulsEnabled = match.Options.fouls;
            planner.GoalWidth = match.GoalWidth;
            planner.FarFoulDistance = match.Options.farFoulDistance;
            planner.HasYellow = match.HasYellow(Side);
            planner.AllDiscs = match.AllDiscs;
            planner.Keepers = match.Keepers;
            simulator.FoulsEnabled = match.Options.fouls;
            simulator.GoalWidth = match.GoalWidth;
            brain ??= new AiBrain(Side, planner, simulator);
        }

        /// <summary>O planejador do adversário: chutes que ele teria (goleiro da IA e perigo de passar a vez).</summary>
        private ShotPlanner OpponentPlanner()
        {
            attackerPlanner ??= new ShotPlanner(Opponent(Side), aim.Tuning, match.Ball);
            attackerPlanner.FoulsEnabled = match.Options.fouls;
            attackerPlanner.GoalWidth = match.GoalWidth;
            return attackerPlanner;
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
                    if (bestX != null && keeperThreats != null)
                        log.KeeperPlaced(match, keeperThreats, keeperXs, keeperConceded, bestX.Value, targetX, keeperMs);
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

        // A última conta do goleiro, para o log.
        private List<ShotCandidate> keeperThreats;
        private float[] keeperXs;
        private float[] keeperConceded;
        private double keeperMs;

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

            var threats = OpponentPlanner().GoalThreats(usable, difficulty.keeperShotsToConsider);
            keeperThreats = null;
            if (threats.Count == 0)
            {
                done(null);
                yield break;
            }

            double computeMs = 0;
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

            keeperThreats = threats;
            keeperXs = xs;
            keeperConceded = conceded;
            keeperMs = computeMs;
            done(bestX);
        }

        /// <summary>Fração final do tempo em que o placar puxa a personalidade.</summary>
        private const float LateFraction = 0.25f;
        /// <summary>Diferença de gols que, no fim do jogo, puxa a personalidade por inteiro.</summary>
        private const int FullPullGoals = 2;

        /// <summary>
        /// Personalidade desta jogada: a do esquema, puxada nos 25% finais para o Ofensivo (perdendo) ou o Defensivo
        /// (ganhando). Quanto menos tempo falta e maior a diferença, mais forte. Os números do nível recebem o resultado.
        /// </summary>
        private void UpdateProfile()
        {
            if (match == null || levelDifficulty == null || baseProfile == null || profile == null) return;
            int diff = match.Score(Side) - match.Score(Opponent(Side));
            float window = match.Options.DurationSeconds * LateFraction;
            float late = window > 0f ? Mathf.Clamp01(1f - match.ClockSeconds / window) : 0f;
            float pull = diff == 0 ? 0f : late * Mathf.Clamp01(Mathf.Abs(diff) / (float)FullPullGoals);
            var toward = diff < 0 ? AiStyle.Offensive : AiStyle.Defensive;
            AiProfile.Blend(baseProfile, pull > 0f ? AiProfile.Load(toward) : baseProfile, pull, profile);
            profile.Apply(levelDifficulty, difficulty);
            profileLine = $"perfil={AiProfile.FileName(AiProfile.StyleOf(MatchSession.Scheme(Side, match.Options.discsPerTeam)))}" +
                          (pull > 0f
                              ? string.Format(System.Globalization.CultureInfo.InvariantCulture, " ajuste={0}:{1:0.00}",
                                  AiProfile.FileName(toward), pull)
                              : string.Empty);
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
        // ---- Tiro de meta ----

        private const int GoalKickSpots = 5;
        private const int GoalKickSimulationsPerSpot = 4;

        /// <summary>
        /// Tiro de meta: testa algumas posições da bola na largura da área, simulando os melhores chutes de cada uma, e
        /// deixa a bola onde o melhor chute dá a melhor nota (avança sem bater em ninguém); depois aperta "Pronto" (a
        /// mira e o chute vêm no toque normal). Uma posição por quadro.
        /// </summary>
        private IEnumerator PlaceGoalKickBall()
        {
            busy = true;
            try
            {
                int version = match.StateVersion;
                yield return new WaitForSeconds(difficulty.thinkSeconds * 0.5f);
                if (!StillValid(version) || !match.IsPlacingGoalKick) yield break;
                EnsureBrain();

                Vector2 ballPosition = match.Ball.Body.position;
                float range = match.GoalKickHalfRange;
                simulator.Sync();
                float currentScore = BestBallKick(ballPosition);
                float bestScore = currentScore;
                float bestX = ballPosition.x;
                try
                {
                    for (int i = 0; i < GoalKickSpots; i++)
                    {
                        float x = Mathf.Lerp(-range, range, i / (float)(GoalKickSpots - 1));
                        var spot = new Vector2(x, ballPosition.y);
                        if (Mathf.Abs(x - ballPosition.x) < 0.05f || !BallSpotFree(spot)) continue;
                        float score = BestBallKick(spot);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestX = x;
                        }
                        yield return null;
                        if (!StillValid(version) || !match.IsPlacingGoalKick) yield break;
                    }
                }
                finally
                {
                    simulator.ClearBallOverride();
                }

                // Só troca de lugar se ficar claramente melhor.
                if (Mathf.Abs(bestX - ballPosition.x) > 0.01f && bestScore > currentScore + 2f)
                {
                    match.MoveGoalKickBall(bestX);
                    yield return new WaitForSeconds(0.4f);
                    if (!StillValid(version)) yield break;
                }
                match.ConfirmReady();
            }
            finally
            {
                busy = false;
            }
        }

        /// <summary>Melhor nota simulada de um chute direto na bola como se ela estivesse em <paramref name="ballPosition"/>.</summary>
        private float BestBallKick(Vector2 ballPosition)
        {
            var candidates = planner.BallKickCandidates(ballPosition);
            if (candidates.Count == 0) return float.MinValue;
            candidates.Sort((a, b) => b.PreScore.CompareTo(a.PreScore));
            simulator.OverrideBallPosition(ballPosition);
            float maxImpulse = aim.Tuning.ballKickMaxImpulse;
            float best = float.MinValue;
            int touchesAfter = match.TouchesLeft - 1;
            for (int i = 0; i < Mathf.Min(GoalKickSimulationsPerSpot, candidates.Count); i++)
            {
                var candidate = candidates[i];
                best = Mathf.Max(best, SimulateAndScore(null, candidate.Direction, candidate.Power01, maxImpulse, true, false,
                    touchesAfter, out _, out _));
            }
            return best;
        }

        /// <summary>A bola cabe ali sem encostar em botão (o mesmo teste da partida ao arrastar a bola).</summary>
        private bool BallSpotFree(Vector2 position)
        {
            foreach (var disc in match.AllDiscs)
            {
                if (disc == null || !disc.isActiveAndEnabled) continue;
                float minDistance = match.Ball.Radius + disc.Radius + 0.02f;
                if ((position - disc.Body.position).sqrMagnitude < minDistance * minDistance) return false;
            }
            return true;
        }

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
                                false, scoring, touchesAfter, out _, out _));
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
