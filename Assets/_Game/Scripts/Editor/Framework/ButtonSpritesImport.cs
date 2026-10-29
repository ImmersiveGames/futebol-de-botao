using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace FutebolDeBotao.Editor
{
    /// <summary>
    /// Importa a folha de UI (Art/UI/Botões_UI-Sheet.png, recortada pelo usuário no Sprite Editor: Green_Button,
    /// Green_Button_Pressed... Gray_Button e Hud_BG_Tile): 32 px por unidade, filtro Point, sem compressão, borda 9-slice
    /// de 10 px nos botões e de 4 px no fundo da HUD (os outros, como o Selected_UI, sem borda). Os recortes continuam
    /// os do usuário (só a borda muda).
    /// Guarda os sprites em Resources/BotoesUI (<see cref="ButtonArt"/>) para o <see cref="ButtonSkin"/> achar no jogo.
    /// Chamado pelo "Criar telas".
    /// </summary>
    public static class ButtonSpritesImport
    {
        public const string SheetPath = "Assets/_Game/Art/UI/Botões_UI-Sheet.png";
        private const string ArtPath = "Assets/_Game/Resources/" + ButtonArt.ResourcePath + ".asset";
        private const int PixelsPerUnit = 32;
        private const float HudPanelBorder = 4f;

        public static void Configure()
        {
            if (AssetImporter.GetAtPath(SheetPath) is not TextureImporter importer)
            {
                Debug.LogWarning($"[Futebol de Botão] Sem a folha de UI em {SheetPath}: botões com cor lisa.");
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
            var rects = provider.GetSpriteRects();
            foreach (var rect in rects)
            {
                bool isButton = ButtonSkin.StyleNames.Any(n => rect.name == n || rect.name == n + ButtonSkin.PressedSuffix);
                float border = rect.name == ButtonSkin.HudPanelName ? HudPanelBorder : isButton ? ButtonSkin.BorderPixels : 0f;
                rect.border = new Vector4(border, border, border, border);
            }
            provider.SetSpriteRects(rects);
            provider.Apply();
            importer.SaveAndReimport();

            var sprites = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().ToArray();
            foreach (var name in ButtonSkin.StyleNames)
                if (sprites.All(s => s.name != name))
                    Debug.LogWarning($"[Futebol de Botão] A folha {SheetPath} não tem o recorte {name}.");

            var art = AssetDatabase.LoadAssetAtPath<ButtonArt>(ArtPath);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<ButtonArt>();
                AssetDatabase.CreateAsset(art, ArtPath);
            }
            art.sprites = sprites;
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
        }

        /// <summary>O fundo 9-slice das faixas da HUD, ou nulo se a folha não tiver.</summary>
        public static Sprite HudPanel()
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(SheetPath))
                if (asset is Sprite sprite && sprite.name == ButtonSkin.HudPanelName) return sprite;
            Debug.LogWarning($"[Futebol de Botão] Sem {ButtonSkin.HudPanelName} em {SheetPath}: HUD com fundo liso.");
            return null;
        }
    }
}
