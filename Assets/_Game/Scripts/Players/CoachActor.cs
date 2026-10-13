using System.Collections.Generic;
using Immersive.Framework.Pause;
using Immersive.Framework.PlayerParticipation;
using Immersive.Framework.PlayerSlots;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FutebolDeBotao
{
    /// <summary>
    /// O técnico de um time: o Actor do jogador no framework. Fica no prefab do Actor, ao lado do
    /// Player Gameplay Input Reader, e lê os comandos do mapa "Jogo" por ele. Fora da vez o framework bloqueia
    /// o jogador (MatchPlayers) e o leitor não devolve nada.
    /// Jogador 1 = time de baixo (Azul), Jogador 2 = time de cima (Vermelho).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CoachActor : MonoBehaviour, ITeamInput
    {
        [SerializeField] private PlayerGameplayInputReader reader;

        [Header("Ações do mapa Jogo")]
        [SerializeField] private InputActionReference point;
        [SerializeField] private InputActionReference press;
        [SerializeField] private InputActionReference back;
        [SerializeField] private InputActionReference side;
        [SerializeField] private InputActionReference aimRotate;
        [SerializeField] private InputActionReference aimStick;
        [SerializeField] private InputActionReference power;
        [SerializeField] private InputActionReference select;
        [SerializeField] private InputActionReference kick;
        [SerializeField] private InputActionReference shotCall;

        private static readonly List<CoachActor> Active = new();

        private LocalPlayerHostAuthoring host;
        private PlayerPauseInput pauseInput;

        /// <summary>Lado do time de cada vaga.</summary>
        public static TeamSide SideFor(PlayerSlotId slot) => slot == PlayerSlotId.Player2 ? TeamSide.Top : TeamSide.Bottom;

        /// <summary>Técnico do lado, ou nulo se ninguém entrou por ele.</summary>
        public static CoachActor For(TeamSide side)
        {
            foreach (var coach in Active)
                if (coach.TryGetSide(out var coachSide) && coachSide == side) return coach;
            return null;
        }

        /// <summary>Algum técnico tem o Esc ligado pelo framework (Pause PlayerInput Binding). Aí a partida não lê o Esc direto.</summary>
        public static bool AnyPauseBound
        {
            get
            {
                foreach (var coach in Active)
                    if (coach.pauseInput != null && coach.pauseInput.HasActiveBinding) return true;
                return false;
            }
        }

        public bool TryGetSide(out TeamSide side)
        {
            side = TeamSide.Bottom;
            if (host == null || !host.HasJoinedSlot) return false;
            side = SideFor(host.JoinedPlayerSlotId);
            return true;
        }

        /// <summary>Liga o leitor e as ações do mapa Jogo (nomes em ControlsDefinition).</summary>
        public void Configure(PlayerGameplayInputReader inputReader, System.Func<string, InputActionReference> action)
        {
            reader = inputReader;
            point = action(ControlsDefinition.Point);
            press = action(ControlsDefinition.Press);
            back = action(ControlsDefinition.Back);
            side = action(ControlsDefinition.Side);
            aimRotate = action(ControlsDefinition.AimRotate);
            aimStick = action(ControlsDefinition.AimStick);
            power = action(ControlsDefinition.Power);
            select = action(ControlsDefinition.Select);
            kick = action(ControlsDefinition.Kick);
            shotCall = action(ControlsDefinition.ShotCall);
        }

        private void OnEnable()
        {
            if (reader == null) reader = GetComponent<PlayerGameplayInputReader>();
            host = GetComponentInParent<LocalPlayerHostAuthoring>();
            pauseInput = host != null ? host.GetComponent<PlayerPauseInput>() : null;
            if (!Active.Contains(this)) Active.Add(this);
        }

        private void OnDisable() => Active.Remove(this);

        // ---- ITeamInput ----

        public bool TryGetPointer(out Vector2 screenPosition)
        {
            screenPosition = default;
            return reader != null && point != null && reader.TryReadValue(point, out screenPosition);
        }

        public bool PointerPressedThisFrame => WasPressed(press);
        public bool PointerReleasedThisFrame =>
            reader != null && press != null && reader.TryWasReleasedThisFrame(press, out bool released) && released;
        public bool PointerHeld => reader != null && press != null && reader.TryIsPressed(press, out bool held) && held;
        public bool BackPressedThisFrame => WasPressed(back);
        public float SideAxis => ReadAxis(side);
        public float AimRotateAxis => ReadAxis(aimRotate);
        public Vector2 AimStick => reader != null && aimStick != null && reader.TryReadValue(aimStick, out Vector2 value) ? value : Vector2.zero;
        public float PowerAxis => ReadAxis(power);
        public bool SelectPressedThisFrame => WasPressed(select);
        public bool KickPressedThisFrame => WasPressed(kick);
        public bool ShotCallPressedThisFrame => WasPressed(shotCall);

        private float ReadAxis(InputActionReference action) =>
            reader != null && action != null && reader.TryReadValue(action, out float axis) ? Mathf.Clamp(axis, -1f, 1f) : 0f;

        private bool WasPressed(InputActionReference action) =>
            reader != null && action != null && reader.TryWasPressedThisFrame(action, out bool pressed) && pressed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();
    }
}
