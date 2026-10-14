using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Fonte dos textos do jogo (telas e HUD): a fonte que estiver em Resources/Fontes (qualquer nome); sem nenhuma,
    /// a fonte padrão do Unity.
    /// </summary>
    public static class GameFont
    {
        public const string ResourceFolder = "Fontes";

        private static Font font;

        public static Font Get()
        {
            if (font != null) return font;
            var fonts = Resources.LoadAll<Font>(ResourceFolder);
            if (fonts.Length > 0) font = fonts[0];
            else font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }
    }
}
