# Pro Gol! - Documentação técnica

> Cópia exportada do Claude Doc em 23/11/2026 (rev 37). O original é o Claude Doc: https://claude.ai/artifact/HM4UgEQL9YGSowBaaYWpz9. Não editar aqui; exportar de novo quando o doc mudar.

Nov 23, 2026 · @Immersive Games

## Visão geral

O jogo tem cerca de 28 mil linhas de C# em 166 arquivos dentro de `Assets/_Game/Scripts` (assembly `FutebolDeBotao` e os dois de editor), mais 4 arquivos em `Assets/_Game/Lojas` que ligam os SDKs do Google e 5 arquivos de teste em `Assets/_Game/Tests`. Esta documentação descreve o código do commit 123f156 (23/11/2026, versão 0.1.1, código 18, a do teste aberto). A primeira versão do doc (18/11) foi escrita antes da etapa 3 da otimização, que reorganizou partida, torneio, save e telas; esta revisão já descreve a estrutura nova.

**Quatro assemblies do jogo e um de ligação:**

| Assembly | Onde | O que tem |
| --- | --- | --- |
| FutebolDeBotao | Scripts/ | Todo o jogo: partida, IA, telas, save, torneio, loja. Não conhece nenhum SDK do Google. |
| FutebolDeBotao.Editor | Scripts/Editor/ | Menus de editor (Aplicar artes, Inspectors com prévia, janela de textos, atalhos de teste). |
| FutebolDeBotao.Editor.Framework | Scripts/Editor/Framework/ | Geradores de cena e de assets do framework (Criar telas, Criar jogadores, Criar áudio, câmera). |
| FutebolDeBotao.Tests | Tests/Editor/ | Testes automáticos EditMode (Test Runner da Unity). |
| Assembly-CSharp | Lojas/ | AdMob, Firebase, Play Games e Unity IAP. Cada arquivo implementa uma interface do jogo (IAdsProvider, IStoreProvider, IPlayGamesBackend) e se registra na abertura. |

Essa separação é boa: o jogo compila e roda no editor e no WebGL sem os SDKs, usando provedores simulados.

**Pastas de Scripts:**

| Pasta | Arquivos | Linhas | Papel |
| --- | --- | --- | --- |
| Screens | 25 | 6.490 | Telas: Menu, Seleção de times, Resultado, Torneio, Meu time, Loja, Estatísticas, Configurações, navegação por teclado/controle e a fábrica UiKit. |
| Editor (+ Framework) | 21 | 4.247 | Geradores, Inspectors e janelas. |
| Ai + Ai/Goals | 15 | 3.542 | IA por objetivos, planejador de chutes e simulador de física. |
| Match | 22 | 3.112 | Regras e estado da partida (MatchController em 11 arquivos parciais), sessão entre telas (MatchSession), times e opções. |
| Art | 12 | 1.502 | Mesa, trave, feltro, camisas, bolas, Color Palette e mesa da partida (Venue). |
| Input | 12 | 1.477 | Mira, joystick, goleiro, dispositivos por jogador. |
| Hud | 5 | 1.386 | HUD da partida, fogos, texto ondulado, destaque da vez. |
| Save | 7 | 1.284 | SaveData, GameSave, SaveFlusher, perfil, estatísticas, loja, financeiro. |
| Tournament | 8 | 1.198 | Torneio e liga (5 arquivos parciais), configuração, placares sorteados. |
| Physics | 14 | 1.093 | Botão, bola, goleiro, paredes, monitor de movimento, lados do campo. |
| Tutorial | 3 | 748 | Lições, textos e o diretor dentro da partida. |
| Monetization | 7 | 554 | Anúncios, idade, compras, patrocínio. |
| PlayGames | 4 | 479 | Login, nuvem, conquistas. |
| Players | 3 | 441 | Jogadores do framework (Join/Leave, bloqueios, Actor). |
| Audio, Localization, Analytics, Dev | 3, 3, 1, 1 | 679 | Sons, idiomas, eventos do Firebase, tecla F12 de captura. |

**Pacotes:** Immersive Framework 1.1.0 (rotas, Activity, pausa, reiniciar, jogadores, Progression Save), Foundation por commit (igual à 0.2.2), Audio 0.2.3, Logging 0.2.3, Pooling 0.2.2; Unity Input System 1.19, uGUI, Cinemachine 3.1.7, Tilemap, Unity IAP 5.4.4, Test Framework 1.7 e o External Dependency Manager do Google. Os pacotes sem uso saíram do manifest em 18/11. O framework 1.2.0 (15/11) fica para depois do teste aberto: ele não corrige o Leave depois de abandonar a partida (ver Jogadores no framework).

