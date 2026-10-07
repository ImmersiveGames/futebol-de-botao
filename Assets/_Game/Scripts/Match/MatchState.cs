namespace FutebolDeBotao
{
    /// <summary>Estados da partida.</summary>
    public enum MatchState
    {
        /// <summary>Saída: tudo volta para a formação e a bola vai ao centro.</summary>
        KickOff,
        /// <summary>Mira: o time da vez escolhe um botão e arrasta.</summary>
        Aim,
        /// <summary>"Vai chutar" declarado: o defensor ajusta o goleiro.</summary>
        ShotCall,
        /// <summary>"Vai chutar": o atacante mira com um botão do campo de ataque.</summary>
        ShotAim,
        /// <summary>Movimento: esperando a mesa parar.</summary>
        Moving,
        /// <summary>Gol (ou gol anulado): pausa curta antes de seguir.</summary>
        Goal,
        /// <summary>Fim do tempo.</summary>
        End
    }
}
