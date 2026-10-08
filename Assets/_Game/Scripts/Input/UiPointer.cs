using UnityEngine.EventSystems;

namespace FutebolDeBotao
{
    /// <summary>Saber se o ponteiro está em cima de um botão do HUD, para o toque não virar mira ou goleiro.</summary>
    public static class UiPointer
    {
        public static bool IsOverUi()
        {
            var events = EventSystem.current;
            return events != null && events.IsPointerOverGameObject();
        }
    }
}