**Cenas:** Abertura, Menu, Partida, Resultado e PersistentContent (o que fica vivo entre rotas). Os assets do framework (GameApplication, rotas, Activity Jogo, jogadores, câmera) ficam em `_Game/App`; opções, formações, física e traves em `_Game/Data`; o que é carregado em tempo de jogo fica em `_Game/Resources` (times, torneios, IA, mesas, feltros, camisas, bolas, Color Palette, idiomas).

**Padrões que se repetem no código:**

- **Classes estáticas como serviços:** MatchSession, GameSave, Tournament, Shop, Finance, PlayerStats, Txt, Ads, PlayGames. São globais e guardam estado entre cenas.
- **Classes parciais por assunto:** as classes grandes (MatchController, Tournament, MatchHud) ficam divididas em arquivos com o nome do assunto (`MatchController.Penaltis.cs`, `Tournament.Tabela.cs`).
- **ScriptableObjects como dados ajustáveis:** times, torneios, níveis da IA, opções, mesas, traves, feltros, camisas, bolas, paleta. O usuário cria itens novos sem código, com prévia no Inspector.
- **UI montada por código** pela fábrica `UiKit`: as telas do framework vêm do gerador "Criar telas"; as telas mais novas (Meu time, Loja, Configurações, Estatísticas, Torneio) se montam sozinhas em tempo de jogo.
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

**Telas por cima de tudo**, montadas por código com `OverlayUi`: Configurações, idade (AgeScreen), diálogos (MessageDialog, cujo painel cresce com o texto da mensagem) e o anúncio simulado. Enquanto uma está aberta, `OverlayUi.PauseNavigators` desliga os outros menus para a mesma tecla não mexer nos dois.

**Caminho de uma partida única:** Jogar → Partida única → Controles (só com mais de um dispositivo) → Opções → Seleção de times → `MenuPlayers` coloca os jogadores na sessão do framework → rota Partida → rota Resultado → Jogar de novo (mesma mesa) ou Menu. O torneio e a liga passam pela chave (TournamentScreen) e voltam a ela pelo Avançar do Resultado (`MatchSession.OpenTournament`). O tutorial vai dos Controles direto para a Partida.

**MatchSession guarda entre as rotas:**

- Modo (`VsAi`), nível da IA, dispositivos dos jogadores e se dividem o mesmo dispositivo.
- Opções: uma cópia em memória do asset `OpcoesDaPartida` (o asset nunca muda); o torneio e o tutorial têm a sua própria cópia.
- Times por lado (índice no catálogo ou -1 = Meu time), uniforme, esquemas de 3 e 5 botões, camisas sorteadas para a IA.
- Mesa de casa e de visitante (`VenueSetup`: mesa, feltro, cor da trave, bola), resultado e números da última partida.
- Estado do torneio na tela (`TournamentSetup`, `InTournamentMatch`, `TournamentId`, `LeagueId`, `LeagueLegs`).

**Jogadores no framework:** `MenuPlayers` faz o Join/Leave das vagas antes da partida (contra a IA só o Jogador 1; dois jogadores com dispositivos diferentes, os dois; no mesmo dispositivo, ninguém, e a partida lê os dispositivos direto). Na partida, `MatchPlayers` bloqueia quem não é da vez e, antes de sair, pede para soltar todos os bloqueios e espera até 1,5 s que eles soltem. `CoachActor` é o Actor de cada jogador e lê o mapa de comandos "Jogo".

**Contorno do Leave (20/11).** No framework 1.1.0, depois de abandonar uma partida o Leave no Menu falhava ("could not restore gameplay after releasing the consumer block") e o Join seguinte dava `RejectedDeviceAlreadyOwned`: o `UnityPlayerInputGateAdapter` guardava "mapa desligado" como o estado de antes do bloqueio. Por isso `MenuPlayers.AlignGameplayMaps` restaura o adaptador, liga o mapa de gameplay e reaplica o portão antes do Leave; se o Leave falhar, alinha de novo e tenta mais uma vez. A 1.2.0 não mexe nesse código: não tirar o contorno enquanto o framework não corrigir (avisar o Renato).

**Navegação por teclado e controle:** cada painel tem um `MenuNavigator` com a lista de `MenuEntry` (botão ou linha de opção com setas). Com dono (Jogador 1 ou 2), só o dispositivo dele navega; a moldura amarela só aparece com teclado ou controle.

## Partida

A partida inteira é uma máquina de estados dentro de **MatchController**, um componente que recebe a entrada e a saída da Activity "Jogo" do framework (`IActivityContentLifecycleReceiver`). Desde 18/11 a classe é parcial e fica dividida em 11 arquivos por assunto (2.017 linhas no total), todos na pasta `Match`:

