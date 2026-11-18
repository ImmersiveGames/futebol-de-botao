namespace FutebolDeBotao
{
    public static class TeamSideExtensions
    {
        /// <summary>O outro lado: Bottom vira Top e Top vira Bottom.</summary>
        public static TeamSide Opponent(this TeamSide side) => side == TeamSide.Bottom ? TeamSide.Top : TeamSide.Bottom;
    }
}
