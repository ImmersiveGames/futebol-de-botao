using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Desenho da bola em pixel art. Os quadros de rolagem avançam pela distância que a bola andou e o desenho
    /// gira para a direção do movimento, em passos fixos (8 direções); a luz e a sombra ficam sempre paradas.
    /// Com a bola rápida aparece o rastro atrás dela: o quadro cresce com a velocidade e encolhe quando ela freia.
    /// Montado por "Futebol de Botão/Aplicar artes".
    /// </summary>
    public sealed class BallVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer rolling;
        [SerializeField] private Transform shine;
        [SerializeField] private Transform shadow;
        [SerializeField] private Sprite[] frames = new Sprite[0];
        [Tooltip("Distância que a bola anda por quadro. O desenho anda 2 px por quadro: 2/32 = 0,0625.")]
        [SerializeField] private float distancePerFrame = 0.0625f;
        [Tooltip("Direções do giro do desenho (8 = de 45 em 45°, 4 = de 90 em 90°).")]
        [SerializeField] private int directions = 8;
        [Tooltip("Abaixo desta velocidade o desenho mantém a última direção.")]
        [SerializeField] private float minSpeed = 0.05f;

        [Header("Rastro")]
        [SerializeField] private SpriteRenderer trail;
        [Tooltip("Quadros do rastro; o primeiro é vazio (bola devagar).")]
        [SerializeField] private Sprite[] trailFrames = new Sprite[0];
        [Tooltip("Velocidade (unidades/s) em que aparece o menor rastro.")]
        [SerializeField, Min(0f)] private float trailMinSpeed = 5f;
        [Tooltip("Velocidade (unidades/s) do maior rastro.")]
        [SerializeField, Min(0f)] private float trailMaxSpeed = 9f;
        [Tooltip("Transparência do rastro (0 = invisível, 1 = opaco).")]
        [SerializeField, Range(0f, 1f)] private float trailAlpha = 0.5f;

        // Um salto maior que isso num quadro é a bola sendo reposicionada, não rolando.
        private const float TeleportDistance = 0.5f;

        private Rigidbody2D body;
        private Vector2 lastPosition;
        private float travelled;
        private float angle;

        public void Configure(SpriteRenderer rollingRenderer, Transform shineTransform, Transform shadowTransform, Sprite[] rollingFrames)
        {
            rolling = rollingRenderer;
            shine = shineTransform;
            shadow = shadowTransform;
            frames = rollingFrames;
        }

        public void ConfigureTrail(SpriteRenderer trailRenderer, Sprite[] frames)
        {
            trail = trailRenderer;
            trailFrames = frames;
        }

        private void Awake() => body = GetComponentInParent<Rigidbody2D>();

        private void OnEnable() => lastPosition = transform.position;

        private void LateUpdate()
        {
            Vector2 position = transform.position;
            float moved = (position - lastPosition).magnitude;
            lastPosition = position;
            if (moved < TeleportDistance) travelled += moved;

            var velocity = body != null ? body.linearVelocity : Vector2.zero;
            if (velocity.sqrMagnitude > minSpeed * minSpeed)
            {
                // O desenho original rola para baixo (-Y); ele gira a partir daí.
                float step = 360f / Mathf.Max(1, directions);
                float heading = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg + 90f;
                angle = Mathf.Round(heading / step) * step;
            }

            if (rolling != null)
            {
                if (frames.Length > 0)
                {
                    float cycle = distancePerFrame * frames.Length;
                    travelled = Mathf.Repeat(travelled, cycle);
                    rolling.sprite = frames[Mathf.Min(frames.Length - 1, (int)(travelled / distancePerFrame))];
                }
                rolling.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }

            UpdateTrail(velocity.magnitude);

            if (shine != null) shine.rotation = Quaternion.identity;
            if (shadow != null) shadow.rotation = Quaternion.identity;
        }

        /// <summary>O desenho do rastro sai para baixo (bola subindo): gira meia volta a partir do giro da bola.</summary>
        private void UpdateTrail(float speed)
        {
            if (trail == null || trailFrames.Length < 2) return;
            bool shown = speed >= trailMinSpeed;
            trail.enabled = shown;
            var color = trail.color;
            if (!Mathf.Approximately(color.a, trailAlpha)) trail.color = new Color(color.r, color.g, color.b, trailAlpha);
            if (!shown) return;
            float t = Mathf.InverseLerp(trailMinSpeed, Mathf.Max(trailMaxSpeed, trailMinSpeed + 0.01f), speed);
            int last = trailFrames.Length - 1;
            trail.sprite = trailFrames[Mathf.Clamp(1 + (int)(t * last), 1, last)];
            trail.transform.rotation = Quaternion.Euler(0f, 0f, angle + 180f);
        }
    }
}
