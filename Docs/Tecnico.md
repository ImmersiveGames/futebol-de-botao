# Pro Gol! - Documentação técnica

> Cópia exportada do Claude Doc em 18/11/2026 (rev 14). O original é o Claude Doc: https://claude.ai/artifact/HM4UgEQL9YGSowBaaYWpz9. Não editar aqui; exportar de novo quando o doc mudar.

Nov 18, 2026 · @Immersive Games

## Visão geral

O jogo tem cerca de 28 mil linhas de C# em 145 arquivos dentro de `Assets/_Game/Scripts` (assembly `FutebolDeBotao`), mais 4 arquivos em `Assets/_Game/Lojas` que ligam os SDKs do Google. Esta documentação descreve o código do commit 4979392 (18/11/2026); nada foi alterado para escrevê-la.

**Três assemblies do jogo e um de ligação:**

| Assembly | Onde | O que tem |
| --- | --- | --- |
| FutebolDeBotao | Scripts/ | Todo o jogo: partida, IA, telas, save, torneio, loja. Não conhece nenhum SDK do Google. |
| FutebolDeBotao.Editor | Scripts/Editor/ | Menus de editor (Aplicar artes, Inspectors, janela de textos, atalhos de teste). |
| FutebolDeBotao.Editor.Framework | Scripts/Editor/Framework/ | Geradores de cena e de assets do framework (Criar telas, Criar jogadores, Criar áudio, câmera). |
| Assembly-CSharp | Lojas/ | AdMob, Firebase, Play Games e Unity IAP. Cada arquivo implementa uma interface do jogo (IAdsProvider, IStoreProvider, IPlayGamesBackend) e se registra na abertura. |

Essa separação é boa: o jogo compila e roda no editor e no WebGL sem os SDKs, usando provedores simulados.

**Pastas de Scripts, por tamanho:**

| Pasta | Arquivos | Papel |
| --- | --- | --- |
| Match | 12 | Regras e estado da partida (MatchController com 1.918 linhas), sessão entre telas (MatchSession), times e opções. |
| Screens | 24 | Telas: Menu, Seleção de times, Resultado, Torneio, Meu time, Loja, Estatísticas, Configurações, navegação por teclado/controle. |
| Ai + Ai/Goals | 15 | IA por objetivos, planejador de chutes e simulador de física. |
| Physics | 13 | Botão, bola, goleiro, paredes, monitor de movimento. |
| Input | 12 | Mira, joystick, goleiro, dispositivos por jogador. |
| Art | 11 | Mesa, feltro, camisas, bolas, paleta de cores (ScriptableObjects + pintura). |
| Editor (+ Framework) | 18 | Geradores e Inspectors. |
| Monetization | 7 | Anúncios, idade, compras, patrocínio. |
| Save | 6 | SaveData, GameSave, perfil, estatísticas, loja, financeiro. |
| Hud | 5 | HUD da partida, fogos, texto ondulado, destaque da vez. |
| PlayGames | 5 | Login, nuvem, conquistas, ranking semanal. |
| Tournament, Tutorial, Localization, Audio, Players | 3 cada | Torneio e liga, tutorial, idiomas, sons, jogadores do framework. |
| Analytics, Dev | 1 cada | Eventos do Firebase; tecla F12 de captura. |

**Pacotes:** Immersive Framework 1.1.0 (rotas, Activity, pausa, reiniciar, jogadores, Progression Save), Foundation por commit, Audio 0.2.3, Logging 0.2.3, Pooling 0.2.2; Unity Input System 1.19, uGUI, Cinemachine, Unity IAP 5.4.4. O manifest ainda traz pacotes que o jogo não usa (ver Pontos de atenção).

**Cenas:** Abertura, Menu, Partida, Resultado e PersistentContent (o que fica vivo entre rotas). Os assets do framework (GameApplication, rotas, Activity Jogo, jogadores, câmera) ficam em `_Game/App`; opções, formações e física em `_Game/Data`; o que é carregado em tempo de jogo fica em `_Game/Resources` (times, torneios, IA, mesas, feltros, camisas, bolas, idiomas).

**Padrões que se repetem no código:**

- **Classes estáticas como serviços:** MatchSession, GameSave, Tournament, Shop, Finance, PlayerStats, Txt, Ads, PlayGames. São globais e guardam estado entre cenas.
- **ScriptableObjects como dados ajustáveis:** times, torneios, níveis da IA, opções, mesas, camisas. O usuário cria itens novos sem código.
- **UI montada por código:** as telas do framework vêm do gerador "Criar telas"; as telas mais novas (Meu time, Loja, Configurações, Estatísticas, Torneio) se montam sozinhas em tempo de jogo.
- **Comentários em português** com `<summary>` em quase toda classe e método público, o que ajuda muito a manutenção.

## Fluxo do jogo e telas

O jogo tem quatro rotas do framework (Abertura, Menu, Partida e Resultado), e quase toda a navegação acontece em painéis dentro da cena do Menu. O que passa de uma rota para outra fica na classe estática **MatchSession**.

**Abertura do aplicativo** (sem cena, por `RuntimeInitializeOnLoadMethod`):

1. Antes da primeira cena: `GameSave` lê o save, `MobileSettings` acerta a taxa de quadros e a tela, e `FirebaseServices` e `AdMobAds` (Lojas) se registram.
2. Depois da primeira cena: `Ads`, `ScreenFit`, `GameVolume`, `UnityIapStore`, `GooglePlayGamesBackend` e `ScreenshotKey` (só editor) se ligam.

**Rotas e telas:**

