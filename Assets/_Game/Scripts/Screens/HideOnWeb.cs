using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>Esconde o objeto no navegador (WebGL), onde fechar o aplicativo não faz nada.</summary>
    public sealed class HideOnWeb : MonoBehaviour
    {
        private void Awake()
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer) gameObject.SetActive(false);
        }
    }
}
