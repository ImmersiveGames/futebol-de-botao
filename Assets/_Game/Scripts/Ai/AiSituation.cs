using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// O que a IA lê da partida no começo de cada toque, calculado uma vez: onde está a bola, quantos toques sobram,
    /// se existe chute livre, o perigo se a vez passar agora, placar e tempo. Os objetivos decidem só por estes fatos.
    /// </summary>
    public sealed class AiSituation
    {
        /// <summary>Bola mais perto que isto do próprio gol: perigo.</summary>
        public const float DangerDistance = 3.5f;

        public TeamSide Side;
        public MatchState State;
        public Vector2 Ball;
        public bool BallInAttackHalf;
        public float BallToOwnGoal;
        public float BallToOpponentGoal;
        /// <summary>Toques que sobram contando este.</summary>
        public int TouchesLeft;
        public bool IsLastTouch => TouchesLeft <= 1;
        /// <summary>Um gol nesta jogada valeria (chute anunciado ou jogo sem goleiro no campo de ataque).</summary>
        public bool GoalsCount;
        public bool IsBallKick;
        public bool IsFreeKick;
        public bool CanCallShot;
        /// <summary>Tem chute a gol com caminho livre até a bola e da bola até o gol (sem contar o goleiro).</summary>
        public bool HasClearShot;
        public float ShotCallDistance;
        /// <summary>A IA olhou o perigo neste toque (depende do nível).</summary>
        public bool ReadDanger;
        /// <summary>0 a 1: chance de o adversário ter um chute fácil a gol se a vez passasse agora.</summary>
        public float Danger;
        public int ScoreDifference;
        public float SecondsLeft;
        public bool HasYellow;
        /// <summary>Botões que a IA testa na bola (os mais perto dela, conforme o nível).</summary>
        public List<Disc> Usable;
        /// <summary>Todos os botões que podem jogar agora (o toque seguro escolhe entre eles).</summary>
        public List<Disc> AllUsable;
        /// <summary>Todos os botões da IA em campo (no toque seguinte qualquer um joga, mesmo depois de um tiro livre).</summary>
        public List<Disc> TeamDiscs;
        public bool HasGoalkeeper;
        /// <summary>Personalidade da jogada para o log ("perfil=Ofensivo ajuste=Defensivo:0.50").</summary>
        public string Profile = string.Empty;

        public static AiSituation Read(MatchController match, TeamSide side, ShotPlanner planner, ShotPlanner opponentPlanner,
            AiDifficulty difficulty, List<Disc> usable, List<Disc> allUsable)
        {
            var opponent = side == TeamSide.Bottom ? TeamSide.Top : TeamSide.Bottom;
            Vector2 ball = match.Ball.Body.position;
            var situation = new AiSituation
            {
                Side = side,
                State = match.State,
                Ball = ball,
                BallInAttackHalf = FieldLayout.AttackDirection(side).y * ball.y > 0f,
                BallToOwnGoal = Vector2.Distance(ball, new Vector2(0f, FieldLayout.GoalLineY(side))),
                BallToOpponentGoal = Vector2.Distance(ball, new Vector2(0f, FieldLayout.GoalLineY(opponent))),
                TouchesLeft = match.TouchesLeft,
                GoalsCount = match.GoalWouldCount,
                IsBallKick = match.State == MatchState.GoalKick,
                IsFreeKick = match.FreeKickDisc != null,
                CanCallShot = match.State == MatchState.Aim && match.CanCallShot,
                ShotCallDistance = difficulty.shotCallDistance,
                ScoreDifference = match.Score(side) - match.Score(opponent),
                SecondsLeft = match.ClockSeconds,
                HasYellow = match.HasYellow(side),
                Usable = usable,
                AllUsable = allUsable,
                TeamDiscs = new List<Disc>(),
                HasGoalkeeper = match.Options.hasGoalkeeper
            };
            foreach (var disc in match.AllDiscs)
                if (disc != null && disc.isActiveAndEnabled && disc.Side == side) situation.TeamDiscs.Add(disc);

            if (!situation.IsBallKick && usable.Count > 0)
                situation.HasClearShot = planner.HasClearShot(usable);

            // Perigo: os chutes a gol que o adversário teria daqui, pela geometria (sem simular). Nem sempre olha.
            // Com a bola no campo de ataque da IA o adversário não pode anunciar o chute: sem perigo.
            situation.ReadDanger = Random.value < difficulty.dangerAwareness;
            if (situation.ReadDanger && !situation.BallInAttackHalf)
            {
                var opponentDiscs = new List<Disc>();
                foreach (var disc in match.AllDiscs)
                    if (disc != null && disc.isActiveAndEnabled && disc.Side == opponent) opponentDiscs.Add(disc);
                var threats = opponentPlanner.GoalThreats(opponentDiscs, 3);
                float danger = 0f;
                foreach (var threat in threats)
                {
                    // Chute fácil (pouca força) e sem nada no caminho da bola = perigo alto.
                    float ease = Mathf.Clamp01(1.2f - threat.Power01);
                    if (threat.BallBlocked) ease *= 0.4f;
                    danger = Mathf.Max(danger, ease);
                }
                situation.Danger = danger;
            }
            return situation;
        }

        /// <summary>Resumo para o log: "campo=ataque bola=(0,5;2,1) gol_proprio=7,2 ...".</summary>
        public string Describe()
        {
            var c = CultureInfo.InvariantCulture;
            return string.Join(" ",
                $"estado={State}",
                $"campo={(BallInAttackHalf ? "ataque" : "defesa")}",
                string.Format(c, "bola=({0:0.0};{1:0.0})", Ball.x, Ball.y),
                string.Format(c, "gol_proprio={0:0.0}", BallToOwnGoal),
                string.Format(c, "gol_adversario={0:0.0}", BallToOpponentGoal),
                $"toques={TouchesLeft}",
                $"vale_gol={(GoalsCount ? 1 : 0)}",
                $"chute_livre={(HasClearShot ? 1 : 0)}",
                ReadDanger ? string.Format(c, "perigo={0:0.00}", Danger) : "perigo=nao_olhou",
                $"placar={ScoreDifference:+0;-0;0}",
                string.Format(c, "tempo={0:0}s", SecondsLeft),
                Profile);
        }
    }
}