| Rota | Script principal | O que acontece |
| --- | --- | --- |
| Abertura | TitleScreen | Mostra o nome; qualquer tecla, clique ou toque pede a rota do Menu. |
| Menu | MenuScreen + painéis | Primeira tela (Jogar, Meu time, Loja, Configurações, Sair), painel Jogar, Controles (DeviceSelectionScreen), Opções, Seleção de times (TeamSelectionScreen), Torneio/Liga (TournamentScreen), Meu time (ProfileScreen), Loja (ShopScreen), Estatísticas (StatsScreen), pergunta do tutorial. |
| Partida | MatchController + MatchHud | A Activity "Jogo" do framework. Pausa (PauseMenu) e Reiniciar são do framework. |
| Resultado | ResultScreen | Placar, tabela dos times, página Premiação; Jogar de novo, Menu ou Avançar (torneio). |

**Telas por cima de tudo**, montadas por código com `OverlayUi`: Configurações, idade (AgeScreen), diálogos (MessageDialog) e o anúncio simulado. Enquanto uma está aberta, `OverlayUi.PauseNavigators` desliga os outros menus para a mesma tecla não mexer nos dois.

**Caminho de uma partida única:** Jogar → Partida única → Controles (só com mais de um dispositivo) → Opções → Seleção de times → `MenuPlayers` coloca os jogadores na sessão do framework → rota Partida → rota Resultado → Jogar de novo (mesma mesa) ou Menu. O torneio e a liga passam pela chave (TournamentScreen) e voltam a ela pelo Avançar do Resultado (`MatchSession.OpenTournament`). O tutorial vai dos Controles direto para a Partida.

**MatchSession guarda entre as rotas:**

- Modo (`VsAi`), nível da IA, dispositivos dos jogadores e se dividem o mesmo dispositivo.
- Opções: uma cópia em memória do asset `OpcoesDaPartida` (o asset nunca muda); o torneio e o tutorial têm a sua própria cópia.
- Times por lado (índice no catálogo ou -1 = Meu time), uniforme, esquemas de 3 e 5 botões, camisas sorteadas para a IA.
- Mesa de casa e de visitante (`VenueSetup`: mesa, feltro, cor da trave, bola), resultado e números da última partida.
- Estado do torneio na tela (`TournamentSetup`, `InTournamentMatch`, `TournamentId`, `LeagueId`, `LeagueLegs`).

**Jogadores no framework:** `MenuPlayers` faz o Join/Leave das vagas antes da partida (contra a IA só o Jogador 1; dois jogadores com dispositivos diferentes, os dois; no mesmo dispositivo, ninguém, e a partida lê os dispositivos direto). Na partida, `MatchPlayers` bloqueia quem não é da vez e solta todos os bloqueios antes de sair (sem isso o Leave do Menu falhava). `CoachActor` é o Actor de cada jogador e lê o mapa de comandos "Jogo".

**Navegação por teclado e controle:** cada painel tem um `MenuNavigator` com a lista de `MenuEntry` (botão ou linha de opção com setas). Com dono (Jogador 1 ou 2), só o dispositivo dele navega; a moldura amarela só aparece com teclado ou controle.

## Partida

A partida inteira é uma máquina de estados dentro de **MatchController** (`Match/MatchController.cs`, 1.918 linhas), um componente que recebe a entrada e a saída da Activity "Jogo" do framework (`IActivityContentLifecycleReceiver`). Ela não tem sub-sistemas: regras, bola parada, pênalti, disputa, tutorial, pintura dos times e registro de estatísticas moram todos nela.

**Estados (`MatchState`):**

| Estado | Quem age | Sai quando |
| --- | --- | --- |
| Waiting | ninguém | a Activity entra (`StartMatch`) |
| KickOff | ninguém | na mesma hora: formação, bola no centro, vai para Aim |
| Aim | time da vez mira | peteleco (Moving), Pro Gol! (ShotCall), tempo da mira (passa a vez) ou relógio zerado (fim) |
| ShotCall | defensor ajusta o goleiro | Pronto ou tempo: ShotAim, ou bola do goleiro |
| ShotAim | atacante chuta | peteleco (Moving) |
| PenaltySetup | batedor gira em volta da bola | Pronto ou tempo: tiro livre vira Aim; pênalti vira ShotCall |
| GoalKick | defensor move a bola e chuta direto nela | chute (Moving) |
| Moving | ninguém | `MotionMonitor.Settled` (tudo parado), gol ou falta |
| Goal | aviso de gol | pausa: saída, tiro de meta ou próxima cobrança |
| End | placar final | pausa: rota do Resultado |

**Como uma jogada é decidida.** O `AimController` avisa `Flicked` (botão) ou `BallKicked` (tiro de meta); o estado vira Moving. Quando o `MotionMonitor` diz que tudo parou, `OnSettled` decide em ordem: relógio zerado (fim), bola na área do goleiro (`KeeperBall.Owner`, tiro de meta), ninguém tocou ("Errou!"), adversário tocou por último, chute anunciado que não entrou ("Pra fora!"), sobrou toque (volta para Aim) ou acabaram os toques.

**Gol.** `GoalTrigger` avisa quando a bola passa inteira da linha. `OnBallEntered` decide se vale: gol contra se quem jogou defende aquele gol; gol normal só com Pro Gol! (sem goleiro: chute do campo de ataque); parede lateral anula, a menos que a opção "Gol após tocar a parede" seja Válido.

**Faltas.** O `Disc` levanta três eventos: `Fouled` (o botão do peteleco acertou adversário antes da bola), `HitOpponent` (companheiro empurrado acertou adversário) e `HitOpponentKeeper`. `CommitFoul` congela a mesa, decide pênalti (vítima na área do defensor) ou tiro livre, e aplica cartão se a vítima estava a mais de `farFoulDistance` (3,5) da bola no começo da jogada. O segundo cartão tira o botão; sem botões, `Walkover`.

