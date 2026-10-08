using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Desenha a ajuda de mira: barra de força, seta do botão e linha curta da bola (sem rebotes).
    /// </summary>
    [RequireComponent(typeof(AimController))]
    public sealed class AimVisuals : MonoBehaviour
    {
        [SerializeField] private AimAssistMode mode = AimAssistMode.Short;
        [Tooltip("Comprimento da seta do botão com força máxima.")]
        [SerializeField] private float maxArrowLength = 2f;
        [SerializeField] private float shortBallLine = 1f;
        [SerializeField] private float fullBallLine = 2.5f;
        [SerializeField] private float powerBarLength = 1.2f;
        [SerializeField] private float lineWidth = 0.06f;

        private AimController aim;
        private Ball ball;
        private LineRenderer arrow;
        private LineRenderer ballLine;
        private LineRenderer powerBar;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[8];

        public AimAssistMode Mode
        {
            get => mode;
            set => mode = value;
        }

        private void Awake()
        {
            aim = GetComponent<AimController>();
            var material = new Material(Shader.Find("Sprites/Default"));
            arrow = CreateLine("Seta do botão", material, Color.white);
            ballLine = CreateLine("Linha da bola", material, new Color(1f, 1f, 1f, 0.6f));
            powerBar = CreateLine("Barra de força", material, Color.green);
            powerBar.widthMultiplier = lineWidth * 3f;
        }

        private void Start()
        {
            ball = FindAnyObjectByType<Ball>();
        }

        private void LateUpdate()
        {
            bool show = aim.HasValidAim;
            powerBar.enabled = show;
            arrow.enabled = show && mode != AimAssistMode.Off;
            ballLine.enabled = false;
            if (!show) return;

            Vector2 origin = aim.AimOrigin;
            float radius = aim.AimRadius;
            Vector2 dir = aim.Direction;

            // Barra de força parada ao lado do botão (ou da bola), em pé e crescendo para cima, de verde a vermelho.
            // Fica à direita; perto da parede da direita passa para a esquerda.
            float offset = radius + 0.25f;
            float sideX = origin.x + offset <= FieldLayout.HalfWidth - 0.1f ? offset : -offset;
            Vector2 barStart = origin + new Vector2(sideX, -powerBarLength * 0.5f);
            powerBar.SetPosition(0, barStart);
            powerBar.SetPosition(1, barStart + Vector2.up * (powerBarLength * aim.Power01));
            var barColor = Color.Lerp(Color.green, Color.red, aim.Power01);
            powerBar.startColor = barColor;
            powerBar.endColor = barColor;

            if (mode == AimAssistMode.Off) return;

            arrow.SetPosition(0, origin + dir * radius);
            arrow.SetPosition(1, origin + dir * (radius + maxArrowLength * aim.Power01));

            // No chute direto na bola a seta já é a direção da bola.
            if (!aim.IsBallKick) DrawBallPrediction(aim.SelectedDisc, origin, dir);
        }

        private void DrawBallPrediction(Disc disc, Vector2 origin, Vector2 dir)
        {
            if (ball == null) return;

            var filter = new ContactFilter2D { useTriggers = false };
            int count = Physics2D.CircleCast(origin, disc.Radius, dir, filter, hits, 20f);
            RaycastHit2D? first = null;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider.isTrigger || hit.collider.attachedRigidbody == disc.Body) continue;
                if (first == null || hit.distance < first.Value.distance) first = hit;
            }

            if (first == null || first.Value.collider.GetComponent<Ball>() == null) return;

            Vector2 discAtContact = origin + dir * first.Value.distance;
            Vector2 ballPos = ball.Body.position;
            Vector2 ballDir = (ballPos - discAtContact).normalized;
            float length = mode == AimAssistMode.Full ? fullBallLine : shortBallLine;

            ballLine.enabled = true;
            ballLine.SetPosition(0, ballPos + ballDir * ball.Radius);
            ballLine.SetPosition(1, ballPos + ballDir * (ball.Radius + length));
        }

        private LineRenderer CreateLine(string lineName, Material material, Color color)
        {
            var go = new GameObject(lineName);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.widthMultiplier = lineWidth;
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = 50;
            line.numCapVertices = 2;
            line.enabled = false;
            return line;
        }
    }
}
