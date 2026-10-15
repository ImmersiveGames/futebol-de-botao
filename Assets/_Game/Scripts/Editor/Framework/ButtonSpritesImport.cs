using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace FutebolDeBotao.Editor
{
    /// <summary>
    /// Importa a folha de botões (Resources/Botoes/Buttons_Sprites.png, 2 colunas x 4 linhas de 32x32): 32 px por
    /// unidade, filtro Point, sem compressão e cortada em 8 sprites com borda 9-slice de 10 px. Coluna da esquerda é o
    /// normal, a da direita o apertado; os nomes vêm de <see cref="ButtonSkin.RowNames"/> (Verde, Verde_Apertado...).
    /// Chamado pelo "Criar telas".
    /// </summary>
    public static class ButtonSpritesImport
    {
        public const string SheetPath = "Assets/_Game/Resources/Botoes/Buttons_Sprites.png";
        private const int PixelsPerUnit = 32;

        public static void Configure()
        {
            if (AssetImporter.GetAtPath(SheetPath) is not TextureImporter importer)
            {
                Debug.LogWarning($"[Futebol de Botão] Sem a folha de botões em {SheetPath}: botões com cor lisa.");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            provider.GetDataProvider<ITextureDataProvider>().GetTextureActualWidthAndHeight(out int width, out int height);

            // Mantém o ID de cada recorte que já existe, para não quebrar referências nas cenas.
            var previous = provider.GetSpriteRects().GroupBy(r => r.name).ToDictionary(g => g.Key, g => g.First().spriteID);
            int size = ButtonSkin.TileSize;
            float border = ButtonSkin.BorderPixels;
            var rects = new List<SpriteRect>();
            for (int row = 0; row < ButtonSkin.RowNames.Length; row++)
            {
                for (int column = 0; column < 2; column++)
                {
                    string name = ButtonSkin.RowNames[row] + (column == 1 ? ButtonSkin.PressedSuffix : string.Empty);
                    var rect = new Rect(column * size, height - (row + 1) * size, size, size);
                    if (rect.xMax > width || rect.y < 0f)
                    {
                        Debug.LogWarning($"[Futebol de Botão] A folha {SheetPath} tem {width}x{height}; esperava 64x128.");
                        continue;
                    }
                    rects.Add(new SpriteRect
                    {
                        name = name,
                        rect = rect,
                        border = new Vector4(border, border, border, border),
                        alignment = SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f),
                        spriteID = previous.TryGetValue(name, out var id) ? id : GUID.Generate(),
                    });
                }
            }

            provider.SetSpriteRects(rects.ToArray());
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>()
                .SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());
            provider.Apply();
            importer.SaveAndReimport();
        }
    }
}