**Bola parada:**

- **Tiro livre** (`SetupFreeKick`): a bola vai ao lugar da falta, quem fez a falta volta à formação e quem estiver na roda do batedor é empurrado para o lugar livre mais perto (`FindFreeSpot`, busca em anéis de 0,15 até 8 unidades, 24 direções).
- **Pênalti** (`SetupPenalty`): bola na marca, atacantes na formação, defensores espalhados na linha do meio. Com goleiro é o **pênalti escondido**: `BeginPenaltyChoice` decide quem escolhe primeiro (contra a IA, a IA), e `UpdatePenaltyCover` desenha a faixa preta por cima de quem já escolheu.
- **Tiro de meta** (`SetupGoalKick`): todos na formação, bola na frente do goleiro; o jogador arrasta a bola para os lados (`MoveGoalKickBall`, anda de 0,02 em 0,02 até encostar num botão) e depois chuta.

**Fim, prorrogação e pênaltis.** `EndMatch` olha o empate: prorrogação (se ligada e ainda não jogada), depois disputa (se ligada e com goleiro). Na disputa só ficam ativos o batedor (revezando os botões que estavam em campo) e o goleiro de quem defende; `ShootoutWinner` encerra quando um lado não alcança mais ou, na morte súbita, quando um faz mais com o mesmo número de cobranças.

**Avisos que param o jogo.** `Hold()` trava controles, relógio e IA por `noticeHoldSeconds` (3 s) depois de falta, cartão, pênalti, gol e bola do goleiro. As mensagens ficam em `Message` com um contador de versão (`MessageVersion`) que o HUD observa.

**Registro no fim** (`RecordStats`): números dos dois times para o Resultado (`MatchSession.LastMatchNumbers`), estatísticas e moedas do jogador (`PlayerStats.Finish`), conquistas, Analytics e o resultado do jogo do torneio (`Tournament.RecordPlayerMatch`). Sair antes do fim conta abandono (`OnActivityContentExited`).

**Dados da partida:**

- `MatchOptions` (asset `Data/OpcoesDaPartida`): duração, toques, faltas, prorrogação, pênaltis, botões, goleiro e largura, tempos de mira, goleiro e batedor, arco e lados do pênalti, tempos de aviso.
- `FieldLayout`: medidas do campo em constantes (7 x 11, gol 2,4 x 0,8, área 4 x 1,5, marca do pênalti, círculo central) e funções de lado e direção de ataque.
- `Formation` (assets `Formacao 3` e `Formacao 5` e os esquemas do catálogo): posições do lado de baixo, espelhadas para o de cima.
- `KeeperBall`: a regra de "bola do goleiro" (caixa do goleiro + 0,15 para os lados e 0,2 para a frente, e o espaço até o gol).
- `TeamData`, `TeamCatalog` e `TeamUniform`: times, siglas, dois uniformes e o catálogo (`Resources/Times/_CatalogoDeTimes`, carregado uma vez e guardado).

A IA e o tutorial entram como componentes adicionados na hora: `ConfigureAi` põe um `AiPlayer` no time de cima contra a IA, e `ConfigureTutorial` põe o `TutorialDirector`, que recebe as jogadas pelos métodos `Tutorial*` e por `OnPlayEnded`/`OnGoal`/`OnFoul`.

## Física, input e câmera

A mesa usa a física 2D da Unity sem gravidade, e todos os números de sensação ficam num asset só, **PhysicsTuning** (`Data/PhysicsTuning`): massa e amortecimento de botão e bola, quique, atrito, impulso máximo, arrasto máximo e mínimo, quique das paredes, empurrão das paredes e do canto, velocidade e tempo de repouso, velocidade do goleiro.

**Peças da mesa (pasta Physics):**

| Script | Papel |
| --- | --- |
| Disc | Botão: recebe o impulso (`Flick`), detecta falta (acertar adversário ou goleiro antes da bola) e toque na bola, guarda a velocidade antes do passo para a batida da bola. |
| Ball | Bola sem atrito: guarda quem tocou por último, a lista de batidas da jogada (até 16, para o log), se tocou a parede lateral; para dentro da rede depois do gol. |
| BallRebound | Faz a bola rebater no botão parado como numa parede (o botão não se mexe). Usado pelo jogo e pela simulação da IA. |
| Goalkeeper | Goleiro cinemático que só desliza em X, com velocidade limitada; para encostado no que estiver no caminho, nunca empurra. `SetWidth` estica a arte. |
| Wall, WallElastic, WallRepulsion | Paredes: marcação (lateral anula gol), quique com ganho, e um empurrão suave para nada ficar parado encostado (mais forte no canto). |
| GoalTrigger | Avisa quando a bola passa inteira da linha do gol. |
| MotionMonitor | Olha botões e bola a cada passo de física e avisa `Settled` quando tudo fica abaixo de `restSpeed` por `restTime` (0,3 s). |
| BallVisual | Desenho da bola: quadros de rolagem pela distância, 8 direções, rastro quando rápida. |
| TableImpacts | Evento estático de batidas (botão, bola, parede, goleiro) com a velocidade, para os sons. |

**Input (pasta Input).** O peteleco nasce no **AimController**: com mouse é arrastar do botão para trás; com toque, um botão fica escolhido e o arrasto pode começar em qualquer lugar; com teclado ou controle, um cursor anda entre os botões e a mira abre apontando para a bola. No toque, a direção do chute é a de 0,08 s antes de soltar o dedo (o histórico fica numa fila). A IA usa a mesma mira (`BeginScripted`, `SetScripted`, `ReleaseScripted`), então o jogador vê a mira dela.

