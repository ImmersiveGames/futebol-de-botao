using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>Um botão do time. Recebe o peteleco como impulso.</summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class Disc : MonoBehaviour
    {
        [SerializeField] private TeamSide side;
        [SerializeField] private PhysicsTuning tuning;

        private Rigidbody2D body;
        private Vector2 startPosition;

        public TeamSide Side => side;
        public Rigidbody2D Body => body;
        public float Radius => GetComponent<CircleCollider2D>().radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            startPosition = body.position;
            ApplyTuning();
        }

        public void Configure(TeamSide teamSide, PhysicsTuning physicsTuning)
        {
            side = teamSide;
            tuning = physicsTuning;
        }

        public void ApplyTuning()
        {
            if (tuning == null || body == null) return;
            body.gravityScale = 0f;
            body.mass = tuning.discMass;
            body.linearDamping = tuning.discLinearDamping;
            body.angularDamping = tuning.discAngularDamping;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            GetComponent<CircleCollider2D>().sharedMaterial = new PhysicsMaterial2D("Botão")
            {
                bounciness = tuning.bounciness,
                friction = tuning.friction
            };
        }

        public void Flick(Vector2 impulse)
        {
            body.AddForce(impulse, ForceMode2D.Impulse);
        }

        public void ResetToStart()
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = startPosition;
            transform.position = startPosition;
        }
    }
}
