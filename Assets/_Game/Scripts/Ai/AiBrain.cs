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

        /// <summary>Uma jogada simulada de uma ação: o resultado sem erro e, se já testadas, as versões com erro.</summary>
        private sealed class Trial
        {
            public ShotCandidate Candidate;
            public SimResult Main;
            public SimResult[] Variants;
        }

        private struct Rated
        {
            public Trial Trial;
            public AiVerdict Verdict;
            public bool Checked;
        }

        private readonly TeamSide side;
        private readonly ShotPlanner planner;
        private readonly ShotSimulator simulator;
        private readonly Dictionary<AiAction, List<Trial>> trials = new();
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
            stillValid = valid;
            aborted = false;
            simulations = 0;
            computeMs = 0;
            int stepsBefore = simulator.TotalSteps;
            float maxImpulse = situation.IsBallKick ? tuning.ballKickMaxImpulse : tuning.maxImpulse;
            var context = new AiScoreContext(planner, match.Options.goalAfterWallIsValid, difficulty.foulCaution);

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
                    chosen = new AiDecision { Goal = goal, Action = action };
                    break;
                }

                if (!trials.TryGetValue(action, out var list))
                {
                    yield return SimulateAction(action, situation, match, difficulty, maxImpulse, l => list = l);
                    if (aborted) yield break;
                    trials[action] = list;
                }
                if (list.Count == 0) continue;

                Rated? pick = null;
                yield return Rate(goal, action, list, situation, context, difficulty, maxImpulse, r => pick = r);
                if (aborted) yield break;
                if (pick == null) continue;

                var decision = new AiDecision
                {
                    Goal = goal,
                    Action = action,
                    Candidate = pick.Value.Trial.Candidate,
                    Verdict = pick.Value.Verdict,
                    Expected = pick.Value.Trial.Main,
                    HasExpected = true
                };
                if (goal.Accepts(pick.Value.Verdict, situation, difficulty))
                {
                    chosen = decision;
                    break;
                }
                rejected.Append(rejected.Length > 0 ? ", " : string.Empty).Append(goal.Name).Append(' ')
                    .Append(pick.Value.Verdict.Score.ToString("0"));
                fallback ??= decision;
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
            List<ShotCandidate> queue;
            if (action.TouchesBall)
            {
                candidates.Sort((a, b) => b.PreScore.CompareTo(a.PreScore));
                queue = Diversify(candidates, difficulty.simulations);
            }
            else
            {
                queue = candidates.GetRange(0, Mathf.Min(PositionSimulations, candidates.Count));
            }

            var list = new List<Trial>(queue.Count);
            foreach (var candidate in queue)
            {
                list.Add(new Trial { Candidate = candidate, Main = Simulate(candidate.Disc, candidate.Direction, candidate.Power01, maxImpulse) });
                if (Budget()) yield return Pause();
                if (aborted) yield break;
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
                    Verdict = new AiVerdict { Score = score.Value, Foul = trial.Main.Foul, OwnGoalRisk = trial.Main.GoalOf == side },
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
                        float sum = entry.Verdict.Score;
                        int count = 1;
                        foreach (var variant in entry.Trial.Variants)
                        {
                            if (variant.Foul) entry.Verdict.FoulNearby = true;
                            if (variant.GoalOf == side) entry.Verdict.OwnGoalRisk = true;
                            float? score = goal.Score(variant, entry.Trial.Candidate, action, situation, context);
                            if (score == null) continue;
                            sum += score.Value;
                            count++;
                        }
                        entry.Verdict.Score = sum / count;
                    }
                    if (!entry.Verdict.Risky) foundSafe = true;
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
                if (rated[i].Verdict.Risky) continue;
                first = i;
                break;
            }
            float best = rated[first].Verdict.Score;
            var pool = new List<Rated> { rated[first] };
            for (int i = 0; i < rated.Count && rated[i].Checked && pool.Count < difficulty.pickAmongBest; i++)
                if (i != first && !rated[i].Verdict.Risky && rated[i].Verdict.Score >= best - PickWindow) pool.Add(rated[i]);
            done(pool[Random.Range(0, pool.Count)]);
        }

        /// <summary>A jogada com o erro do nível: mira para cada lado, força para mais e para menos.</summary>
        private SimResult[] Variants(in ShotCandidate candidate, AiDifficulty difficulty, float maxImpulse)
        {
            float spread = difficulty.angleErrorDegrees * 0.75f;
            float powerSpread = difficulty.powerError * 0.75f;
            var variants = new List<SimResult>(4);
            foreach (var (angle, powerScale) in new[] { (-spread, 1f), (spread, 1f), (0f, 1f - powerSpread), (0f, 1f + powerSpread) })
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