- **AimVisuals** desenha a faixa de força (arte Power\_Rings ou uma textura feita em código), a mira de bolinhas e a linha prevista da bola, conforme a Ajuda de mira.
- **TouchJoystick** é só visual: o círculo e a bolinha onde o dedo encostou.
- **GoalkeeperControl** move o goleiro do defensor no Pro Gol! (arrasto, A/D, analógico).
- **TeamInput** define `ITeamInput`, a interface de comandos de um time; `MatchInput.For(side)` devolve o `CoachActor` do lado (jogador do framework) ou um `DeviceTeamInput` que lê os dispositivos direto (dois jogadores revezando).
- **ControlsDefinition** é a fonte única das teclas: o "Criar jogadores" grava o `Controles.inputactions` a partir dela, e o modo sem técnico monta as mesmas ações em tempo de jogo.
- **DeviceChoice** guarda o dispositivo de cada jogador (tela Controles), o dono de cada dispositivo e quem pausou; **PadMode** sabe se o último comando veio de teclado/controle; **AndroidBack** entrega o Voltar do Android a um só dono por quadro; **UiPointer** impede que um toque no HUD vire mira.

**Câmera e tela.** A câmera nasce do Camera Output do framework depois que a cena carrega, por isso `WorldCamera.Resolve` procura de novo até ela existir. **ScreenFit** roda em todas as cenas e acerta o tamanho ortográfico para caber a mesa e as duas faixas de HUD na largura da tela (tela em pé) ou só a mesa (tela deitada). **MobileSettings** põe 60 quadros por segundo no celular e mantém a tela acesa durante a partida.

## IA

A IA decide cada toque por objetivos numa ordem fixa e testa as jogadas numa cópia invisível da mesa, com física própria; depois chuta pelo mesmo `AimController` do jogador, então segue exatamente as mesmas regras. São 15 arquivos e cerca de 3.300 linhas em `Scripts/Ai`.

**As peças:**

| Script | Papel |
| --- | --- |
| AiPlayer (659 linhas) | Componente que a partida põe no time de cima. Na vez da IA roda corrotinas: o toque (`PlayTouch`), o goleiro no Pro Gol! do jogador (`PlaceKeeper`/`BestKeeperX`), o batedor no tiro livre e pênalti (`SetUpKicker`) e a bola do tiro de meta (`PlaceGoalKickBall`). Mostra a mira antes de soltar e aplica o erro do nível. |
| AiSituation | Fatos lidos uma vez no começo do toque: bola, toques, se há chute livre, placar, tempo. |
| AiBrain (545 linhas) | Decide: monta a ordem dos objetivos (`Plan`), simula as jogadas de cada ação, dá nota, testa o erro nas melhores e sorteia entre as melhores seguras. Lembra as jogadas de saída já usadas. |
| AiGoal + Goals/ | Os objetivos: ScoreGoal (Marcar), ClearDangerGoal (Afastar), PrepareShotGoal (Preparar), ProtectGoal (Proteger), CloseGoalGoal (Fechar o gol). Cada um tem uma conta só (`Score`). |
| AiAction | O que dá para fazer num toque (peteleco, chute na bola, posicionar, anunciar Pro Gol!), com pré-condição e as jogadas candidatas. |
| ShotPlanner (862 linhas) | O cérebro sem física: gera jogadas pela geometria (como sinuca), confere o caminho com CircleCast, sabe se há chute livre, quantos botões têm chute, as ameaças do adversário, e dá nota ao resultado. Tem o modo pênalti (forças e alvos próprios). |
| ShotSimulator (400 linhas) | A mesa invisível: copia paredes, botões, bola e goleiros numa `PhysicsScene2D` separada e simula um peteleco por até 4 s de jogo em passos de `fixedDeltaTime`. Refaz o rebote da bola e o quique das paredes; o empurrão suave das paredes fica de fora. |
| AiDifficulty | Números de cada nível (`Resources/IA`: Facil, Medio, Dificil, Pro): botões testados, simulações, escolha entre as N melhores, falta aceita, planejar o toque seguinte, chance e tamanho do erro, distância do Pro Gol!, erro e chutes considerados pelo goleiro, pausas. Sem asset, usa números do código. |
| AiProfile | Perfis Ofensivo, Equilibrado e Defensivo (`Resources/IA/Perfis`), escolhidos pelo esquema; mexem só na distância do Pro Gol! e no risco de falta. Nos 25% finais o placar puxa o perfil. |
| AiLog | Uma linha no console por toque e, só no editor, um CSV por partida em `Logs/IA` com fatos, objetivos, jogada, nota esperada e o que aconteceu. |

**Uma decisão, passo a passo:**

1. `AiPlayer.PlayTouch` espera a pausa de "pensar" e lê a `AiSituation`; o `AiBrain` sincroniza a mesa simulada com a de verdade (`ShotSimulator.Sync`).
2. `AiBrain.Decide` percorre os objetivos do plano. Para cada ação pede candidatos ao `ShotPlanner`, escolhe os que vão à mesa simulada (`Diversify`: primeiro a melhor de cada botão) e simula.
3. As melhores jogadas são simuladas de novo com o erro de mira e de força para os dois lados, só para medir o risco (falta, gol contra, perder a bola); a nota continua a da jogada perfeita.
4. No Preparar com 3 toques, Médio, Difícil e Pro imaginam também o toque seguinte (`FollowUp`) a partir de onde a jogada simulada parou.
5. Fica no primeiro objetivo que aceitar uma jogada segura; sorteia entre as melhores (`pickAmongBest`).
6. Na hora de chutar, sorteia se erra (`errorChance`) e quanto (`angleErrorDegrees`, `powerError`), mostra a mira e solta.

