using Immersive.Framework.GameFlow;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Abertura: mostra o nome do jogo e "Aperte qualquer botão ou toque na tela".
    /// Não avança sozinha; qualquer tecla, clique ou toque pede a Route do Menu.
    /// </summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        [SerializeField] private RouteRequestTrigger menuRoute;
        [Tooltip("Texto que pisca chamando o jogador.")]
        [SerializeField] private Text prompt;
        [SerializeField, Min(0.2f)] private float blinkSeconds = 1.2f;

        public void Configure(RouteRequestTrigger menu, Text promptText)
        {
            menuRoute = menu;
            prompt = promptText;
        }

        private void Update()
        {
            if (prompt != null)
                prompt.enabled = Mathf.Repeat(Time.unscaledTime, blinkSeconds) < blinkSeconds * 0.7f;

            // O trigger ignora pedidos repetidos enquanto a troca de tela está em andamento.
            if (menuRoute != null && AnyPressed()) menuRoute.RequestRoute();
        }

        private static bool AnyPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return true;

            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame ||
                                  mouse.middleButton.wasPressedThisFrame)) return true;

            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) return true;

            var gamepad = Gamepad.current;
            return gamepad != null && (gamepad.buttonSouth.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame);
        }
    }
}
