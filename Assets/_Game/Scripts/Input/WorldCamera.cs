using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Acha a câmera da mesa. Com o Immersive Framework a câmera nasce do Camera Output prefab depois que a cena
    /// carrega, então a busca é refeita até ela existir.
    /// </summary>
    public static class WorldCamera
    {
        public static Camera Resolve(Camera current)
        {
            if (current != null && current.isActiveAndEnabled) return current;
            var main = Camera.main;
            return main != null ? main : Object.FindAnyObjectByType<Camera>();
        }

        public static Vector2 ScreenToWorld(Camera camera, Vector2 screen)
        {
            var world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z));
            return new Vector2(world.x, world.y);
        }
    }
}
