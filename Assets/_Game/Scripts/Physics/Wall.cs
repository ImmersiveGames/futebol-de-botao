using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>Marca um collider como parede do campo (regra de gol anulado).</summary>
    public sealed class Wall : MonoBehaviour
    {
        [Tooltip("Empurra bola e botões de volta para o campo (desligado nas redes do gol).")]
        [SerializeField] private bool pushesBack = true;

        public bool PushesBack => pushesBack;

        public void Configure(bool pushBack)
        {
            pushesBack = pushBack;
        }
    }
}