| Arquivo | Assunto |
| --- | --- |
| MatchController.cs (415) | Estado, vez, toques, relógio, placar, mensagens e avisos que param o jogo. |
| .Inicio (228) | Activity do framework, peças, opções, gol sem goleiro, IA, tutorial e saída de bola. |
| .Movimento (155) | Peteleco, bola e botões andando, fim da jogada (`OnSettled`) e troca de vez. |
| .ProGol (101) | Aviso de chute a gol e o goleiro do defensor. |
| .Gol (152) | Bola na rede, gol contra, gol anulado e reinício. |
| .Falta (134) | Quem fez a falta, cartões e W.O. |
| .BolaParada (372) | Tiro livre e pênalti: batedor, pênalti escondido, lugar livre. |
| .TiroDeMeta (157) | Posicionar a bola e chutar no tiro de meta, e o recuo para o goleiro. |
| .Penaltis (114) | Disputa de pênaltis. |
| .Fim (91) | Resultado, estatísticas, moedas, conquistas, Analytics e torneio; atalhos do menu Teste. |
| .Tutorial (98) | Ganchos do tutorial (montar a jogada da lição, avisos para o diretor). |

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

**Como uma jogada é decidida.** O `AimController` avisa `Flicked` (botão) ou `BallKicked` (tiro de meta); o estado vira Moving. Quando o `MotionMonitor` diz que tudo parou, `OnSettled` decide em ordem: relógio zerado (fim), bola na área do goleiro (`KeeperBall.Owner`, tiro de meta; se a jogada foi do próprio time é recuo: sem toque sobrando a bola fica onde parou, com toque o goleiro chuta), ninguém tocou ("Errou!"), adversário tocou por último, chute anunciado que não entrou ("Pra fora!"), sobrou toque (volta para Aim) ou acabaram os toques.

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
| BallVisual | Desenho da bola: quadros de rolagem pela distância, 8 direções, camada de cor por baixo dos quadros (criada na hora), luz e sombra paradas, rastro quando rápida. |
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

A IA decide cada toque por objetivos numa ordem fixa e testa as jogadas numa cópia invisível da mesa, com física própria; depois chuta pelo mesmo `AimController` do jogador, então segue exatamente as mesmas regras. São 15 arquivos e cerca de 3.500 linhas em `Scripts/Ai`.

**As peças:**

| Script | Papel |
| --- | --- |
| AiPlayer (658 linhas) | Componente que a partida põe no time de cima. Na vez da IA roda corrotinas: o toque (`PlayTouch`), o goleiro no Pro Gol! do jogador (`PlaceKeeper`/`BestKeeperX`), o batedor no tiro livre e pênalti (`SetUpKicker`) e a bola do tiro de meta (`PlaceGoalKickBall`). Mostra a mira antes de soltar e aplica o erro do nível. |
| AiSituation | Fatos lidos uma vez no começo do toque: bola, toques, se há chute livre, placar, tempo. |
| AiBrain (560 linhas) | Decide: monta a ordem dos objetivos (`Plan`), simula as jogadas de cada ação, dá nota, testa o erro nas melhores e sorteia entre as melhores seguras. Lembra as jogadas de saída já usadas. |
| AiGoal + Goals/ | Os objetivos: ScoreGoal (Marcar), ClearDangerGoal (Afastar), PrepareShotGoal (Preparar), ProtectGoal (Proteger), CloseGoalGoal (Fechar o gol). Cada um tem uma conta só (`Score`). |
| AiAction | O que dá para fazer num toque (peteleco, chute na bola, posicionar, anunciar Pro Gol!), com pré-condição e as jogadas candidatas. |
| ShotPlanner (862 linhas) | O cérebro sem física: gera jogadas pela geometria (como sinuca), confere o caminho com CircleCast, sabe se há chute livre, quantos botões têm chute, as ameaças do adversário, e dá nota ao resultado. Tem o modo pênalti (forças e alvos próprios). |
| ShotSimulator (400 linhas) | A mesa invisível: copia paredes, botões, bola e goleiros numa `PhysicsScene2D` separada e simula um peteleco por até 4 s de jogo em passos de `fixedDeltaTime`. Refaz o rebote da bola e o quique das paredes; o empurrão suave das paredes fica de fora. |
| AiDifficulty | Números de cada nível (`Resources/IA`: Facil, Medio, Dificil, Pro): botões testados, simulações, escolha entre as N melhores, falta aceita, planejar o toque seguinte, chance e tamanho do erro, distância do Pro Gol!, erro e chutes considerados pelo goleiro, pausas. Sem asset, usa números do código. |
| AiProfile | Perfis Ofensivo, Equilibrado e Defensivo (`Resources/IA/Perfis`), escolhidos pelo esquema; mexem só na distância do Pro Gol! e no risco de falta. Nos 25% finais o placar puxa o perfil. |
| AiLog | Uma linha no console por toque e, só no editor, um CSV por partida em `Logs/IA` com fatos, objetivos, jogada, nota esperada e o que aconteceu. Fora do editor (AiLog.Enabled falso) os textos do log nem são montados. |

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

