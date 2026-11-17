using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Joystick na tela (só no toque, ligado nas Configurações): ao encostar o dedo para mirar, um círculo transparente
    /// aparece onde ele encostou, do tamanho do arrasto máximo, e uma bolinha segue o dedo (presa na borda), com uma
    /// linha fina até o centro. Só visual: a mira continua a mesma do <see cref="AimController"/>. A força aparece no
    /// próprio botão, então aqui tudo é neutro.
    /// </summary>
    public sealed class TouchJoystick : MonoBehaviour
    {
        private const int SortingOrder = 80;
        private const int TextureSize = 64;
        private const float KnobRadius = 0.32f;
        private const float LineWidth = 0.05f;
        private static readonly Color BaseFill = new(1f, 1f, 1f, 0.08f);
        private static readonly Color BaseEdge = new(1f, 1f, 1f, 0.35f);
        private static readonly Color KnobColor = new(1f, 1f, 1f, 0.4f);
        private static readonly Color LineColor = new(1f, 1f, 1f, 0.25f);

        private static Sprite ringSprite, discSprite, pixelSprite;

        private AimController aim;
        private Transform root;
        private SpriteRenderer ring, knob, line;

        public void Configure(AimController controller) => aim = controller;

        private void LateUpdate()
        {
            bool show = aim != null && aim.Tuning != null && GameSave.Data.settings.touchJoystick &&
                        aim.TryGetTouchDrag(out _, out _);
            if (!show)
            {
                if (root != null && root.gameObject.activeSelf) root.gameObject.SetActive(false);
                return;
            }
            aim.TryGetTouchDrag(out var anchor, out var pointer);
            Build();
            root.gameObject.SetActive(true);

            float radius = aim.Tuning.maxDragDistance;
            Vector2 offset = Vector2.ClampMagnitude(pointer - anchor, radius);
            ring.transform.position = anchor;
            ring.transform.localScale = new Vector3(radius, radius, 1f);
            knob.transform.position = anchor + offset;

            float length = offset.magnitude;
            line.enabled = length > KnobRadius;
            if (line.enabled)
            {
                line.transform.position = anchor + offset * 0.5f;
                line.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg);
                line.transform.localScale = new Vector3(length, LineWidth, 1f);
            }
        }

        private void Build()
        {
            if (root != null) return;
            root = new GameObject("Joystick na tela").transform;
            root.SetParent(transform, false);
            ring = CreateRenderer("Base", RingSprite(), Color.white, SortingOrder);
            line = CreateRenderer("Linha", PixelSprite(), LineColor, SortingOrder + 1);
            knob = CreateRenderer("Bolinha", DiscSprite(), KnobColor, SortingOrder + 2);
            knob.transform.localScale = new Vector3(KnobRadius, KnobRadius, 1f);
        }

        private SpriteRenderer CreateRenderer(string objectName, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(root, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
        }

        // ---- Sprites feitos no código (2 unidades de diâmetro: a escala é o raio) ----

        private static Sprite RingSprite()
        {
            if (ringSprite != null) return ringSprite;
            ringSprite = CircleSprite("Joystick (base)", (distance, radius) =>
                distance > radius ? Color.clear : distance > radius - 2f ? BaseEdge : BaseFill);
            return ringSprite;
        }

        private static Sprite DiscSprite()
        {
            if (discSprite != null) return discSprite;
            discSprite = CircleSprite("Joystick (bolinha)", (distance, radius) => distance > radius ? Color.clear : Color.white);
            return discSprite;
        }

        private static Sprite PixelSprite()
        {
            if (pixelSprite != null) return pixelSprite;
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            pixelSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            pixelSprite.hideFlags = HideFlags.DontSave;
            return pixelSprite;
        }

        private static Sprite CircleSprite(string spriteName, System.Func<float, float, Color> pixel)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            float radius = TextureSize * 0.5f;
            for (int y = 0; y < TextureSize; y++)
                for (int x = 0; x < TextureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                    texture.SetPixel(x, y, pixel(distance, radius));
                }
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), radius);
            sprite.name = spriteName;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
