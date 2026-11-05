using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Desenho de um botão ou goleiro com as camadas de um <see cref="ShirtPattern"/>. O desenho nunca gira, mesmo com o botão
    /// girando na física: o padrão fica sempre igual na tela, nos dois times.
    /// Montado por "Futebol de Botão/Aplicar artes"; a Partida pinta com <see cref="Paint"/>.
    /// </summary>
    public sealed class PieceArt : MonoBehaviour
    {
        [SerializeField] private bool keeper;
        [Tooltip("Um por camada, na ordem de ShirtLayer.")]
        [SerializeField] private SpriteRenderer[] layers = new SpriteRenderer[0];
        [SerializeField] private Transform spinning;
        [SerializeField] private Transform still;

        public void Configure(bool isKeeper, SpriteRenderer[] layerRenderers, Transform spinningLayers, Transform stillLayers)
        {
            keeper = isKeeper;
            layers = layerRenderers;
            spinning = spinningLayers;
            still = stillLayers;
        }

        /// <summary>Troca o padrão e pinta com as cores do time.</summary>
        public void Paint(Color primary, Color secondary, ShirtPattern pattern)
        {
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] == null) continue;
                var layer = (ShirtLayer)i;
                // Camada que o padrão não tem (o contorno do goleiro, por exemplo) fica vazia.
                if (pattern != null) layers[i].sprite = pattern.Get(layer);
                layers[i].color = ShirtArt.Tint(layer, primary, secondary, keeper);
            }
        }

        private void LateUpdate()
        {
            if (still != null) still.rotation = Quaternion.identity;
            if (spinning != null) spinning.rotation = Quaternion.identity;
        }
    }
}
