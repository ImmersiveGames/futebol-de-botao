using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Miniatura da mesa inteira (9 x 13 tiles) montada com as mesmas peças da Seleção de times: moldura e gols da mesa
    /// clássica (recortes da folha do campo) ou as placas e traves das outras mesas, o feltro pintado, as marcações e a
    /// bola no meio. Usada na aba Mesa do Meu time.
    /// </summary>
    public sealed class TablePreview
    {
        private static readonly string[] GoalPieces = { "Corner_Left", "Center", "Corner_Right" };
        private static readonly Color BallShadowColor = new(0f, 0f, 0f, 0.4f);

        private readonly float tile;
        private readonly Dictionary<string, Sprite> art = new();
        private readonly RectTransform root;
        private readonly GameObject classicLayer, classicGoals, classicPoles;
        private readonly RectTransform boardsLayer, goalsLayer;
        private readonly List<Image> feltRows = new();
        private readonly List<Image> feltEnds = new();
        private readonly Image feltUpperHalf, marks, ball, ballShine, ballShadow;
        private readonly Sprite oldBright, oldDark;

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

            // Mesa clássica: madeira, paredes e cantos.
            classicLayer = CreateRect(root, "Moldura clássica", Vector2.zero, Vector2.zero).gameObject;
            if (art.TryGetValue("Wall_Fill", out var wood))
                CreateTiled(classicLayer.transform, "Madeira", wood, Vector2.zero, new Vector2(halfW, halfH) * 2f);
            var horizontal = new Vector2(FieldLayout.Width, 1f);
            var vertical = new Vector2(1f, FieldLayout.Height);
            Tiled("Wall_Top", new Vector2(0f, halfH), horizontal);
            Tiled("Wall_Bottom", new Vector2(0f, -halfH), horizontal);
            Tiled("Wall_Left", new Vector2(-halfW, 0f), vertical);
            Tiled("Wall_Right", new Vector2(halfW, 0f), vertical);
            Single(classicLayer.transform, "Top_Corner_L", new Vector2(-halfW, halfH));
            Single(classicLayer.transform, "Top_Corner_R", new Vector2(halfW, halfH));
            Single(classicLayer.transform, "Bottom_Corner_L", new Vector2(-halfW, -halfH));
            Single(classicLayer.transform, "Bottom_Corner_R", new Vector2(halfW, -halfH));

            // Feltro: uma faixa por fileira, mais as de baixo das placas/gols e a meia fileira do Meio a meio.
            art.TryGetValue("Field_Braight", out oldBright);
            art.TryGetValue("Field_Dark", out oldDark);
            int rows = Mathf.RoundToInt(FieldLayout.Height);
            for (int row = 0; row < rows; row++)
                feltRows.Add(CreateTiled(root, $"Feltro {row + 1}", oldBright, new Vector2(0f, -FieldLayout.HalfHeight + 0.5f + row), horizontal));
            foreach (int row in new[] { -1, rows })
                feltEnds.Add(CreateTiled(root, "Feltro da ponta", oldBright, new Vector2(0f, -FieldLayout.HalfHeight + 0.5f + row), horizontal));
            feltUpperHalf = CreateTiled(root, "Feltro (meia fileira)", oldBright,
                new Vector2(0f, -FieldLayout.HalfHeight + rows / 2 + 0.75f), new Vector2(FieldLayout.Width, 0.5f));

            if (art.TryGetValue("Field_Marks", out var marksSprite))
                marks = CreateImage(root, "Marcações", marksSprite, Vector2.zero, new Vector2(FieldLayout.Width, FieldLayout.Height));

            boardsLayer = CreateRect(root, "Placas", Vector2.zero, Vector2.zero);
            classicGoals = GoalGroup("Gols", "Goal");

            if (art.TryGetValue("Ball_Shadow", out var shadow)) ballShadow = CreateImage(root, "Sombra da bola", shadow, Vector2.zero, Vector2.one);
            if (ballShadow != null) ballShadow.color = BallShadowColor;
            art.TryGetValue("Ball_Frame_1", out var ballSprite);
            ball = CreateImage(root, "Bola", ballSprite, Vector2.zero, Vector2.one);
            ball.enabled = ballSprite != null;
            if (art.TryGetValue("Ball_Light", out var light)) ballShine = CreateImage(root, "Luz da bola", light, Vector2.zero, Vector2.one);

            classicPoles = GoalGroup("Traves", "Pole");
            goalsLayer = CreateRect(root, "Traves da mesa", Vector2.zero, Vector2.zero);

            void Tiled(string spriteName, Vector2 at, Vector2 tiles)
            {
                if (art.TryGetValue(spriteName, out var sprite)) CreateTiled(classicLayer.transform, spriteName, sprite, at, tiles);
            }
        }

        /// <summary>Pinta a mesa montada: mesa, feltro, cor das traves e bola.</summary>
        public void Paint(VenueSetup setup)
        {
            var table = setup?.Table;
            var felt = setup?.Felt;
            bool boards = table != null && table.HasBoards;

            foreach (var layer in new[] { boardsLayer, goalsLayer })
                for (int i = layer.childCount - 1; i >= 0; i--)
                    Object.Destroy(layer.GetChild(i).gameObject);
            classicLayer.SetActive(!boards);
            SetActive(classicGoals, !boards);
            SetActive(classicPoles, !boards);
            if (boards)
            {
                AddPieces(boardsLayer, table.Boards(false));
                AddPieces(goalsLayer, table.Goals(false, setup.GoalColor));
            }

            // Feltro: nas mesas de placas cobre tudo (por baixo das placas); na clássica, só o campo e embaixo dos gols.
            var feltArt = FeltArt.Load();
            bool themed = felt != null && feltArt != null && feltArt.Has(felt.tile);
            int rows = feltRows.Count;
            float width = boards ? FieldLayout.Width + 2f : FieldLayout.Width;
            for (int row = 0; row < rows; row++)
            {
                var image = feltRows[row];
                image.sprite = themed ? feltArt.GetSprite(felt.tile) : row % 2 == 0 ? oldBright : oldDark;
                image.color = themed ? felt.RowColor(row, rows) : Color.white;
                image.enabled = image.sprite != null;
                image.rectTransform.sizeDelta = new Vector2(width, 1f) * tile;
            }
            for (int i = 0; i < feltEnds.Count; i++)
            {
                var source = feltRows[i == 0 ? 0 : rows - 1];
                feltEnds[i].sprite = source.sprite;
                feltEnds[i].color = source.color;
                feltEnds[i].enabled = source.enabled;
                feltEnds[i].rectTransform.sizeDelta = new Vector2(boards ? width : 3f, 1f) * tile;
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
                ball.sprite = ballTheme.frames[0];
                ball.color = ballTheme.color;
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
                var rect = CreateImage(parent, piece.Sprite.name, piece.Sprite, piece.Position, Vector2.one).rectTransform;
                rect.localRotation = Quaternion.Euler(0f, 0f, piece.Rotation);
                if (piece.FlipX) rect.localScale = new Vector3(-1f, 1f, 1f);
            }
        }

        /// <summary>Os dois gols da folha do campo (3 peças cada); nulo se faltar recorte.</summary>
        private GameObject GoalGroup(string groupName, string prefix)
        {
            foreach (var end in new[] { "Top", "Bottom" })
                foreach (var piece in GoalPieces)
                    if (!art.ContainsKey($"{prefix}_{end}_{piece}")) return null;
            var group = CreateRect(root, groupName, Vector2.zero, Vector2.zero);
            float y = FieldLayout.HalfHeight + 0.5f;
            foreach (var (end, sign) in new[] { ("Top", 1f), ("Bottom", -1f) })
                for (int i = 0; i < GoalPieces.Length; i++)
                    CreateImage(group, $"{end} {i + 1}", art[$"{prefix}_{end}_{GoalPieces[i]}"], new Vector2(i - 1, sign * y), Vector2.one);
            return group.gameObject;
        }

        private void Single(Transform parent, string spriteName, Vector2 position)
        {
            if (art.TryGetValue(spriteName, out var sprite)) CreateImage(parent, spriteName, sprite, position, Vector2.one);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null) target.SetActive(active);
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

        private static RectTransform CreateRect(Transform parent, string objectName, Vector2 position, Vector2 size)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }
    }
}
