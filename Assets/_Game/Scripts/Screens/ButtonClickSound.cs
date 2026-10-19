using UnityEngine;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>Toca o clique (<see cref="UiSounds"/>) quando o botão é acionado, pelo toque, mouse, teclado ou controle.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class ButtonClickSound : MonoBehaviour
    {
        private void Awake() => GetComponent<Button>().onClick.AddListener(UiSounds.PlayClick);
    }
}
