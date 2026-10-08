using UnityEngine;
using UnityEngine.InputSystem;

namespace FutebolDeBotao
{
    /// <summary>
    /// Comandos de um time humano na partida: ponteiro (mira, goleiro, batedor) e teclas.
    /// Quem tem um técnico (Actor do framework) lê pelo leitor de input dele; quem não tem lê o mouse/teclado direto.
    /// </summary>
    public interface ITeamInput
    {
        /// <summary>Posição do ponteiro na tela. False quando não dá para ler (sem ponteiro ou fora da vez).</summary>
        bool TryGetPointer(out Vector2 screenPosition);
        bool PointerPressedThisFrame { get; }
        bool PointerReleasedThisFrame { get; }
        bool PointerHeld { get; }
        /// <summary>Cancelar a mira (botão direito do mouse).</summary>
        bool CancelPressedThisFrame { get; }
        /// <summary>-1 a 1: setas ou A/D (goleiro e batedor).</summary>
        float KeeperAxis { get; }
        /// <summary>Pronto (espaço).</summary>
        bool ConfirmPressedThisFrame { get; }
        /// <summary>"Vai chutar" (V).</summary>
        bool ShotCallPressedThisFrame { get; }
        /// <summary>Reiniciar a partida (R).</summary>
        bool RestartPressedThisFrame { get; }
    }

    /// <summary>Escolhe de onde vem o input de cada time.</summary>
    public static class MatchInput
    {
        /// <summary>O técnico do lado, se houver; senão o mouse/teclado direto (modo 2 jogadores, por enquanto).</summary>
        public static ITeamInput For(TeamSide side)
        {
            var coach = CoachActor.For(side);
            return coach != null ? coach : (ITeamInput)DeviceTeamInput.Instance;
        }
    }

    /// <summary>Lê o mouse, o toque e o teclado direto, como o jogo fazia antes dos Actors.</summary>
    public sealed class DeviceTeamInput : ITeamInput
    {
        public static readonly DeviceTeamInput Instance = new();

        public bool TryGetPointer(out Vector2 screenPosition)
        {
            var pointer = Pointer.current;
            screenPosition = pointer != null ? pointer.position.ReadValue() : default;
            return pointer != null;
        }

        public bool PointerPressedThisFrame => Pointer.current != null && Pointer.current.press.wasPressedThisFrame;
        public bool PointerReleasedThisFrame => Pointer.current != null && Pointer.current.press.wasReleasedThisFrame;
        public bool PointerHeld => Pointer.current != null && Pointer.current.press.isPressed;
        public bool CancelPressedThisFrame => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;

        public float KeeperAxis
        {
            get
            {
                var keyboard = Keyboard.current;
                if (keyboard == null) return 0f;
                float axis = 0f;
                if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) axis -= 1f;
                if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) axis += 1f;
                return axis;
            }
        }

        public bool ConfirmPressedThisFrame => Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        public bool ShotCallPressedThisFrame => Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame;
        public bool RestartPressedThisFrame => Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
    }
}
