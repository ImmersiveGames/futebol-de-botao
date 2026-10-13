using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FutebolDeBotao
{
    /// <summary>Os dispositivos que um jogador pode escolher na tela Controles: teclado e mouse (juntos), toque e cada controle.</summary>
    public static class DeviceChoice
    {
        /// <summary>Esquema de controle do framework para o dispositivo (o Join usa para parear o resto, como o mouse).</summary>
        public static string SchemeFor(InputDevice device) => device switch
        {
            Gamepad => ControlsDefinition.Gamepad,
            Touchscreen => ControlsDefinition.Touch,
            _ => ControlsDefinition.KeyboardMouse
        };

        /// <summary>Junta os dispositivos disponíveis agora: teclado (representa teclado e mouse), toque e os controles.</summary>
        public static void Collect(List<InputDevice> devices)
        {
            devices.Clear();
            // No celular (inclusive no navegador) o teclado é o virtual: não conta como opção.
            if (!Application.isMobilePlatform && Keyboard.current != null) devices.Add(Keyboard.current);
            if (Touchscreen.current != null) devices.Add(Touchscreen.current);
            foreach (var gamepad in Gamepad.all) devices.Add(gamepad);
        }

        /// <summary>
        /// Dispositivos do jogador (1 ou 2) escolhidos na tela Controles, com o mouse junto do teclado. Nulo = qualquer um:
        /// sem dono, revezando no mesmo dispositivo, ou o dispositivo escolhido não está mais conectado.
        /// </summary>
        public static InputDevice[] DevicesOf(int player)
        {
            if (player == 0 || MatchSession.SharedDevice) return null;
            var device = player == 1 ? MatchSession.Player1Device : MatchSession.Player2Device;
            if (device == null || !device.added) return null;
            if (device is Keyboard && Mouse.current != null) return new InputDevice[] { device, Mouse.current };
            return new[] { device };
        }

        /// <summary>De qual jogador é o ponteiro (mouse vai com o teclado; toque com o toque). 0 = de ninguém ou dos dois.</summary>
        public static int PointerOwner(Pointer pointer)
        {
            if (pointer == null || MatchSession.SharedDevice) return 0;
            bool Owns(InputDevice device) => device != null && (device == pointer || (pointer is Mouse && device is Keyboard));
            if (Owns(MatchSession.Player1Device)) return 1;
            if (Owns(MatchSession.Player2Device)) return 2;
            return 0;
        }

        /// <summary>Quem pausou: 1 ou 2 mexe no menu de pausa; 0 = qualquer um.</summary>
        public static int PausedBy { get; set; }

        /// <summary>
        /// Dono do dispositivo: 1 ou 2; 0 quando ninguém escolheu dispositivo ou os dois revezam; -1 quando é de ninguém
        /// (há donos e este não foi escolhido).
        /// </summary>
        public static int OwnerOf(InputDevice device)
        {
            if (MatchSession.SharedDevice) return 0;
            var first = MatchSession.Player1Device;
            var second = MatchSession.Player2Device;
            if (first == null && second == null) return 0;
            if (device == first) return 1;
            if (device == second) return 2;
            return -1;
        }

        /// <summary>Nome na tela: "Teclado e mouse", "Toque" ou "Controle N" (N pela ordem em <paramref name="devices"/>).</summary>
        public static string Label(InputDevice device, List<InputDevice> devices)
        {
            switch (device)
            {
                case Gamepad:
                    int number = 0;
                    foreach (var other in devices)
                    {
                        if (other is Gamepad) number++;
                        if (other == device) break;
                    }
                    return $"Controle {number}";
                case Touchscreen:
                    return "Toque";
                default:
                    return "Teclado e mouse";
            }
        }
    }
}
