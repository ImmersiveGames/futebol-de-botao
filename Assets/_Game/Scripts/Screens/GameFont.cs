using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Fonte dos textos do jogo (telas e HUD). Usa a fonte pixel em Resources/Fontes/PixelifySans quando ela existe;
    /// sem o arquivo, a fonte padrão do Unity.
    /// </summary>
    public static class GameFont
    {
        public const string ResourcePath = "Fontes/PixelifySans";

        private static Font font;

        public static Font Get()
        {
            if (font != null) return font;
            font = Resources.Load<Font>(ResourcePath);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }
    }
}
