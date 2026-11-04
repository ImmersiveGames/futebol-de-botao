using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FutebolDeBotao
{
    /// <summary>
    /// Os tiles brancos da folha de feltros (liso, gradiente e textura), que o jogo pinta com as cores de cada
    /// <see cref="FeltTheme"/>. Fica em Resources (montado pelo "Aplicar artes"). Também dá a metade de cima de cada
    /// tile, para a fileira do meio do Meio a meio.
    /// </summary>
    public sealed class FeltArt : ScriptableObject
    {
        public const string ResourcePath = "FeltroArte";

        [Tooltip("Felt_White, Felt_White_Gradient e Felt_White_Texture, na ordem de FeltTheme.TileKind.")]
        public Sprite[] sprites = new Sprite[3];
        [Tooltip("Um Tile por recorte (sem trava de cor, para cada célula ter a sua).")]
        public TileBase[] tiles = new TileBase[3];

        private readonly Dictionary<int, Sprite> upperSprites = new();
        private readonly Dictionary<int, Tile> upperTiles = new();

        private static FeltArt cached;

        /// <summary>A arte dos feltros, ou nulo se o "Aplicar artes" ainda não montou.</summary>
        public static FeltArt Load()
        {
            if (cached == null) cached = Resources.Load<FeltArt>(ResourcePath);
            return cached;
        }

        public Sprite GetSprite(FeltTheme.TileKind kind) => Get(sprites, (int)kind);

        public TileBase GetTile(FeltTheme.TileKind kind) => Get(tiles, (int)kind);

        public bool Has(FeltTheme.TileKind kind) => GetSprite(kind) != null && GetTile(kind) != null;

        /// <summary>A metade de cima do tile (32x16), desenhada da metade da célula para cima.</summary>
        public Sprite UpperHalf(FeltTheme.TileKind kind)
        {
            int index = (int)kind;
            if (upperSprites.TryGetValue(index, out var half) && half != null) return half;
            var sprite = GetSprite(kind);
            if (sprite == null) return null;
            // textureRect: com o Sprite Atlas, o recorte está no lugar dele dentro do atlas.
            var rect = sprite.textureRect;
            half = Sprite.Create(sprite.texture,
                new Rect(rect.x, rect.y + rect.height * 0.5f, rect.width, rect.height * 0.5f),
                new Vector2(0.5f, 0f), sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            half.name = sprite.name + "_Cima";
            half.hideFlags = HideFlags.DontSave;
            upperSprites[index] = half;
            return half;
        }

        public Tile UpperHalfTile(FeltTheme.TileKind kind)
        {
            int index = (int)kind;
            if (upperTiles.TryGetValue(index, out var tile) && tile != null) return tile;
            var half = UpperHalf(kind);
            if (half == null) return null;
            tile = CreateInstance<Tile>();
            tile.name = half.name;
            tile.hideFlags = HideFlags.DontSave;
            tile.sprite = half;
            tile.flags = TileFlags.None;
            tile.colliderType = Tile.ColliderType.None;
            upperTiles[index] = tile;
            return tile;
        }

        private static T Get<T>(T[] array, int index) where T : Object =>
            array != null && index >= 0 && index < array.Length ? array[index] : null;
    }
}
