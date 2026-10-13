using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FutebolDeBotao
{
    /// <summary>O que a IA decidiu fazer num toque, e por quê (para executar e para o log).</summary>
    public sealed class AiDecision
    {
        /// <summary>Objetivo atendido. Nulo: nenhuma ação rodou e a IA bateu reto na bola.</summary>
        public AiGoal Goal;
        public AiAction Action;
        public ShotCandidate Candidate;
        public AiVerdict Verdict;
        /// <summary>Como a jogada escolhida terminou na simulação (sem erro).</summary>
        public SimResult Expected;
        public bool HasExpected;
        /// <summary>Nenhum objetivo aceitou a melhor jogada: ficou a do objetivo mais alto.</summary>
        public bool NoGoalAccepted;
        /// <summary>Objetivos com prioridade, na ordem tentada ("Marcar 90 > Avançar 40 > ...").</summary>
        public string GoalsLine;
        /// <summary>Objetivos tentados que não aceitaram a jogada deles.</summary>
        public string Rejected;
        /// <summary>O melhor toque seguinte que a IA viu para a jogada escolhida (vazio se não pensou).</summary>
        public string FollowLine = string.Empty;
        public int Simulations;
        public int Steps;
        public double Milliseconds;
    }

    /// <summary>
    /// Decide o toque da IA por objetivos: ordena os objetivos pela prioridade da situação, e para cada um pega a
    /// ação que pode rodar, simula as jogadas dela, dá nota com a conta do objetivo (testando o erro do nível nas
    /// melhores) e sorteia entre as melhores seguras. Fica no primeiro objetivo que aceitar a jogada; as simulações de
    /// uma ação são reaproveitadas pelos outros objetivos que usam a mesma ação.
    /// </summary>
    public sealed class AiBrain
    {
        private const float FrameBudgetMs = 4f;
        private const float PickWindow = 30f;
        private const int RobustChecks = 3;
        /// <summary>Se as melhores têm risco, testa mais algumas procurando uma segura.</summary>
        private const int MaxRobustChecks = 6;
        private const int PositionSimulations = 6;
        private const int MaxPositionSimulations = 14;
        /// <summary>Quanto o toque seguinte pesa na nota (o futuro é incerto).</summary>
        private const float FollowWeight = 0.7f;
        /// <summary>Nota do toque seguinte quando não existe jogada nenhuma (bola sem saída).</summary>
        private const float DeadEndScore = -20f;

        /// <summary>Uma jogada simulada de uma ação: o resultado sem erro e, se já testadas, as versões com erro.</summary>
        private sealed class Trial
        {
            public ShotCandidate Candidate;
            public SimResult Main;
            public SimResult[] Variants;
            /// <summary>Nota do melhor toque seguinte (nulo: ainda não pensou).</summary>
            public float? Follow;
            public string FollowLine;
        }

        private struct Rated
        {
            public Trial Trial;
            public AiVerdict Verdict;
            /// <summary>Nota da jogada sem erro, sem o toque seguinte.</summary>
            public float Immediate;
            public bool Checked;
        }

        private readonly TeamSide side;
        private readonly ShotPlanner planner;
        private readonly ShotSimulator simulator;
        private readonly Dictionary<AiAction, List<Trial>> trials = new();
        private PhysicsTuning currentTuning;
        private readonly Stopwatch frame = new();
        private Func<bool> stillValid;
        private bool aborted;
        private int simulations;
        private double computeMs;

        public AiBrain(TeamSide aiSide, ShotPlanner shotPlanner, ShotSimulator shotSimulator)
        {
            side = aiSide;
            planner = shotPlanner;
            simulator = shotSimulator;
        }

        /// <summary>
        /// Decide o toque. <paramref name="done"/> recebe a decisão; não é chamado se a partida mudou no meio da conta
        /// (<paramref name="valid"/> falso).
        /// </summary>
        public IEnumerator Decide(AiSituation situation, MatchController match, PhysicsTuning tuning, AiDifficulty difficulty,
            Func<bool> valid, Action<AiDecision> done)
        {
            trials.Clear();
            currentTuning = tuning;
            stillValid = valid;
            aborted = false;
            simulations = 0;
            computeMs = 0;
            int stepsBefore = simulator.TotalSteps;
            float maxImpulse = situation.IsBallKick ? tuning.ballKickMaxImpulse : tuning.maxImpulse;
            var context = new AiScoreContext(planner, match.Options.goalAfterWallIsValid, difficulty.foulCaution,
                difficulty.wallStuckPenalty);

            // Objetivos que valem agora, do mais alto para o mais baixo (empate: a ordem da lista).
            var goals = new List<(AiGoal Goal, float Priority)>();
            foreach (var goal in AiGoal.All)
            {
                float priority = goal.Priority(situation);
                if (priority > 0f) goals.Add((goal, priority));
            }
            goals = goals.OrderByDescending(g => g.Priority).ToList();

            var line = new StringBuilder();
            foreach (var (goal, priority) in goals)
                line.Append(line.Length > 0 ? " > " : string.Empty).Append(goal.Name).Append(' ').Append(priority.ToString("0"));

            var rejected = new StringBuilder();
            AiDecision fallback = null;
            AiDecision chosen = null;
            simulator.Sync();
            frame.Restart();

            foreach (var (goal, _) in goals)
            {
                AiAction action = null;
                foreach (var candidate in goal.Actions)
                    if (candidate.CanRun(situation))
                    {
                        action = candidate;
                        break;
                    }
                if (action == null) continue;

                if (action.IsInstant)
                {
                    // Vai chutar: só anuncia se algum chute passa do goleiro que o adversário vai posicionar.
                    float chance = 0f;
                    string callLine = string.Empty;
                    yield return ShotCallChance(situation, match, difficulty, (c, l) => { chance = c; callLine = l; });
                    if (aborted) yield break;
                    if (chance >= difficulty.callShotChance)
                    {
                        chosen = new AiDecision { Goal = goal, Action = action, FollowLine = callLine };
                        break;
                    }
                    rejected.Append(rejected.Length > 0 ? ", " : string.Empty).Append(goal.Name).Append(' ').Append(callLine);
                    continue;
                }

                if (!trials.TryGetValue(action, out var list))
                {
                    yield return SimulateAction(action, situation, match, difficulty, maxImpulse, l => list = l);
                    if (aborted) yield break;
                    trials[action] = list;
                }
                if (list.Count == 0)
                {
                    rejected.Append(rejected.Length > 0 ? ", " : string.Empty).Append(goal.Name).Append(" sem jogada");
                    continue;
                }

                Rated? pick = null;
                yield return Rate(goal, action, list, situation, context, difficulty, maxImpulse, r => pick = r);
                if (aborted) yield break;
                if (pick == null)
                {
                    rejected.Append(rejected.Length > 0 ? ", " : string.Empty).Append(goal.Name).Append(" sem jogada");
                    continue;
                }

                var decision = new AiDecision
                {
                    Goal = goal,
                    Action = action,
                    Candidate = pick.Value.Trial.Candidate,
                    Verdict = pick.Value.Verdict,
                    Expected = pick.Value.Trial.Main,
                    HasExpected = true,
                    FollowLine = pick.Value.Verdict.HasFollow ? pick.Value.Trial.FollowLine ?? string.Empty : string.Empty
                };
                if (goal.Accepts(pick.Value.Verdict, situation, difficulty))
                {
                    chosen = decision;
                    break;
                }
                rejected.Append(rejected.Length > 0 ? ", " : string.Empty).Append(goal.Name).Append(' ')
                    .Append(pick.Value.Verdict.Score.ToString("0"));
                if (pick.Value.Verdict.Foul) rejected.Append(" (falta)");
                // Se nenhum aceitar, fica a do objetivo mais alto; jogada sem falta certa ganha de uma com falta.
                if (fallback == null || (fallback.Verdict.Foul && !decision.Verdict.Foul)) fallback = decision;
            }

            if (chosen == null && fallback != null)
            {
                chosen = fallback;
                chosen.NoGoalAccepted = true;
            }
            chosen ??= new AiDecision { Candidate = Straight(situation, match) };

            chosen.GoalsLine = line.ToString();
            chosen.Rejected = rejected.ToString();
            chosen.Simulations = simulations;
            chosen.Steps = simulator.TotalSteps - stepsBefore;
            chosen.Milliseconds = computeMs;
            done(chosen);
        }

        /// <summary>Gera as jogadas da ação pela geometria e simula as melhores (sem erro).</summary>
        private IEnumerator SimulateAction(AiAction action, AiSituation situation, MatchController match, AiDifficulty difficulty,
            float maxImpulse, Action<List<Trial>> done)
        {
            var candidates = action.Candidates(situation, planner, match);
            var list = new List<Trial>();
            if (action.TouchesBall)
            {
                candidates.Sort((a, b) => b.PreScore.CompareTo(a.PreScore));
                foreach (var candidate in Diversify(candidates, difficulty.simulations))
                {
                    list.Add(new Trial { Candidate = candidate, Main = Simulate(candidate.Disc, candidate.Direction, candidate.Power01, maxImpulse) });
                    if (Budget()) yield return Pause();
                    if (aborted) yield break;
                }
            }
            else
            {
                // Posicionamento: segue na lista (já em ordem de nota) até ter algumas que não tocam em nada.
                int valid = 0;
                for (int i = 0; i < candidates.Count && valid < PositionSimulations && i < MaxPositionSimulations; i++)
                {
                    var candidate = candidates[i];
                    var main = Simulate(candidate.Disc, candidate.Direction, candidate.Power01, maxImpulse);
                    list.Add(new Trial { Candidate = candidate, Main = main });
                    if (planner.ScorePosition(main, candidate) != null) valid++;
                    if (Budget()) yield return Pause();
                    if (aborted) yield break;
                }
            }
            done(list);
        }

        /// <summary>
        /// Nota de cada jogada para o objetivo; as melhores também são testadas com o erro de mira para cada lado e
        /// com o erro de força para mais e para menos, e ficam com a média. Devolve a escolhida entre as melhores.
        /// </summary>
        private IEnumerator Rate(AiGoal goal, AiAction action, List<Trial> list, AiSituation situation, AiScoreContext context,
            AiDifficulty difficulty, float maxImpulse, Action<Rated?> done)
        {
            var rated = new List<Rated>(list.Count);
            foreach (var trial in list)
            {
                float? score = goal.Score(trial.Main, trial.Candidate, action, situation, context);
                if (score == null) continue;
                rated.Add(new Rated
                {
                    Trial = trial,
                    Immediate = score.Value,
                    Verdict = new AiVerdict
                    {
                        Score = score.Value,
                        Foul = trial.Main.Foul,
                        OwnGoalRisk = trial.Main.GoalOf == side,
                        FoulChance = trial.Main.Foul ? 1f : 0f,
                        OwnGoalChance = trial.Main.GoalOf == side ? 1f : 0f,
                        // Posicionar sempre passa a vez: não conta como perder a bola.
                        LoseChance = action.TouchesBall && Loses(trial.Main, situation) ? 1f : 0f
                    },
                    // Posicionamento não toca na bola: não precisa do teste com erro.
                    Checked = !action.TouchesBall
                });
            }
            if (rated.Count == 0)
            {
                done(null);
                yield break;
            }
            rated.Sort((a, b) => b.Verdict.Score.CompareTo(a.Verdict.Score));

            // Pensar o toque seguinte: as melhores jogadas que ficam com a bola também são testadas um toque à frente,
            // para não levar a bola a um canto sem saída.
            if (action.TouchesBall && goal.PlansAhead && difficulty.planAhead > 0 && situation.TouchesLeft > 1)
            {
                int planned = 0;
                for (int i = 0; i < rated.Count && planned < difficulty.planAhead; i++)
                {
                    var entry = rated[i];
                    var main = entry.Trial.Main;
                    if (main.Foul || main.GoalOf != null || main.End == null || Loses(main, situation)) continue;
                    if (entry.Trial.Follow == null)
                    {
                        yield return FollowUp(entry.Trial, situation, context, difficulty);
                        if (aborted) yield break;
                    }
                    entry.Verdict.Follow = entry.Trial.Follow.GetValueOrDefault() * FollowWeight;
                    entry.Verdict.HasFollow = true;
                    entry.Verdict.Score += entry.Verdict.Follow;
                    rated[i] = entry;
                    planned++;
                }
                rated.Sort((a, b) => b.Verdict.Score.CompareTo(a.Verdict.Score));
            }

            if (action.TouchesBall)
            {
                int checks = 0;
                bool foundSafe = false;
                for (int i = 0; i < rated.Count && checks < MaxRobustChecks; i++)
                {
                    if (checks >= RobustChecks && foundSafe) break;
                    var entry = rated[i];
                    checks++;
                    entry.Checked = true;
                    if (!entry.Trial.Main.Foul)
                    {
                        if (entry.Trial.Variants == null)
                        {
                            entry.Trial.Variants = Variants(entry.Trial.Candidate, difficulty, maxImpulse);
                            if (Budget()) yield return Pause();
                            if (aborted) yield break;
                        }
                        float sum = entry.Immediate;
                        int count = 1;
                        int fouls = 0, ownGoals = entry.Verdict.OwnGoalRisk ? 1 : 0, loses = entry.Verdict.LoseChance > 0f ? 1 : 0;
                        foreach (var variant in entry.Trial.Variants)
                        {
                            if (variant.Foul)
                            {
                                entry.Verdict.FoulNearby = true;
                                fouls++;
                            }
                            if (variant.GoalOf == side)
                            {
                                entry.Verdict.OwnGoalRisk = true;
                                ownGoals++;
                            }
                            if (Loses(variant, situation)) loses++;
                            float? score = goal.Score(variant, entry.Trial.Candidate, action, situation, context);
                            if (score == null) continue;
                            sum += score.Value;
                            count++;
                        }
                        entry.Verdict.Score = sum / count + entry.Verdict.Follow;
                        float samples = entry.Trial.Variants.Length + 1;
                        entry.Verdict.FoulChance = fouls / samples;
                        entry.Verdict.OwnGoalChance = ownGoals / samples;
                        entry.Verdict.LoseChance = loses / samples;
                    }
                    if (entry.Verdict.SafeFor(difficulty)) foundSafe = true;
                    rated[i] = entry;
                }
                rated.Sort((a, b) =>
                {
                    if (a.Checked != b.Checked) return a.Checked ? -1 : 1;
                    return b.Verdict.Score.CompareTo(a.Verdict.Score);
                });
            }

            // Sorteio entre as melhores testadas e seguras. Arriscada só se nenhuma testada for segura (aí o objetivo
            // decide se aceita).
            int first = 0;
            for (int i = 0; i < rated.Count && rated[i].Checked; i++)
            {
                if (!rated[i].Verdict.SafeFor(difficulty)) continue;
                first = i;
                break;
            }
            float best = rated[first].Verdict.Score;
            var pool = new List<Rated> { rated[first] };
            for (int i = 0; i < rated.Count && rated[i].Checked && pool.Count < difficulty.pickAmongBest; i++)
                if (i != first && rated[i].Verdict.SafeFor(difficulty) && rated[i].Verdict.Score >= best - PickWindow) pool.Add(rated[i]);
            done(pool[Random.Range(0, pool.Count)]);
        }

        private const int KeeperPositions = 9;

        /// <summary>
        /// Chance de gol se anunciar o Vai chutar agora. A IA imagina o goleiro do adversário onde menos chutes dela
        /// entram (a mesma conta do goleiro da IA), e testa cada chute contra ele com o erro do nível. Devolve a chance
        /// do melhor chute e uma linha para o log.
        /// </summary>
        private IEnumerator ShotCallChance(AiSituation situation, MatchController match, AiDifficulty difficulty,
            Action<float, string> done)
        {
            var opponent = side == TeamSide.Bottom ? TeamSide.Top : TeamSide.Bottom;
            Goalkeeper keeper = null;
            foreach (var k in match.Keepers)
                if (k != null && k.isActiveAndEnabled && k.Side == opponent) keeper = k;
            var shots = planner.GoalThreats(situation.AllUsable, difficulty.keeperShotsToConsider);
            if (keeper == null || shots.Count == 0)
            {
                done(0f, shots.Count == 0 ? "sem chute livre" : "sem goleiro");
                yield break;
            }

            float maxImpulse = currentTuning.maxImpulse;
            bool wallOk = match.Options.goalAfterWallIsValid;
            bool Scores(in SimResult r) => r.GoalOf == opponent && (wallOk || !r.BallTouchedWall);

            float bestChance = 0f, keeperX = 0f;
            string bestShot = string.Empty;
            try
            {
                // Onde o goleiro dele fica: a posição em que entram menos chutes (chutes fáceis pesam mais).
                float least = float.MaxValue;
                float center = (keeper.MinX + keeper.MaxX) * 0.5f;
                for (int p = 0; p < KeeperPositions; p++)
                {
                    float x = Mathf.Lerp(keeper.MinX, keeper.MaxX, p / (KeeperPositions - 1f));
                    simulator.OverrideKeeperX(opponent, x);
                    float conceded = 0f;
                    foreach (var shot in shots)
                    {
                        if (!Scores(Simulate(shot.Disc, shot.Direction, shot.Power01, maxImpulse))) continue;
                        float weight = 1f / (0.5f + shot.Power01);
                        if (shot.BallBlocked) weight *= 0.7f;
                        conceded += weight;
                    }
                    if (conceded < least - 0.001f || (Mathf.Abs(conceded - least) <= 0.001f && Mathf.Abs(x - center) < Mathf.Abs(keeperX - center)))
                    {
                        least = conceded;
                        keeperX = x;
                    }
                    if (Budget()) yield return Pause();
                    if (aborted) yield break;
                }

                // Contra esse goleiro, cada chute com o erro do nível.
                simulator.OverrideKeeperX(opponent, keeperX);
                foreach (var shot in shots)
                {
                    int goals = Scores(Simulate(shot.Disc, shot.Direction, shot.Power01, maxImpulse)) ? 1 : 0;
                    var variants = Variants(shot, difficulty, maxImpulse);
                    foreach (var variant in variants)
                        if (Scores(variant)) goals++;
                    float chance = goals / (variants.Length + 1f);
                    if (chance > bestChance)
                    {
                        bestChance = chance;
                        bestShot = shot.Disc.name;
                    }
                    if (Budget()) yield return Pause();
                    if (aborted) yield break;
                }
            }
            finally
            {
                simulator.ClearKeeperOverride();
            }

            string line = $"chute contra o goleiro em x={keeperX:0.00}: {Mathf.RoundToInt(bestChance * 100f)}%" +
                          (bestShot.Length > 0 ? $" ({bestShot})" : string.Empty);
            done(bestChance, line);
        }

        /// <summary>
        /// O melhor toque seguinte a partir de onde a jogada parou: gera jogadas pela geometria nas posições simuladas,
        /// simula algumas (sem erro) e fica com a melhor nota. Sem jogada nenhuma: bola sem saída.
        /// </summary>
        private IEnumerator FollowUp(Trial trial, AiSituation situation, AiScoreContext context, AiDifficulty difficulty)
        {
            var state = trial.Main.End;
            int touchesAfter = situation.TouchesLeft - 2;
            bool attackHalf = FieldLayout.AttackDirection(side).y * state.Ball.y > 0f;
            bool goalsCount = !situation.HasGoalkeeper && attackHalf;
            var discs = ShotPlanner.Nearest(state, situation.TeamDiscs, difficulty.discsToTest);
            var candidates = planner.FollowUpCandidates(state, discs, goalsCount);
            if (Budget()) yield return Pause();
            if (aborted) yield break;
            float best = DeadEndScore;
            string line = "sem saída";
            foreach (var candidate in Diversify(candidates, difficulty.followUpSimulations))
            {
                var watch = Stopwatch.StartNew();
                var next = simulator.Simulate(candidate.Disc.Body, candidate.Direction * (candidate.Power01 * currentTuning.maxImpulse),
                    side, state);
                computeMs += watch.Elapsed.TotalMilliseconds;
                simulations++;

                float score = planner.Score(next, false, goalsCount, context.GoalAfterWallIsValid, touchesAfter,
                    context.FoulCaution, state.Ball);
                if (touchesAfter > 0 && !next.Foul && next.GoalOf == null && next.LastTouchSide == side &&
                    planner.HasShotSetup(next.End, situation.TeamDiscs, situation.ShotCallDistance))
                    score += ShotPlanner.ShotSetupBonus;
                if (score > best)
                {
                    best = score;
                    line = $"{candidate.Disc.name} nota {score:0}" + (next.GoalOf != null && next.GoalOf != side ? " (gol)" : string.Empty);
                }
                if (Budget()) yield return Pause();
                if (aborted) yield break;
            }
            trial.Follow = best;
            trial.FollowLine = line;
        }

        /// <summary>A vez passa: o último toque não fica com a IA (no tiro de meta, ninguém tocar conta como dela).</summary>
        private bool Loses(in SimResult result, AiSituation situation)
        {
            if (result.Foul) return true;
            if (situation.IsBallKick && result.LastTouchSide == null) return false;
            return result.LastTouchSide != side;
        }

        /// <summary>A jogada com o erro do nível: mira para cada lado, força para mais e para menos, e combinadas.</summary>
        private SimResult[] Variants(in ShotCandidate candidate, AiDifficulty difficulty, float maxImpulse)
        {
            float spread = difficulty.angleErrorDegrees * 0.75f;
            float powerSpread = difficulty.powerError * 0.75f;
            var variants = new List<SimResult>(6);
            foreach (var (angle, powerScale) in new[]
                     {
                         (-spread, 1f), (spread, 1f), (0f, 1f - powerSpread), (0f, 1f + powerSpread),
                         (-spread, 1f + powerSpread), (spread, 1f - powerSpread)
                     })
            {
                if (Mathf.Abs(angle) < 0.01f && Mathf.Abs(powerScale - 1f) < 0.001f) continue;
                Vector2 tilted = Quaternion.Euler(0f, 0f, angle) * candidate.Direction;
                float power01 = Mathf.Clamp(candidate.Power01 * powerScale, 0.1f, 1f);
                variants.Add(Simulate(candidate.Disc, tilted, power01, maxImpulse));
            }
            return variants.ToArray();
        }

        private SimResult Simulate(Disc disc, Vector2 direction, float power01, float maxImpulse)
        {
            var watch = Stopwatch.StartNew();
            var result = simulator.Simulate(disc != null ? disc.Body : null, direction * (power01 * maxImpulse), side);
            computeMs += watch.Elapsed.TotalMilliseconds;
            simulations++;
            return result;
        }

        /// <summary>Passou do tempo do quadro: hora de dividir a conta para o jogo não travar.</summary>
        private bool Budget() => frame.Elapsed.TotalMilliseconds > FrameBudgetMs;

        private IEnumerator Pause()
        {
            yield return null;
            if (stillValid != null && !stillValid()) aborted = true;
            frame.Restart();
        }

        /// <summary>
        /// Escolhe quem vai para a mesa simulada: primeiro a melhor jogada de cada botão (para a IA sempre comparar
        /// botões diferentes), depois as outras pela nota da geometria.
        /// </summary>
        private static List<ShotCandidate> Diversify(List<ShotCandidate> sorted, int count)
        {
            var queue = new List<ShotCandidate>();
            var taken = new bool[sorted.Count];
            // Uma parte fica para as jogadas para o lado e para trás, que perdem na nota da geometria mas salvam quando
            // a bola está atrás dos botões.
            bool hasWide = sorted.Exists(c => c.Wide);
            int wideSlots = hasWide ? Mathf.Max(1, count / 4) : 0;
            Take(sorted, taken, queue, count - wideSlots, false);
            Take(sorted, taken, queue, count, true);
            for (int i = 0; i < sorted.Count && queue.Count < count; i++)
                if (!taken[i]) queue.Add(sorted[i]);
            return queue;
        }

        /// <summary>Até a fila ter <paramref name="limit"/>: a melhor jogada de cada botão, do tipo pedido.</summary>
        private static void Take(List<ShotCandidate> sorted, bool[] taken, List<ShotCandidate> queue, int limit, bool wide)
        {
            var discs = new HashSet<Disc>();
            for (int i = 0; i < sorted.Count && queue.Count < limit; i++)
            {
                var disc = sorted[i].Disc;
                if (taken[i] || sorted[i].Wide != wide || disc == null || !discs.Add(disc)) continue;
                queue.Add(sorted[i]);
                taken[i] = true;
            }
        }

        /// <summary>Nenhuma jogada serviu: bate reto na bola com força média.</summary>
        private ShotCandidate Straight(AiSituation situation, MatchController match)
        {
            if (situation.IsBallKick || situation.Usable.Count == 0)
                return new ShotCandidate { Direction = FieldLayout.AttackDirection(side), Power01 = 0.6f };

            var disc = situation.Usable[0];
            var toBall = match.Ball.Body.position - disc.Body.position;
            return new ShotCandidate { Disc = disc, Direction = toBall.normalized, Power01 = 0.5f };
        }
    }
}