**Custo e tempo.** O `AiBrain` trabalha em corrotina com um orçamento de 4 ms por quadro (`FrameBudgetMs`, medido com `Stopwatch`): passou disso, espera o quadro seguinte. O goleiro e o tiro de meta testam uma posição por quadro. Cada mudança de estado da partida invalida a conta em andamento (`StateVersion`), para a IA nunca jogar uma decisão velha.

## HUD, telas e arte

Toda a interface é uGUI montada por código, de dois jeitos: as telas mais antigas (Abertura, Menu, Opções, Seleção, Resultado, Pausa, HUD da partida) são geradas no editor pelo **Criar telas** (`ScreensBuilder`) e ficam salvas nas cenas; as mais novas (Meu time, Loja, Estatísticas, Configurações, Torneio, idade, diálogos) se montam sozinhas quando abrem. Por isso, mudou uma tela gerada, tem que rodar o Criar telas de novo.

**HUD da partida (pasta Hud):**

- **MatchHud** (747 linhas + `MatchHud.Tutorial.cs`): faixas por jogador na tela em pé (contra a IA, informações em cima e botões embaixo; dois jogadores, uma faixa girada para cada um), painel lateral na tela deitada, mensagens no meio do campo, placar da disputa de pênaltis e o balão do tutorial. Lê tudo do `MatchController` a cada quadro.
- **TurnHighlight**: o anel girando atrás dos botões que podem jogar, da bola no tiro de meta e do goleiro no Pro Gol!.
- **GoalFireworks** e **TextWave**: fogos nas pontas da faixa de mensagem e o GOOOOOL! ondulando nas cores do time.

**Peças comuns das telas:** `ButtonSkin` (botões 9-slice da folha de UI em várias cores, guardados em `Resources/BotoesUI`), `ButtonSkinText`, `ButtonClickSound`, `GameFont` (a primeira fonte de `Resources/Fontes`), `TextFit` (diminui a fonte até 60% quando o texto não cabe), `MenuNavigator`/`MenuEntry` e `OverlayUi`.

**Arte da mesa e das peças (pasta Art).** A arte é desenhada em cinza e tingida no jogo, e cada item vendável é um ScriptableObject em Resources:

| Item | Script | Onde ficam | Como entra no jogo |
| --- | --- | --- | --- |
| Mesa | TableTheme | Resources/Mesas | `TableArt` monta placas e traves (a mesa clássica vem do Aplicar artes) e acerta a colisão das paredes na borda de dentro das placas. |
| Feltro | FeltTheme + FeltArt | Resources/Feltros | Padrão (liso, listrado, meio a meio) com os tiles brancos da folha, pintados com as cores do item. |
| Camisa | ShirtPattern + ShirtArt | Resources/PadroesCamisa | `PieceArt` empilha as camadas (sombra, corpo, padrão, contorno, sombra interna, brilho); corpo na cor principal, padrão na segunda cor. O desenho nunca gira. |
| Bola | BallTheme | Resources/Bolas | `BallVisual` troca os quadros, tingidos com a cor da bola. |
| Cores do Meu time | TeamColorPalette | Resources/CoresDoTime | Setas do Meu time; o save guarda a cor, não a posição. |

**Venue** junta mesa, feltro, cor da trave e bola numa `VenueSetup`: a de casa vem do Meu time, a de visitante é sorteada entre os itens liberados, com mesa e feltro diferentes dos de casa.

**Prévias:** `TablePreview` desenha a mesa em miniatura no Meu time e na Loja; os Inspectors de camisa e de time mostram a prévia no editor.

## Save e dados

Tudo o que o jogo guarda está num único objeto, **SaveData** (versão 12), gravado em JSON (`JsonUtility`) num slot do backend do Progression Save do framework (`progol.principal`, pasta ProGol). A gravação é segura: o framework escreve sem corromper se o aparelho desligar no meio.

**O que tem no SaveData:**

| Parte | Conteúdo | Vai para a nuvem |
| --- | --- | --- |
| settings | volumes, idioma, nível da IA, torneio e liga escolhidos, turnos, joystick | não |
| options | últimas opções da partida | não |
| players\[2\] | último time, uniforme e esquemas de cada jogador | não |
| profile | Meu time (nome, sigla, dois uniformes, mesa de casa, bola) e as moedas | sim |
| stats | total e por nível (4), última partida, partida em andamento, moedas ganhas e gastas | sim |
| unlocked | itens comprados ("camisa:id", "bola:id", "mesa:id", "feltro:id") | sim |
| tournament, league | o torneio e a liga em andamento (`TournamentRun`) | sim |
| tutorial | lições feitas, bônus pago, se já perguntou | sim |
| finance | totais por origem e as 30 últimas movimentações | sim |
| achievements, weekly | conquistas guardadas e os pontos da semana | sim |
| ads | ano de nascimento, anúncios removidos, contagem do intersticial, patrocínios do dia | não |
| cloud | identificador e data do último envio, resumo do que foi sincronizado | controle |

**GameSave** (estático) faz o resto:

- `Load` roda antes da primeira cena: lê, converte (`Upgrade`), aplica as escolhas na `MatchSession` e conta como abandonada a partida que ficou no meio quando o jogo fechou.
- `Save(motivo)` junta o estado da sessão (nível, opções, escolhas) e grava; `WriteOnly` grava sem juntar (o tutorial usa, para não virar a última escolha). Toda gravação avisa o Play Games que o save mudou. Há 27 chamadas de gravação espalhadas pelo jogo.
- `CloudJson`, `CloudHash`, `ParseCloud` e `ApplyCloud` preparam e aplicam o save da nuvem, deixando de fora o que é só do aparelho.

