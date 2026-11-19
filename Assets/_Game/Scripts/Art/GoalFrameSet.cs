using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// A trave e a rede dos gols, desenhadas para o gol de cima (o de baixo gira 180°): peças da esquerda e da direita
    /// do gol grande e do pequeno e a do meio (a mesma nos dois). A trave é pintada com a cor escolhida na mesa; a rede
    /// fica como está, atrás da bola. Uma trave pode servir para várias mesas (<see cref="TableTheme.goal"/>).
    /// </summary>
    [CreateAssetMenu(menuName = "Futebol de Botão/Trave", fileName = "Trave")]
    public sealed class GoalFrameSet : ScriptableObject
    {
        [Header("Trave (desenho branco, pintado com a cor)")]
        public Sprite bigLeft;
        public Sprite bigRight;
        public Sprite smallLeft;
        public Sprite smallRight;
        public Sprite center;

        [Header("Rede (não muda de cor; opcional)")]
        public Sprite bigNetLeft;
        public Sprite bigNetRight;
        public Sprite smallNetLeft;
        public Sprite smallNetRight;
        public Sprite centerNet;

        /// <summary>Todas as peças da trave (a rede é opcional).</summary>
        public bool Complete => bigLeft != null && bigRight != null && smallLeft != null && smallRight != null && center != null;
    }
}
