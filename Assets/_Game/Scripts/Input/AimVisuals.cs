using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Desenha a ajuda de mira: a faixa de força em volta do botão (cresce da borda para fora, de 1 em 1 pixel da arte,
    /// de verde a vermelho, com um anel pontilhado fraco no máximo), a mira de bolinhas andando na direção do chute e,
    /// na Completa, a linha da bola em bolinhas mais fracas (sem rebotes).
    /// As bolinhas usam a arte Aim_Visual (Dot e Mark, ligadas pelo "Aplicar artes") no tamanho dos pixels do jogo;
    /// sem a arte, um círculo provisório. A faixa é desenhada por código numa textura na escala da arte (32 px por unidade).
    /// </summary>
    [RequireComponent(typeof(AimController))]
    public sealed class AimVisuals : MonoBehaviour
    {
        private const float ArtPixelsPerUnit = 32f;

        [SerializeField] private AimAssistMode mode = AimAssistMode.Full;
        [Tooltip("Comprimento da mira de bolinhas com força máxima.")]
        [SerializeField] private float maxArrowLength = 2f;
        [Tooltip("Comprimento da linha da bola (só na ajuda Completa).")]
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("fullBallLine")] private float ballLineLength = 2.5f;

        [Header("Arte das bolinhas (Aplicar artes)")]
        [SerializeField] private Sprite dotArt;
        [SerializeField] private Sprite tipArt;

        [Header("Faixa de força")]
        [Tooltip("Largura da faixa em volta do botão com força máxima.")]
        [SerializeField] private float maxPowerBand = 1.2f;
        [SerializeField] private Color lowPowerColor = Color.green;
        [SerializeField] private Color midPowerColor = Color.yellow;
        [SerializeField] private Color highPowerColor = Color.red;
        [SerializeField, Range(0f, 1f)] private float powerBandAlpha = 0.35f;
        [Tooltip("Anel pontilhado do máximo.")]
        [SerializeField, Range(0f, 1f)] private float maxRingAlpha = 0.25f;
        [SerializeField] private float maxRingSpacing = 0.3f;

        [Header("Bolinhas")]
        [SerializeField] private float dotSpacing = 0.25f;
        [Tooltip("Tamanho das bolinhas provisórias (sem a arte, a arte usa o próprio tamanho).")]
        [SerializeField] private float dotSize = 0.125f;
        [SerializeField] private float tipDotSize = 0.19f;
        [Tooltip("Velocidade das bolinhas andando para frente (unidades/s).")]
        [SerializeField] private float dotSpeed = 0.5f;
        [SerializeField, Range(0f, 1f)] private float ballDotAlpha = 0.5f;

        private AimController aim;
        private Ball ball;
        private SpriteRenderer powerBand;
        private Texture2D bandTexture;
        private int bandInner = -1;
        private int bandWidth = -1;
        private Sprite fallbackDot;
        private readonly List<SpriteRenderer> aimDots = new();
        private readonly List<SpriteRenderer> ballDots = new();
        private readonly List<SpriteRenderer> ringDots = new();
        private readonly RaycastHit2D[] hits = new RaycastHit2D[8];

        public AimAssistMode Mode
        {
            get => mode;
            set => mode = value;
        }

        /// <summary>Liga a arte das bolinhas (usado pelo "Aplicar artes").</summary>
        public void ConfigureArt(Sprite dot, Sprite tip)
        {
            dotArt = dot;
            tipArt = tip;
        }

        private void Awake()
        {
            aim = GetComponent<AimController>();
            var go = new GameObject("Faixa de força");
            go.transform.SetParent(transform, false);
            powerBand = go.AddComponent<SpriteRenderer>();
            powerBand.sortingOrder = 50;
            powerBand.enabled = false;
            fallbackDot = CreateFallbackDot();
        }

        private void Start()
        {
            ball = FindAnyObjectByType<Ball>();
        }

        private void OnDestroy()
        {
            if (bandTexture != null) Destroy(bandTexture);
        }

        private void LateUpdate()
        {
            bool show = aim.HasValidAim;
            powerBand.enabled = show;
            int aimCount = 0, ballCount = 0, ringCount = 0;
            if (show)
            {
                Vector2 origin = aim.AimOrigin;
                float radius = aim.AimRadius;
                Vector2 dir = aim.Direction;
                float power = aim.Power01;

                DrawPowerBand(origin, radius, power);
                ringCount = DrawMaxRing(origin, radius + maxPowerBand);

                if (mode != AimAssistMode.Off)
                {
                    aimCount = DrawDots(aimDots, origin + dir * radius, dir, maxArrowLength * power, 1f);
                    // No chute direto na bola a mira já é a direção da bola. Na Curta fica só a mira, sem a linha da bola.
                    if (mode == AimAssistMode.Full && !aim.IsBallKick) ballCount = DrawBallPrediction(aim.SelectedDisc, origin, dir);
                }
            }
            HideFrom(aimDots, aimCount);
            HideFrom(ballDots, ballCount);
            HideFrom(ringDots, ringCount);
        }

        // ---- Faixa de força ----

        /// <summary>Faixa colada na borda do botão, engordando para fora de 1 em 1 pixel da arte.</summary>
        private void DrawPowerBand(Vector2 origin, float radius, float power)
        {
            int inner = Mathf.RoundToInt(radius * ArtPixelsPerUnit);
            int width = Mathf.Max(1, Mathf.RoundToInt(maxPowerBand * power * ArtPixelsPerUnit));
            if (inner != bandInner || width != bandWidth) RebuildBand(inner, width);

            var color = power < 0.5f
                ? Color.Lerp(lowPowerColor, midPowerColor, power * 2f)
                : Color.Lerp(midPowerColor, highPowerColor, (power - 0.5f) * 2f);
            color.a = powerBandAlpha;
            powerBand.color = color;
            powerBand.transform.position = origin;
        }

        /// <summary>Desenha o anel (raio de dentro e largura em pixels da arte) numa textura branca, sem suavizar.</summary>
        private void RebuildBand(int inner, int width)
        {
            bandInner = inner;
            bandWidth = width;
            int outer = inner + width;
            int size = outer * 2 + 2;
            if (bandTexture == null || bandTexture.width != size)
            {
                if (bandTexture != null) Destroy(bandTexture);
                bandTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                    { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            }

            var pixels = new Color32[size * size];
            float center = size * 0.5f;
            var white = new Color32(255, 255, 255, 255);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - center;
                float dy = y + 0.5f - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                if (distance >= inner && distance < outer) pixels[y * size + x] = white;
            }
            bandTexture.SetPixels32(pixels);
            bandTexture.Apply();

            var old = powerBand.sprite;
            powerBand.sprite = Sprite.Create(bandTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ArtPixelsPerUnit);
            if (old != null) Destroy(old);
        }

        /// <summary>Anel pontilhado fraco no tamanho máximo da faixa.</summary>
        private int DrawMaxRing(Vector2 origin, float ringRadius)
        {
            float spacing = Mathf.Max(0.1f, maxRingSpacing);
            int count = Mathf.Max(8, Mathf.RoundToInt(2f * Mathf.PI * ringRadius / spacing));
            var color = new Color(1f, 1f, 1f, maxRingAlpha);
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                PlaceDot(ringDots, i, origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ringRadius, false, color);
            }
            return count;
        }

        // ---- Bolinhas ----

        /// <summary>Bolinhas de <paramref name="start"/> até <paramref name="length"/>, andando para frente; a da ponta é maior.</summary>
        private int DrawDots(List<SpriteRenderer> pool, Vector2 start, Vector2 dir, float length, float alpha)
        {
            if (length <= 0.01f) return 0;
            var color = new Color(1f, 1f, 1f, alpha);
            int count = 0;
            // Espaçamento mínimo: com 0 no Inspector o laço nunca terminaria.
            float spacing = Mathf.Max(0.05f, dotSpacing);
            float phase = Mathf.Repeat(Time.time * dotSpeed, spacing);
            for (float d = phase; d < length - spacing * 0.5f; d += spacing)
                PlaceDot(pool, count++, start + dir * d, false, color);
            PlaceDot(pool, count++, start + dir * length, true, color);
            return count;
        }

        private void PlaceDot(List<SpriteRenderer> pool, int index, Vector2 position, bool tip, Color color)
        {
            while (pool.Count <= index) pool.Add(CreateDot());
            var dot = pool[index];
            var art = tip ? (tipArt != null ? tipArt : dotArt) : dotArt;
            if (art != null)
            {
                // Arte no tamanho dos pixels do jogo (1 pixel = 1/32 de unidade), qualquer que seja o PPU da importação.
                dot.sprite = art;
                dot.transform.localScale = Vector3.one * (art.pixelsPerUnit / ArtPixelsPerUnit);
            }
            else
            {
                dot.sprite = fallbackDot;
                dot.transform.localScale = Vector3.one * (tip ? tipDotSize : dotSize);
            }
            dot.enabled = true;
            dot.color = color;
            dot.transform.position = position;
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
            return DrawDots(ballDots, ballPos + ballDir * ball.Radius, ballDir, ballLineLength, ballDotAlpha);
        }

        private SpriteRenderer CreateDot()
        {
            var go = new GameObject("Bolinha da mira");
            go.transform.SetParent(transform, false);
            var dot = go.AddComponent<SpriteRenderer>();
            dot.sortingOrder = 51;
            dot.enabled = false;
            return dot;
        }

        /// <summary>Círculo branco de 1 unidade, provisório para quando falta a arte.</summary>
        private static Sprite CreateFallbackDot()
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