**Versões e conversão.** `Upgrade` completa campos que faltam e converte saves antigos: v2 marca de partida em andamento, v4 moedas ganhas e gastas, v6 nível Pro, v8 cor reserva virou Uniforme 2, v9 Financeiro, v10 volumes no lugar da Música liga/desliga. Os campos antigos continuam na classe só para essa conversão (`music`, `hasReserve`, `reserve`, `reserveReplacesSecondary`, `secondary`, `discPattern` e `keeperPattern` numéricos).

**Serviços em cima do save** (todos estáticos): `PlayerProfile` (Meu time), `PlayerStats` (começo, fim e abandono de partida, Dobrar moedas), `Shop` (saldo, compras, recompensa por nível), `Finance` (ganhos, gastos e extrato), `Tutorial` e `Tournament`. Eles mexem em `GameSave.Data` direto; quem grava é quem chama.

## Torneio, liga e ranking semanal

Torneio e liga são o mesmo sistema, a classe estática **Tournament** (786 linhas), com dois saves separados: `SaveData.tournament` e `SaveData.league`. A propriedade `Tournament.LeagueMode` diz com qual dos dois o Menu, a partida e o Resultado estão mexendo, e por isso dá para ter um de cada em andamento.

**Configuração.** Cada competição é um `TournamentConfig` em `Resources/Torneios` (copa8, copa16, copa32, mundial16, liga10, liga20): formato (Mata-mata, Copa com grupos, Liga), número de times, tamanho do grupo e classificados, turnos, prêmios por colocação, multiplicadores de 2 turnos, peso no ranking, placares sorteados (`SimulatedScores`, asset `Placares`), prorrogação e troféus. O `TournamentConfigEditor` mostra só os campos que valem para o formato.

**Ciclo de uma competição:**

1. `StartNew` guarda opções, nível e turnos, sorteia os times do catálogo (`slots`, o jogador é o -1) e monta os jogos: a chave do mata-mata, ou os grupos e a liga por rodízio (método do círculo, `RoundRobin`, com o mando invertido no 2º turno).
2. `PrepareMatch` prepara a sessão para o próximo jogo do jogador: opções do torneio, Meu time embaixo, adversário em cima com esquema e camisas sorteados, mesa do mandante.
3. Ao fim da partida, `RecordPlayerMatch` grava o jogo, sorteia os outros da fase ou da rodada (`Simulate`), avança (`AdvanceGroups`, `BuildKnockoutFromGroups`, `AdvanceKnockout`) e, se o jogador terminou, chama `Finish`.
4. `Finish` calcula colocação e prêmio (`PrizeFor`), atualiza estatísticas, Financeiro, conquistas e o ranking semanal. `DoublePrize` paga de novo depois do anúncio.
5. Sair no meio: `Forfeit` (no torneio é o fim dele; na liga só aquele jogo, por W.O.). `ResolveLeftover` trata a partida que ficou aberta quando o jogo fechou.

**Tabela** (`Table`): calculada na hora a partir dos jogos, com desempate por pontos, saldo, gols pró, confronto direto e um sorteio fixo pela semente da competição. Os jogos que o jogador não joga são sorteados só pelo peso de cada placar, sem levar em conta qual time é mais forte.

**Tela** (`TournamentScreen`, 769 linhas, montada em código): pergunta Continuar/Novo, chave com uma aba por fase, aba Grupos na copa, abas Tabela e Rodadas na liga, e a página final com troféu, colocação e moedas.

**Ranking semanal** (`PlayGames/WeeklyRanking.cs`): cada competição terminada vale colocação × fator do nível (Fácil 0,5, Médio 1, Difícil 1,5, Pro 2) × peso da competição, mais o saldo de gols (até +3 por partida, nunca negativo) e +10 por título logo depois de outro. Colocação no mata-mata: 1º 100, 2º 60, 3º 40, 4º 30, eliminado nas quartas ou depois 15, antes 8; na liga, 100/60/40/30, até a metade da tabela 15, abaixo 8; caiu nos grupos, 5. A semana soma os 10 melhores e vira de sábado para domingo no horário do Pacífico, como o ranking semanal do Google, então a pontuação enviada nunca diminui na semana.

## 9. Economia, loja e estatísticas

Tudo aqui é estático e mexe direto em `GameSave.Data`. A regra é "quem chama grava": Finance e AddCoins não gravam sozinhos.

| Script | O que faz |
| --- | --- |
| Shop.cs | Tabela de moedas por nível (Fácil 5/3/1, Médio 10/5/2, Difícil 12/6/3, Pro 15/8/3). `Buy(key, price, itemName)`: debita via Finance.Spend, conta itemsBought, põe a chave ("camisa:", "bola:", "mesa:", "feltro:" + id) em `unlocked`, grava, manda o evento spend\_virtual\_currency e checa conquistas. `PriceOf` procura o item nos quatro catálogos. |
| Finance.cs | `Earn(FinanceSource)` e `Spend`; extrato com as 30 últimas linhas em SaveData.finance, descrição gravada no idioma da hora. |
| Sponsorship.cs | "Vender patrocínio": anúncio com prêmio paga 5 moedas, até 3 por dia. |
| PlayerStats.cs | BeginMatch (marca partida em andamento = abandono se o app fechar), Abandon, Finish (paga e conta), DoubleCoins (anúncio do Resultado), ResetAll. Sequência recorde a partir de 2 vitórias. |
| PlayerProfile.cs | Meu time: nome, sigla, uniformes 1 e 2, mesa de casa. `Uniform(index)`, `CleanName`, `CleanAbbreviation`. |

