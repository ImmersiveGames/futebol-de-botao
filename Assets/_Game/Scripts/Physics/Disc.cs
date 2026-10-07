using System;
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
        /// <summary>Este botão tocou a bola desde o último <see cref="BeginShot"/>.</summary>
        public bool TouchedBallThisShot { get; private set; }

        /// <summary>
        /// Falta: este botão, depois de um peteleco, acertou um adversário antes da bola.
        /// (quem fez, quem sofreu, posição de quem sofreu no contato)
        /// </summary>
        public event Action<Disc, Disc, Vector2> Fouled;

        private bool shooting;
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

        /// <summary>Chamado quando este botão recebe o peteleco: passa a vigiar falta e toque na bola.</summary>
        public void BeginShot()
        {
            shooting = true;
            TouchedBallThisShot = false;
        }

        public void EndShot() => shooting = false;

        private void OnCollisionEnter2D(Collision2D collision)
        {
            WallElastic.OnEnter(body, collision, tuning);
            if (!shooting) return;

            if (collision.collider.GetComponent<Ball>() != null)
            {
                TouchedBallThisShot = true;
                return;
            }

            var other = collision.collider.GetComponent<Disc>();
            if (!TouchedBallThisShot && other != null && other.Side != side)
            {
                shooting = false;
                Fouled?.Invoke(this, other, other.Body.position);
            }
        }

        public void ResetToStart() => PlaceAt(startPosition);

        public void PlaceAt(Vector2 position)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = position;
            transform.position = position;
        }
    }
}
