#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FutebolDeBotao
{
    /// <summary>
    /// Só no editor: F12 salva a tela do jogo em PNG na pasta Capturas (ao lado de Assets), para as capturas da loja.
    /// Shift+F12 salva em dobro (1080x1920 vira 2160x3840). Ponha a aba Game numa resolução fixa 9:16 (1080x1920).
    /// </summary>
    public sealed class ScreenshotKey : MonoBehaviour
    {
        private const string Folder = "Capturas";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var go = new GameObject("Capturas (F12)") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<ScreenshotKey>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.f12Key.wasPressedThisFrame) return;
            int scale = keyboard.shiftKey.isPressed ? 2 : 1;
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, $"captura_{System.DateTime.Now:yyyyMMdd_HHmmss}_{Screen.width * scale}x{Screen.height * scale}.png");
            ScreenCapture.CaptureScreenshot(path, scale);
            float aspect = (float)Mathf.Max(Screen.width, Screen.height) / Mathf.Min(Screen.width, Screen.height);
            string warning = Mathf.Abs(aspect - 16f / 9f) > 0.01f ? " ATENÇÃO: não é 9:16, o Google recusa. Use a resolução 1080x1920 na aba Game." : string.Empty;
            Debug.Log($"[Capturas] Salva em {Path.GetFullPath(path)}{warning}");
        }
    }
}
#endif
