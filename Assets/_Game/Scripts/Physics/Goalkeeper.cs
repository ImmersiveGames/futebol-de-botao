using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Goleiro caixinha: desliza só no eixo X, dentro dos limites da área, com velocidade limitada.
    /// Não empurra nada: para encostado na bola ou num botão que estiver no caminho (é cinemático, empurraria com força
    /// infinita). Quando a bola vem nele, ela bate e volta normalmente.
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

        // Folga até a bola/botão: a física 2D já encosta (e empurra) a 2 x Default Contact Offset (0,01 cada collider).
        private const float ContactGap = 0.03f;

        private readonly RaycastHit2D[] hits = new RaycastHit2D[8];
        private ContactFilter2D blockers = new() { useTriggers = false };
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
            float step = Mathf.MoveTowards(position.x, targetX, speed * Time.fixedDeltaTime) - position.x;
            if (Mathf.Approximately(step, 0f)) return;
            position.x += Allowed(step);
            body.MovePosition(position);
        }

        /// <summary>Quanto dá para andar de <paramref name="step"/> sem entrar na bola ou num botão (para encostado).</summary>
        private float Allowed(float step)
        {
            var direction = new Vector2(Mathf.Sign(step), 0f);
            float distance = Mathf.Abs(step);
            int count = body.Cast(direction, blockers, hits, distance + ContactGap);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                // Só o que se mexe segura o goleiro (bola e botões); o que fica para trás não.
                if (hit.rigidbody == null || hit.rigidbody.bodyType != RigidbodyType2D.Dynamic) continue;
                if (Vector2.Dot(hit.normal, direction) >= 0f) continue;
                distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - ContactGap));
            }
            return direction.x * distance;
        }
    }
}
