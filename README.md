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

> Foundation e Logging vêm direto do GitHub (fixados por commit no `manifest.json`), porque as versões 0.2.2 e 0.2.3 que o framework 1.1.0-preview.3 pede ainda não têm release publicada. Quando forem publicadas no OpenUPM, dá para voltar para versões normais.

## Cena da partida

1. Menu **Futebol de Botão > Criar cena da partida**. Isso cria:
   - `Assets/_Game/Scenes/Partida.unity` com campo vertical, paredes, gols, goleiros, 5 botões azuis, 5 vermelhos e a bola (sem Main Camera quando a câmera do framework já existe);
   - `Assets/_Game/Data/PhysicsTuning.asset` com todos os números de física;
   - `Assets/_Game/Data/OpcoesDaPartida.asset` (tempo, toques, 5 ou 3 botões, goleiro, gol após parede, ajuda de mira) e as formações `Formacao 5` e `Formacao 3`;
   - sprites placeholder em `Assets/_Game/Art/Placeholder/`.
2. Rode o menu de novo sempre que o builder mudar (ex.: área e marca do pênalti). Dê Play. Clique num botão do time da vez, arraste para trás e solte. Botão direito cancela, `R` reinicia a partida.
3. Ajuste o `PhysicsTuning` até o peteleco ficar gostoso. Os valores de botão e bola são aplicados quando a cena começa.

Para testar o goleiro sozinho, marque `Track Ball For Testing` no componente `Goalkeeper`.

### Como a partida funciona hoje (fase 2, em andamento)

- Saída de um time sorteado. Cada vez tem até N toques (`Touches Per Turn`), 15 s de mira por toque. O relógio só corre durante a mira.
- **Perda da vez:** errar a bola, último toque na bola ser do adversário (goleiro conta), acabar os toques, ou tempo de mira esgotado.
- **Falta:** se o botão do peteleco acertar um adversário antes da bola, a jogada para. O botão atingido volta para onde levou a falta, a bola fica à frente dele virada para o gol que ele ataca, e só ele pode bater. No campo de ataque já entra no "Vai chutar".
- **Pênalti:** falta do defensor dentro da própria área (4 x 1,6 na frente do gol). Bola na marca (meio do campo do defensor), quem sofreu bate, os outros atacantes voltam para a formação e os defensores vão para a linha do meio-campo. Primeiro o atacante posiciona o batedor num arco atrás da bola (arrastar ou setas, 5 s, `Espaço` encerra), depois o defensor ajusta o goleiro, depois sai o chute.
- **Gol só vale com "Vai chutar"** (e, por padrão, sem tocar a parede antes; opção `Goal After Wall Is Valid`). Gol contra (último toque de um botão do defensor) vale sempre. Sem goleiro não existe "Vai chutar": entrou, é gol.
- **Tiro de meta:** gol anulado vira tiro de meta. A bola fica na frente do goleiro e o jogador arrasta direto na bola (força em `PhysicsTuning > Ball Kick Max Impulse`). Conta 1 toque. Também vira tiro de meta (para o time do goleiro) quando a jogada termina com a bola parada na faixa do goleiro: da linha do gol até 0,6 à frente, na largura da área.
- **Vai chutar** (botão no HUD ou tecla `V`): aparece quando a bola está no campo de ataque do time da vez. O defensor tem 5 s para arrastar o goleiro para os lados (ou setas/A-D; `Espaço` encerra antes). Depois o atacante chuta com qualquer botão, e a vez passa ao adversário.
- Gol: quem levou dá a saída.
- Fim do tempo: a jogada em andamento termina e aparece o resultado com "Jogar de novo".

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
  Scenes/     Partida e Persistent Content
  Scripts/
    Physics/  Disc, Ball, Goalkeeper, GoalTrigger, Wall, MotionMonitor, PhysicsTuning
    Input/    AimController (mira por arrasto), AimVisuals (ajuda de mira), GoalkeeperControl
    Match/    MatchController (máquina de estados), MatchOptions, Formation
    Editor/   PrototypeSceneBuilder (monta a cena), Framework/FrameworkCameraBuilder (câmera)
```
