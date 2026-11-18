using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Objeto escondido que grava o save marcado (<see cref="GameSave.Flush"/>) no fim de cada quadro, quando o app vai
    /// para o fundo e quando o jogo fecha. O <see cref="GameSave"/> cria na primeira vez que algo marca o save.
    /// </summary>
    internal sealed class SaveFlusher : MonoBehaviour
    {
        public static SaveFlusher Create()
        {
            var go = new GameObject("GameSave") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            return go.AddComponent<SaveFlusher>();
        }

        private void LateUpdate() => GameSave.Flush();

        // Android: o sistema pode fechar o jogo depois de ir para o fundo, sem aviso.
        private void OnApplicationPause(bool paused)
        {
            if (paused) GameSave.Flush();
        }

        private void OnApplicationQuit() => GameSave.Flush();

        private void OnDestroy() => GameSave.Flush();
    }
}
