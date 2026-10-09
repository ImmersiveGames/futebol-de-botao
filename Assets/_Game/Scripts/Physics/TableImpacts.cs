using System;

namespace FutebolDeBotao
{
    /// <summary>O que bateu em quê na mesa (para os sons).</summary>
    public enum TableImpactKind
    {
        DiscDisc,
        BallDisc,
        BallKeeper,
        Wall
    }

    /// <summary>
    /// Batidas na mesa de verdade, com a velocidade relativa do choque. A simulação da IA usa só colisores (sem
    /// <see cref="Ball"/> nem <see cref="Disc"/>), então ela não avisa nada aqui.
    /// </summary>
    public static class TableImpacts
    {
        public static event Action<TableImpactKind, float> Hit;

        public static void Raise(TableImpactKind kind, float speed) => Hit?.Invoke(kind, speed);
    }
}
