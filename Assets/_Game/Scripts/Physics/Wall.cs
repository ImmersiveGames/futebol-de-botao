using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>Marca um collider como parede do campo (regra de gol anulado).</summary>
    public sealed class Wall : MonoBehaviour
    {
        [Tooltip("Empurra bola e botões de volta para o campo (desligado nas redes do gol).")]
        [SerializeField] private bool pushesBack = true;

        public bool PushesBack => pushesBack;

        /// <summary>
        /// Parede lateral (esquerda ou direita do campo). Só ela anula o gol: o fundo e as traves não contam.
        /// </summary>
        public bool IsSideWall => pushesBack && Mathf.Abs(transform.position.x) > FieldLayout.HalfWidth;

        public void Configure(bool pushBack)
        {
            pushesBack = pushBack;
        }
    }
}
