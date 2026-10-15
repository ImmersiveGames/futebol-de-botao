using Unity.Cinemachine;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// A câmera da mesa sempre mostra a mesa inteira na largura (paredes incluídas). Em tela comprida (ex.: 20:9) ela
    /// abre o zoom e sobra espaço em cima e embaixo; nas telas de 9:16 para baixo fica o enquadramento padrão
    /// (<see cref="BaseOrthoSize"/>). Roda sozinha em todas as cenas e acompanha a troca de tamanho da tela.
    /// </summary>
    public sealed class ScreenFit : MonoBehaviour
    {
        /// <summary>Enquadramento padrão (meia altura): campo + gols + margem.</summary>
        public const float BaseOrthoSize = 6.9f;

        /// <summary>Meia largura que precisa aparecer: campo + parede (0,5) + uma folga.</summary>
        public const float VisibleHalfWidth = FieldLayout.HalfWidth + 0.5f + 0.05f;

        /// <summary>Meia altura da câmera para a tela atual.</summary>
        public static float OrthoSizeFor(float aspect) =>
            aspect <= 0f ? BaseOrthoSize : Mathf.Max(BaseOrthoSize, VisibleHalfWidth / aspect);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var go = new GameObject("Ajuste de tela") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            go.AddComponent<ScreenFit>();
        }

        // Antes do LateUpdate do CinemachineBrain, que aplica a lente na câmera.
        private void Update()
        {
            float size = OrthoSizeFor(Screen.height > 0 ? (float)Screen.width / Screen.height : 0f);

            for (int i = 0; i < CinemachineCore.VirtualCameraCount; i++)
            {
                if (CinemachineCore.GetVirtualCamera(i) is not CinemachineCamera cam) continue;
                if (Mathf.Approximately(cam.Lens.OrthographicSize, size)) continue;
                var lens = cam.Lens;
                lens.OrthographicSize = size;
                cam.Lens = lens;
            }

            // Câmera sem Cinemachine (cena de teste do editor).
            var main = Camera.main;
            if (main != null && main.orthographic && main.GetComponent<CinemachineBrain>() == null) main.orthographicSize = size;
        }
    }
}
