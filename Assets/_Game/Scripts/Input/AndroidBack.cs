using UnityEngine;
using UnityEngine.InputSystem;

namespace FutebolDeBotao
{
    /// <summary>
    /// Botão Voltar do Android (o Input System entrega como Esc do teclado). Vale uma vez por quadro: quem pega
    /// primeiro (menu de pausa, tela aberta ou a partida) usa, e os outros não veem o mesmo aperto.
    /// </summary>
    public static class AndroidBack
    {
        private static int usedFrame = -1;

        public static bool IsAndroid => Application.platform == RuntimePlatform.Android;

        /// <summary>Verdadeiro se o Voltar foi apertado agora e ninguém usou ainda neste quadro.</summary>
        public static bool TryUse()
        {
            if (!IsAndroid || usedFrame == Time.frameCount) return false;
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return false;
            usedFrame = Time.frameCount;
            return true;
        }
    }
}
