using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>A bola. Lembra se tocou uma parede desde o último peteleco.</summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class Ball : MonoBehaviour
    {
        [SerializeField] private PhysicsTuning tuning;

        private Rigidbody2D body;
        private Vector2 startPosition;
        private bool inGoal;

        private const float GoalDamping = 10f;

        public Rigidbody2D Body => body;
        public bool TouchedWallSinceShot { get; private set; }
        public Disc LastDiscTouch { get; private set; }
        /// <summary>Time do último botão ou goleiro que tocou a bola desde o último peteleco. Nulo: ninguém tocou.</summary>
        public TeamSide? LastTouchSide { get; private set; }

        private Vector2 velocityBeforeStep;
        public float Radius => GetComponent<CircleCollider2D>().radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            startPosition = body.position;
            ApplyTuning();
        }

        public void Configure(PhysicsTuning physicsTuning)
        {
            tuning = physicsTuning;
        }

        public void ApplyTuning()
        {
            if (tuning == null || body == null) return;
            body.gravityScale = 0f;
            body.mass = tuning.ballMass;
            body.linearDamping = tuning.ballLinearDamping;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            GetComponent<CircleCollider2D>().sharedMaterial = new PhysicsMaterial2D("Bola")
            {
                bounciness = tuning.bounciness,
                friction = tuning.friction
            };
        }

        /// <summary>Chamado no início de cada peteleco.</summary>
        public void BeginShot()
        {
            TouchedWallSinceShot = false;
            LastDiscTouch = null;
            LastTouchSide = null;
        }

        // Roda antes do passo de física; as batidas são avisadas depois dele.
        private void FixedUpdate() => velocityBeforeStep = body.linearVelocity;

        /// <summary>Marca um toque de <paramref name="side"/> sem colisão (chute direto na bola).</summary>
        public void MarkTouchedBy(TeamSide side)
        {
            LastDiscTouch = null;
            LastTouchSide = side;
        }

        /// <summary>Gol marcado: a bola continua rolando até a rede e para nela, sem quicar para fora.</summary>
        public void StopInGoal()
        {
            inGoal = true;
            body.angularVelocity = 0f;
        }

        public void ResetTo(Vector2 position)
        {
            inGoal = false;
            if (tuning != null) body.linearDamping = tuning.ballLinearDamping;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = position;
            transform.position = position;
            BeginShot();
        }

        public void ResetToStart() => ResetTo(startPosition);

        // Botão que já estava encostado na bola e a empurra não gera batida nova ("Enter"); conta pelo "Stay".
        private void OnCollisionStay2D(Collision2D collision)
        {
            if (!Disc.IsPushing(collision)) return;
            var disc = collision.collider.GetComponent<Disc>();
            if (disc != null)
            {
                TableImpacts.Raise(TableImpactKind.BallDisc, collision.relativeVelocity.magnitude);
                LastDiscTouch = disc;
                LastTouchSide = disc.Side;
                return;
            }

            var keeper = collision.collider.GetComponent<Goalkeeper>();
            if (keeper != null)
            {
                LastDiscTouch = null;
                LastTouchSide = keeper.Side;
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            var wall = collision.collider.GetComponent<Wall>();
            if (wall != null)
            {
                TableImpacts.Raise(TableImpactKind.Wall, collision.relativeVelocity.magnitude);
                // Só a parede lateral anula o gol: trave, fundo e rede não contam.
                if (wall.IsSideWall) TouchedWallSinceShot = true;
                if (inGoal)
                {
                    // Bateu na rede depois do gol: para ali mesmo.
                    body.linearVelocity = Vector2.zero;
                    body.linearDamping = GoalDamping;
                    return;
                }
                WallElastic.OnEnter(body, collision, tuning);
                return;
            }

            var disc = collision.collider.GetComponent<Disc>();
            if (disc != null)
            {
                TableImpacts.Raise(TableImpactKind.BallDisc, collision.relativeVelocity.magnitude);
                LastDiscTouch = disc;
                LastTouchSide = disc.Side;
                // Botão parado que não é o do peteleco: a bola rebate nele e ele fica no lugar.
                if (!disc.IsShotDisc && tuning != null)
                    BallRebound.Apply(body, velocityBeforeStep, disc.Body, disc.VelocityBeforeStep, tuning.bounciness);
                return;
            }

            var keeper = collision.collider.GetComponent<Goalkeeper>();
            if (keeper != null)
            {
                TableImpacts.Raise(TableImpactKind.BallKeeper, collision.relativeVelocity.magnitude);
                LastDiscTouch = null;
                LastTouchSide = keeper.Side;
            }
        }
    }
}