**UiKit** (`Screens/UiKit.cs`, desde 18/11) é a fábrica única das peças: retângulo, texto, botão, botão escondido e imagem, já com a fonte do jogo, sombra, TextFit, tradução (`LocalizedText`) e a arte dos botões. As telas chamam a UiKit pelos seus métodos de sempre (CreateText, CreateButton), então mudar o estilo é mexer num lugar só.

**HUD da partida (pasta Hud):**

- **MatchHud** (783 linhas + `MatchHud.Tutorial.cs`): faixas por jogador na tela em pé (contra a IA, informações em cima e botões embaixo; dois jogadores, uma faixa girada para cada um), painel lateral na tela deitada, mensagens no meio do campo, placar da disputa de pênaltis e o balão do tutorial. Desde 18/11 só remonta um texto quando o valor dele muda e só acerta a câmera dos canvases quando a câmera troca.
- **TurnHighlight**: o anel girando atrás dos botões que podem jogar, da bola no tiro de meta e do goleiro no Pro Gol!.
- **GoalFireworks** e **TextWave**: fogos nas pontas da faixa de mensagem e o GOOOOOL! ondulando nas cores do time.

**Peças comuns das telas:** `ButtonSkin` (botões 9-slice da folha de UI em várias cores, guardados em `Resources/BotoesUI`), `ButtonSkinText`, `ButtonClickSound`, `GameFont` (a primeira fonte de `Resources/Fontes`), `TextFit` (diminui a fonte até 60% quando o texto não cabe), `MenuNavigator`/`MenuEntry` e `OverlayUi`.

**Arte da mesa e das peças (pasta Art).** A arte é desenhada em branco ou cinza e tingida no jogo, e cada item vendável é um ScriptableObject em Resources. A mesa antiga (Field\_Sprites, madeira e moldura) saiu em 20/11; todas as mesas usam o formato novo.

| Item | Script | Onde ficam | Como entra no jogo |
| --- | --- | --- | --- |
| Mesa | TableTheme | Resources/Mesas | 6 peças de placa desenhadas para a parede de cima (canto, parede, parede e canto do fundo do gol grande e do pequeno), cada uma com camada de cor opcional; Fill opcional por fora; recuo das paredes em pixels; a trave usada; preço e miniatura. `TableArt` gira e espelha as peças nos outros lados e acerta a colisão das paredes. Mesa incompleta usa a primeira completa (`TableTheme.Complete`). |
| Trave | GoalFrameSet | Data/Traves | Esquerda e direita do gol grande e do pequeno, a peça do meio e as redes (opcionais), desenhadas para o gol de cima. A trave é pintada com a cor escolhida; a rede fica por cima da bola. |
| Feltro | FeltTheme + FeltArt | Resources/Feltros | Padrão (Liso, Listrado, Meio a meio) com um dos tiles brancos (liso, gradiente, textura), pintados com as cores do item; cor das marcações; cores de trave aceitas. Sem feltro: verde listrado. |
| Camisa | ShirtPattern + ShirtArt | Resources/PadroesCamisa | `PieceArt` empilha as camadas (sombra, corpo, padrão, contorno, sombra interna, brilho); corpo na cor principal, padrão na segunda cor. O desenho nunca gira. |
| Bola | BallTheme | Resources/Bolas | Quadros sem cor, camada de cor de cada quadro (fundo branco), luz e sombra; lista de cores aceitas. `BallVisual` monta as camadas; a cor é uma posição na paleta. |
| Paleta | ColorPalette | Resources/ColorPalette | Lista única de cores (desde 19/11): cores do Meu time, traves, camadas das placas e cor da bola. O time guarda a cor; a mesa e a bola guardam o nome, então mudar a ordem não troca a escolha de ninguém. |

**Venue** (`Art/Venue.cs`) junta mesa, feltro, cor da trave e bola com a cor dela numa `VenueSetup`: a de casa vem do Meu time, a de visitante é sorteada entre os itens liberados, com mesa e feltro diferentes dos de casa. Contra a IA a bola e a cor são sorteadas.

**Prévias:** `TablePreview` desenha a mesa em miniatura no Meu time e na Loja; os Inspectors de mesa, bola, camisa e time mostram a prévia no editor (mesa e bola aceitam vários selecionados), e as listas de cores aceitas viram um dropdown de marcar (`PaletteMaskInspector`).

## Save e dados

Tudo o que o jogo guarda está num único objeto, **SaveData** (versão 14), gravado em JSON (`JsonUtility`) num slot do backend do Progression Save do framework (`progol.principal`, pasta ProGol). A gravação é segura: o framework escreve sem corromper se o aparelho desligar no meio.

**O que tem no SaveData:**

