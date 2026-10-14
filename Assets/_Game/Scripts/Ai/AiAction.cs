using System.Collections.Generic;

namespace FutebolDeBotao
{
    /// <summary>
    /// Uma coisa que a IA sabe fazer num toque: quando pode (pré-condição), o que espera conseguir (efeito) e as
    /// jogadas concretas que a fazem. Quem escolhe entre elas é o objetivo (AiGoal) e o decisor (AiBrain).
    /// </summary>
    public abstract class AiAction
    {
        public abstract string Name { get; }
        /// <summary>O que a ação espera conseguir, em palavras (para o log).</summary>
        public abstract string Effect { get; }
        /// <summary>A jogada encosta na bola. Falso: posicionamento (sem tocar em nada; a vez passa).</summary>
        public virtual bool TouchesBall => true;
        /// <summary>Não precisa de jogada simulada (anunciar o Vai chutar).</summary>
        public virtual bool IsInstant => false;

        public abstract bool CanRun(AiSituation situation);

        /// <summary>Jogadas pela geometria, antes de simular.</summary>
        public abstract List<ShotCandidate> Candidates(AiSituation situation, ShotPlanner planner, MatchController match);

        public static readonly AiAction KickBallForward = new KickForwardAction();
        public static readonly AiAction ShootAtGoal = new ShootAction();
        public static readonly AiAction CallShot = new CallShotAction();
        public static readonly AiAction PlaceDisc = new PlaceDiscAction();

        /// <summary>Toca a bola para pontos à frente (no tiro de meta, chute direto na bola).</summary>
        private sealed class KickForwardAction : AiAction
        {
            public override string Name => "Tocar a bola";
            public override string Effect => "bola mais à frente, com o último toque";
            // Sem goleiro, no ataque, qualquer toque pode virar gol: também serve quando o chute direto não vale a pena.
            public override bool CanRun(AiSituation s) => s.State is MatchState.Aim or MatchState.GoalKick;
            public override List<ShotCandidate> Candidates(AiSituation s, ShotPlanner planner, MatchController match) =>
                s.IsBallKick ? planner.BallKickCandidates() : planner.DiscCandidates(s.Usable, false);
        }

        /// <summary>Chuta a gol (chute anunciado, ou sem goleiro com a bola no ataque).</summary>
        private sealed class ShootAction : AiAction
        {
            public override string Name => "Chutar a gol";
            public override string Effect => "gol";
            public override bool CanRun(AiSituation s) => s.GoalsCount && !s.IsBallKick && s.AllUsable.Count > 0;
            // Todos os botões que podem jogar: o chute pode vir do mais longe, que o adversário não esperava.
            public override List<ShotCandidate> Candidates(AiSituation s, ShotPlanner planner, MatchController match) =>
                planner.DiscCandidates(s.AllUsable, true);
        }

        /// <summary>
        /// Anuncia o "Vai chutar": o próximo toque vale gol e o adversário ajusta o goleiro. Anuncia sempre que algum
        /// botão tem chute livre, a bola está a distância de chute e o goleiro não alcança a bola (sem conta de chance).
        /// </summary>
        private sealed class CallShotAction : AiAction
        {
            public override string Name => "Anunciar o Pro Gol!";
            public override string Effect => "próximo toque vale gol";
            public override bool IsInstant => true;
            public override bool CanRun(AiSituation s) =>
                s.CanCallShot && s.HasClearShot && !s.KeeperCanTakeBall && s.BallToOpponentGoal <= s.ShotCallDistance;
            public override List<ShotCandidate> Candidates(AiSituation s, ShotPlanner planner, MatchController match) => new();
        }

        /// <summary>Leva um botão até um ponto sem tocar em nada: cobrir o gol, marcar um adversário, ficar atrás da bola.</summary>
        private sealed class PlaceDiscAction : AiAction
        {
            public override string Name => "Posicionar botão";
            public override string Effect => "botão no ponto, sem tocar na bola (a vez passa)";
            public override bool TouchesBall => false;
            public override bool CanRun(AiSituation s) => s.State == MatchState.Aim && !s.IsFreeKick && s.AllUsable.Count > 0;
            public override List<ShotCandidate> Candidates(AiSituation s, ShotPlanner planner, MatchController match) =>
                planner.PositionCandidates(s.AllUsable, match.AllDiscs);
        }
    }
}
