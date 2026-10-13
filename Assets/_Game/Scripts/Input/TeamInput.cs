using UnityEngine;
using UnityEngine.InputSystem;

namespace FutebolDeBotao
{
    /// <summary>
    /// Comandos de um time humano na partida: ponteiro (mouse/toque) e teclado/controle (ControlsDefinition).
    /// Quem tem um técnico (Actor do framework) lê pelo leitor de input dele; quem não tem lê os dispositivos direto.
    /// </summary>
    public interface ITeamInput
    {
        /// <summary>Posição do ponteiro na tela. False quando não dá para ler (sem ponteiro ou fora da vez).</summary>
        bool TryGetPointer(out Vector2 screenPosition);
        bool PointerPressedThisFrame { get; }
        bool PointerReleasedThisFrame { get; }
        bool PointerHeld { get; }
        /// <summary>Voltar um passo: cancelar a mira ou soltar o botão escolhido (Q, B, botão direito do mouse).</summary>
        bool BackPressedThisFrame { get; }
        /// <summary>-1 a 1, esquerda/direita: cursor, goleiro, batedor e bola do tiro de meta (A/D, setas, analógico esq., D-pad).</summary>
        float SideAxis { get; }
        /// <summary>-1 a 1: girar a mira (A/D, setas, D-pad).</summary>
        float AimRotateAxis { get; }
        /// <summary>Analógico esquerdo, para apontar a mira.</summary>
        Vector2 AimStick { get; }
        /// <summary>-1 a 1: diminuir/aumentar a força (S/W, setas, analógico direito).</summary>
        float PowerAxis { get; }
        /// <summary>Escolher o botão e Pronto (E, A).</summary>
        bool SelectPressedThisFrame { get; }
        /// <summary>Chutar (Espaço, X).</summary>
        bool KickPressedThisFrame { get; }
        /// <summary>"Vai chutar" (C, LB, RB).</summary>
        bool ShotCallPressedThisFrame { get; }
    }

    /// <summary>Escolhe de onde vem o input de cada time.</summary>
    public static class MatchInput
    {
        /// <summary>O técnico do lado, se houver; senão os dispositivos direto (modo 2 jogadores, por enquanto).</summary>
        public static ITeamInput For(TeamSide side)
        {
            var coach = CoachActor.For(side);
            return coach != null ? coach : (ITeamInput)DeviceTeamInput.Instance;
        }

        /// <summary>
        /// Sinal do "direita" de quem joga em relação ao x do mundo: -1 para o time de cima com a tela em pé (celular),
        /// que olha a mesa de cabeça para baixo; 1 nos outros casos.
        /// </summary>
        public static float ScreenSign(TeamSide side) => side == TeamSide.Top && Screen.height >= Screen.width ? -1f : 1f;

        /// <summary>
        /// Sinal de giro para "direita" levar a ponta da mira (ou o batedor, atrás da bola) para a direita de quem joga:
        /// -1 só para o time de cima com a tela deitada (PC), que vê o próprio ataque apontando para baixo.
        /// </summary>
        public static float TurnSign(TeamSide side) => side == TeamSide.Top && Screen.height < Screen.width ? -1f : 1f;
    }

    /// <summary>Lê mouse, toque, teclado e controles direto, com as mesmas ações do técnico (ControlsDefinition).</summary>
    public sealed class DeviceTeamInput : ITeamInput
    {
        public static readonly DeviceTeamInput Instance = new();

        private InputActionAsset asset;
        private InputAction point, press, back, side, rotate, stick, power, select, kick, shotCall;

        private void Ensure()
        {
            if (asset != null) return;
            asset = ControlsDefinition.Create();
            var game = asset.FindActionMap(ControlsDefinition.GameplayMap);
            point = game.FindAction(ControlsDefinition.Point);
            press = game.FindAction(ControlsDefinition.Press);
            back = game.FindAction(ControlsDefinition.Back);
            side = game.FindAction(ControlsDefinition.Side);
            rotate = game.FindAction(ControlsDefinition.AimRotate);
            stick = game.FindAction(ControlsDefinition.AimStick);
            power = game.FindAction(ControlsDefinition.Power);
            select = game.FindAction(ControlsDefinition.Select);
            kick = game.FindAction(ControlsDefinition.Kick);
            shotCall = game.FindAction(ControlsDefinition.ShotCall);
            game.Enable();
        }

        public bool TryGetPointer(out Vector2 screenPosition)
        {
            Ensure();
            screenPosition = default;
            if (Pointer.current == null) return false;
            screenPosition = point.ReadValue<Vector2>();
            return true;
        }

        public bool PointerPressedThisFrame { get { Ensure(); return press.WasPressedThisFrame(); } }
        public bool PointerReleasedThisFrame { get { Ensure(); return press.WasReleasedThisFrame(); } }
        public bool PointerHeld { get { Ensure(); return press.IsPressed(); } }
        public bool BackPressedThisFrame { get { Ensure(); return back.WasPressedThisFrame(); } }
        public float SideAxis { get { Ensure(); return Mathf.Clamp(side.ReadValue<float>(), -1f, 1f); } }
        public float AimRotateAxis { get { Ensure(); return Mathf.Clamp(rotate.ReadValue<float>(), -1f, 1f); } }
        public Vector2 AimStick { get { Ensure(); return stick.ReadValue<Vector2>(); } }
        public float PowerAxis { get { Ensure(); return Mathf.Clamp(power.ReadValue<float>(), -1f, 1f); } }
        public bool SelectPressedThisFrame { get { Ensure(); return select.WasPressedThisFrame(); } }
        public bool KickPressedThisFrame { get { Ensure(); return kick.WasPressedThisFrame(); } }
        public bool ShotCallPressedThisFrame { get { Ensure(); return shotCall.WasPressedThisFrame(); } }
    }
}
