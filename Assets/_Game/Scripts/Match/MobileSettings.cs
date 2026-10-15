using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Ajustes do app no celular (Android): 60 quadros por segundo (a Unity usa 30 no celular por padrão) e tela
    /// sempre acesa durante a partida, enquanto o jogador pensa na jogada.
    /// </summary>
    public static class MobileSettings
    {
        private static bool IsNativeMobile => Application.isMobilePlatform && Application.platform != RuntimePlatform.WebGLPlayer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            if (IsNativeMobile) Application.targetFrameRate = 60;
        }

        /// <summary>Liga ou desliga o "tela sempre acesa" (só no celular).</summary>
        public static void KeepScreenOn(bool on)
        {
            if (IsNativeMobile) Screen.sleepTimeout = on ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
        }
    }
}
