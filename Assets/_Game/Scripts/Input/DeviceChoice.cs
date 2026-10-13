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
