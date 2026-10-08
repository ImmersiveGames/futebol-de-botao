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

        private static readonly ContactFilter2D NoFilter = ContactFilter2D.noFilter;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[16];
        private readonly TeamSide side;
        private readonly PhysicsTuning tuning;
        private readonly Ball ball;

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
                AddDiscCandidates(list, disc, ballPosition, target, scoring);

            return list;
        }

        /// <summary>Chute direto na bola, no tiro de meta.</summary>
        public List<ShotCandidate> BallKickCandidates()
        {
            var list = new List<ShotCandidate>();
            Vector2 ballPosition = ball.Body.position;
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

            foreach (var candidate in DiscCandidates(usable, true))
                if (!candidate.FoulRisk && !candidate.Blocked && !candidate.BallBlocked && candidate.Power01 < 1f) return true;
            return false;
        }

        private void AddDiscCandidates(List<ShotCandidate> list, Disc disc, Vector2 ballPosition, Vector2 target, bool scoring)
        {
            // Sinuca: para a bola ir na direção do alvo, o botão tem que bater nela pelo lado oposto ("bola fantasma").
            Vector2 toTarget = target - ballPosition;
            float length = toTarget.magnitude;
            if (length < 0.1f) return;
            Vector2 ballDirection = toTarget / length;

            Vector2 discPosition = disc.Body.position;
            Vector2 ghost = ballPosition - ballDirection * (ball.Radius + disc.Radius);
            Vector2 toGhost = ghost - discPosition;
            float travel = toGhost.magnitude;
            if (travel < 0.01f) return;
            Vector2 aimDirection = toGhost / travel;

            // Corte fino demais (mais de ~70°) não leva a bola para onde se quer.
            float cut = Vector2.Dot(aimDirection, ballDirection);
            if (cut < 0.35f) return;

            float power = PowerFor(disc, travel, cut, scoring ? length + 1.5f : length);
            CheckDiscPath(disc, aimDirection, travel, out bool foulRisk, out bool blocked);
            bool ballBlocked = BallPathBlocked(ballPosition, ballDirection, length, disc);

            float preScore = scoring ? 20f : Progress(toTarget) * 2f;
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
                    PreScore = preScore
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
            foreach (float x in GoalOffsets) targets.Add(new Vector2(x, y));
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
                if (other != null && other.Side != side) foulRisk = true;
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
                if (collider.GetComponent<Goalkeeper>() != null) return true;
            }
            return false;
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
            int touchesLeftAfter, float foulCaution)
        {
            Vector2 start = ball.Body.position;

            if (result.Foul)
            {
                bool penalty = FieldLayout.InArea(result.FoulPosition, side);
                // A falta tira nota mas não é proibida: no Fácil a IA liga menos para ela.
                return -(penalty ? 120f : 45f) * Mathf.Lerp(0.3f, 1f, foulCaution);
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

            if (FieldLayout.InKeeperZone(end, Opponent)) score -= 15f;
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