| Parte | Conteúdo | Vai para a nuvem |
| --- | --- | --- |
| settings | volumes, idioma, nível da IA, torneio e liga escolhidos, turnos, joystick | não |
| options | últimas opções da partida | não |
| players\[2\] | último time, uniforme e esquemas de cada jogador | não |
| profile | Meu time (nome, sigla, dois uniformes, mesa de casa, cor da trave, bola e cor da bola) e as moedas | sim |
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
- `Save(motivo)` junta o estado da sessão (nível, opções, escolhas) e **marca** o save para gravar; `WriteOnly` marca sem juntar (o tutorial usa, para não virar a última escolha). Desde 18/11 nenhuma das duas grava na hora: o `SaveFlusher` (objeto escondido criado na primeira marcação) chama `Flush` no fim do quadro, quando o app vai para o fundo e quando o jogo fecha. Assim as 27 chamadas espalhadas pelo jogo viram no máximo uma gravação por quadro. Cada gravação avisa o Play Games que o save mudou.
- `CloudJson`, `CloudHash`, `ParseCloud` e `ApplyCloud` preparam e aplicam o save da nuvem, deixando de fora o que é só do aparelho.

**Versões e conversão.** `Upgrade` primeiro completa o que faltar (listas nulas, níveis, perfil sem nome ou cor) e depois roda, em ordem, cada passo da lista `Migrations` mais novo que o save. Versão nova com conversão = um passo novo no fim da lista; versões sem passo só ganharam campos com padrão.

| Passo | O que converte |
| --- | --- |
| v2 | Marca de partida em andamento. |
| v4 | Moedas ganhas e gastas (gastos pelo preço do que já foi comprado). |
| v8 | Cor reserva vira o Uniforme 2; sem reserva, as cores invertidas. |
| v9 | Financeiro: totais vindos das estatísticas. |
| v10 | Música Ligado/Desligado vira volume da música. |
| v13 | Cores da mesa passam a vir da Color Palette; o nome salvo da cor das traves acompanha. |
| v14 | Bolas em padrões: as bolas amarela e vermelha antigas viram a bola padrão nessas cores. |

Os campos antigos ficam num bloco marcado "Legado" em cada classe do save, só para essas conversões; o jogo não usa. Tirar um deles só com cuidado, porque um save antigo pode voltar pela nuvem.

**Serviços em cima do save** (todos estáticos): `PlayerProfile` (Meu time), `PlayerStats` (começo, fim e abandono de partida, Dobrar moedas), `Shop` (saldo, compras, recompensa por nível), `Finance` (ganhos, gastos e extrato), `Tutorial` e `Tournament`. Eles mexem em `GameSave.Data` direto e pedem `Save` no fim; como a gravação é uma só por quadro, pedir mais de uma vez não custa nada.

## Torneio, liga e ranking semanal

Torneio e liga são o mesmo sistema, a classe estática **Tournament**, com dois saves separados: `SaveData.tournament` e `SaveData.league`. A propriedade `Tournament.LeagueMode` diz com qual dos dois o Menu, a partida e o Resultado estão mexendo, e por isso dá para ter um de cada em andamento. Desde 18/11 a classe é parcial, em 5 arquivos na pasta `Tournament` (824 linhas): `Tournament.cs` (início, jogo do jogador, avanço, desistência), `.Tabela` (grupos e liga, classificação), `.MataMata` (chave), `.Premios` (colocação, prêmio, estatísticas e conquistas) e `.Sorteio` (times e placares sorteados). O ranking semanal também mudou para essa pasta (`WeeklyRanking.cs`).

**Configuração.** Cada competição é um `TournamentConfig` em `Resources/Torneios` (copa8, copa16, copa32, mundial16, liga10, liga20): formato (Mata-mata, Copa com grupos, Liga), número de times, tamanho do grupo e classificados, turnos, prêmios por colocação, multiplicadores de 2 turnos, peso no ranking, placares sorteados (`SimulatedScores`, asset `Placares`), prorrogação e troféus. O `TournamentConfigEditor` mostra só os campos que valem para o formato.

**Ciclo de uma competição:**

1. `StartNew` guarda opções, nível e turnos, sorteia os times do catálogo (`slots`, o jogador é o -1) e monta os jogos: a chave do mata-mata, ou os grupos e a liga por rodízio (método do círculo, `RoundRobin`, com o mando invertido no 2º turno).
2. `PrepareMatch` prepara a sessão para o próximo jogo do jogador: opções do torneio, Meu time embaixo, adversário em cima com esquema e camisas sorteados, mesa do mandante.
3. Ao fim da partida, `RecordPlayerMatch` grava o jogo, sorteia os outros da fase ou da rodada (`Simulate`), avança (`AdvanceGroups`, `BuildKnockoutFromGroups`, `AdvanceKnockout`) e, se o jogador terminou, chama `Finish`.
4. `Finish` calcula colocação e prêmio (`PrizeFor`), atualiza estatísticas, Financeiro, conquistas e o ranking semanal. `DoublePrize` paga de novo depois do anúncio.
5. Sair no meio: `Forfeit` (no torneio é o fim dele; na liga só aquele jogo, por W.O.). `ResolveLeftover` trata a partida que ficou aberta quando o jogo fechou.

