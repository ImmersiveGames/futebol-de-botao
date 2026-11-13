using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Abre espaço para o banner do topo numa tela (Canvas do Menu e do Resultado): com o banner à mostra, a referência
    /// do CanvasScaler cresce na altura do banner (em telas baixas, como 16:9, tudo encolhe um pouco) e o conteúdo desce:
    /// filhos esticados perdem essa altura em cima, os outros descem metade dela (o meio da área livre). O fundo
    /// ("Fundo") continua na tela inteira. Sem banner, volta tudo como era. Confere a cada quadro (o banner do AdMob chega
    /// depois de abrir a tela).
    /// </summary>
    public sealed class BannerSpace : MonoBehaviour
    {
        private const string BackgroundName = "Fundo";

        private readonly List<(RectTransform rect, bool stretched, Vector2 position, Vector2 offsetMax)> children = new();
        private CanvasScaler scaler;
        private Vector2 baseReference;
        private float applied = -1f;
        private Vector2Int screen;

        /// <summary>Liga no Canvas da tela (uma vez). Chamar depois de montar os filhos de primeiro nível.</summary>
        public static void Attach(Component screenRoot)
        {
            if (screenRoot == null) return;
            var canvas = screenRoot.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            canvas = canvas.rootCanvas;
            if (!canvas.TryGetComponent<BannerSpace>(out _)) canvas.gameObject.AddComponent<BannerSpace>();
        }

        private void Awake()
        {
            scaler = GetComponent<CanvasScaler>();
            if (scaler != null) baseReference = scaler.referenceResolution;
            foreach (Transform child in transform)
            {
                if (child is not RectTransform rect || child.name == BackgroundName) continue;
                children.Add((rect, !Mathf.Approximately(rect.anchorMin.y, rect.anchorMax.y), rect.anchoredPosition, rect.offsetMax));
            }
        }

        private void LateUpdate()
        {
            float pixels = Ads.BannerHeightPixels;
            var size = new Vector2Int(Screen.width, Screen.height);
            if (Mathf.Approximately(pixels, applied) && size == screen) return;
            applied = pixels;
            screen = size;
            Apply(pixels);
        }

        private void OnDestroy() => Apply(0f);

        private void Apply(float pixels)
        {
            if (scaler == null) return;
            scaler.referenceResolution = baseReference;
            foreach (var (rect, stretched, position, offsetMax) in children)
            {
                if (rect == null) continue;
                if (stretched) rect.offsetMax = offsetMax;
                else rect.anchoredPosition = position;
            }
            if (pixels <= 0f || baseReference.x <= 0f || baseReference.y <= 0f) return;

            // Altura do banner em unidades da UI. A escala (modo Expand = a menor das duas) depende da própria altura extra:
            // duas voltas bastam.
            float width = Screen.width, height = Screen.height;
            float scale = Mathf.Min(width / baseReference.x, height / baseReference.y);
            float units = pixels / scale;
            for (int i = 0; i < 2; i++)
            {
                scale = Mathf.Min(width / baseReference.x, height / (baseReference.y + units));
                units = pixels / scale;
            }
            scaler.referenceResolution = new Vector2(baseReference.x, baseReference.y + units);
            foreach (var (rect, stretched, position, offsetMax) in children)
            {
                if (rect == null) continue;
                if (stretched) rect.offsetMax = offsetMax - new Vector2(0f, units);
                else rect.anchoredPosition = position - new Vector2(0f, units * 0.5f);
            }
        }
    }
}