Telas (todas montadas em runtime): ShopScreen (abas Botão, Goleiro, Bola, Mesa, Feltro; cartões e confirmação), ProfileScreen (abas Time, Mesa, Financeiro, Carreira; Carreira abre Estatísticas, Pontos da semana e Play Games), StatsScreen (Geral, Fácil, Médio, Difícil, Pro, Última partida) e ResultScreen (página Premiação com "Dobrar moedas"; o intersticial vem depois de sair dela).

Itens (ShirtPattern, BallTheme, TableTheme, FeltTheme) são assets em Resources com `All()`, preço e `unlockedByDefault`. Um item está liberado se vem liberado ou foi comprado.

## 10. Idiomas

Sistema próprio, sem o pacote Localization da Unity.

| Script | O que faz |
| --- | --- |
| Txt.cs | `Get(id)`, `Format(id, args)`, `IdOf(texto)`, evento `Changed`. O id é o próprio texto em português. Lê Resources/Idiomas/textos.json (uma entrada por linha, campos id, pt, en, es; espanhol vazio cai no português). Idioma vazio no save = Automático (aparelho em PT vira pt, senão en). |
| LocalizedText.cs | Componente para texto fixo; troca sozinho quando o idioma muda. `Attach(Text)` acha o id mesmo se o texto já estiver traduzido. |
| TextFit.cs | `Shrink`: diminui a fonte até 60% quando o texto não cabe. |
| Editor: TextsWindow / TextsTool | Janela Idiomas/Textos (abas Textos, Itens, Times; busca; filtros "falta inglês/espanhol", "longos") e os menus Gravar textos que faltam e Recarregar textos. |

Regra para texto novo: fixo numa tela runtime usa `LocalizedText.Attach`; texto que muda usa `Txt.Get`/`Format`. Nomes de itens e times passam por `Txt.Get(nome)`; Meu time nunca é traduzido.

Ponto fraco conhecido: como o id é o texto em português, corrigir uma vírgula no PT quebra a ligação com EN/ES (precisa regravar a entrada). Ver Pontos de atenção.

## 11. Serviços Android (anúncios, compras, Play Games, Firebase)

O jogo fala só com fachadas estáticas no assembly FutebolDeBotao. As implementações reais ficam em Assets/\_Game/Lojas (Assembly-CSharp, sem asmdef), atrás de símbolos de compilação (Android: PROGOL\_ADMOB, PROGOL\_FIREBASE, PROGOL\_PLAYGAMES), e se registram sozinhas com `RuntimeInitializeOnLoadMethod`. Sem o SDK (itch, editor) o jogo segue funcionando.

| Fachada | Implementação | Como liga |
| --- | --- | --- |
| Ads (Monetization) | AdMobAds.cs; no editor/dev, SimulatedAds (anúncios falsos na tela, SimulatedBanner) | `Ads.Factory` antes da 1ª cena. Intersticial a cada 2 saídas do Resultado (`InterstitialEvery`), com prêmio sob pedido, banner por "donos" (Menu, Resultado) e BannerSpace para empurrar a UI. |
| AgeGate / AgeScreen | – | Ano de nascimento na 1ª abertura; decide criança (child-directed, G, não personalizado), adolescente ou adulto (UMP). |
| Purchases | UnityIapStore.cs (Unity IAP 5) | `Purchases.Register`. Um produto não consumível (remover anúncios): BuyRemoveAds, Restore, Retry, Grant. |
| PlayGames | GooglePlayGamesBackend.cs | `PlayGames.Register`. Login, save na nuvem (envio no máximo a cada 20 s, nova tentativa em 90 s, pergunta ao jogador quando nuvem e aparelho divergem), Achievements (21 conquistas, guardadas no save e reenviadas no login), WeeklyRanking (pontos da semana, semana no fuso do Pacífico). Ids em GPGSIds. |
| GameAnalytics (Analytics) | FirebaseServices.cs | `GameAnalytics.Sink`. Eventos match\_start, match\_end, match\_abandon e Log genérico; Crashlytics junto. |

O desenho com interface + registro é bom: mantém o jogo compilando sem SDKs e permite testar no editor.

## 12. Ferramentas de editor

Dois assemblies: FutebolDeBotao.Editor (Scripts/Editor) e FutebolDeBotao.Editor.Framework (Scripts/Editor/Framework, o que depende do framework da Immersive). Tudo no menu "Futebol de Botão".

| Menu / ferramenta | Arquivo | Para que serve |
| --- | --- | --- |
| Criar telas | ScreensBuilder (753 linhas) | Gera na cena as telas fixas (Menu, Seleção, HUD...). Mudou tela gerada, precisa rodar de novo. |
| Aplicar artes | ArtBuilder (854) | Recorta folhas e liga sprites em mesa, botões, bola, goleiro. |
| Criar times / jogadores / áudio / câmera do framework | TeamsBuilder, PlayersBuilder, AudioBuilder (+ ChiptuneSounds, sons provisórios gerados), FrameworkCameraBuilder | Criam assets e objetos do framework. |
| Criar cena da partida | PrototypeSceneBuilder | Monta a cena de partida. |
| Aplicar ícone do app | IconBuilder | Ícones do Android. |
| Idiomas/... | TextsWindow, TextsTool | Ver seção 10. |
| Teste/... | MatchCheats, ShopCheats | Encerrar tempo, Empatar placar, W.O., Dar 100 moedas, Zerar moedas, Zerar compras (só em Play). |
| Inspectors | ShirtPatternEditor, TeamDataEditor (+ TeamUniformConverter, ShirtPatternIdDrawer), TournamentConfigEditor, GoalColorNameDrawer | Prévias e listas suspensas nos assets. |
| Importação | ButtonSpritesImport | Ajusta a importação das folhas de botões. |

