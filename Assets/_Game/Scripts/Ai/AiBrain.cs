using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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
        /// <summary>Nenhum objetivo aceitou a melhor jogada: ficou a mais segura dos tentados.</summary>
        public bool NoGoalAccepted;
        /// <summary>Objetivos na ordem tentada ("Marcar > Preparar o chute > Proteger > Fechar o gol").</summary>
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
    /// Decide o toque da IA por objetivos, numa ordem fixa pela situação (<see cref="Plan"/>): Marcar; com toque
    /// sobrando, Preparar o chute; senão (ou se preparar não deu) Afastar ou Proteger; por fim, Fechar o gol. Para cada
    /// objetivo pega a ação que pode rodar, simula as jogadas dela, dá nota com a conta do objetivo (testando o erro do
    /// nível nas melhores) e sorteia entre as melhores seguras. Fica no primeiro que aceitar; as simulações de uma ação
    /// são reaproveitadas pelos outros objetivos que usam a mesma ação.
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
            ShotPlanner opponentPlanner, Func<bool> valid, Action<AiDecision> done)
        {
            trials.Clear();
            currentTuning = tuning;
            stillValid = valid;
            aborted = false;
            simulations = 0;
            computeMs = 0;
            int stepsBefore = simulator.TotalSteps;
            float maxImpulse = situation.IsBallKick ? tuning.ballKickMaxImpulse : tuning.maxImpulse;
            var context = new AiScoreContext(planner, opponentPlanner, match.Options.goalAfterWallIsValid);

            var goals = Plan(situation);
            var rejected = new StringBuilder();
            AiDecision fallback = null;
            AiDecision chosen = null;
            AiDecision deferredCall = null;
            simulator.Sync();
            frame.Restart();

            foreach (var goal in goals)
            {
                AiAction action = null;
                foreach (var candidate in goal.Actions)
                    if (candidate.CanRun(situation))
                    {
                        action = candidate;
                        break;
                    }
                if (action == null) continue;

                // Vai chutar adiado (só um botão com ângulo): se preparar não deixou dois, anuncia agora.
                if (deferredCall != null && goal != AiGoal.Preparing)
                {
                    chosen = deferredCall;
                    break;
                }

                if (action.IsInstant)
                {
                    // Vai chutar: a ação só roda com chute livre, então anuncia. Quem chuta e onde, a IA decide depois,
                    // contra o goleiro já posicionado.
                    var names = new StringBuilder();
                    foreach (var disc in situation.ClearShooters)
                        names.Append(names.Length > 0 ? ", " : string.Empty).Append(disc.name);
                    var call = new AiDecision { Goal = goal, Action = action, FollowLine = $"chute livre: {names}" };
                    // Com toque sobrando e só um botão com ângulo, o adversário sabe de onde vem o chute: tenta antes
                    // deixar a bola onde dois botões tenham ângulo.
                    if (!situation.IsLastTouch && situation.ClearShooters.Count < 2 && goals.Contains(AiGoal.Preparing))
                    {
                        deferredCall = call;
                        rejected.Append(rejected.Length > 0 ? ", " : string.Empty).Append(goal.Name)
                            .Append(" adiado (só 1 botão com ângulo)");
                        continue;
                    }
                    chosen = call;
                    break;
                }

                if (!trials.TryGetValue(action, out var list))
                {
                    yield return SimulateAction(action, situation, match, difficulty, maxImpulse, l => list = l);
                    if (aborted) yield break;
                    trials[action] = list;
                }

                Rated? pick = null;
                if (list.Count > 0)
                {
                    yield return Rate(goal, action, list, situation, context, difficulty, maxImpulse, r => pick = r);
                    if (aborted) yield break;
                }
                if (pick == null)
                {
                    rejected.Append(rejected.Length > 0 ? ", " : string.Empty).Append(goal.Name).Append(" sem jogada")
                        .Append(NoPlayReason(list));
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
                    // Vai chutar adiado: só vale preparar se a bola fica com dois botões com ângulo.
                    if (deferredCall != null && goal == AiGoal.Preparing &&
                        PrepareShotGoal.Shooters(pick.Value.Trial.Main, situation, context) < 2)
                    {
                        chosen = deferredCall;
                        rejected.Append(", Preparar o chute não deixou 2 botões com ângulo");
                        break;
                    }
                    chosen = decision;
                    break;
                }
                rejected.Append(rejected.Length > 0 ? ", " : string.Empty).Append(goal.Name).Append(' ')
                    .Append(pick.Value.Verdict.Score.ToString("0"));
                if (pick.Value.Verdict.Foul) rejected.Append(" (falta)");
                else if (pick.Value.Verdict.OwnGoalRisk) rejected.Append(" (gol contra)");
                // Se nenhum aceitar, fica a do primeiro objetivo; uma jogada segura ganha de uma arriscada.
                if (fallback == null || (!fallback.Verdict.SafeFor(difficulty) && decision.Verdict.SafeFor(difficulty)))
                    fallback = decision;
            }

            chosen ??= deferredCall;
            if (chosen == null && fallback != null)
            {
                chosen = fallback;
                chosen.NoGoalAccepted = true;
            }
            chosen ??= new AiDecision { Candidate = Straight(situation, match) };

            var line = new StringBuilder();
            foreach (var goal in goals)
                line.Append(line.Length > 0 ? " > " : string.Empty).Append(goal.Name);
            chosen.GoalsLine = line.ToString();
            chosen.Rejected = rejected.ToString();
            chosen.Simulations = simulations;
            chosen.Steps = simulator.TotalSteps - stepsBefore;
            chosen.Milliseconds = computeMs;
            done(chosen);
        }

        /// <summary>
        /// A ordem dos objetivos neste toque: dá pra chutar? Bola perto do próprio gol: afasta primeiro. Tem toque
        /// sobrando: prepara o chute. Senão protege. Nada serviu: fecha o gol.
        /// </summary>
        private static List<AiGoal> Plan(AiSituation situation)
        {
            var goals = new List<AiGoal> { AiGoal.Scoring };
            bool danger = situation.BallToOwnGoal < AiSituation.DangerDistance;
            if (danger) goals.Add(AiGoal.Clearing);
            if (!situation.IsLastTouch) goals.Add(AiGoal.Preparing);
            if (!danger) goals.Add(AiGoal.Protecting);
            goals.Add(AiGoal.Closing);
            return goals;
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
            // com a mesma conta do objetivo (o chute fica pronto no toque seguinte? não fica sem saída?).
            // Só quando ainda sobra um toque depois do seguinte (o toque seguinte também prepara).
            if (action.TouchesBall && goal.PlansAhead && difficulty.planAhead > 0 && situation.TouchesLeft > 2)
            {
                int planned = 0;
                for (int i = 0; i < rated.Count && planned < difficulty.planAhead; i++)
                {
                    var entry = rated[i];
                    var main = entry.Trial.Main;
                    if (main.Foul || main.GoalOf != null || main.End == null || Loses(main, situation)) continue;
                    if (entry.Trial.Follow == null)
                    {
                        yield return FollowUp(goal, action, entry.Trial, situation, context, difficulty);
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

        /// <summary>
        /// O melhor toque seguinte a partir de onde a jogada parou: gera jogadas pela geometria nas posições simuladas,
        /// simula algumas (sem erro) e fica com a melhor nota do mesmo objetivo. Sem jogada nenhuma: bola sem saída.
        /// </summary>
        private IEnumerator FollowUp(AiGoal goal, AiAction action, Trial trial, AiSituation situation, AiScoreContext context,
            AiDifficulty difficulty)
        {
            var state = trial.Main.End;
            bool attackHalf = FieldLayout.AttackDirection(side).y * state.Ball.y > 0f;
            bool goalsCount = !situation.HasGoalkeeper && attackHalf;
            var discs = ShotPlanner.Nearest(state, situation.TeamDiscs, difficulty.discsToTest);
            var candidates = planner.FollowUpCandidates(state, discs, false);
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

                float? score = next.Foul ? null : goal.Score(next, candidate, action, situation, context, state.Ball);
                if (score is float value && value > best)
                {
                    best = value;
                    line = $"{candidate.Disc.name} nota {value:0}" +
                           (next.GoalOf != null && next.GoalOf != side && goalsCount ? " (gol)" : string.Empty);
                }
                if (Budget()) yield return Pause();
                if (aborted) yield break;
            }
            trial.Follow = best;
            trial.FollowLine = line;
        }

        /// <summary>Por que nenhuma jogada serviu (para o log): nenhuma gerada ou todas dão falta.</summary>
        private static string NoPlayReason(List<Trial> list)
        {
            if (list.Count == 0) return " (nenhum toque possível)";
            foreach (var trial in list)
                if (!trial.Main.Foul) return string.Empty;
            return $" (as {list.Count} dão falta)";
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
