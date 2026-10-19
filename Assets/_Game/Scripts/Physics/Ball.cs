using System.Collections.Generic;
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

        /// <summary>Uma batida da bola desde o último peteleco: no que bateu e, se for botão ou goleiro, de que time.</summary>
        public readonly struct Touch
        {
            public readonly string What;
            public readonly TeamSide? Side;
            public Touch(string what, TeamSide? side)
            {
                What = what;
                Side = side;
            }
        }

        private const int MaxTouches = 16;
        private readonly List<Touch> touches = new();
        /// <summary>As batidas desde o último peteleco, em ordem (para o log da partida). Até 16.</summary>
        public IReadOnlyList<Touch> Touches => touches;

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
            // Sem atrito: a bola sai na linha entre os centros (a mesma da previsão da mira), sem ser arrastada
            // para o lado em que o botão andava.
            GetComponent<CircleCollider2D>().sharedMaterial = new PhysicsMaterial2D("Bola")
            {
                bounciness = tuning.bounciness,
                friction = 0f
            };
        }

        /// <summary>Chamado no início de cada peteleco.</summary>
        public void BeginShot()
        {
            TouchedWallSinceShot = false;
            LastDiscTouch = null;
            LastTouchSide = null;
            touches.Clear();
        }

        private void AddTouch(string what, TeamSide? touchSide)
        {
            if (inGoal || touches.Count >= MaxTouches) return;
            // Encostado empurrando ("Stay") gera a mesma batida vários quadros seguidos: conta uma vez.
            if (touches.Count > 0 && touches[^1].What == what && touches[^1].Side == touchSide) return;
            touches.Add(new Touch(what, touchSide));
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
                AddTouch(disc.name, disc.Side);
                return;
            }

            var keeper = collision.collider.GetComponent<Goalkeeper>();
            if (keeper != null)
            {
                LastDiscTouch = null;
                LastTouchSide = keeper.Side;
                AddTouch("goleiro", keeper.Side);
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            var wall = collision.collider.GetComponent<Wall>();
            if (wall != null)
            {
                TableImpacts.Raise(TableImpactKind.Wall, collision.relativeVelocity.magnitude);
                AddTouch(wall.IsSideWall ? (wall.transform.position.x < 0f ? "parede esquerda" : "parede direita")
                    : wall.gameObject.name.ToLowerInvariant(), null);
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
                AddTouch(disc.name, disc.Side);
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
                AddTouch("goleiro", keeper.Side);
            }
        }
    }
}