No jogo, só para o editor: ScreenshotKey (F12 grava em Capturas) e AiLog (CSV por partida em Logs/IA).

## 13. Pontos de atenção (para discutir)

Nada aqui foi mudado. Prioridade: Alta = vale fazer antes de crescer mais; Média = quando mexer na área; Baixa = limpeza. Esforço: P (horas), M (um dia), G (vários dias).

### Organização e refatoração

| # | Onde | Problema | Sugestão | Prior. / esforço |
| --- | --- | --- | --- | --- |
| 1 | MatchController.cs (1918 linhas) | Faz tudo: regras, bola parada, pênalti escondido, disputa, tutorial, pintura dos times, stats/analytics/torneio. Difícil de mexer sem quebrar outra parte. | Dividir em PenaltyFlow (pênalti + disputa), SetPieces (tiro livre, tiro de meta, posicionamento), TeamPainter e MatchRecorder (stats, conquistas, analytics, torneio). | Alta / G |
| 2 | CreateText/CreateButton em \~11 arquivos (ScreensBuilder, MatchHud, telas runtime) | Mesmo código de UI copiado; mudar estilo exige mexer em todos. | Uma fábrica única (UiKit) com fonte, TextFit, LocalizedText e som de clique. | Alta / M |
| 3 | Telas geradas no editor x telas runtime | Duas formas de montar tela; as geradas exigem rodar Criar telas. | Escolher uma (runtime para tudo, ou prefabs). | Média / G |
| 4 | Tournament.cs (786) | Mistura chave, avanço, prêmios, stats, conquistas; o ranking semanal está em PlayGames/. | Separar por formato (mata-mata, grupos, liga) e um TournamentRewards. | Média / M |
| 5 | SaveData.Upgrade() | Função longa que cresce a cada versão; campos legados misturados. | Lista de migrações v→v+1 e bloco Legacy; remover campos velhos só com cuidado (saves antigos da nuvem). | Média / M |
| 6 | Serviços estáticos (Finance, Shop, PlayerStats, Tournament) | "Quem chama grava": fácil esquecer de gravar, ou gravar 3 vezes seguidas no fim da partida (27 chamadas a Save). | Marcar "sujo" e gravar uma vez no fim do quadro/tela (`GameSave.Commit`). | Média / P |
| 7 | Ternário Opponent(side) em 12 lugares | Duplicação. | Extensão `TeamSide.Opponent()`. | Baixa / P |
| 8 | Sem testes automáticos | test-framework instalado, nenhum teste. Regras (save, torneio, Finance, Txt) quebram sem ninguém ver. | Testes EditMode para save/migração, tabela do torneio e economia. | Média / M |
| 9 | Idiomas: id = texto PT | Corrigir o PT solta EN/ES. | Na janela de textos, ao editar o PT, oferecer "renomear id" que leva as traduções junto. | Baixa / P |

### Desempenho (celular)

| # | Onde | Problema | Sugestão | Prior. / esforço |
| --- | --- | --- | --- | --- |
| 10 | MatchHud.Update | Reescreve todos os textos todo quadro (strings novas = lixo para o GC) e resolve a câmera de todos os canvases. | Atualizar só quando o valor muda; câmera só na troca de cena. | Alta / P |
| 11 | Disc.Radius / Ball.Radius | GetComponent a cada leitura, usado em laços da IA e de posicionamento. | Guardar o collider no Awake. | Média / P |
| 12 | OnCollisionEnter2D de Disc/Ball | 3-4 GetComponent por batida. | TryGetComponent ou cache. | Baixa / P |
| 13 | AiLog / AiBrain | Textos de log (StringBuilder, GoalsLine, Rejected) montados também no celular, onde ninguém lê. | Pôr dentro de `#if UNITY_EDITOR` ou atrás de um "log ligado". | Média / P |
| 14 | Debug.Log em build (Falta, Play Games, jogadores) | Custo pequeno, mas polui o logcat. | Usar o Logging do framework ou `[Conditional]`. | Baixa / P |
| 15 | Tournament.Table | Recalcula a tabela inteira a cada chamada; confronto direto percorre todos os jogos dentro do sort. Liga de 20 = 380 jogos. | Cachear por rodada. | Baixa / P |

### Limpeza

| # | Onde | Problema | Sugestão | Prior. / esforço |
| --- | --- | --- | --- | --- |
| 16 | MatchController 1852-1916 | HUD provisório OnGUI ("R: reiniciar", "Vai chutar"), código morto. | Remover. | Baixa / P |
| 17 | Comentários | Resumos órfãos (MatchController 1462, 1751), "Vai chutar" em MatchState e IA, resumos velhos (SaveData, ProfileScreen "três abas", MenuScreen sem Liga/Tutorial). | Atualizar comentários. | Baixa / P |
| 18 | MatchSession.ResetStatics | Não zera TournamentId, LeagueId, LeagueLegs e aparelhos dos jogadores (só afeta o editor sem domain reload). | Incluir. | Baixa / P |
| 19 | Packages/manifest.json | Pacotes que o jogo não usa: ai.navigation, microsoft.gdk + gdk.tools, multiplayer.center, xr.legacyinputhelpers, timeline, provavelmente collab-proxy. | Tirar (menos tempo de compilação/import). Conferir antes no projeto do usuário. | Baixa / P |
| 20 | Tournament (design) | Jogos simulados ignoram a força dos times. | Ideia: nota por time; decide se é desejado. | Discutir |

O que está bom e vale manter: fachadas com registro para SDKs (seção 11), texturas geradas em cache estático, IA em assets por nível, save com versão e conversão.
