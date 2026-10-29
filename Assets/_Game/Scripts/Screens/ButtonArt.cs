using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Recortes da folha de UI (Art/UI/Botões_UI-Sheet: botões 9-slice e o fundo da HUD), guardados em Resources/BotoesUI
    /// para o <see cref="ButtonSkin"/> achar no jogo. Montado pelo "Criar telas".
    /// </summary>
    public sealed class ButtonArt : ScriptableObject
    {
        public const string ResourcePath = "BotoesUI";

        public Sprite[] sprites = new Sprite[0];

        private static ButtonArt cached;

        /// <summary>O recorte com esse nome, ou nulo se o "Criar telas" ainda não guardou.</summary>
        public static Sprite Find(string name)
        {
            if (cached == null) cached = Resources.Load<ButtonArt>(ResourcePath);
            if (cached == null) return null;
            foreach (var sprite in cached.sprites)
                if (sprite != null && sprite.name == name) return sprite;
            return null;
        }
    }
}