**Tabela** (`Table`): calculada na hora a partir dos jogos, com desempate por pontos, saldo, gols pró, confronto direto e um sorteio fixo pela semente da competição. Os jogos que o jogador não joga são sorteados só pelo peso de cada placar, sem levar em conta qual time é mais forte.

**Tela** (`TournamentScreen`, 724 linhas, montada em código): pergunta Continuar/Novo, chave com uma aba por fase, aba Grupos na copa, abas Tabela e Rodadas na liga, e a página final com troféu, colocação e moedas.

**Ranking semanal** (`PlayGames/WeeklyRanking.cs`): cada competição terminada vale colocação × fator do nível (Fácil 0,5, Médio 1, Difícil 1,5, Pro 2) × peso da competição, mais o saldo de gols (até +3 por partida, nunca negativo) e +10 por título logo depois de outro. Colocação no mata-mata: 1º 100, 2º 60, 3º 40, 4º 30, eliminado nas quartas ou depois 15, antes 8; na liga, 100/60/40/30, até a metade da tabela 15, abaixo 8; caiu nos grupos, 5. A semana soma os 10 melhores e vira de sábado para domingo no horário do Pacífico, como o ranking semanal do Google, então a pontuação enviada nunca diminui na semana.

## Economia, loja e estatísticas

Tudo aqui é estático e mexe direto em `GameSave.Data`. Finance e AddCoins só mexem nos dados; quem chama pede o Save (a gravação acontece uma vez no fim do quadro).

| Script | O que faz |
| --- | --- |
| Shop.cs | Tabela de moedas por nível (Fácil 5/3/1, Médio 10/5/2, Difícil 12/6/3, Pro 15/8/3). `Buy(key, price, itemName)`: debita via Finance.Spend, conta itemsBought, põe a chave ("camisa:", "bola:", "mesa:", "feltro:" + id) em `unlocked`, grava, manda o evento spend\_virtual\_currency e checa conquistas. `PriceOf` procura o item nos quatro catálogos. |
| Finance.cs | `Earn(FinanceSource)` e `Spend`; extrato com as 30 últimas linhas em SaveData.finance, descrição gravada no idioma da hora. |
| Sponsorship.cs | "Vender patrocínio": anúncio com prêmio paga 5 moedas, até 3 por dia. |
| PlayerStats.cs | BeginMatch (marca partida em andamento = abandono se o app fechar), Abandon, Finish (paga e conta), DoubleCoins (anúncio do Resultado), ResetAll. Sequência recorde a partir de 2 vitórias. |
| PlayerProfile.cs | Meu time: nome, sigla, uniformes 1 e 2, mesa de casa. `Uniform(index)`, `CleanName`, `CleanAbbreviation`. |

Telas (todas montadas em runtime): ShopScreen (abas Botão, Goleiro, Bola, Mesa, Feltro; cartões e confirmação), ProfileScreen (abas Time, Mesa, Financeiro, Carreira; Carreira abre Estatísticas, Pontos da semana e Play Games), StatsScreen (Geral, Fácil, Médio, Difícil, Pro, Última partida) e ResultScreen (página Premiação com "Dobrar moedas"; o intersticial vem depois de sair dela).

Itens (ShirtPattern, BallTheme, TableTheme, FeltTheme) são assets em Resources com `All()`, preço e `unlockedByDefault`. Um item está liberado se vem liberado ou foi comprado.

## Idiomas

Sistema próprio, sem o pacote Localization da Unity.

| Script | O que faz |
| --- | --- |
| Txt.cs | `Get(id)`, `Format(id, args)`, `IdOf(texto)`, evento `Changed`. O id é o próprio texto em português. Lê Resources/Idiomas/textos.json (uma entrada por linha, campos id, pt, en, es; espanhol vazio cai no português). Idioma vazio no save = Automático (aparelho em PT vira pt, senão en). |
| LocalizedText.cs | Componente para texto fixo; troca sozinho quando o idioma muda. `Attach(Text)` acha o id mesmo se o texto já estiver traduzido. |
| TextFit.cs | `Shrink`: diminui a fonte até 60% quando o texto não cabe. |
| Editor: TextsWindow / TextsTool | Janela Idiomas/Textos (abas Textos, Itens, Times; busca; filtros "falta inglês/espanhol", "longos") e os menus Gravar textos que faltam e Recarregar textos. |

Regra para texto novo: fixo numa tela runtime usa `LocalizedText.Attach`; texto que muda usa `Txt.Get`/`Format`. Nomes de itens e times passam por `Txt.Get(nome)`; Meu time nunca é traduzido.

