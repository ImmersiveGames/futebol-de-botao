# Futebol de Botão

Jogo de futebol de botão em Unity, 2D em pixel art, com campo vertical. Primeiro no itch.io (WebGL), depois no Android.

- GDD: https://claude.ai/code/artifact/5e3edd5e-57b7-4107-a1ad-6fe48fe23353
- Plano de implementação: https://claude.ai/code/artifact/7ce1e1de-a460-48b8-8689-bf02c112ac6d
- Framework: [Immersive Framework](https://github.com/ImmersiveGames/com.immersive.framework)

## Requisitos

- Unity **6000.5.0f1** ou mais nova da linha 6000.5 (exigência do Immersive Framework)
- Git e [Git LFS](https://git-lfs.com) (imagens e áudio vão pelo LFS)

## Primeira abertura

1. Clone o repositório e rode `git lfs install` uma vez na máquina.
2. No Unity Hub, use **Add > Add project from disk** e escolha a pasta clonada. A Unity gera `ProjectSettings/` e baixa os pacotes do `Packages/manifest.json` (o framework vem do OpenUPM).
3. Se a Unity perguntar sobre o novo Input System, aceite e deixe reiniciar. Confira em **Project Settings > Player > Active Input Handling** que está em `Input System Package (New)` ou `Both`.
4. Faça o commit da pasta `ProjectSettings/` e dos `.meta` gerados.

> Foundation e Logging vêm direto do GitHub (fixados por commit no `manifest.json`), porque as versões 0.2.2 e 0.2.3 que o framework 1.1.0-preview.5 pede ainda não têm release publicada. Quando forem publicadas no OpenUPM, dá para voltar para versões normais.

## Cena da partida

1. Menu **Futebol de Botão > Criar cena da partida**. Isso cria:
   - `Assets/_Game/Scenes/Partida.unity` com campo vertical, paredes, gols, goleiros, 5 botões azuis, 5 vermelhos e a bola (sem Main Camera quando a câmera do framework já existe);
   - `Assets/_Game/Data/PhysicsTuning.asset` com todos os números de física;
   - `Assets/_Game/Data/OpcoesDaPartida.asset` (tempo, toques, 5 ou 3 botões, goleiro, gol após parede, ajuda de mira) e as formações `Formacao 5` e `Formacao 3`;
   - sprites placeholder em `Assets/_Game/Art/Placeholder/`.
2. Rode o menu de novo sempre que o builder mudar (ex.: área e marca do pênalti) e, **depois dele, sempre rode também "Criar telas"** (abaixo), que liga a partida ao framework. Dê Play. Clique num botão do time da vez, arraste para trás e solte. Botão direito cancela, `R` reinicia a partida, `Esc` pausa.
3. Ajuste o `PhysicsTuning` até o peteleco ficar gostoso. Os valores de botão e bola são aplicados quando a cena começa.

Para testar o goleiro sozinho, marque `Track Ball For Testing` no componente `Goalkeeper`.

### Como a partida funciona hoje (fase 2, em andamento)

- Saída de um time sorteado. Cada vez tem até N toques (`Touches Per Turn`), 30 s de mira por toque. O relógio só corre durante a mira.
- **Perda da vez:** errar a bola, último toque na bola ser do adversário (goleiro conta), acabar os toques, ou tempo de mira esgotado.
- **Falta (tiro livre):** se o botão do peteleco, ou um companheiro empurrado por ele, acertar um adversário antes de alguém tocar a bola, a jogada para (opção `Fouls`; desligada, bater em adversário é só física). A bola fica onde o botão atingido levou a falta e ele vira o batedor, posicionado em volta da bola: arraste (ou setas/A-D) para girá-lo 360°, `Espaço`/"Pronto" encerra (30 s, o mesmo tempo do pênalti). Quem fez a falta volta para o lugar dele na formação, e os outros botões que estavam perto da bola vão para o lugar livre mais perto, fora da roda em que o batedor gira (nunca um em cima do outro). O batedor não entra em cima de outro botão, do goleiro ou fora do campo. Depois, no campo de ataque entra no "Vai chutar"; no de defesa vira mira normal, e só o batedor pode jogar.
- **Pênalti:** falta do defensor dentro da própria área (4 x 1,6 na frente do gol).
- **Cartões:** falta longe do lance (botão atingido a mais de `Far Foul Distance`, padrão 3,5, da bola no começo da jogada) dá cartão para o jogador, não para o botão. A 1ª é amarelo; a partir da 2ª, cada uma é vermelho e o botão que fez a falta sai até o fim da partida (fica pelo menos `Min Discs After Red`). O HUD mostra os cartões ao lado do nome no placar. Bola na marca (meio do campo do defensor), quem sofreu bate, os outros atacantes voltam para a formação e os defensores vão para a linha do meio-campo. Primeiro o atacante posiciona o batedor num arco atrás da bola (arrastar ou setas, 30 s, `Espaço` encerra), depois o defensor ajusta o goleiro, depois sai o chute.
- **Gol:** só conta quando a bola passa inteira da linha de fundo (`FieldLayout.BallFullyInGoal`); depois ela freia forte e fica dentro da rede.
- **Gol só vale com "Vai chutar"** (e, por padrão, sem tocar a parede antes; opção `Goal After Wall Is Valid`). Gol contra (último toque de um botão do defensor) vale sempre. Sem goleiro não existe "Vai chutar": entrou, é gol. Sem goleiro o gol também fica menor: duas traves de parede fecham a boca de 2,4 para `Goal Width Without Keeper` (padrão 1,4), criadas na hora pelo `MatchController`.
- **Tiro de meta:** gol anulado vira tiro de meta. Todos os botões dos dois times voltam para a formação inicial, os goleiros vão para o centro e a bola fica na frente do goleiro de quem bate. O jogador arrasta direto na bola (força em `PhysicsTuning > Ball Kick Max Impulse`). Conta 1 toque. Também é **bola do goleiro** (mesmo tiro de meta, para o time dele, `KeeperBall`) quando a jogada termina com a bola parada na área em volta do goleiro: a caixa dele mais 0,3 para cada lado e 0,4 para a frente, e o espaço atrás dele até a linha do gol.
- **Bola nos botões:** a bola rebate nos botões parados como numa parede, com o quique do `PhysicsTuning`, e o botão não sai do lugar (`BallRebound`). Só o botão do peteleco empurra a bola; botão em movimento segue a física normal. A simulação da IA usa a mesma regra.
- **Vai chutar** (botão no HUD ou tecla `V`): aparece quando a bola está no campo de ataque do time da vez. O defensor tem 5 s para arrastar o goleiro para os lados (ou setas/A-D; `Espaço` encerra antes). Depois o atacante chuta com qualquer botão, e a vez passa ao adversário.
- Gol: quem levou dá a saída.
- Fim do tempo: a jogada em andamento termina, o placar final fica na tela por 2,5 s e o jogo vai para a tela de Resultado.

## Telas (fase 2)

Menu **Futebol de Botão > Criar telas** (rode depois de "Criar cena da partida"). Ele cria ou recria:

- as Routes `Abertura`, `Menu` e `Resultado` em `Assets/_Game/App/` e as cenas `Abertura.unity`, `Menu.unity` e `Resultado.unity` (uGUI, visual provisório);
- na cena `Partida.unity`: a `Activity Content Contribution` da Activity **Jogo** no objeto `Partida (sistemas)`, o objeto `Framework (telas)` com os triggers do framework (Route Resultado, Route Menu, Pause, Activity Restart) e o Canvas `Tela de pausa` com o `Unity Pause Surface Adapter` e o objeto `HUD da partida` (`MatchHud`);
- **Startup Route = Abertura** no `GameApplicationAsset`, e as 5 cenas na lista da build.

Depois commite os assets e cenas gerados (com os `.meta`) e o `ProjectSettings/EditorBuildSettings.asset`.

Fluxo:

```text
Abertura --(qualquer tecla, clique ou toque)--> Menu --Iniciar--> Partida --fim do tempo--> Resultado
                                                 ^                  | Pausa: Continuar / Reiniciar / Sair para o menu
                                                 +------ Menu ------+--------- Jogar de novo --> Partida
```

- **Abertura:** não avança sozinha; mostra "Aperte qualquer botão ou toque na tela".
- **Menu:** "Iniciar" e "Opções da partida" (duração, botões por time, goleiro, toques, gol após parede, ajuda de mira, faltas). Opções de sim ou não aparecem como Ligado/Desligado. As opções ficam numa cópia em memória (`MatchSession`); o `OpcoesDaPartida.asset` não muda. Salvar entre sessões fica para a fase 4.
- **Partida:** começa quando a Activity **Jogo** entra (`IActivityContentLifecycleReceiver`), não mais no `Start()`. `R` e "Reiniciar partida" usam o `ActivityRestartTrigger` do framework (Reset, Clear e Reenter da Activity).
- **Pausa:** botão "Pausa" no HUD e `Esc` chamam o `PauseRequestTrigger`. O framework põe `Time.timeScale = 0` (física, relógio e timer de mira param) e o adaptador mostra a tela de pausa. A mira e o goleiro ignoram o ponteiro na pausa. Com o Jogador 1 na sessão, o `Esc` é dele (`PlayerPauseInput`, mapa Global).
- **Resultado:** placar final e vencedor, com "Jogar de novo" (Route Partida) e "Menu".
- **HUD (`MatchHud`, uGUI montado em código):** com a tela em pé (celular, versão principal) usa os 4 cantos ao lado dos gols, 2 por jogador; os de cima ficam girados 180° para o jogador de cima. Canto esquerdo: placar, relógio e Pausa. Canto direito: de quem é a vez, toques, timer da jogada (vermelho nos últimos 5 s) e o botão de ação ("Vai chutar" ou "Pronto"). As mensagens (falta, gol...) aparecem no meio do campo, uma virada para cada jogador. Com a tela deitada (PC) fica um painel provisório à esquerda. O `TurnHighlight` faz brilhar os botões que podem jogar (mais forte no que está sendo mirado), a bola no tiro de meta e o goleiro no "Vai chutar". Toques no HUD não viram mira nem movem o goleiro (`UiPointer`).

## Jogadores (Actors do framework)

Plano: [Plano dos Actors](https://claude.ai/code/artifact/e05b681a-5319-4b4e-a983-a2c8ec3b502c). Menu **Futebol de Botão > Criar jogadores** (rode antes de "Criar telas"). Ele cria em `Assets/_Game/App/Jogadores/`:

- `Controles.inputactions`: mapa **Jogo** (Apontar, Pressionar, Cancelar, MoverGoleiro, Confirmar, VaiChutar, Reiniciar) e mapa **Global** (Pausa = Esc), esquemas "Teclado e mouse" e "Toque". Só é criado se ainda não existe; depois pode editar à vontade.
- perfis: `Tecnico` (ActorProfile), `Jogador1` (player.1, Azul, embaixo), `Jogador2` (player.2, Vermelho, em cima) e `SessaoDeJogadores` (Manager-Provisioned, Join aberto);
- prefabs: `Tecnico` (Actor: `PlayerActorRuntimeHost`, `PlayerActorDeclaration`, `PlayerGameplayInputReader`, `CoachActor`) e `Jogador` (host: `PlayerInput`, `LocalPlayerHostAuthoring`, `UnityPlayerInputGateAdapter`, `PlayerPauseInput`);
- liga a Player Session na Game Application, a participação na Activity **Jogo** (todos que entraram, sem participante também vale, exige Gameplay Ready) e o `Local Player Provisioning` na `PersistentContent` (2 jogadores no máximo).

Como funciona hoje:

- **Contra a IA:** ao escolher o nível, o Menu faz o Join do Jogador 1 (`MenuPlayers`). Na partida o Azul lê pelo `CoachActor` (leitor do framework) e o `MatchPlayers` bloqueia o Jogador 1 fora da vez dele (bloqueio de disponibilidade, IF-ADR-044). `R` vale só para quem age; na vez da IA, o Reiniciar fica na pausa.
- **2 jogadores:** o Menu tira todo mundo da sessão e a partida lê o mouse/teclado direto, como antes. Os dois times como Actors esperam a feature do framework para dois jogadores no mesmo dispositivo (o Join com um dispositivo que já é de outro jogador é recusado).
- **Partida aberta direto no editor:** o `MatchPlayers` faz um Join de teste do Jogador 1.

## IA (fase 3)

Plano: [Plano da IA](https://claude.ai/code/artifact/ba328677-b577-4234-9834-d188c402ef89). No Menu, "Iniciar" pergunta o modo: contra a IA (Fácil, Médio ou Difícil; você é o Azul, embaixo, e a IA o Vermelho) ou 2 jogadores. A escolha fica no `MatchSession`. Abrindo a cena da partida direto no editor, vale o `Ai Plays Top` do `MatchController` (objeto `Partida (sistemas)`). Na vez da IA, o HUD mostra "IA pensando...".

- `AiPlayer`: na vez da IA pega os botões mais perto da bola (`AiDifficulty.discsToTest`), gera jogadas por geometria e confere o caminho com CircleCasts (`ShotPlanner`), simula as melhores (`AiDifficulty.simulations`) numa mesa invisível (`ShotSimulator`, uma `PhysicsScene2D` própria), dá nota e escolhe com erro de ângulo e força. Mostra a mira por `aimSeconds` e solta pelo `AimController`, o mesmo caminho do jogador.
- Falta tira nota mas não é proibida; o erro de ângulo faz a IA cometer falta às vezes.
- Posicionamento: quando a melhor jogada é ruim (falta provável ou nota abaixo de `poorShotScore`), com chance `repositionChance` (Fácil ~0,2, Médio 0,5, Difícil ~0,8) a IA só anda com um botão sem tocar na bola: cobre o próprio gol, para entre a bola e o adversário mais perto ou fica atrás da bola. A simulação confere que não toca em nada. A vez passa ("Errou a bola").
- Gol contra: as melhores jogadas são testadas também com o erro de mira (para cada lado) e de força (para mais e para menos) do nível. Se alguma versão vira gol contra, a jogada fica marcada e só sai se não houver uma segura (testa até 6 procurando). Com a bola a menos de 3,5 do próprio gol, afastá-la (também para os lados) vale nota. Se toda jogada tiver risco, com chance `ownGoalDefendChance` (Fácil 0,4, Médio 0,85, Difícil 1) a IA só põe um botão bloqueando.
- Tiro livre e pênalti: a IA gira o batedor por várias posições em volta da bola (uma por quadro), simula os chutes de cada uma e fica com a melhor.
- Também anuncia o "Vai chutar". Quando você anuncia, o goleiro da IA pega os seus chutes a gol mais prováveis (`Keeper Shots To Consider` no nível: 3/6/10), simula cada um contra 9 posições do goleiro e fica onde entram menos gols (chutes mais fáceis pesam mais), com o erro do nível (`Keeper Error`). O Console mostra `[IA] Goleiro: ...`.
- Cada jogada da IA escreve no Console `[IA] ... pensou X ms (N jogadas simuladas, P passos de física)`.
- Níveis: assets `Assets/_Game/Resources/IA/Facil`, `Medio` e `Dificil` (ajuste no Inspector). Sem o asset, valem os números do plano no código (`AiDifficulty.Create`). O campo `Ai Difficulty` do `MatchController` só vale abrindo a partida direto, sem o Menu.

## Fase 1: ligar no Immersive Framework

Segue o [Getting Started do framework](https://github.com/ImmersiveGames/com.immersive.framework/blob/master/Documentation~/Guides/Getting-Started.md):

1. Em `Assets/_Game/App/`, crie pelo menu de assets do Immersive Framework um `GameApplicationAsset`, um `RouteAsset` chamado **Partida** e um `ActivityAsset` chamado **Jogo**.
2. No `GameApplicationAsset`, defina **Startup Route = Partida**. Depois atribua esse asset em **Project Settings > Immersive Framework > Active Game Application** (sem isso o boot falha com "Active Game Application is missing").
3. Na Route **Partida**, defina a cena primária `Assets/_Game/Scenes/Partida.unity` e **Startup Activity = Jogo**.
4. Crie a Persistent Content Scene por **File > New Scene > Immersive Persistent Content**, salve em `Assets/_Game/Scenes/PersistentContent.unity` e atribua em `GameApplicationAsset > Persistent Content > Content Scene`.
5. Câmera: menu **Futebol de Botão > Criar câmera do framework**. Ele cria o prefab `CameraOutputMesa` e coloca em `Camera Session > Output Prefabs`. Depois apague a **Main Camera** da cena Partida.
6. Coloque as cenas na lista da build (**File > Build Profiles > Scene List**): `PersistentContent` e `Partida`. Sem isso o framework não consegue carregar as cenas. Dá para usar a ação do inspector do `GameApplicationAsset` que adiciona as cenas ao Build Profile. Commite o `ProjectSettings/EditorBuildSettings.asset`.
7. No `GameApplicationAsset`, rode a validação. Em **Project Settings > Immersive Framework**, deixe **Play Mode Startup = Framework Startup**.
8. Crie os Build Profiles **Web** e **Android** e faça um build Web de teste para confirmar que o framework sobe no navegador.

## Estrutura

```text
Assets/_Game/
  App/        assets do framework (GameApplication, Routes, Activities)
  Art/        sprites (placeholder por enquanto)
  Data/       ScriptableObjects e materiais de física
  Scenes/     Abertura, Menu, Partida, Resultado e Persistent Content
  Scripts/
    Physics/  Disc, Ball, Goalkeeper, GoalTrigger, Wall, MotionMonitor, PhysicsTuning
    Input/    AimController (mira por arrasto), AimVisuals (ajuda de mira), GoalkeeperControl
    Match/    MatchController (máquina de estados), MatchOptions, Formation, MatchSession (dados entre telas)
    Screens/  TitleScreen, MenuScreen, ResultScreen, PauseMenu
    Editor/   PrototypeSceneBuilder (monta a cena), Framework/FrameworkCameraBuilder (câmera), Framework/ScreensBuilder (telas)
```
