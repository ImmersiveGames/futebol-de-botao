using UnityEngine;
using UnityEngine.InputSystem;

namespace FutebolDeBotao
{
    /// <summary>
    /// Os controles do jogo num lugar só: o "Criar jogadores" grava o Controles.inputactions a partir daqui e o modo sem
    /// técnico (input direto) monta as mesmas ações em tempo de execução.
    /// Mouse e toque: arrastar do botão para trás e soltar. Teclado e controle: cursor entre os botões, escolher, mirar,
    /// força e chutar (tabela em planos/controles.md).
    /// </summary>
    public static class ControlsDefinition
    {
        public const string GameplayMap = "Jogo";
        public const string GlobalMap = "Global";

        public const string KeyboardMouse = "Teclado e mouse";
        public const string Touch = "Toque";
        public const string Gamepad = "Controle";

        /// <summary>Posição do ponteiro (mouse ou dedo).</summary>
        public const string Point = "Apontar";
        /// <summary>Apertar/soltar o ponteiro.</summary>
        public const string Press = "Pressionar";
        /// <summary>Volta um passo: cancela a mira ou solta o botão escolhido. Q, B, botão direito do mouse.</summary>
        public const string Back = "Voltar";
        /// <summary>Esquerda/direita: cursor entre os botões, goleiro, batedor e bola do tiro de meta. A/D, setas, analógico esq., D-pad.</summary>
        public const string Side = "Lados";
        /// <summary>Gira a mira. A/D, setas, D-pad.</summary>
        public const string AimRotate = "GirarMira";
        /// <summary>Aponta a mira para a direção do analógico esquerdo.</summary>
        public const string AimStick = "ApontarMira";
        /// <summary>Força: W/S, setas cima/baixo, analógico direito.</summary>
        public const string Power = "Forca";
        /// <summary>Escolher o botão e Pronto. E, A.</summary>
        public const string Select = "Confirmar";
        /// <summary>Chutar. Espaço, X.</summary>
        public const string Kick = "Chutar";
        /// <summary>"Vai chutar". C, LB, RB.</summary>
        public const string ShotCall = "VaiChutar";
        /// <summary>Pausa (mapa Global). Esc, Start.</summary>
        public const string Pause = "Pausa";

        /// <summary>Cria o asset com os esquemas, os mapas, as ações e as teclas.</summary>
        public static InputActionAsset Create()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "Controles";
            asset.AddControlScheme(KeyboardMouse).WithRequiredDevice("<Keyboard>").WithRequiredDevice("<Mouse>");
            asset.AddControlScheme(Touch).WithRequiredDevice("<Touchscreen>");
            asset.AddControlScheme(Gamepad).WithRequiredDevice("<Gamepad>");

            const string pointers = KeyboardMouse + ";" + Touch;
            var game = asset.AddActionMap(GameplayMap);

            var point = game.AddAction(Point, InputActionType.PassThrough);
            point.expectedControlType = "Vector2";
            point.AddBinding("<Pointer>/position", groups: pointers);
            game.AddAction(Press, InputActionType.Button).AddBinding("<Pointer>/press", groups: pointers);

            var back = game.AddAction(Back, InputActionType.Button);
            back.AddBinding("<Mouse>/rightButton", groups: KeyboardMouse);
            back.AddBinding("<Keyboard>/q", groups: KeyboardMouse);
            back.AddBinding("<Gamepad>/buttonEast", groups: Gamepad);

            var side = Axis(game, Side);
            Keys(side, "<Keyboard>/a", "<Keyboard>/d", KeyboardMouse);
            Keys(side, "<Keyboard>/leftArrow", "<Keyboard>/rightArrow", KeyboardMouse);
            side.AddBinding("<Gamepad>/leftStick/x", groups: Gamepad);
            Keys(side, "<Gamepad>/dpad/left", "<Gamepad>/dpad/right", Gamepad);

            var rotate = Axis(game, AimRotate);
            Keys(rotate, "<Keyboard>/a", "<Keyboard>/d", KeyboardMouse);
            Keys(rotate, "<Keyboard>/leftArrow", "<Keyboard>/rightArrow", KeyboardMouse);
            Keys(rotate, "<Gamepad>/dpad/left", "<Gamepad>/dpad/right", Gamepad);

            var stick = game.AddAction(AimStick, InputActionType.Value);
            stick.expectedControlType = "Vector2";
            stick.AddBinding("<Gamepad>/leftStick", groups: Gamepad);

            var power = Axis(game, Power);
            Keys(power, "<Keyboard>/s", "<Keyboard>/w", KeyboardMouse);
            Keys(power, "<Keyboard>/downArrow", "<Keyboard>/upArrow", KeyboardMouse);
            power.AddBinding("<Gamepad>/rightStick/y", groups: Gamepad);

            var select = game.AddAction(Select, InputActionType.Button);
            select.AddBinding("<Keyboard>/e", groups: KeyboardMouse);
            select.AddBinding("<Gamepad>/buttonSouth", groups: Gamepad);

            var kick = game.AddAction(Kick, InputActionType.Button);
            kick.AddBinding("<Keyboard>/space", groups: KeyboardMouse);
            kick.AddBinding("<Gamepad>/buttonWest", groups: Gamepad);

            var shotCall = game.AddAction(ShotCall, InputActionType.Button);
            shotCall.AddBinding("<Keyboard>/c", groups: KeyboardMouse);
            shotCall.AddBinding("<Gamepad>/leftShoulder", groups: Gamepad);
            shotCall.AddBinding("<Gamepad>/rightShoulder", groups: Gamepad);

            var global = asset.AddActionMap(GlobalMap);
            var pause = global.AddAction(Pause, InputActionType.Button);
            pause.AddBinding("<Keyboard>/escape", groups: KeyboardMouse);
            pause.AddBinding("<Gamepad>/start", groups: Gamepad);

            return asset;
        }

        private static InputAction Axis(InputActionMap map, string name)
        {
            var action = map.AddAction(name, InputActionType.Value);
            action.expectedControlType = "Axis";
            return action;
        }

        private static void Keys(InputAction action, string negative, string positive, string group) =>
            action.AddCompositeBinding("1DAxis").With("Negative", negative, group).With("Positive", positive, group);
    }
}
