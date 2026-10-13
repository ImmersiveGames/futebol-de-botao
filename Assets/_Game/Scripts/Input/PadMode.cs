namespace FutebolDeBotao
{
    /// <summary>
    /// Se o último comando veio do teclado/controle (true) ou do mouse/toque (false). Com teclado/controle aparecem o
    /// cursor da partida e o destaque dos menus; com mouse/toque eles somem.
    /// </summary>
    public static class PadMode
    {
        public static bool Active { get; set; }
    }
}
