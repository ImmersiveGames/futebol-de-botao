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
    }
}
