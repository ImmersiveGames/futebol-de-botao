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

        public Rigidbody2D Body => body;
        public bool TouchedWallSinceShot { get; private set; }
        public Disc LastDiscTouch { get; private set; }
        /// <summary>Time do último botão ou goleiro que tocou a bola desde o último peteleco. Nulo: ninguém tocou.</summary>
        public TeamSide? LastTouchSide { get; private set; }
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

        public void ResetTo(Vector2 position)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = position;
            transform.position = position;
            BeginShot();
        }

        public void ResetToStart() => ResetTo(startPosition);

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.collider.GetComponent<Wall>() != null)
            {
                TouchedWallSinceShot = true;
                WallElastic.OnEnter(body, collision, tuning);
                return;
            }

            var disc = collision.collider.GetComponent<Disc>();
            if (disc != null)
            {
                LastDiscTouch = disc;
                LastTouchSide = disc.Side;
                return;
            }

            var keeper = collision.collider.GetComponent<Goalkeeper>();
            if (keeper != null) LastTouchSide = keeper.Side;
        }
    }
}
