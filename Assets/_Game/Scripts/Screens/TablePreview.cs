using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Miniatura da mesa inteira (9 x 13 tiles) montada com as mesmas peças da Seleção de times: as placas e traves da
    /// mesa, o feltro pintado, as marcações e a bola no meio. Usada na aba Mesa do Meu time.
    /// </summary>
    public sealed class TablePreview
    {
        private static readonly Color BallShadowColor = new(0f, 0f, 0f, 0.4f);

        private readonly float tile;
        private readonly Dictionary<string, Sprite> art = new();
        private readonly RectTransform root;
        private readonly RectTransform boardsLayer, goalsLayer;
        private readonly List<Image> feltRows = new();
        private readonly List<Image> feltEnds = new();
        private readonly Image feltUpperHalf, marks, ball, ballColor, ballShine, ballShadow;
        // Sem feltro: o tile liso em verde listrado (fileira do meio clara).
        private static readonly Color FallbackBright = new Color32(0x1A, 0x7A, 0x3E, 0xFF);
        private static readonly Color FallbackDark = new Color32(0x24, 0x52, 0x3B, 0xFF);

        /// <param name="tileSize">Lado de um tile (1 unidade do campo) em unidades da UI.</param>
        public TablePreview(Transform parent, Vector2 position, float tileSize, Sprite[] sprites)
        {
            tile = tileSize;
            if (sprites != null)
                foreach (var sprite in sprites)
                    if (sprite != null) art[sprite.name] = sprite;

            float halfW = FieldLayout.HalfWidth + 0.5f, halfH = FieldLayout.HalfHeight + 0.5f;
            root = CreateRect(parent, "Mesa", position, new Vector2(halfW, halfH) * 2f * tile);
            root.gameObject.AddComponent<RectMask2D>();

            var horizontal = new Vector2(FieldLayout.Width, 1f);

            // Feltro: uma faixa por fileira, mais as de baixo das placas/gols e a meia fileira do Meio a meio.
            int rows = Mathf.RoundToInt(FieldLayout.Height);
            for (int row = 0; row < rows; row++)
                feltRows.Add(CreateTiled(root, $"Feltro {row + 1}", null, new Vector2(0f, -FieldLayout.HalfHeight + 0.5f + row), horizontal));
            foreach (int row in new[] { -1, rows })
                feltEnds.Add(CreateTiled(root, "Feltro da ponta", null, new Vector2(0f, -FieldLayout.HalfHeight + 0.5f + row), horizontal));
            feltUpperHalf = CreateTiled(root, "Feltro (meia fileira)", null,
                new Vector2(0f, -FieldLayout.HalfHeight + rows / 2 + 0.75f), new Vector2(FieldLayout.Width, 0.5f));

            if (art.TryGetValue("Field_Marks", out var marksSprite))
                marks = CreateImage(root, "Marcações", marksSprite, Vector2.zero, new Vector2(FieldLayout.Width, FieldLayout.Height));

            boardsLayer = CreateRect(root, "Placas", Vector2.zero, Vector2.zero);

            if (art.TryGetValue("Ball_Shadow", out var shadow)) ballShadow = CreateImage(root, "Sombra da bola", shadow, Vector2.zero, Vector2.one);
            if (ballShadow != null) ballShadow.color = BallShadowColor;
            art.TryGetValue("Ball_Frame_1", out var ballSprite);
            // A cor é o fundo da bola: vem antes (por baixo) dos quadros.
            ballColor = CreateImage(root, "Cor da bola", null, Vector2.zero, Vector2.one);
            ballColor.enabled = false;
            ball = CreateImage(root, "Bola", ballSprite, Vector2.zero, Vector2.one);
            ball.enabled = ballSprite != null;
            if (art.TryGetValue("Ball_Light", out var light)) ballShine = CreateImage(root, "Luz da bola", light, Vector2.zero, Vector2.one);

            goalsLayer = CreateRect(root, "Traves da mesa", Vector2.zero, Vector2.zero);
        }

        /// <summary>Pinta a mesa montada: mesa, feltro, cor das traves e bola.</summary>
        public void Paint(VenueSetup setup)
        {
            var table = TableTheme.Complete(setup?.Table);
            var felt = setup?.Felt;

            foreach (var layer in new[] { boardsLayer, goalsLayer })
                for (int i = layer.childCount - 1; i >= 0; i--)
                    Object.Destroy(layer.GetChild(i).gameObject);
            if (table != null)
            {
                int color = setup?.GoalColor ?? 0;
                AddPieces(boardsLayer, table.Boards(false, color, 0));
                AddPieces(goalsLayer, table.Goals(false, color));
            }

            // Feltro: cobre tudo, por baixo das placas.
            var feltArt = FeltArt.Load();
            bool themed = felt != null && feltArt != null && feltArt.Has(felt.tile);
            int rows = feltRows.Count;
            float width = FieldLayout.Width + 2f;
            for (int row = 0; row < rows; row++)
            {
                var image = feltRows[row];
                image.sprite = feltArt == null ? null : feltArt.GetSprite(themed ? felt.tile : FeltTheme.TileKind.Liso);
                image.color = themed ? felt.RowColor(row, rows) : (row - rows / 2) % 2 == 0 ? FallbackBright : FallbackDark;
                image.enabled = image.sprite != null;
                image.rectTransform.sizeDelta = new Vector2(width, 1f) * tile;
            }
            for (int i = 0; i < feltEnds.Count; i++)
            {
                var source = feltRows[i == 0 ? 0 : rows - 1];
                feltEnds[i].sprite = source.sprite;
                feltEnds[i].color = source.color;
                feltEnds[i].enabled = source.enabled;
                feltEnds[i].rectTransform.sizeDelta = new Vector2(width, 1f) * tile;
            }
            bool split = themed && felt.SplitsMiddleRow(rows);
            feltUpperHalf.gameObject.SetActive(split);
            if (split)
            {
                feltUpperHalf.sprite = feltArt.UpperHalf(felt.tile);
                feltUpperHalf.color = felt.secondary;
                feltUpperHalf.rectTransform.sizeDelta = new Vector2(width, 0.5f) * tile;
            }
            if (marks != null) marks.color = themed ? felt.marks : Color.white;

            var ballTheme = setup?.Ball;
            if (ballTheme != null && ballTheme.HasFrames)
            {
                int color = setup.BallColor;
                ball.sprite = ballTheme.frames[0];
                ball.color = ballTheme.FrameTint(color);
                ballColor.sprite = ballTheme.ColorFrame(0);
                ballColor.color = BallTheme.Tint(color);
                ballColor.enabled = ballTheme.HasColorLayer;
                ball.enabled = true;
                if (ballShine != null)
                {
                    ballShine.sprite = ballTheme.shine;
                    ballShine.enabled = ballTheme.shine != null;
                }
                if (ballShadow != null && ballTheme.shadow != null) ballShadow.sprite = ballTheme.shadow;
            }
        }

        public void SetVisible(bool visible) => root.gameObject.SetActive(visible);

        private void AddPieces(Transform parent, List<TableTheme.Piece> pieces)
        {
            foreach (var piece in pieces)
            {
                var image = CreateImage(parent, piece.Sprite.name, piece.Sprite, piece.Position, Vector2.one);
                image.color = piece.Tint;
                var rect = image.rectTransform;
                rect.localRotation = Quaternion.Euler(0f, 0f, piece.Rotation);
                if (piece.FlipX) rect.localScale = new Vector3(-1f, 1f, 1f);
            }
        }

        private Image CreateTiled(Transform parent, string objectName, Sprite sprite, Vector2 position, Vector2 tiles)
        {
            var image = CreateImage(parent, objectName, sprite, position, tiles);
            image.type = Image.Type.Tiled;
            // Image.Tiled desenha cada tile de 32 px (32 PPU) com 100 unidades da UI: o multiplicador leva para o tamanho do tile.
            image.pixelsPerUnitMultiplier = 100f / tile;
            return image;
        }

        /// <summary>Imagem em unidades do campo (posição e tamanho vezes o tamanho do tile).</summary>
        private Image CreateImage(Transform parent, string objectName, Sprite sprite, Vector2 position, Vector2 size)
        {
            var image = CreateRect(parent, objectName, position * tile, size * tile).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform CreateRect(Transform parent, string objectName, Vector2 position, Vector2 size) =>
            UiKit.CreateRect(parent, objectName, position, size);
    }
}
