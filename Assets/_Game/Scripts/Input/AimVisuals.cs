using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Desenha a ajuda de mira: a faixa de força em volta do botão (cresce da borda para fora, de verde a vermelho,
    /// com um anel fraco no máximo), a mira de bolinhas andando na direção do chute e, na Completa, a linha da bola
    /// em bolinhas menores (sem rebotes). Tudo desenhado por código até a arte final.
    /// </summary>
    [RequireComponent(typeof(AimController))]
    public sealed class AimVisuals : MonoBehaviour
    {
        private const int CircleSegments = 48;

        [SerializeField] private AimAssistMode mode = AimAssistMode.Full;
        [Tooltip("Comprimento da mira de bolinhas com força máxima.")]
        [SerializeField] private float maxArrowLength = 2f;
        [Tooltip("Comprimento da linha da bola (só na ajuda Completa).")]
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("fullBallLine")] private float ballLineLength = 2.5f;

        [Header("Faixa de força")]
        [Tooltip("Largura da faixa em volta do botão com força máxima.")]
        [SerializeField] private float maxPowerBand = 1.2f;
        [SerializeField, Range(0f, 1f)] private float powerBandAlpha = 0.35f;
        [SerializeField, Range(0f, 1f)] private float maxRingAlpha = 0.25f;
        [SerializeField] private float maxRingWidth = 0.03f;

        [Header("Bolinhas")]
        [SerializeField] private float dotSpacing = 0.2f;
        [SerializeField] private float dotSize = 0.09f;
        [SerializeField] private float tipDotSize = 0.16f;
        [Tooltip("Velocidade das bolinhas andando para frente (unidades/s).")]
        [SerializeField] private float dotSpeed = 0.5f;
        [SerializeField] private float ballDotSize = 0.06f;
        [SerializeField, Range(0f, 1f)] private float ballDotAlpha = 0.5f;

        private AimController aim;
        private Ball ball;
        private LineRenderer powerBand;
        private LineRenderer maxRing;
        private Sprite dotSprite;
        private readonly List<SpriteRenderer> aimDots = new();
        private readonly List<SpriteRenderer> ballDots = new();
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
            powerBand = CreateCircle("Faixa de força", material);
            maxRing = CreateCircle("Força máxima", material);
            maxRing.widthMultiplier = maxRingWidth;
            dotSprite = CreateDotSprite();
        }

        private void Start()
        {
            ball = FindAnyObjectByType<Ball>();
        }

        private void LateUpdate()
        {
            bool show = aim.HasValidAim;
            powerBand.enabled = show;
            maxRing.enabled = show;
            int aimCount = 0;
            int ballCount = 0;
            if (show)
            {
                Vector2 origin = aim.AimOrigin;
                float radius = aim.AimRadius;
                Vector2 dir = aim.Direction;
                float power = aim.Power01;

                DrawPowerBand(origin, radius, power);

                if (mode != AimAssistMode.Off)
                {
                    aimCount = DrawDots(aimDots, origin + dir * radius, dir, maxArrowLength * power, dotSize, tipDotSize, Color.white);
                    // No chute direto na bola a mira já é a direção da bola. Na Curta fica só a mira, sem a linha da bola.
                    if (mode == AimAssistMode.Full && !aim.IsBallKick) ballCount = DrawBallPrediction(aim.SelectedDisc, origin, dir);
                }
            }
            HideFrom(aimDots, aimCount);
            HideFrom(ballDots, ballCount);
        }

        /// <summary>Faixa colada na borda do botão, engordando para fora com a força; o anel fraco marca o máximo.</summary>
        private void DrawPowerBand(Vector2 origin, float radius, float power)
        {
            float width = Mathf.Max(0.03f, maxPowerBand * power);
            var color = power < 0.5f
                ? Color.Lerp(Color.green, Color.yellow, power * 2f)
                : Color.Lerp(Color.yellow, Color.red, (power - 0.5f) * 2f);
            color.a = powerBandAlpha;
            powerBand.widthMultiplier = width;
            powerBand.startColor = powerBand.endColor = color;
            SetCircle(powerBand, origin, radius + width * 0.5f);

            maxRing.startColor = maxRing.endColor = new Color(1f, 1f, 1f, maxRingAlpha);
            SetCircle(maxRing, origin, radius + maxPowerBand + maxRingWidth * 0.5f);
        }

        /// <summary>Bolinhas de <paramref name="start"/> até <paramref name="length"/>, andando para frente; a da ponta é maior.</summary>
        private int DrawDots(List<SpriteRenderer> pool, Vector2 start, Vector2 dir, float length, float size, float tipSize, Color color)
        {
            if (length <= 0.01f) return 0;
            int count = 0;
            float phase = Mathf.Repeat(Time.time * dotSpeed, dotSpacing);
            for (float d = phase; d < length - dotSpacing * 0.5f; d += dotSpacing)
                PlaceDot(pool, count++, start + dir * d, size, color);
            PlaceDot(pool, count++, start + dir * length, tipSize, color);
            return count;
        }

        private void PlaceDot(List<SpriteRenderer> pool, int index, Vector2 position, float size, Color color)
        {
            while (pool.Count <= index) pool.Add(CreateDot(pool == aimDots ? "Mira" : "Linha da bola"));
            var dot = pool[index];
            dot.enabled = true;
            dot.color = color;
            dot.transform.position = position;
            dot.transform.localScale = Vector3.one * size;
        }

        private static void HideFrom(List<SpriteRenderer> pool, int count)
        {
            for (int i = count; i < pool.Count; i++)
                if (pool[i].enabled) pool[i].enabled = false;
        }

        private int DrawBallPrediction(Disc disc, Vector2 origin, Vector2 dir)
        {
            if (ball == null) return 0;

            var filter = new ContactFilter2D { useTriggers = false };
            int count = Physics2D.CircleCast(origin, disc.Radius, dir, filter, hits, 20f);
            RaycastHit2D? first = null;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider.isTrigger || hit.collider.attachedRigidbody == disc.Body) continue;
                if (first == null || hit.distance < first.Value.distance) first = hit;
            }

            if (first == null || first.Value.collider.GetComponent<Ball>() == null) return 0;

            Vector2 discAtContact = origin + dir * first.Value.distance;
            Vector2 ballPos = ball.Body.position;
            Vector2 ballDir = (ballPos - discAtContact).normalized;
            return DrawDots(ballDots, ballPos + ballDir * ball.Radius, ballDir, ballLineLength, ballDotSize, ballDotSize,
                new Color(1f, 1f, 1f, ballDotAlpha));
        }

        private LineRenderer CreateCircle(string lineName, Material material)
        {
            var go = new GameObject(lineName);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.positionCount = CircleSegments;
            line.loop = true;
            line.useWorldSpace = true;
            line.sortingOrder = 50;
            line.enabled = false;
            return line;
        }

        private static void SetCircle(LineRenderer line, Vector2 center, float radius)
        {
            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / CircleSegments;
                line.SetPosition(i, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        private SpriteRenderer CreateDot(string groupName)
        {
            var go = new GameObject($"Bolinha ({groupName})");
            go.transform.SetParent(transform, false);
            var dot = go.AddComponent<SpriteRenderer>();
            dot.sprite = dotSprite;
            dot.sortingOrder = 51;
            dot.enabled = false;
            return dot;
        }

        /// <summary>Círculo branco de 1 unidade (provisório até a arte da bolinha).</summary>
        private static Sprite CreateDotSprite()
        {
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                byte alpha = (byte)(Mathf.Clamp01(size * 0.5f - distance) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
