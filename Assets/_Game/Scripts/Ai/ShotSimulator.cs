using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FutebolDeBotao
{
    /// <summary>Como terminou uma jogada simulada.</summary>
    public struct SimResult
    {
        /// <summary>O botão do peteleco (ou um companheiro empurrado por ele) acertou um adversário antes da bola.</summary>
        public bool Foul;
        /// <summary>Onde estava o botão atingido (falta na área de quem fez = pênalti).</summary>
        public Vector2 FoulPosition;
        /// <summary>O botão do peteleco tocou a bola.</summary>
        public bool TouchedBall;
        /// <summary>Time do último botão ou goleiro que tocou a bola. Nulo: ninguém.</summary>
        public TeamSide? LastTouchSide;
        public bool BallTouchedWall;
        /// <summary>A bola entrou no gol de quem defende este lado. Nulo: não entrou.</summary>
        public TeamSide? GoalOf;
        public Vector2 BallEnd;
        /// <summary>Onde o botão do peteleco parou (ou estava, se a jogada parou numa falta).</summary>
        public Vector2 ShooterEnd;
        public int Steps;
    }

    /// <summary>
    /// Mesa invisível para a IA testar jogadas: uma cópia das paredes, botões, bola e goleiros numa cena com física
    /// própria (PhysicsScene2D), que só anda quando a IA manda. Os scripts do jogo não rodam na cópia; o quique
    /// elástico das paredes é refeito aqui, e o empurrão das paredes (WallRepulsion) fica de fora.
    /// </summary>
    public sealed class ShotSimulator : IDisposable
    {
        private enum Kind { Disc, Ball, Keeper }

        private sealed class SimBody
        {
            public Rigidbody2D Source;
            public Rigidbody2D Copy;
            public Collider2D Collider;
            public Kind Kind;
            public TeamSide Side;
            public bool WasOnWall;
            public bool WasOnBall;
            public Vector2 VelocityBefore;
        }

        private const float MaxSeconds = 4f;
        private static int sceneCount;

        private readonly PhysicsTuning tuning;
        private readonly List<SimBody> bodies = new();
        private readonly Dictionary<Rigidbody2D, SimBody> bySource = new();
        private readonly Dictionary<Collider2D, SimBody> byCollider = new();
        private readonly HashSet<Collider2D> walls = new();
        private readonly HashSet<Collider2D> pushingWalls = new();
        private readonly ContactPoint2D[] contacts = new ContactPoint2D[16];
        private readonly HashSet<SimBody> touching = new();
        private readonly List<(Wall Source, BoxCollider2D Copy)> wallCopies = new();
        private Scene scene;
        private PhysicsScene2D physics;
        private SimBody ball;
        private float ballRadius = 0.2f;
        private TeamSide? keeperOverrideSide;
        private float keeperOverrideX;

        /// <summary>Passos de física simulados desde a criação (para o log de desempenho).</summary>
        public int TotalSteps { get; private set; }

        public ShotSimulator(PhysicsTuning physicsTuning, IEnumerable<Disc> allDiscs, Ball realBall, IEnumerable<Goalkeeper> keepers)
        {
            tuning = physicsTuning;
            scene = SceneManager.CreateScene($"Simulação da IA {++sceneCount}", new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            physics = scene.GetPhysicsScene2D();

            foreach (var wall in Object.FindObjectsByType<Wall>())
            {
                if (!wall.TryGetComponent(out BoxCollider2D box) || box.isTrigger) continue;
                var copy = CopyBox(box);
                wallCopies.Add((wall, copy));
                walls.Add(copy);
                if (wall.PushesBack) pushingWalls.Add(copy);
            }

            foreach (var disc in allDiscs)
                AddBody(disc.GetComponent<Rigidbody2D>(), CopyCircle(disc.GetComponent<CircleCollider2D>()), Kind.Disc, disc.Side);

            if (realBall != null) ballRadius = realBall.Radius;
            if (realBall != null)
                ball = AddBody(realBall.GetComponent<Rigidbody2D>(), CopyCircle(realBall.GetComponent<CircleCollider2D>()), Kind.Ball, TeamSide.Bottom);

            foreach (var keeper in keepers)
                AddBody(keeper.GetComponent<Rigidbody2D>(), CopyBox(keeper.GetComponent<BoxCollider2D>()), Kind.Keeper, keeper.Side);
        }

        /// <summary>Copia quem está ligado na mesa de verdade (3 ou 5 botões, com ou sem goleiro). Chame antes de cada pensamento.</summary>
        public void Sync()
        {
            foreach (var body in bodies)
            {
                bool active = body.Source != null && body.Source.gameObject.activeInHierarchy;
                if (body.Copy.gameObject.activeSelf != active) body.Copy.gameObject.SetActive(active);
            }
            foreach (var (source, copy) in wallCopies)
            {
                bool active = source != null && source.gameObject.activeInHierarchy;
                if (copy.gameObject.activeSelf != active) copy.gameObject.SetActive(active);
            }
        }

        /// <summary>Largura da boca do gol para contar gol (menor sem goleiro).</summary>
        public float GoalWidth { get; set; } = FieldLayout.GoalWidth;

        /// <summary>Nas próximas simulações, o goleiro de <paramref name="side"/> fica em <paramref name="x"/> (o goleiro da IA testando posições).</summary>
        public void OverrideKeeperX(TeamSide side, float x)
        {
            keeperOverrideSide = side;
            keeperOverrideX = x;
        }

        public void ClearKeeperOverride() => keeperOverrideSide = null;

        /// <summary>
        /// Simula um peteleco a partir da mesa de verdade como está agora.
        /// <paramref name="shooter"/> nulo = chute direto na bola (tiro de meta).
        /// </summary>
        /// <summary>Opção da partida: desligada, bater em adversário não para a jogada.</summary>
        public bool FoulsEnabled { get; set; } = true;

        public SimResult Simulate(Rigidbody2D shooter, Vector2 impulse, TeamSide shooterSide)
        {
            var result = new SimResult();
            if (ball == null) return result;

            foreach (var body in bodies)
            {
                if (!body.Copy.gameObject.activeSelf) continue;
                body.Copy.position = body.Source.position;
                body.Copy.rotation = body.Source.rotation;
                body.Copy.linearVelocity = Vector2.zero;
                body.Copy.angularVelocity = 0f;
                body.WasOnWall = false;
                body.WasOnBall = false;
                if (body.Kind == Kind.Keeper && body.Side == keeperOverrideSide)
                    body.Copy.position = new Vector2(keeperOverrideX, body.Source.position.y);
            }

            SimBody shot = shooter != null && bySource.TryGetValue(shooter, out var found) ? found : ball;
            shot.Copy.AddForce(impulse, ForceMode2D.Impulse);
            bool shooterDecided = shot == ball;

            float dt = Time.fixedDeltaTime;
            int maxSteps = Mathf.CeilToInt(MaxSeconds / dt);
            float restSpeed = tuning != null ? tuning.restSpeed : 0.05f;

            for (int step = 0; step < maxSteps; step++)
            {
                foreach (var body in bodies) body.VelocityBefore = body.Copy.linearVelocity;
                physics.Simulate(dt);
                TotalSteps++;
                ReboundOffRestingDiscs(shot);
                result.Steps = step + 1;
                bool moving = false;

                foreach (var body in bodies)
                {
                    if (body.Kind == Kind.Keeper || !body.Copy.gameObject.activeSelf) continue;

                    int count = body.Collider.GetContacts(contacts);
                    bool onWall = false;
                    Vector2 wallPoint = default;

                    // Igual ao jogo: bola e adversário no mesmo passo conta como bola primeiro.
                    if (!shooterDecided && body == shot)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            var touched = contacts[i].collider == body.Collider ? contacts[i].otherCollider : contacts[i].collider;
                            if (byCollider.TryGetValue(touched, out var touchedBody) && touchedBody == ball)
                            {
                                result.TouchedBall = true;
                                shooterDecided = true;
                            }
                        }
                    }

                    for (int i = 0; i < count; i++)
                    {
                        var other = contacts[i].collider == body.Collider ? contacts[i].otherCollider : contacts[i].collider;
                        if (pushingWalls.Contains(other))
                        {
                            onWall = true;
                            wallPoint = contacts[i].point;
                        }

                        byCollider.TryGetValue(other, out var otherBody);

                        if (body == ball)
                        {
                            if (walls.Contains(other)) result.BallTouchedWall = true;
                            else if (otherBody != null) result.LastTouchSide = otherBody.Side;
                        }

                        if (!shooterDecided && body == shot)
                        {
                            if (otherBody == ball)
                            {
                                result.TouchedBall = true;
                                shooterDecided = true;
                            }
                            else if (FoulsEnabled && otherBody != null && otherBody.Kind == Kind.Disc && otherBody.Side != shooterSide)
                            {
                                // O jogo para na falta.
                                result.Foul = true;
                                result.FoulPosition = otherBody.Copy.position;
                                result.BallEnd = ball.Copy.position;
                                result.ShooterEnd = shot.Copy.position;
                                return result;
                            }
                        }
                    }

                    // Companheiro empurrado que bate num adversário antes de alguém tocar a bola: falta, como no jogo.
                    if (FoulsEnabled && body != shot && body.Kind == Kind.Disc && body.Side == shooterSide &&
                        result.LastTouchSide == null && body.Copy.linearVelocity.sqrMagnitude > restSpeed * restSpeed)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            var other = contacts[i].collider == body.Collider ? contacts[i].otherCollider : contacts[i].collider;
                            if (!byCollider.TryGetValue(other, out var otherBody) || otherBody.Kind != Kind.Disc ||
                                otherBody.Side == shooterSide) continue;
                            result.Foul = true;
                            result.FoulPosition = otherBody.Copy.position;
                            result.BallEnd = ball.Copy.position;
                            result.ShooterEnd = shot.Copy.position;
                            return result;
                        }
                    }

                    if (onWall && !body.WasOnWall) Bounce(body.Copy, wallPoint);
                    body.WasOnWall = onWall;

                    if (body.Copy.linearVelocity.sqrMagnitude > restSpeed * restSpeed) moving = true;
                }

                // A bola entra no gol quando passa inteira da linha de fundo entre as traves (GoalTrigger).
                var ballPosition = ball.Copy.position;
                if (Mathf.Abs(ballPosition.x) < GoalWidth * 0.5f && FieldLayout.BallFullyInGoal(ballPosition, ballRadius))
                {
                    result.GoalOf = ballPosition.y > 0f ? TeamSide.Top : TeamSide.Bottom;
                    break;
                }

                if (!moving && step > 2) break;
            }

            result.BallEnd = ball.Copy.position;
            result.ShooterEnd = shot.Copy.position;
            return result;
        }

        /// <summary>Mesma regra do jogo (BallRebound): a bola rebate nos botões parados que não são o do peteleco.</summary>
        private void ReboundOffRestingDiscs(SimBody shot)
        {
            int count = ball.Collider.GetContacts(contacts);
            touching.Clear();
            for (int i = 0; i < count; i++)
            {
                var other = contacts[i].collider == ball.Collider ? contacts[i].otherCollider : contacts[i].collider;
                if (!byCollider.TryGetValue(other, out var otherBody)) continue;
                if (otherBody.Kind != Kind.Disc || !touching.Add(otherBody)) continue;
                // Só a batida nova, como o OnCollisionEnter2D do jogo.
                if (otherBody != shot && !otherBody.WasOnBall && tuning != null)
                    BallRebound.Apply(ball.Copy, ball.VelocityBefore, otherBody.Copy, otherBody.VelocityBefore, tuning.bounciness);
            }
            foreach (var body in bodies)
                if (body.Kind == Kind.Disc) body.WasOnBall = touching.Contains(body);
        }

        /// <summary>Mesma regra do WallElastic: sai da parede com um pouco de ganho.</summary>
        private void Bounce(Rigidbody2D body, Vector2 contactPoint)
        {
            if (tuning == null) return;
            var away = body.position - contactPoint;
            if (away.sqrMagnitude < 0.0001f) return;
            var normal = away.normalized;

            var velocity = body.linearVelocity;
            float outward = Vector2.Dot(velocity, normal);
            float desired = Mathf.Max(outward * tuning.wallBounceBoost, tuning.wallMinBounceSpeed);
            if (desired > outward) body.linearVelocity = velocity + normal * (desired - outward);
        }

        public void Dispose()
        {
            if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
            bodies.Clear();
            bySource.Clear();
            byCollider.Clear();
        }

        // ---- Montagem da cópia ----

        private SimBody AddBody(Rigidbody2D source, Collider2D copyCollider, Kind kind, TeamSide side)
        {
            var copy = copyCollider.gameObject.AddComponent<Rigidbody2D>();
            copy.bodyType = source.bodyType;
            copy.gravityScale = 0f;
            if (source.bodyType == RigidbodyType2D.Dynamic) copy.mass = source.mass;
            copy.linearDamping = source.linearDamping;
            copy.angularDamping = source.angularDamping;
            copy.collisionDetectionMode = source.collisionDetectionMode;
            copy.constraints = source.constraints;

            var body = new SimBody { Source = source, Copy = copy, Collider = copyCollider, Kind = kind, Side = side };
            bodies.Add(body);
            bySource[source] = body;
            byCollider[copyCollider] = body;
            return body;
        }

        private BoxCollider2D CopyBox(BoxCollider2D source)
        {
            var box = NewObject(source).AddComponent<BoxCollider2D>();
            box.size = source.size;
            box.offset = source.offset;
            box.edgeRadius = source.edgeRadius;
            box.sharedMaterial = source.sharedMaterial;
            return box;
        }

        private CircleCollider2D CopyCircle(CircleCollider2D source)
        {
            var circle = NewObject(source).AddComponent<CircleCollider2D>();
            circle.radius = source.radius;
            circle.offset = source.offset;
            circle.sharedMaterial = source.sharedMaterial;
            return circle;
        }

        private GameObject NewObject(Component source)
        {
            var go = new GameObject(source.name);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.layer = source.gameObject.layer;
            go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            go.transform.localScale = source.transform.lossyScale;
            go.SetActive(source.gameObject.activeInHierarchy);
            return go;
        }
    }
}
