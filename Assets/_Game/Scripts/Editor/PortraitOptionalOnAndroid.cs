using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using UnityEngine;

namespace FutebolDeBotao.Editor
{
    /// <summary>
    /// O jogo é travado em retrato e o Unity declara no manifest que a tela em pé é obrigatória
    /// (android.hardware.screen.portrait). O Play avisa que isso tira da loja aparelhos que só ficam deitados
    /// (Chromebooks, Play Games no PC, Android XR). Depois de gerar o projeto Gradle, marca o recurso como opcional
    /// (android:required="false"); a orientação do jogo continua em retrato.
    /// </summary>
    public sealed class PortraitOptionalOnAndroid : IPostGenerateGradleAndroidProject
    {
        private const string Feature = "android.hardware.screen.portrait";
        private static readonly Regex Tag = new("<uses-feature[^>]*android:name=\"" + Regex.Escape(Feature) + "\"[^>]*>");

        public int callbackOrder => 1000;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // path = unityLibrary; o launcher fica ao lado.
            Fix(Path.Combine(path, "src", "main", "AndroidManifest.xml"));
            Fix(Path.Combine(path, "..", "launcher", "src", "main", "AndroidManifest.xml"));
        }

        private static void Fix(string manifest)
        {
            if (!File.Exists(manifest)) return;
            string text = File.ReadAllText(manifest);
            bool changed = false;
            string fixedText = Tag.Replace(text, match =>
            {
                string tag = match.Value;
                string result = tag.Contains("android:required=")
                    ? Regex.Replace(tag, "android:required=\"[^\"]*\"", "android:required=\"false\"")
                    : tag.Replace("android:name=\"" + Feature + "\"", "android:name=\"" + Feature + "\" android:required=\"false\"");
                if (result != tag) changed = true;
                return result;
            });
            if (!changed) return;
            File.WriteAllText(manifest, fixedText);
            Debug.Log($"[Android] Tela em pé marcada como opcional em {manifest}");
        }
    }
}
