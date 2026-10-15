using Unity.Cinemachine;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// A câmera sempre encaixa a moldura da partida inteira: a mesa e, com a tela em pé, uma faixa de HUD por jogador
    /// além de cada gol (256 x 519 px na arte). Em tela mais comprida sobra fundo em cima e embaixo; em
    /// tela mais larga, dos lados. Tela deitada (PC) encaixa só a mesa. Roda sozinha em todas as cenas e acompanha a
    /// troca de tamanho da tela.
    /// </summary>
    public sealed class ScreenFit : MonoBehaviour
    {
        /// <summary>Enquadramento padrão (meia altura): campo + gols + margem.</summary>
        public const float BaseOrthoSize = 6.9f;

        /// <summary>Altura da faixa de HUD de cada jogador (48 px na arte).</summary>
        public const float StripHeight = 1.5f;

        /// <summary>Folga de 5 px (na arte) entre a rede e a faixa, e entre a faixa e a borda da moldura.</summary>
        public const float StripGap = 5f / 32f;

        /// <summary>Onde a faixa começa: 5 px depois da rede do gol.</summary>
        public const float StripStart = FieldLayout.HalfHeight + FieldLayout.GoalDepth + StripGap;

        /// <summary>Meia altura da moldura com a tela em pé: mesa até a rede + folga + faixa + folga.</summary>
        public const float FrameHalfHeight = StripStart + StripHeight + StripGap;

        /// <summary>Meia largura que precisa aparecer: campo + parede (0,5) + uma folga.</summary>
        public const float VisibleHalfWidth = FieldLayout.HalfWidth + 0.5f + 0.05f;

        /// <summary>Meia altura da câmera para a tela atual.</summary>
        public static float OrthoSizeFor(float aspect)
        {
            if (aspect <= 0f) return BaseOrthoSize;
            // Deitada (PC): o HUD é um painel na tela, a moldura é só a mesa.
            float halfHeight = aspect > 1f ? BaseOrthoSize : FrameHalfHeight;
            return Mathf.Max(halfHeight, VisibleHalfWidth / aspect);
        }

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
