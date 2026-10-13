using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>Uma jogada que a IA está considerando.</summary>
    public struct ShotCandidate
    {
        /// <summary>Botão do peteleco. Nulo = chute direto na bola (tiro de meta).</summary>
        public Disc Disc;
        public Vector2 Direction;
        public float Power01;
        /// <summary>Nota pela geometria, antes de simular.</summary>
        public float PreScore;
        /// <summary>Tem adversário entre o botão e a bola.</summary>
        public bool FoulRisk;
        /// <summary>O caminho do botão até a bola está bloqueado (parede, goleiro ou botão do próprio time).</summary>
        public bool Blocked;
        /// <summary>Tem adversário ou goleiro no caminho da bola até o alvo.</summary>
        public bool BallBlocked;
        public Vector2 Target;
        /// <summary>Alvo para o lado ou para trás (quando não há jogada boa para a frente; a parede devolve a bola).</summary>
        public bool Wide;
    }

    /// <summary>
    /// O "cérebro" da IA, sem física: gera jogadas por geometria (como na sinuca), confere o caminho com
    /// CircleCasts e dá nota ao resultado de uma jogada simulada.
    /// </summary>
    public sealed class ShotPlanner
    {
        private const float MinPower = 0.1f;
        private static readonly float[] AdvanceDistances = { 2.5f, 4f };
        private static readonly float[] AdvanceOffsets = { -1.5f, 0f, 1.5f };
        private static readonly float[] GoalOffsets = { -0.8f, -0.4f, 0f, 0.4f, 0.8f };
        private static readonly float[] PowerScales = { 0.85f, 1f, 1.2f };
        private const float WideSideDistance = 2.5f;
        private const float WideBackDistance = 1.5f;
        /// <summary>Empurrãozinho do toque seguro: um botão anda só isso, longe de tudo.</summary>
        private const float NudgeDistance = 0.6f;

        private static readonly ContactFilter2D NoFilter = ContactFilter2D.noFilter;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[16];
        private readonly TeamSide side;
        private readonly PhysicsTuning tuning;
        private readonly Ball ball;
        private Goalkeeper[] keepers;
        private bool ignoreKeepers;

        /// <summary>Opção da partida: desligada, adversário no caminho não é risco de falta.</summary>
        public bool FoulsEnabled { get; set; } = true;

        /// <summary>Distância da falta longe do lance (dá cartão) e se a IA já tem amarelo.</summary>
        public float FarFoulDistance { get; set; } = 3.5f;
        public bool HasYellow { get; set; }

        /// <summary>Bola mais perto que isto do próprio gol: a IA prefere afastá-la (também para os lados).</summary>
        private const float DangerDistance = 3.5f;

        /// <summary>Largura da boca do gol (menor sem goleiro): os alvos de chute ficam dentro dela.</summary>
        public float GoalWidth { get; set; } = FieldLayout.GoalWidth;

        /// <summary>Todos os botões e goleiros da mesa (para conferir caminhos num momento simulado).</summary>
        public IReadOnlyList<Disc> AllDiscs { get; set; }
        public IReadOnlyList<Goalkeeper> Keepers { get; set; }

        /// <summary>Bônus na nota quando a jogada deixa um chute livre para o toque seguinte (Preparar o chute).</summary>
        public const float ShotSetupBonus = 25f;

        /// <summary>Bola a menos que isto da parede: grudada nela (no canto, nas duas).</summary>
        private const float WallNear = 0.45f;
        /// <summary>Passo do círculo que confere o caminho num momento simulado.</summary>
        private const float PathStep = 0.1f;

        public ShotPlanner(TeamSide aiSide, PhysicsTuning physicsTuning, Ball matchBall)
        {
            side = aiSide;
            tuning = physicsTuning;
            ball = matchBall;
        }

        private TeamSide Opponent => side == TeamSide.Bottom ? TeamSide.Top : TeamSide.Bottom;
        private Vector2 Attack => FieldLayout.AttackDirection(side);

        // ---- Jogadas por geometria ----

        /// <summary>
        /// Peteleco com botão. <paramref name="scoring"/>: os alvos são o gol (chute anunciado ou jogo sem goleiro);
        /// senão, pontos à frente da bola.
        /// </summary>
        public List<ShotCandidate> DiscCandidates(IReadOnlyList<Disc> usable, bool scoring)
        {
            var list = new List<ShotCandidate>();
            Vector2 ballPosition = ball.Body.position;
            var targets = scoring ? GoalTargets() : AdvanceTargets(ballPosition);

            foreach (var disc in usable)
            foreach (var target in targets)
                AddDiscCandidates(list, disc, ballPosition, target, scoring, false);

            // Também para os lados e para trás: com a bola atrás dos botões (no canto, perto do gol) não há jogada
            // para a frente, e a parede devolve a bola. A simulação diz se presta.
            if (!scoring)
                foreach (var disc in usable)
                foreach (var target in WideTargets(ballPosition))
                    AddDiscCandidates(list, disc, ballPosition, target, false, true);

            return list;
        }

        /// <summary>
        /// Jogadas do toque seguinte, a partir de um momento simulado (onde a bola e os botões pararam). O caminho é
        /// conferido nas posições desse momento, sem a física da mesa de verdade. Em ordem de nota da geometria.
        /// </summary>
        public List<ShotCandidate> FollowUpCandidates(SimState state, IReadOnlyList<Disc> discs, bool scoring)
        {
            var list = new List<ShotCandidate>();
            Vector2 ballPosition = state.Ball;
            var targets = scoring ? GoalTargets() : AdvanceTargets(ballPosition);
            foreach (var disc in discs)
            {
                foreach (var target in targets)
                    AddDiscCandidates(list, disc, ballPosition, target, scoring, false, state);
                if (!scoring)
                    foreach (var target in WideTargets(ballPosition))
                        AddDiscCandidates(list, disc, ballPosition, target, false, true, state);
            }
            list.Sort((a, b) => b.PreScore.CompareTo(a.PreScore));
            return list;
        }

        /// <summary>
        /// Neste momento simulado, algum botão teria chute a gol livre (sem adversário no caminho, sem força máxima)
        /// com a bola perto do gol? É o "Preparar o chute": no toque seguinte dá para anunciar o Vai chutar.
        /// </summary>
        public bool HasShotSetup(SimState state, IReadOnlyList<Disc> discs, float maxDistance)
        {
            if (state == null) return false;
            Vector2 goal = new(0f, FieldLayout.GoalLineY(Opponent));
            if (Vector2.Distance(state.Ball, goal) > maxDistance || Progress(state.Ball) <= 0f) return false;
            ignoreKeepers = true;
            try
            {
                var targets = GoalTargets();
                setupList.Clear();
                foreach (var disc in Nearest(state, discs, SetupDiscs))
                foreach (var target in targets)
                {
                    int before = setupList.Count;
                    AddDiscCandidates(setupList, disc, state.Ball, target, true, false, state);
                    for (int i = before; i < setupList.Count; i++)
                    {
                        var candidate = setupList[i];
                        if (!candidate.FoulRisk && !candidate.Blocked && !candidate.BallBlocked && candidate.Power01 < 1f) return true;
                    }
                }
                return false;
            }
            finally { ignoreKeepers = false; }
        }

        /// <summary>Quantos botões (os mais perto da bola) contam para o chute preparado.</summary>
        private const int SetupDiscs = 3;
        private readonly List<ShotCandidate> setupList = new();

        /// <summary>Os <paramref name="count"/> botões mais perto da bola neste momento simulado.</summary>
        public static List<Disc> Nearest(SimState state, IReadOnlyList<Disc> discs, int count)
        {
            var list = new List<Disc>(discs);
            Vector2 ballPosition = state.Ball;
            list.Sort((a, b) => (state.PositionOf(a.Body) - ballPosition).sqrMagnitude
                .CompareTo((state.PositionOf(b.Body) - ballPosition).sqrMagnitude));
            if (list.Count > count) list.RemoveRange(count, list.Count - count);
            return list;
        }

        /// <summary>Quantas paredes a bola está encostando (0, 1 ou 2 no canto).</summary>
        public static int WallsTouching(Vector2 ballPosition)
        {
            int walls = 0;
            if (FieldLayout.HalfWidth - Mathf.Abs(ballPosition.x) < WallNear) walls++;
            if (FieldLayout.HalfHeight - Mathf.Abs(ballPosition.y) < WallNear) walls++;
            return walls;
        }

        /// <summary>
        /// Chutes a gol mais prováveis deste lado (o atacante, visto pelo goleiro da IA): sem risco de falta nem
        /// caminho fechado, os mais fáceis (menos força) primeiro. Só a força do meio de cada jogada.
        /// </summary>
        public List<ShotCandidate> GoalThreats(IReadOnlyList<Disc> usable, int count)
        {
            // O goleiro ainda vai se mexer: ele não fecha caminho na escolha dos chutes.
            ignoreKeepers = true;
            List<ShotCandidate> all;
            try { all = DiscCandidates(usable, true); }
            finally { ignoreKeepers = false; }
            var list = new List<ShotCandidate>();
            int middle = System.Array.IndexOf(PowerScales, 1f);
            for (int i = middle; i < all.Count; i += PowerScales.Length)
                if (!all[i].FoulRisk && !all[i].Blocked) list.Add(all[i]);
            list.Sort((a, b) =>
            {
                int byScore = b.PreScore.CompareTo(a.PreScore);
                return byScore != 0 ? byScore : a.Power01.CompareTo(b.Power01);
            });
            if (list.Count > count) list.RemoveRange(count, list.Count - count);
            return list;
        }

        /// <summary>Chute direto na bola, no tiro de meta.</summary>
        public List<ShotCandidate> BallKickCandidates() => BallKickCandidates(ball.Body.position);

        /// <summary>Chutes direto na bola como se ela estivesse em <paramref name="ballPosition"/> (tiro de meta).</summary>
        public List<ShotCandidate> BallKickCandidates(Vector2 ballPosition)
        {
            var list = new List<ShotCandidate>();
            float damping = Mathf.Max(ball.Body.linearDamping, 0.1f);

            foreach (var target in AdvanceTargets(ballPosition))
            {
                Vector2 toTarget = target - ballPosition;
                float length = toTarget.magnitude;
                if (length < 0.1f) continue;
                Vector2 direction = toTarget / length;
                float power = length * damping * ball.Body.mass / Mathf.Max(tuning.ballKickMaxImpulse, 0.01f);
                bool ballBlocked = BallPathBlocked(ballPosition, direction, length, null);

                foreach (float scale in PowerScales)
                {
                    list.Add(new ShotCandidate
                    {
                        Direction = direction,
                        Power01 = Mathf.Clamp(power * scale, MinPower, 1f),
                        Target = target,
                        BallBlocked = ballBlocked,
                        PreScore = Progress(target - ballPosition) * 2f - (ballBlocked ? 8f : 0f)
                    });
                }
            }

            return list;
        }

        /// <summary>Alvos do tiro livre e do pênalti: o gol quando o chute vale gol, senão pontos à frente da bola.</summary>
        public List<Vector2> KickTargets(bool scoring) => scoring ? GoalTargets() : AdvanceTargets(ball.Body.position);

        /// <summary>
        /// Jogadas de posicionamento: o botão anda até um ponto sem tocar em nada (a vez passa por "errou a bola").
        /// Pontos: na frente do próprio gol (cobrindo a bola), entre a bola e os adversários mais perto dela e atrás
        /// da bola, pronto para o próximo ataque. <see cref="ShotCandidate.Target"/> é onde o botão deve parar.
        /// </summary>
        public List<ShotCandidate> PositionCandidates(IReadOnlyList<Disc> usable, IReadOnlyList<Disc> allDiscs)
        {
            var list = new List<ShotCandidate>();
            if (usable.Count == 0) return list;
            Vector2 ballPosition = ball.Body.position;
            float discRadius = usable[0].Radius;
            float near = ball.Radius + discRadius + 0.45f;
            bool defending = Progress(ballPosition) < 0f;

            var points = new List<(Vector2 point, float value)>();
            Vector2 ownGoal = new(0f, FieldLayout.GoalLineY(side));
            Vector2 toOwnGoal = ownGoal - ballPosition;
            if (toOwnGoal.magnitude > near + 0.5f)
                points.Add((ballPosition + toOwnGoal.normalized * near, defending ? 14f : 7f));

            // Adversários mais perto da bola: o botão para no meio do caminho deles.
            var opponents = new List<Disc>();
            foreach (var disc in allDiscs)
                if (disc != null && disc.isActiveAndEnabled && disc.Side != side) opponents.Add(disc);
            opponents.Sort((a, b) => (a.Body.position - ballPosition).sqrMagnitude.CompareTo((b.Body.position - ballPosition).sqrMagnitude));
            for (int i = 0; i < Mathf.Min(2, opponents.Count); i++)
            {
                Vector2 toOpponent = opponents[i].Body.position - ballPosition;
                if (toOpponent.magnitude < near + discRadius * 2f + 0.1f) continue;
                points.Add((ballPosition + toOpponent.normalized * near, 11f - i * 2f));
            }

            Vector2 behind = ballPosition - Attack * near;
            points.Add((behind, defending ? 5f : 9f));

            float maxX = FieldLayout.HalfWidth - discRadius - 0.05f;
            float maxY = FieldLayout.HalfHeight - discRadius - 0.05f;

            foreach (var disc in usable)
            foreach (var (rawPoint, value) in points)
            {
                Vector2 point = new(Mathf.Clamp(rawPoint.x, -maxX, maxX), Mathf.Clamp(rawPoint.y, -maxY, maxY));
                Vector2 travel = point - disc.Body.position;
                float distance = travel.magnitude;
                if (distance < 0.3f) continue;
                Vector2 direction = travel / distance;
                if (!PathClear(disc, direction, distance)) continue;

                // Com o damping da Unity o botão anda uns v / damping.
                float speed = distance * Mathf.Max(disc.Body.linearDamping, 0.1f);
                float power = speed * disc.Body.mass / Mathf.Max(tuning.maxImpulse, 0.01f);
                if (power > 1f) continue;

                list.Add(new ShotCandidate
                {
                    Disc = disc,
                    Direction = direction,
                    Power01 = Mathf.Max(power, MinPower),
                    Target = point,
                    PreScore = value - distance * 0.5f
                });
            }

            // Empurrãozinho: cada botão anda um pouco para trás ou para os lados, longe de tudo. Nota baixa, só para o
            // toque seguro nunca ficar sem saída.
            foreach (var disc in usable)
            foreach (var step in new[] { -Attack, Vector2.left, Vector2.right })
            {
                Vector2 from = disc.Body.position;
                Vector2 point = from + step * NudgeDistance;
                point = new Vector2(Mathf.Clamp(point.x, -maxX, maxX), Mathf.Clamp(point.y, -maxY, maxY));
                Vector2 travel = point - from;
                float distance = travel.magnitude;
                if (distance < 0.3f) continue;
                Vector2 direction = travel / distance;
                if (!PathClear(disc, direction, distance + 0.3f)) continue;
                float speed = distance * Mathf.Max(disc.Body.linearDamping, 0.1f);
                float power = speed * disc.Body.mass / Mathf.Max(tuning.maxImpulse, 0.01f);
                list.Add(new ShotCandidate
                {
                    Disc = disc,
                    Direction = direction,
                    Power01 = Mathf.Clamp(power, MinPower, 1f),
                    Target = point,
                    PreScore = 1f - Vector2.Distance(from, ballPosition) * 0.05f
                });
            }

            list.Sort((a, b) => b.PreScore.CompareTo(a.PreScore));
            return list;
        }

        /// <summary>Nota de um posicionamento simulado. Nulo: não serve (tocou na bola, fez falta ou parou longe).</summary>
        public float? ScorePosition(in SimResult result, in ShotCandidate candidate)
        {
            if (result.Foul || result.TouchedBall || result.LastTouchSide != null || result.GoalOf != null) return null;
            if ((result.BallEnd - (Vector2)ball.Body.position).sqrMagnitude > 0.01f) return null;
            float miss = Vector2.Distance(result.ShooterEnd, candidate.Target);
            if (miss > 0.8f) return null;
            return candidate.PreScore - miss * 4f;
        }

        /// <summary>
        /// Vale anunciar o "Vai chutar"? Sim quando a bola está perto do gol e existe um chute com caminho livre
        /// até a bola e da bola até o gol (sem contar o goleiro, que ainda vai se mexer).
        /// </summary>
        public bool ShouldCallShot(IReadOnlyList<Disc> usable, float maxDistance)
        {
            Vector2 ballPosition = ball.Body.position;
            Vector2 goal = new(0f, FieldLayout.GoalLineY(Opponent));
            if (Vector2.Distance(ballPosition, goal) > maxDistance) return false;
            return HasClearShot(usable);
        }

        /// <summary>Algum botão tem chute a gol sem risco de falta, com caminho livre e sem precisar da força máxima.</summary>
        public bool HasClearShot(IReadOnlyList<Disc> usable)
        {
            foreach (var candidate in DiscCandidates(usable, true))
                if (!candidate.FoulRisk && !candidate.Blocked && !candidate.BallBlocked && candidate.Power01 < 1f) return true;
            return false;
        }

        private void AddDiscCandidates(List<ShotCandidate> list, Disc disc, Vector2 ballPosition, Vector2 target, bool scoring,
            bool wide, SimState state = null)
        {
            // Sinuca: para a bola ir na direção do alvo, o botão tem que bater nela pelo lado oposto ("bola fantasma").
            Vector2 toTarget = target - ballPosition;
            float length = toTarget.magnitude;
            if (length < 0.1f) return;
            Vector2 ballDirection = toTarget / length;

            Vector2 discPosition = state != null ? state.PositionOf(disc.Body) : disc.Body.position;
            Vector2 ghost = ballPosition - ballDirection * (ball.Radius + disc.Radius);
            Vector2 toGhost = ghost - discPosition;
            float travel = toGhost.magnitude;
            if (travel < 0.01f) return;
            Vector2 aimDirection = toGhost / travel;

            // Corte fino demais (mais de ~70°) não leva a bola para onde se quer.
            float cut = Vector2.Dot(aimDirection, ballDirection);
            if (cut < 0.35f) return;

            float power = PowerFor(disc, travel, cut, scoring ? length + 1.5f : length);
            bool foulRisk, blocked, ballBlocked;
            if (state == null)
            {
                CheckDiscPath(disc, aimDirection, travel, out foulRisk, out blocked);
                ballBlocked = BallPathBlocked(ballPosition, ballDirection, length, disc);
            }
            else
            {
                CheckDiscPathIn(state, disc, discPosition, aimDirection, travel, out foulRisk, out blocked);
                ballBlocked = BallPathBlockedIn(state, ballPosition, ballDirection, length, disc);
            }

            float preScore = scoring ? 20f : Progress(toTarget) * 2f;
            // Bola perto do próprio gol: alvo que a afasta do gol vale mais (para os lados também serve).
            if (!scoring)
            {
                Vector2 ownGoal = new(0f, FieldLayout.GoalLineY(side));
                float fromGoal = Vector2.Distance(ballPosition, ownGoal);
                if (fromGoal < DangerDistance) preScore += (Vector2.Distance(target, ownGoal) - fromGoal) * 2f;
            }
            if (foulRisk) preScore -= 30f;
            if (blocked) preScore -= 25f;
            if (ballBlocked) preScore -= 8f;
            if (power > 1.15f) preScore -= 6f;

            foreach (float scale in PowerScales)
            {
                list.Add(new ShotCandidate
                {
                    Disc = disc,
                    Direction = aimDirection,
                    Power01 = Mathf.Clamp(power * scale, MinPower, 1f),
                    Target = target,
                    FoulRisk = foulRisk,
                    Blocked = blocked,
                    BallBlocked = ballBlocked,
                    PreScore = preScore,
                    Wide = wide
                });
            }
        }

        /// <summary>
        /// Força (0 a 1) para a bola andar <paramref name="ballDistance"/>. Com o damping da Unity, um corpo com
        /// velocidade v para depois de andar uns v / damping; na batida, a bola leva parte da velocidade do botão.
        /// </summary>
        private float PowerFor(Disc disc, float discTravel, float cut, float ballDistance)
        {
            float ballDamping = Mathf.Max(ball.Body.linearDamping, 0.1f);
            float discDamping = Mathf.Max(disc.Body.linearDamping, 0.1f);
            float discMass = disc.Body.mass;
            float ballMass = ball.Body.mass;

            float ballSpeed = ballDistance * ballDamping;
            float transfer = (1f + tuning.bounciness) * discMass / (discMass + ballMass) * cut;
            float contactSpeed = ballSpeed / Mathf.Max(transfer, 0.1f);
            float startSpeed = contactSpeed + discDamping * discTravel;
            return startSpeed * discMass / Mathf.Max(tuning.maxImpulse, 0.01f);
        }

        private List<Vector2> GoalTargets()
        {
            var targets = new List<Vector2>();
            float y = FieldLayout.GoalLineY(Opponent) + Attack.y * 0.4f;
            float scale = GoalWidth / FieldLayout.GoalWidth;
            foreach (float x in GoalOffsets) targets.Add(new Vector2(x * scale, y));
            return targets;
        }

        private List<Vector2> AdvanceTargets(Vector2 ballPosition)
        {
            var targets = new List<Vector2>();
            float maxX = FieldLayout.HalfWidth - 0.4f;
            float maxY = FieldLayout.HalfHeight - 0.4f;
            foreach (float distance in AdvanceDistances)
            foreach (float offset in AdvanceOffsets)
            {
                var target = ballPosition + Attack * distance + new Vector2(offset, 0f);
                targets.Add(new Vector2(Mathf.Clamp(target.x, -maxX, maxX), Mathf.Clamp(target.y, -maxY, maxY)));
            }
            return targets;
        }

        /// <summary>
        /// Alvos para os lados e na diagonal para trás da bola. Não ficam presos dentro do campo: com a bola encostada
        /// na parede, o toque possível é contra ela, e a simulação mostra a bola voltando.
        /// </summary>
        private List<Vector2> WideTargets(Vector2 ballPosition)
        {
            var targets = new List<Vector2>
            {
                ballPosition + new Vector2(-WideSideDistance, 0f),
                ballPosition + new Vector2(WideSideDistance, 0f),
                ballPosition - Attack * WideBackDistance + new Vector2(-WideBackDistance, 0f),
                ballPosition - Attack * WideBackDistance + new Vector2(WideBackDistance, 0f),
                ballPosition - Attack * WideBackDistance,
                // Diagonal para a frente, para o canto e a parede devolverem a bola (tirar do canto).
                ballPosition + Attack * WideBackDistance + new Vector2(-WideSideDistance, 0f),
                ballPosition + Attack * WideBackDistance + new Vector2(WideSideDistance, 0f)
            };
            AddWallTargets(targets, ballPosition);
            return targets;
        }

        /// <summary>
        /// Bola encostada numa parede: toques de lado, ao longo dela, um pouco para dentro da parede (o botão não
        /// cabe entre a bola e a parede, então bate de lado e a parede vai guiando a bola para fora do canto).
        /// </summary>
        private static void AddWallTargets(List<Vector2> targets, Vector2 ballPosition)
        {
            const float along = 2.5f;
            const float inward = 0.8f;
            if (FieldLayout.HalfWidth - Mathf.Abs(ballPosition.x) < WallNear)
            {
                float toWall = Mathf.Sign(ballPosition.x);
                targets.Add(ballPosition + new Vector2(toWall * inward, along));
                targets.Add(ballPosition + new Vector2(toWall * inward, -along));
            }
            // Na boca do gol não tem parede: o toque "para dentro" seria para o gol.
            if (FieldLayout.HalfHeight - Mathf.Abs(ballPosition.y) < WallNear && Mathf.Abs(ballPosition.x) > FieldLayout.GoalWidth * 0.5f + 0.3f)
            {
                float toWall = Mathf.Sign(ballPosition.y);
                targets.Add(ballPosition + new Vector2(along, toWall * inward));
                targets.Add(ballPosition + new Vector2(-along, toWall * inward));
            }
        }

        /// <summary>Quanto um deslocamento avança na direção do gol adversário.</summary>
        private float Progress(Vector2 delta) => Vector2.Dot(delta, Attack);

        // ---- Caminho livre ----

        /// <summary>Passa um círculo do tamanho do botão até a bola e vê o que ele acerta primeiro.</summary>
        private void CheckDiscPath(Disc disc, Vector2 direction, float distance, out bool foulRisk, out bool blocked)
        {
            foulRisk = false;
            blocked = false;
            int count = Physics2D.CircleCast(disc.Body.position, disc.Radius * 0.95f, direction, NoFilter, hits, distance + 0.05f);
            SortByDistance(count);

            for (int i = 0; i < count; i++)
            {
                var collider = hits[i].collider;
                if (collider.isTrigger || collider.attachedRigidbody == disc.Body) continue;
                if (collider.GetComponent<Ball>() != null) return;

                var other = collider.GetComponent<Disc>();
                var keeper = collider.GetComponent<Goalkeeper>();
                if ((other != null && other.Side != side) || (keeper != null && keeper.Side != side)) foulRisk = FoulsEnabled;
                else blocked = true;
                return;
            }
        }

        /// <summary>O botão chega até o ponto sem encostar em nada (bola, botão, goleiro ou parede).</summary>
        private bool PathClear(Disc disc, Vector2 direction, float distance)
        {
            int count = Physics2D.CircleCast(disc.Body.position, disc.Radius + 0.02f, direction, NoFilter, hits, distance + 0.05f);
            for (int i = 0; i < count; i++)
            {
                var collider = hits[i].collider;
                if (collider.isTrigger || collider.attachedRigidbody == disc.Body) continue;
                return false;
            }
            return true;
        }

        /// <summary>Passa um círculo do tamanho da bola até o alvo; adversário ou goleiro no caminho atrapalham.</summary>
        private bool BallPathBlocked(Vector2 from, Vector2 direction, float distance, Disc shooter)
        {
            int count = Physics2D.CircleCast(from, ball.Radius * 0.95f, direction, NoFilter, hits, distance);
            for (int i = 0; i < count; i++)
            {
                var collider = hits[i].collider;
                if (collider.isTrigger || collider.attachedRigidbody == ball.Body) continue;
                if (shooter != null && collider.attachedRigidbody == shooter.Body) continue;

                var other = collider.GetComponent<Disc>();
                if (other != null && other.Side != side) return true;
                if (!ignoreKeepers && collider.GetComponent<Goalkeeper>() != null) return true;
            }
            return false;
        }

        // ---- Caminho livre num momento simulado (sem física: um círculo andando em passos) ----

        private enum Obstacle { None, Ball, Teammate, Opponent, OwnKeeper, OpponentKeeper, Wall }

        /// <summary>Mesmo que <see cref="CheckDiscPath"/>, nas posições de um momento simulado.</summary>
        private void CheckDiscPathIn(SimState state, Disc disc, Vector2 from, Vector2 direction, float distance,
            out bool foulRisk, out bool blocked)
        {
            foulRisk = false;
            blocked = false;
            var hit = FirstObstacle(state, from, direction, distance + 0.05f, disc.Radius * 0.95f, disc, true);
            if (hit is Obstacle.Opponent or Obstacle.OpponentKeeper) foulRisk = FoulsEnabled;
            else if (hit is Obstacle.Teammate or Obstacle.OwnKeeper or Obstacle.Wall) blocked = true;
        }

        /// <summary>Mesmo que <see cref="BallPathBlocked"/>, nas posições de um momento simulado.</summary>
        private bool BallPathBlockedIn(SimState state, Vector2 from, Vector2 direction, float distance, Disc shooter)
        {
            Prepare(state);
            float radius = ball.Radius * 0.95f;
            for (float travelled = PathStep; travelled <= distance; travelled += PathStep)
            {
                Vector2 point = from + direction * travelled;
                foreach (var other in stateDiscs)
                    if (other.Disc != shooter && other.Disc.Side != side && Vector2.Distance(point, other.Position) < other.Radius + radius)
                        return true;
                if (ignoreKeepers) continue;
                foreach (var keeper in stateKeepers)
                    if (BoxOverlaps(keeper.Center, keeper.Half, point, radius)) return true;
            }
            return false;
        }

        /// <summary>O que um círculo andando de <paramref name="from"/> encontra primeiro (a bola conta como chegar).</summary>
        private Obstacle FirstObstacle(SimState state, Vector2 from, Vector2 direction, float distance, float radius, Disc self,
            bool stopAtBall)
        {
            Prepare(state);
            float maxX = FieldLayout.HalfWidth + FieldLayout.WallInset - radius;
            float maxY = FieldLayout.HalfHeight + FieldLayout.WallInset - radius;
            for (float travelled = PathStep; travelled <= distance + 0.001f; travelled += PathStep)
            {
                Vector2 point = from + direction * Mathf.Min(travelled, distance);
                if (stopAtBall && Vector2.Distance(point, state.Ball) < radius + ball.Radius) return Obstacle.Ball;
                foreach (var other in stateDiscs)
                    if (other.Disc != self && Vector2.Distance(point, other.Position) < radius + other.Radius)
                        return other.Disc.Side == side ? Obstacle.Teammate : Obstacle.Opponent;
                foreach (var keeper in stateKeepers)
                    if (BoxOverlaps(keeper.Center, keeper.Half, point, radius))
                        return keeper.Side == side ? Obstacle.OwnKeeper : Obstacle.OpponentKeeper;
                if (Mathf.Abs(point.x) > maxX || Mathf.Abs(point.y) > maxY) return Obstacle.Wall;
            }
            return Obstacle.None;
        }

        private struct StateDisc
        {
            public Disc Disc;
            public Vector2 Position;
            public float Radius;
        }

        private struct StateKeeper
        {
            public TeamSide Side;
            public Vector2 Center;
            public Vector2 Half;
        }

        private readonly List<StateDisc> stateDiscs = new();
        private readonly List<StateKeeper> stateKeepers = new();
        private SimState preparedState;

        /// <summary>Lê uma vez as posições e tamanhos de botões e goleiros do momento simulado.</summary>
        private void Prepare(SimState state)
        {
            if (state == preparedState) return;
            preparedState = state;
            stateDiscs.Clear();
            stateKeepers.Clear();
            if (AllDiscs != null)
                foreach (var disc in AllDiscs)
                    if (disc != null && disc.isActiveAndEnabled)
                        stateDiscs.Add(new StateDisc { Disc = disc, Position = state.PositionOf(disc.Body), Radius = disc.Radius });
            if (Keepers != null)
                foreach (var keeper in Keepers)
                {
                    if (keeper == null || !keeper.isActiveAndEnabled || !keeper.TryGetComponent(out BoxCollider2D box)) continue;
                    stateKeepers.Add(new StateKeeper
                    {
                        Side = keeper.Side,
                        Center = state.PositionOf(keeper.GetComponent<Rigidbody2D>()) + box.offset,
                        Half = Vector2.Scale(box.size, keeper.transform.lossyScale) * 0.5f
                    });
                }
        }

        private static bool BoxOverlaps(Vector2 center, Vector2 half, Vector2 point, float radius)
        {
            Vector2 closest = new(Mathf.Clamp(point.x, center.x - half.x, center.x + half.x),
                Mathf.Clamp(point.y, center.y - half.y, center.y + half.y));
            return (point - closest).sqrMagnitude < radius * radius;
        }

        private void SortByDistance(int count)
        {
            for (int i = 1; i < count; i++)
            {
                var hit = hits[i];
                int j = i - 1;
                while (j >= 0 && hits[j].distance > hit.distance)
                {
                    hits[j + 1] = hits[j];
                    j--;
                }
                hits[j + 1] = hit;
            }
        }

        // ---- Nota da jogada simulada ----

        /// <summary>
        /// Nota do resultado. <paramref name="goalsCount"/>: um gol agora valeria (chute anunciado ou jogo sem goleiro).
        /// <paramref name="touchesLeftAfter"/>: toques que sobram se a vez continuar.
        /// </summary>
        public float Score(in SimResult result, bool isBallKick, bool goalsCount, bool goalAfterWallIsValid,
            int touchesLeftAfter, float foulCaution, Vector2? from = null)
        {
            Vector2 start = from ?? ball.Body.position;

            if (result.Foul)
            {
                bool penalty = FieldLayout.InArea(result.FoulPosition, side);
                // Falta longe do lance dá cartão: pior ainda, e muito pior com amarelo (vermelho tira um botão).
                bool far = Vector2.Distance(result.FoulPosition, start) > FarFoulDistance;
                float card = far ? (HasYellow ? 150f : 40f) : 0f;
                // A falta tira nota mas não é proibida: no Fácil a IA liga menos para ela.
                return -((penalty ? 120f : 45f) + card) * Mathf.Lerp(0.3f, 1f, foulCaution);
            }

            if (result.GoalOf == side) return -120f;
            if (result.GoalOf == Opponent)
            {
                bool valid = goalsCount && (goalAfterWallIsValid || !result.BallTouchedWall);
                // Gol anulado vira tiro de meta do adversário.
                return valid ? 150f : -20f;
            }

            var lastTouch = isBallKick && result.LastTouchSide == null ? side : result.LastTouchSide;
            float score = 0f;
            bool keepsTurn = false;

            if (lastTouch == null) score -= 20f;
            else if (lastTouch != side) score -= 10f;
            else if (!goalsCount)
            {
                keepsTurn = touchesLeftAfter > 0;
                score += 8f;
            }

            Vector2 end = result.BallEnd;
            score += Progress(end - start) * 3f;

            // Bola perto do próprio gol: tirar de lá vale mais que atacar, inclusive afastando para os lados.
            Vector2 ownGoalCenter = new(0f, FieldLayout.GoalLineY(side));
            float startFromGoal = Vector2.Distance(start, ownGoalCenter);
            if (startFromGoal < DangerDistance)
                score += (Vector2.Distance(end, ownGoalCenter) - startFromGoal) * 4f;

            // Bola do goleiro adversário (parada na área em volta dele) vira tiro de meta dele.
            keepers ??= Object.FindObjectsByType<Goalkeeper>();
            bool hasKeeper = KeeperBall.Find(keepers, Opponent) != null;
            if (KeeperBall.Owner(end, ball.Radius, keepers, hasKeeper) == Opponent) score -= 15f;
            if (keepsTurn && Progress(end) > 0f) score += 5f;

            if (!keepsTurn)
            {
                // A vez passa: bola perto do próprio gol é perigo.
                Vector2 ownGoal = new(0f, FieldLayout.GoalLineY(side));
                score -= Mathf.Max(0f, 4f - Vector2.Distance(end, ownGoal)) * 4f;
            }

            return score;
        }
    }
}