Ponto fraco conhecido: como o id é o texto em português, corrigir uma vírgula no PT quebra a ligação com EN/ES (precisa regravar a entrada). Ver Pontos de atenção.

## Serviços Android (anúncios, compras, Play Games, Firebase)

O jogo fala só com fachadas estáticas no assembly FutebolDeBotao. As implementações reais ficam em Assets/\_Game/Lojas (Assembly-CSharp, sem asmdef), atrás de símbolos de compilação (Android: PROGOL\_ADMOB, PROGOL\_FIREBASE, PROGOL\_PLAYGAMES), e se registram sozinhas com `RuntimeInitializeOnLoadMethod`. Sem o SDK (itch, editor) o jogo segue funcionando.

| Fachada | Implementação | Como liga |
| --- | --- | --- |
| Ads (Monetization) | AdMobAds.cs; no editor/dev, SimulatedAds (anúncios falsos na tela, SimulatedBanner) | `Ads.Factory` antes da 1ª cena. Intersticial a cada 2 saídas do Resultado (`InterstitialEvery`), com prêmio sob pedido, banner por "donos" (Menu, Resultado) e BannerSpace para empurrar a UI. |
| AgeGate / AgeScreen | – | Ano de nascimento na 1ª abertura; decide criança (child-directed, G, não personalizado), adolescente ou adulto (UMP). |
| Purchases | UnityIapStore.cs (Unity IAP 5) | `Purchases.Register`. Um produto não consumível (remover anúncios): BuyRemoveAds, Restore, Retry, Grant. |
| PlayGames | GooglePlayGamesBackend.cs | `PlayGames.Register`. Login, save na nuvem (envio no máximo a cada 20 s, nova tentativa em 90 s, pergunta ao jogador quando nuvem e aparelho divergem), Achievements (21 conquistas, guardadas no save e reenviadas no login), WeeklyRanking (pontos da semana, semana no fuso do Pacífico). Ids em GPGSIds. |
| GameAnalytics (Analytics) | FirebaseServices.cs | `GameAnalytics.Sink`. Eventos match\_start, match\_end, match\_abandon e Log genérico; Crashlytics junto. |

O desenho com interface + registro é bom: mantém o jogo compilando sem SDKs e permite testar no editor.

**Anúncios reais x de teste.** O `AdMobAds` usa os IDs de teste do Google, a não ser que o símbolo `PROGOL_ADS_REAIS` esteja ligado no Android (ligado desde 20/11, versão 0.1.0 do teste aberto). O Moto G84 está cadastrado como aparelho de teste no AdMob.

**Manifest do Android.** A biblioteca `Plugins/Android/ProGolPrivacy.androidlib` desliga o ID de publicidade (Firebase e AdMob) e declara `android.hardware.screen.portrait` como opcional: o Google Play deduzia esse recurso como obrigatório por causa da tela travada em pé e avisava que tablets ficavam de fora.

## Ferramentas de editor

Dois assemblies: FutebolDeBotao.Editor (Scripts/Editor) e FutebolDeBotao.Editor.Framework (Scripts/Editor/Framework, o que depende do framework da Immersive). Tudo no menu "Futebol de Botão".

| Menu / ferramenta | Arquivo | Para que serve |
| --- | --- | --- |
| Criar telas | ScreensBuilder (753 linhas) | Gera na cena as telas fixas (Menu, Seleção, HUD...) e o fundo de feltro das telas. Mudou tela gerada, precisa rodar de novo. |
| Aplicar artes | ArtBuilder (697) | Recorta folhas e liga sprites em botões, bola, goleiro e feltro. Acha a bola Clássica pelo id e não sobrescreve os quadros dela. |
| Criar times / jogadores / áudio / câmera do framework | TeamsBuilder, PlayersBuilder, AudioBuilder (+ ChiptuneSounds, sons provisórios gerados), FrameworkCameraBuilder | Criam assets e objetos do framework. O TeamsBuilder só cria times se o catálogo estiver vazio. |
| Criar cena da partida | PrototypeSceneBuilder | Monta a cena de partida. |
| Aplicar ícone do app | IconBuilder | Ícones do Android. |
| Idiomas/... | TextsWindow, TextsTool | Ver Idiomas. |
| Teste/... | MatchCheats, ShopCheats | Encerrar tempo, Empatar placar, W.O., Dar 100 moedas, Zerar moedas, Zerar compras (só em Play). |
| Inspectors | TableThemeEditor, BallThemeEditor, ShirtPatternEditor, TeamDataEditor (+ TeamUniformConverter, ShirtPatternIdDrawer), TournamentConfigEditor, PaletteMaskInspector, GoalColorNameDrawer | Prévias de mesa, bola, camisa e time; listas de cores como dropdown de marcar; campos do torneio conforme o formato. |
| Importação | ButtonSpritesImport | Ajusta a importação das folhas de botões. |

