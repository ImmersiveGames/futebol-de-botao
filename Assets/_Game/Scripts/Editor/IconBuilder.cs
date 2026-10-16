using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace FutebolDeBotao.Editor
{
    /// <summary>
    /// Liga o ícone do app desenhado no Aseprite (Assets/_Game/Art/Icone) ao Player Settings:
    /// ícone padrão e Android Round/Legacy com o Icon.png (512, o mesmo da loja) e Android Adaptive com as duas
    /// camadas (Icon_BG.png de fundo, Icon_FG.png de frente, 80x80 de arte com o disco no círculo central).
    /// Menu: Futebol de Botão > Aplicar ícone do app.
    /// </summary>
    public static class IconBuilder
    {
        private const string IconPath = "Assets/_Game/Art/Icone/Icon.png";
        private const string BackgroundPath = "Assets/_Game/Art/Icone/Icon_BG.png";
        private const string ForegroundPath = "Assets/_Game/Art/Icone/Icon_FG.png";

        [MenuItem("Futebol de Botão/Aplicar ícone do app")]
        public static void Build()
        {
            var missing = new[] { IconPath, BackgroundPath, ForegroundPath }.Where(p => !File.Exists(p)).ToArray();
            if (missing.Length > 0)
            {
                Debug.LogWarning($"[Futebol de Botão] Faltam os ícones: {string.Join(", ", missing)}. Nada foi alterado.");
                return;
            }

            var icon = Import(IconPath);
            var background = Import(BackgroundPath);
            var foreground = Import(ForegroundPath);

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                // Adaptive tem 2 camadas: a 0 é o fundo e a 1 é a frente.
                bool adaptive = kind.ToString() == "Adaptive";
                foreach (var platformIcon in icons)
                {
                    if (adaptive) platformIcon.SetTextures(background, foreground);
                    else platformIcon.SetTexture(icon);
                }
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Futebol de Botão] Ícone do app aplicado (padrão e Android Adaptive, Round e Legacy).");
        }

        // Pixel art sem filtro, sem compressão e sem arredondar o tamanho (640 não é potência de 2).
        private static Texture2D Import(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
