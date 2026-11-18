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

        /// <summary>
        /// Este botão, sem ser o do peteleco, bateu num adversário (ex.: empurrado por um companheiro). Quem decide se
        /// é falta é a partida. (este botão, o adversário, posição do adversário no contato)
        /// </summary>
        public event Action<Disc, Disc, Vector2> HitOpponent;

        /// <summary>
        /// Este botão bateu no goleiro adversário: como botão do peteleco antes de tocar a bola (true) ou empurrado por
        /// outro (false). Quem decide se é falta é a partida.
        /// </summary>
        public event Action<Disc, Goalkeeper, bool> HitOpponentKeeper;

        private bool shooting;
        private bool shotDisc;
        private readonly ContactPoint2D[] contacts = new ContactPoint2D[16];
        public Rigidbody2D Body => body;
        /// <summary>Este é o botão do peteleco atual (empurra a bola; os outros fazem a bola rebater).</summary>
        public bool IsShotDisc => shotDisc;
        /// <summary>Velocidade antes do passo de física atual (para refazer a batida da bola).</summary>
        public Vector2 VelocityBeforeStep { get; private set; }
        private CircleCollider2D circle;
        /// <summary>O colisor, guardado na primeira leitura (o raio é lido muitas vezes por quadro pela IA e pelo posicionamento).</summary>
        private CircleCollider2D Circle => circle != null ? circle : circle = GetComponent<CircleCollider2D>();
        public float Radius => Circle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);

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
            Circle.sharedMaterial = new PhysicsMaterial2D("Botão")
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
            shotDisc = true;
            TouchedBallThisShot = false;
        }

        public void EndShot()
        {
            shooting = false;
            shotDisc = false;
        }

        // Roda antes do passo de física; as batidas são avisadas depois dele.
        private void FixedUpdate() => VelocityBeforeStep = body.linearVelocity;

        private void OnCollisionEnter2D(Collision2D collision)
        {
            WallElastic.OnEnter(body, collision, tuning);
            // Cada componente é procurado uma vez só por batida.
            var hit = collision.collider;
            hit.TryGetComponent(out Disc other);
            RaiseImpact(collision, other);
            hit.TryGetComponent(out Goalkeeper keeper);
            if (!shooting)
            {
                if (other != null && other.Side != side) HitOpponent?.Invoke(this, other, other.Body.position);
                else if (keeper != null && keeper.Side != side) HitOpponentKeeper?.Invoke(this, keeper, false);
                return;
            }

            if (hit.TryGetComponent(out Ball _))
            {
                TouchedBallThisShot = true;
                return;
            }

            if (keeper != null)
            {
                if (keeper.Side == side) return;
                if (!TouchedBallThisShot && TouchingBall()) TouchedBallThisShot = true;
                if (!TouchedBallThisShot)
                {
                    shooting = false;
                    HitOpponentKeeper?.Invoke(this, keeper, true);
                }
                return;
            }

            // Bola de raspão e adversário no mesmo passo de física: a Unity avisa as batidas em qualquer ordem,
            // então confere se a bola também está encostando agora antes de marcar falta.
            if (!TouchedBallThisShot && other != null && other.Side != side && TouchingBall()) TouchedBallThisShot = true;
            if (!TouchedBallThisShot && other != null && other.Side != side)
            {
                shooting = false;
                Fouled?.Invoke(this, other, other.Body.position);
            }
        }

        // Botão já encostado na bola antes do peteleco: a Unity não avisa uma batida nova ("Enter"), só que o
        // contato continua ("Stay"). Se o botão empurra a bola nesse contato, conta como tocar na bola.
        private void OnCollisionStay2D(Collision2D collision)
        {
            if (!shooting || TouchedBallThisShot) return;
            if (collision.collider.TryGetComponent(out Ball _) && IsPushing(collision)) TouchedBallThisShot = true;
        }

        /// <summary>
        /// Som da batida: botão com botão ou botão na parede (a bola avisa as dela). Os dois botões avisam a mesma
        /// batida; o MatchSounds não repete o mesmo som no mesmo instante, então toca uma vez só.
        /// </summary>
        private void RaiseImpact(Collision2D collision, Disc other)
        {
            if (other != null)
            {
                TableImpacts.Raise(TableImpactKind.DiscDisc, collision.relativeVelocity.magnitude);
                return;
            }
            if (collision.collider.TryGetComponent(out Wall _)) TableImpacts.Raise(TableImpactKind.Wall, collision.relativeVelocity.magnitude);
        }

        /// <summary>O contato está trocando força (não é só um encostar parado).</summary>
        public static bool IsPushing(Collision2D collision)
        {
            for (int i = 0; i < collision.contactCount; i++)
                if (collision.GetContact(i).normalImpulse > 0.0001f) return true;
            return false;
        }

        private bool TouchingBall()
        {
            int count = body.GetContacts(contacts);
            var own = Circle;
            for (int i = 0; i < count; i++)
            {
                var hit = contacts[i].collider == own ? contacts[i].otherCollider : contacts[i].collider;
                if (hit != null && hit.TryGetComponent(out Ball _)) return true;
            }
            return false;
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