**Testes automáticos** (`Tests/Editor`, assembly FutebolDeBotao.Tests, 35 testes EditMode, desde 18/11): conversão de saves antigos (`SaveUpgradeTests`), tabela e prêmios do torneio (`TournamentTests`), moedas e Financeiro (`EconomyTests`) e textos dos idiomas (`TextsTests`). O `SaveSandbox` troca o save do jogo por um de mentira durante o teste, sem gravar nada no disco. Rodar em Window > General > Test Runner > EditMode.

No jogo, só para o editor: ScreenshotKey (F12 grava em Capturas) e AiLog (CSV por partida em Logs/IA).

**Documentação em HTML:** `Docs/ferramentas/gdd_html.py` gera `Docs/GDD.html` (GDD e esta documentação técnica em abas) com o template do Immersive Games Documentation Framework (repositório com.immersivegames.docs).

## Pontos de atenção (para discutir)

A lista de 18/11 virou a Fase 7 do roteiro (otimização), feita e testada no mesmo dia em três etapas: etapa 1 (7e3c86b), etapa 2 (15925ec) e etapa 3, partes A a E (944a9b3, 1b72da0, 2fedf29, e79a517, 82686ec). Prioridade: Alta = vale fazer antes de crescer mais; Média = quando mexer na área; Baixa = limpeza. Esforço: P (horas), M (um dia), G (vários dias).

### Ainda abertos

| # | Onde | Problema | Sugestão | Prior. / esforço |
| --- | --- | --- | --- | --- |
| 3 | Telas geradas no editor x telas runtime | Duas formas de montar tela; as geradas exigem rodar Criar telas. A UiKit já deixou as duas iguais por dentro. | Escolher uma (runtime para tudo, ou prefabs). | Média / G |
| 9 | Idiomas: id = texto PT | Corrigir o PT solta EN/ES. | Na janela de textos, ao editar o PT, oferecer "renomear id" que leva as traduções junto. | Baixa / P |
| 14 | Debug.Log em build (116 chamadas: falta, Play Games, jogadores, save) | Custo pequeno, mas polui o logcat. | Usar o Logging do framework ou `[Conditional]`. | Baixa / P |
| 15 | Tournament.Table | Recalcula a tabela inteira a cada chamada; confronto direto percorre todos os jogos dentro do sort. Liga de 20 = 380 jogos. | Cachear por rodada. | Baixa / P |
| 20 | Tournament (design) | Jogos simulados ignoram a força dos times. | Ideia: nota por time; decidir se é desejado. | Discutir |
| 21 | MenuPlayers.AlignGameplayMaps | Contorno do bug do Leave do framework 1.1.0 (ver Jogadores no framework). | Avisar o Renato; tirar quando o framework corrigir. | Média / P |
| 22 | Framework 1.2.0 | Saiu em 15/11; exige Composition 0.1.0-preview.3, Logging 0.2.4 e Foundation 0.2.3. | Atualizar depois do teste aberto, mantendo o contorno do item 21. | Média / M |

### Resolvidos em 18/11

| # | Ponto | Como ficou |
| --- | --- | --- |
| 1 | MatchController com 1.918 linhas fazendo tudo | Classe parcial em 11 arquivos por assunto (parte C). |
| 2 | CreateText/CreateButton copiados em \~11 arquivos | Fábrica única UiKit (parte D). |
| 4 | Tournament.cs misturando tudo | Classe parcial em 5 arquivos; WeeklyRanking na pasta Tournament (parte E). |
| 5 | SaveData.Upgrade() crescendo | Lista de passos por versão (`Migrations`) e campos em blocos Legado (parte B). |
| 6 | Várias gravações seguidas do save | Save só marca; o SaveFlusher grava uma vez por quadro (parte B). |
| 7 | Ternário Opponent(side) em 12 lugares | Extensão `TeamSide.Opponent()` (etapa 2). |
| 8 | Sem testes automáticos | 35 testes EditMode (parte A). |
| 10 | MatchHud reescrevendo textos todo quadro | Texto só muda quando o valor muda; câmera só na troca (etapa 1). |
| 11, 12 | GetComponent repetido em Disc/Ball | Raio guardado e TryGetComponent (etapa 1). |
| 13 | Textos do log da IA montados no celular | `AiLog.Enabled` só no editor (etapa 1). |
| 16, 17 | HUD OnGUI morto e comentários velhos | Removido e atualizados (etapa 2). |
| 18 | MatchSession.ResetStatics incompleto | Zera tudo (etapa 2). |
| 19 | Pacotes sem uso no manifest | Removidos (etapa 2). |

O que está bom e vale manter: fachadas com registro para SDKs (Serviços Android), texturas geradas em cache estático, IA em assets por nível, save com versão e passos de conversão, testes do save e da economia.
