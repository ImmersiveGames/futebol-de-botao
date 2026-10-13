using Immersive.Framework.GameFlow;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Abertura: mostra o nome do jogo e "Clique para iniciar" (no celular, "Toque para iniciar").
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

        /// <summary>Texto da chamada: toque no celular, clique no resto.</summary>
        public static string PromptText => Application.isMobilePlatform ? "Toque para iniciar" : "Clique para iniciar";

        private void Start()
        {
            if (prompt != null) prompt.text = PromptText;
        }

        private void Update()
        {
            if (prompt != null)
                prompt.enabled = Mathf.Repeat(Time.unscaledTime, blinkSeconds) < blinkSeconds * 0.7f;

            // O trigger ignora pedidos repetidos enquanto a troca de tela está em andamento.
            if (menuRoute != null && AnyPressed()) menuRoute.RequestRoute();
        }

        /// <summary>Qualquer tecla, clique ou toque. Teclado/controle já liga o destaque dos menus; mouse/toque desliga.</summary>
        private static bool AnyPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return Pressed(pad: true);

            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame ||
                                  mouse.middleButton.wasPressedThisFrame)) return Pressed(pad: false);

            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) return Pressed(pad: false);

            foreach (var gamepad in Gamepad.all)
                if (gamepad.buttonSouth.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame) return Pressed(pad: true);
            return false;
        }

        private static bool Pressed(bool pad)
        {
            PadMode.Active = pad;
            return true;
        }
    }
}
