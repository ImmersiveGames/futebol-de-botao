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

## Fase 1: cena de teste

1. Menu **Futebol de Botão > Criar cena de teste (fase 1)**. Isso cria:
   - `Assets/_Game/Scenes/Partida.unity` com campo vertical, paredes, gols, goleiros, 5 botões azuis, 5 vermelhos e a bola;
   - `Assets/_Game/Data/PhysicsTuning.asset` com todos os números de física;
   - sprites placeholder em `Assets/_Game/Art/Placeholder/`.
2. Dê Play na cena. Clique num botão, arraste para trás e solte. Botão direito cancela, `R` reinicia.
3. Ajuste o `PhysicsTuning` até o peteleco ficar gostoso. Os valores de botão e bola são aplicados quando a cena começa.

Para testar o goleiro sozinho, marque `Track Ball For Testing` no componente `Goalkeeper`.

## Fase 1: ligar no Immersive Framework

Segue o [Getting Started do framework](https://github.com/ImmersiveGames/com.immersive.framework/blob/master/Documentation~/Guides/Getting-Started.md):

1. Em `Assets/_Game/App/`, crie pelo menu de assets do Immersive Framework um `GameApplicationAsset`, um `RouteAsset` chamado **Partida** e um `ActivityAsset` chamado **Jogo**.
2. No `GameApplicationAsset`, defina **Startup Route = Partida**. Depois atribua esse asset em **Project Settings > Immersive Framework > Active Game Application** (sem isso o boot falha com "Active Game Application is missing").
3. Na Route **Partida**, defina a cena primária `Assets/_Game/Scenes/Partida.unity` e **Startup Activity = Jogo**.
4. Crie a Persistent Content Scene por **File > New Scene > Immersive Persistent Content**, salve em `Assets/_Game/Scenes/PersistentContent.unity` e atribua em `GameApplicationAsset > Persistent Content > Content Scene`.
5. No inspector do `GameApplicationAsset`, use a ação que adiciona a cena ao Build Profile e depois a ação de validação.
6. Crie os Build Profiles **Web** e **Android** e faça um build Web de teste para confirmar que o framework sobe no navegador.

## Estrutura

```text
Assets/_Game/
  App/        assets do framework (GameApplication, Routes, Activities)
  Art/        sprites (placeholder por enquanto)
  Data/       ScriptableObjects e materiais de física
  Scenes/     Partida e Persistent Content
  Scripts/
    Physics/  Disc, Ball, Goalkeeper, GoalTrigger, Wall, MotionMonitor, PhysicsTuning
    Input/    AimController (mira por arrasto), AimVisuals (ajuda de mira)
    Match/    PrototypeMatch (placar e gol anulado da fase 1)
    Editor/   PrototypeSceneBuilder (monta a cena de teste)
```
