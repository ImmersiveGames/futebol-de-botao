using System.Collections.Generic;

namespace FutebolDeBotao
{
    /// <summary>O que um objetivo precisa para dar nota a uma jogada simulada.</summary>
    public readonly struct AiScoreContext
    {
        public readonly ShotPlanner Planner;
        public readonly bool GoalAfterWallIsValid;
        public readonly float FoulCaution;

        public AiScoreContext(ShotPlanner planner, bool goalAfterWallIsValid, float foulCaution)
        {
            Planner = planner;
            GoalAfterWallIsValid = goalAfterWallIsValid;
            FoulCaution = foulCaution;
        }
    }

    /// <summary>Como foi a melhor jogada de um objetivo: nota (média com o erro do nível) e os riscos.</summary>
    public struct AiVerdict
    {
        public float Score;
        /// <summary>A jogada simulada (sem erro) dá falta.</summary>
        public bool Foul;
        /// <summary>Com o erro de mira para um dos lados, dá falta.</summary>
        public bool FoulNearby;
        /// <summary>Com o erro de mira ou de força, alguma versão vira gol contra.</summary>
        public bool OwnGoalRisk;

        public bool Risky => Foul || FoulNearby || OwnGoalRisk;
    }

    /// <summary>
    /// Um objetivo da IA (marcar, tirar do perigo, fechar o gol...). Pelos fatos do toque (AiSituation) diz quanto vale
    /// agora (prioridade), quais ações servem e dá nota às jogadas simuladas com a conta dele. O decisor (AiBrain) tenta
    /// os objetivos do mais alto para o mais baixo e fica no primeiro que achar uma jogada boa.
    /// </summary>
    public abstract class AiGoal
    {
        public abstract string Name { get; }

        /// <summary>Quanto o objetivo vale neste toque. Zero ou menos: não vale agora.</summary>
        public abstract float Priority(AiSituation situation);

        /// <summary>Ações que servem a este objetivo, em ordem de preferência.</summary>
        public abstract IReadOnlyList<AiAction> Actions { get; }

        /// <summary>Nota de uma jogada simulada para este objetivo. Nulo: a jogada não serve.</summary>
        public abstract float? Score(in SimResult result, in ShotCandidate candidate, AiAction action, AiSituation situation,
            in AiScoreContext context);

        /// <summary>A melhor jogada atinge o objetivo? Não: o decisor tenta o próximo objetivo.</summary>
        public virtual bool Accepts(in AiVerdict verdict, AiSituation situation, AiDifficulty difficulty) => !verdict.Risky;

        /// <summary>Toques que sobram se a vez continuar depois desta jogada.</summary>
        protected static int TouchesAfter(AiSituation situation) => situation.TouchesLeft - 1;

        /// <summary>Nota padrão da jogada na bola (gol, falta, gol contra, posse, avanço; ShotPlanner.Score).</summary>
        protected static float BaseScore(in SimResult result, AiSituation situation, in AiScoreContext context) =>
            context.Planner.Score(result, situation.IsBallKick, situation.GoalsCount, context.GoalAfterWallIsValid,
                TouchesAfter(situation), context.FoulCaution);

        /// <summary>Os objetivos do passo 1 (o "Preparar o chute" entra com o plano de 2 toques).</summary>
        public static readonly AiGoal[] All =
        {
            new ScoreGoal(),
            new ClearDangerGoal(),
            new CloseGoalGoal(),
            new AdvanceGoal(),
            new SafeTouchGoal()
        };
    }
}
