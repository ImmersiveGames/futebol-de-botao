using System.Collections.Generic;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>O que um objetivo precisa para dar nota a uma jogada simulada.</summary>
    public readonly struct AiScoreContext
    {
        public readonly ShotPlanner Planner;
        /// <summary>O planejador do lado do adversário: ele teria chute livre de onde a bola parou?</summary>
        public readonly ShotPlanner OpponentPlanner;
        public readonly bool GoalAfterWallIsValid;

        public AiScoreContext(ShotPlanner planner, ShotPlanner opponentPlanner, bool goalAfterWallIsValid)
        {
            Planner = planner;
            OpponentPlanner = opponentPlanner;
            GoalAfterWallIsValid = goalAfterWallIsValid;
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
        /// <summary>Chances (0 a 1) entre a jogada e as versões com o erro do nível.</summary>
        public float FoulChance;
        public float OwnGoalChance;
        /// <summary>Último toque não fica com a IA (a vez passa).</summary>
        public float LoseChance;
        /// <summary>Parte da nota que veio do toque seguinte (já somada em <see cref="Score"/>).</summary>
        public float Follow;
        public bool HasFollow;

        public bool Risky => Foul || FoulNearby || OwnGoalRisk;

        /// <summary>Sem falta certa, sem risco de gol contra e com chance de falta que o nível aceita.</summary>
        public bool SafeFor(AiDifficulty difficulty) => !Foul && !OwnGoalRisk && FoulChance <= difficulty.maxFoulChance;
    }

    /// <summary>
    /// Um objetivo da IA, com uma conta só para saber se a jogada deu certo. A ordem dos objetivos vem da situação
    /// (AiBrain.Plan): Marcar; com toque sobrando, Preparar o chute; no último toque (ou se preparar não deu), Afastar
    /// (bola perto do próprio gol) ou Proteger; por fim, Fechar o gol com um botão.
    /// </summary>
    public abstract class AiGoal
    {
        public abstract string Name { get; }

        /// <summary>Ações que servem a este objetivo, em ordem de preferência.</summary>
        public abstract IReadOnlyList<AiAction> Actions { get; }

        /// <summary>
        /// Nota de uma jogada simulada para este objetivo. Nulo: a jogada não serve. <paramref name="from"/>: onde a bola
        /// estava antes (no toque seguinte imaginado, onde a jogada anterior parou); nulo = a bola de agora.
        /// </summary>
        public abstract float? Score(in SimResult result, in ShotCandidate candidate, AiAction action, AiSituation situation,
            in AiScoreContext context, Vector2? from = null);

        /// <summary>O decisor também imagina o toque seguinte das melhores jogadas.</summary>
        public virtual bool PlansAhead => false;

        /// <summary>A melhor jogada atinge o objetivo? Não: o decisor tenta o próximo objetivo.</summary>
        public virtual bool Accepts(in AiVerdict verdict, AiSituation situation, AiDifficulty difficulty) => verdict.SafeFor(difficulty);

        /// <summary>
        /// Chance mínima de gol para chutar: a do nível (com a personalidade); no último toque basta a metade, porque
        /// a vez vai passar de qualquer jeito.
        /// </summary>
        public static float NeededChance(AiSituation situation, AiDifficulty difficulty) =>
            situation.IsLastTouch ? difficulty.callShotChance * 0.5f : difficulty.callShotChance;

        /// <summary>
        /// Nota quando a jogada acaba em gol: gol contra é o pior; gol que vale é o melhor; gol que não vale (anulado)
        /// entrega um tiro de meta. Nulo: não teve gol.
        /// </summary>
        protected static float? GoalOutcome(in SimResult result, AiSituation situation, in AiScoreContext context)
        {
            if (result.GoalOf == null) return null;
            if (result.GoalOf == situation.Side) return -100f;
            bool valid = situation.GoalsCount && (context.GoalAfterWallIsValid || !result.BallTouchedWall);
            return valid ? 100f : -20f;
        }

        /// <summary>A jogada termina sem falta nem gol e com o último toque da IA (no tiro de meta, ninguém tocar conta).</summary>
        protected static bool KeepsBall(in SimResult result, AiSituation situation)
        {
            if (result.Foul || result.GoalOf != null) return false;
            var last = result.LastTouchSide ?? (situation.IsBallKick ? situation.Side : (TeamSide?)null);
            return last == situation.Side;
        }

        protected static Vector2 OwnGoal(AiSituation situation) => new(0f, FieldLayout.GoalLineY(situation.Side));

        public static readonly AiGoal Scoring = new ScoreGoal();
        public static readonly AiGoal Preparing = new PrepareShotGoal();
        public static readonly AiGoal Clearing = new ClearDangerGoal();
        public static readonly AiGoal Protecting = new ProtectGoal();
        public static readonly AiGoal Closing = new CloseGoalGoal();
    }
}
