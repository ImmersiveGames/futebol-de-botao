namespace FutebolDeBotao
{
    /// <summary>Estados da partida.</summary>
    public enum MatchState
    {
        /// <summary>Esperando a Activity da partida entrar (ou depois que ela saiu).</summary>
        Waiting,
        /// <summary>Saída: tudo volta para a formação e a bola vai ao centro.</summary>
        KickOff,
        /// <summary>Mira: o time da vez escolhe um botão e arrasta.</summary>
        Aim,
        /// <summary>Pênalti: o atacante posiciona o botão no arco atrás da bola.</summary>
        PenaltySetup,
        /// <summary>"Vai chutar" declarado: o defensor ajusta o goleiro.</summary>
        ShotCall,
        /// <summary>"Vai chutar": o atacante mira com um botão do campo de ataque.</summary>
        ShotAim,
        /// <summary>Tiro de meta: o defensor chuta direto na bola, na frente do goleiro.</summary>
        GoalKick,
        /// <summary>Movimento: esperando a mesa parar.</summary>
        Moving,
        /// <summary>Gol (ou gol anulado): pausa curta antes de seguir.</summary>
        Goal,
        /// <summary>Fim do tempo.</summary>
        End
    }
}
