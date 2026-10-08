using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Goleiro caixinha: desliza só no eixo X, dentro dos limites da área, com velocidade limitada.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class Goalkeeper : MonoBehaviour
    {
        [SerializeField] private TeamSide side;
        [SerializeField] private PhysicsTuning tuning;
        [Tooltip("Metade da largura em que o goleiro pode deslizar, a partir do centro do gol.")]
        [SerializeField] private float halfRange = 1.2f;
        [Tooltip("Só para testes da fase 1: segue a bola sozinho.")]
        [SerializeField] private bool trackBallForTesting;
        [SerializeField] private Ball ball;

        private Rigidbody2D body;
        private float centerX;
        private float targetX;

        public TeamSide Side => side;
        /// <summary>Menor e maior X onde o goleiro pode ficar.</summary>
        public float MinX => centerX - halfRange;
        public float MaxX => centerX + halfRange;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            centerX = body.position.x;
            targetX = centerX;
        }

        public void Configure(TeamSide teamSide, PhysicsTuning physicsTuning, float range, Ball trackedBall)
        {
            side = teamSide;
            tuning = physicsTuning;
            halfRange = range;
            ball = trackedBall;
        }

        /// <summary>Define para onde o goleiro deve deslizar (posição X no mundo).</summary>
        public void SetTargetX(float worldX)
        {
            targetX = Mathf.Clamp(worldX, centerX - halfRange, centerX + halfRange);
        }

        /// <summary>Volta para o centro do gol na hora.</summary>
        public void ResetToCenter()
        {
            targetX = centerX;
            var position = body.position;
            position.x = centerX;
            body.position = position;
            transform.position = position;
        }

        private void FixedUpdate()
        {
            if (trackBallForTesting && ball != null) SetTargetX(ball.Body.position.x);

            float speed = tuning != null ? tuning.goalkeeperSpeed : 3f;
            var position = body.position;
            position.x = Mathf.MoveTowards(position.x, targetX, speed * Time.fixedDeltaTime);
            body.MovePosition(position);
        }
    }
}
