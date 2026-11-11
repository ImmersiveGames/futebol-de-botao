using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Tutorial (GDD, Modos de jogo): lições curtas dentro da partida de verdade, contra a IA no Fácil. Aqui fica o que passa
    /// do Menu para a Partida (qual lição, se o tutorial está ativo) e o progresso no <see cref="GameSave"/>: lições
    /// completas, se o Menu já perguntou "Quer aprender a jogar?" e as moedas (15 por lição na primeira vez e mais 10 ao
    /// completar todas). Quem conduz as lições na partida é o <see cref="TutorialDirector"/>.
    /// </summary>
    public static class Tutorial
    {
        /// <summary>Moedas por lição completada pela primeira vez.</summary>
        public const int LessonReward = 15;

        /// <summary>Moedas a mais ao completar todas as lições (uma vez só).</summary>
        public const int CompleteBonus = 10;

        /// <summary>A partida aberta é o tutorial.</summary>
        public static bool Active { get; private set; }

        /// <summary>Lição em andamento (0 a <see cref="TutorialTexts.LessonCount"/> - 1).</summary>
        public static int Lesson { get; set; }

        private static SaveData.TutorialData Data => GameSave.Data.tutorial;

        public static int LessonCount => TutorialTexts.LessonCount;

        public static bool IsDone(int lesson) => Data.done.Contains(TutorialTexts.LessonId(lesson));

        public static int DoneCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < LessonCount; i++)
                    if (IsDone(i)) count++;
                return count;
            }
        }

        public static bool AllDone => DoneCount >= LessonCount;

        /// <summary>O Menu pergunta "Quer aprender a jogar?" no primeiro Jogar (só se nunca perguntou nem fez lição).</summary>
        public static bool ShouldAsk => !Data.asked && Data.done.Count == 0;

        public static void MarkAsked()
        {
            if (Data.asked) return;
            Data.asked = true;
            GameSave.WriteOnly("tutorial perguntado");
        }

        /// <summary>A primeira lição que falta (todas feitas: a primeira).</summary>
        public static int FirstPending()
        {
            for (int i = 0; i < LessonCount; i++)
                if (!IsDone(i)) return i;
            return 0;
        }

        /// <summary>
        /// Prepara a partida do tutorial: o jogador com o Meu time embaixo e um time do catálogo pela IA (Fácil) em cima,
        /// na mesa de casa. As escolhas da Seleção de times ficam guardadas e voltam no <see cref="Leave"/>.
        /// </summary>
        public static void Prepare(int lesson)
        {
            MatchSession.SaveSides();
            Active = true;
            Lesson = Mathf.Clamp(lesson, 0, LessonCount - 1);
            MatchSession.VsAi = true;
            MatchSession.UseProfile(TeamSide.Bottom);
            var catalog = TeamCatalog.Load();
            MatchSession.SetTeamIndex(TeamSide.Top, Random.Range(0, Mathf.Max(1, catalog.TeamCount)));
            MatchSession.UseTeamShirts(TeamSide.Top);
            MatchSession.PrepareVenues();
        }

        /// <summary>Saiu do tutorial (o Menu abriu): as escolhas de antes voltam.</summary>
        public static void Leave()
        {
            if (!Active) return;
            Active = false;
            MatchSession.RestoreSides();
        }

        /// <summary>
        /// Lição completada: na primeira vez paga <see cref="LessonReward"/> e, se era a última que faltava,
        /// <see cref="CompleteBonus"/>. Grava e devolve as moedas da lição e do bônus (0 se já tinha feito).
        /// </summary>
        public static (int lesson, int bonus) Complete(int lesson)
        {
            string id = TutorialTexts.LessonId(lesson);
            if (Data.done.Contains(id)) return (0, 0);
            Data.done.Add(id);
            int coins = LessonReward;
            int bonus = 0;
            if (AllDone && !Data.bonusPaid)
            {
                Data.bonusPaid = true;
                bonus = CompleteBonus;
            }
            Finance.Earn(FinanceSource.Tutorial, $"Tutorial: lição {lesson + 1}", coins);
            Finance.Earn(FinanceSource.Tutorial, "Tutorial completo (bônus)", bonus);
            GameSave.WriteOnly("lição do tutorial");
            return (coins, bonus);
        }

        // Com o "Enter Play Mode" sem recarregar o domínio, os estáticos sobrevivem entre execuções no editor.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active = false;
            Lesson = 0;
        }
    }
}
