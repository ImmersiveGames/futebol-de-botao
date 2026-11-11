namespace FutebolDeBotao
{
    /// <summary>Com o que o jogador está jogando agora: o texto das lições muda conforme o aparelho.</summary>
    public enum TutorialInputKind
    {
        Touch,
        Mouse,
        Keyboard,
        Gamepad
    }

    /// <summary>
    /// Todos os textos do tutorial num lugar só (para traduzir depois): nome das lições, instruções por aparelho e os
    /// avisos de acerto e erro. A ordem das lições é a de <see cref="LessonIds"/>; o id vai para o save, então não muda.
    /// </summary>
    public static class TutorialTexts
    {
        private static readonly string[] LessonIds = { "peteleco", "posse", "progol", "defender", "falta", "penalti" };

        private static readonly string[] LessonTitles =
            { "Peteleco", "Vez e toques", "Pro Gol!", "Defender", "Falta e tiro livre", "Pênalti" };

        public static int LessonCount => LessonIds.Length;

        public static string LessonId(int lesson) => LessonIds[lesson];

        public static string LessonTitle(int lesson) => LessonTitles[lesson];

        public static string Header(int lesson) => $"Lição {lesson + 1} de {LessonCount}: {LessonTitles[lesson]}";

        // ---- Botões e telas ----

        public const string Continue = "Continuar";
        public const string Skip = "Pular";
        public const string NextLesson = "Próxima lição";
        public const string Repeat = "Repetir";
        public const string Menu = "Menu";
        public const string LessonDone = "Lição concluída!";
        public const string AllDone = "Tutorial completo!";
        public const string AlreadyDone = "Você já tinha feito esta lição.";
        public const string AllDoneLine = "Agora é com você: bom jogo!";

        public static string Coins(int coins) => $"+{coins} moedas";

        public static string Bonus(int coins) => $"+{coins} de bônus por completar tudo";

        // ---- Menu ----

        public const string AskTitle = "Quer aprender a jogar?";
        public const string AskLine = "O tutorial tem 6 lições curtas e cada uma vale moedas.";
        public const string AskYes = "Sim";
        public const string AskNo = "Agora não";
        public const string MenuButton = "Tutorial";

        // ---- Instruções curtas (faixa de cima, enquanto joga) ----

        public const string ShortHit = "Acerte a bola";
        public const string ShortFar = "Leve a bola ao campo do adversário";
        public const string ShortTouches = "3 toques seguidos sem errar";
        public const string ShortScore = "Pro Gol! e faça o gol";
        public const string ShortDefend = "Arrume o goleiro e dê Pronto";
        public const string ShortFreeKickPlace = "Escolha de onde bater e dê Pronto";
        public const string ShortFreeKickShot = "Chute! Só o batedor joga";
        public const string ShortPenalty = "Escolha o lado, Pronto e chute";

        // ---- Lição 1: Peteleco ----

        public static string Hit(TutorialInputKind input) => input switch
        {
            TutorialInputKind.Touch => "Toque num botão do seu time para escolher. Depois arraste o dedo para trás, como um estilingue, e solte para chutar a bola.",
            TutorialInputKind.Keyboard => "A/D escolhe o botão e E confirma. Depois A/D mira, W/S muda a força e Espaço chuta. Acerte a bola!",
            TutorialInputKind.Gamepad => "Analógico ou D-pad escolhe o botão e A confirma. Depois o analógico mira, o direito muda a força e X chuta. Acerte a bola!",
            _ => "Clique num botão do seu time e arraste para trás, como um estilingue. Solte para chutar a bola."
        };

        public static string Far(TutorialInputKind input) => input switch
        {
            TutorialInputKind.Keyboard => "W aumenta a força do chute. Agora leve a bola até o campo do adversário!",
            TutorialInputKind.Gamepad => "O analógico direito aumenta a força do chute. Agora leve a bola até o campo do adversário!",
            _ => "Quanto mais você puxa para trás, mais forte sai o chute. Agora leve a bola até o campo do adversário!"
        };

        public const string Missed = "Errou a bola. Tente de novo!";
        public const string TooShort = "Faltou força! Tente de novo.";
        public const string Nice = "Boa!";

        // ---- Lição 2: Vez e toques ----

        public const string TouchesInfo = "Você segue jogando enquanto seu botão tocar na bola, até 3 toques seguidos. " +
                                          "A vez passa se errar a bola, fizer falta ou o adversário tocar nela por último.";

        public const string ThreeTouches = "Dê 3 toques seguidos na bola sem errar. Vale usar qualquer botão do seu time.";

        public static string TouchesLeft(int touches) => touches == 1 ? "Boa! Falta 1 toque." : $"Boa! Faltam {touches} toques.";

        public const string MissedTurn = "Errou a bola: numa partida, a vez passaria para o adversário. De novo!";
        public const string OpponentLast = "O adversário tocou por último: a vez seria dele. De novo!";
        public const string KeeperBall = "A bola parou com o goleiro: seria tiro de meta para ele. De novo!";
        public const string Foul = "Falta! Você bateu no adversário antes da bola. Tente de novo.";

        // ---- Lição 3: Pro Gol! ----

        public const string ShotInfo = "Gol só vale se você avisar antes. Com a bola no campo do adversário aparece o botão " +
                                       "Pro Gol!: aperte, o goleiro dele se arruma e aí você chuta.";

        public static string Score(TutorialInputKind input) => input switch
        {
            TutorialInputKind.Touch => "Aperte Pro Gol! (embaixo, à direita) e depois chute para fazer o gol.",
            TutorialInputKind.Keyboard => "Aperte C para avisar o Pro Gol! e depois chute para fazer o gol.",
            TutorialInputKind.Gamepad => "Aperte LB ou RB para avisar o Pro Gol! e depois chute para fazer o gol.",
            _ => "Clique em Pro Gol! e depois chute para fazer o gol."
        };

        public const string Goal = "GOL! É assim que se faz.";
        public const string NotCalled = "Gol anulado: sem o Pro Gol! o gol não vale. De novo!";
        public const string WallGoal = "Gol anulado: a bola bateu na parede do lado antes. De novo!";
        public const string Saved = "Não entrou. Tente de novo!";
        public const string NoTouches = "Acabaram os toques. Avise o Pro Gol! antes de chutar.";

        // ---- Lição 4: Defender ----

        public const string DefendInfo = "Quando o adversário avisa Pro Gol!, você arruma o seu goleiro antes do chute dele.";

        public static string Defend(TutorialInputKind input) => input switch
        {
            TutorialInputKind.Touch => "Arraste o goleiro para os lados para fechar o gol e aperte Pronto.",
            TutorialInputKind.Keyboard => "A/D mexe o goleiro para os lados e E dá Pronto.",
            TutorialInputKind.Gamepad => "Analógico ou D-pad mexe o goleiro para os lados e A dá Pronto.",
            _ => "Arraste o goleiro para os lados para fechar o gol e clique em Pronto."
        };

        public const string Defended = "Salvou! O chute não entrou.";
        public const string Conceded = "Gol do adversário. Tente de novo!";
        public const string OpponentFoul = "Falta do adversário. Vamos de novo.";

        // ---- Lição 5: Falta e tiro livre ----

        public const string FoulInfo = "Bater num adversário antes de tocar na bola é falta, e quem sofreu bate um tiro livre. " +
                                       "Dentro da área é pênalti. Falta longe da bola dá cartão.";

        public static string FreeKickPlace(TutorialInputKind input) => input switch
        {
            TutorialInputKind.Keyboard => "O adversário fez falta em você! A/D gira o batedor em volta da bola e E dá Pronto.",
            TutorialInputKind.Gamepad => "O adversário fez falta em você! O analógico gira o batedor em volta da bola e A dá Pronto.",
            _ => "O adversário fez falta em você! Arraste em volta da bola para escolher de onde bater e aperte Pronto."
        };

        public const string FreeKickDone = "Boa cobrança!";
        public const string FreeKickMissed = "Errou a bola. Vamos repetir a falta.";

        // ---- Lição 6: Pênalti ----

        public const string PenaltyInfo = "No pênalti cada um escolhe um lado escondido: o batedor não pode ficar reto atrás da " +
                                          "bola e o goleiro não pode ficar no meio. Quem escolhe primeiro fica sob uma faixa preta.";

        public static string Penalty(TutorialInputKind input) => input switch
        {
            TutorialInputKind.Keyboard => "O goleiro escolhe primeiro. Depois A/D gira o batedor, E dá Pronto e você chuta no canto!",
            TutorialInputKind.Gamepad => "O goleiro escolhe primeiro. Depois o analógico gira o batedor, A dá Pronto e você chuta no canto!",
            _ => "O goleiro escolhe primeiro. Depois arraste em volta da bola para escolher o lado, aperte Pronto e chute no canto!"
        };

        public const string PenaltyGoal = "GOL de pênalti!";
        public const string PenaltySaved = "Não foi gol. Tente de novo!";
    }
}
