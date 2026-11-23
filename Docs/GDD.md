# GDD - Futebol de Botão

> Cópia exportada do Claude Doc em 23/11/2026 (rev 262). O original é o Claude Doc: https://claude.ai/artifact/Cdzi4Hhp6EKyHJGAYzz9jQ. Não editar aqui; exportar de novo quando o doc mudar.

Nov 23, 2026 · @Immersive Games

## Visão geral

Um jogo de futebol de botão por turnos, rápido de aprender e com física satisfatória, chamado Pro Gol! (em inglês, button football), publicado no Google Play (Android) e com uma versão de navegador no itch.io.

- **Pitch:** a mesa de botão da infância, com peteleco mirado no mouse ou no dedo, partidas de 5 a 10 minutos.
- **Gênero:** esporte de mesa, estratégia por turnos com física.
- **Público:** jogadores casuais e nostálgicos do botão; partidas curtas que cabem em uma pausa.
- **Plataformas:** 1) Android pelo Google Play (versão principal, toque, em retrato; pacote com.immersivegames.progol); 2) navegador via itch.io (WebGL), sem anúncios nem compras.
- **Engine:** Unity 6000.5 com o Immersive Framework.

## Decisões em aberto

Todas as decisões-base foram fechadas em 07/10; o equilíbrio do goleiro, que era a última pendência, foi fechado em 09/11 (largura 1,2).

| Decisão | Escolha |
| --- | --- |
| Regras | Simplificadas, com opções configuráveis (ver Regras) |
| Time em campo | 3 botões + goleiro caixinha (padrão); opções de 5 botões e de jogar sem goleiro |
| Visual | 2D em pixel art, câmera top-down |
| Adversário | IA + 2 jogadores locais; online só depois, se o jogo ficar bom |
| Monetização | Anúncios + compra para remover anúncios (ver Monetização) |
| Orientação | Campo sempre na vertical; Android em retrato, PC/itch.io com o campo vertical no centro da tela |
| Times | Craques da Rua: 40 times com nomes de pessoas (ver Arte) |

## Regras e gameplay

Cada turno é um peteleco em um botão; quem fizer mais gols no tempo vence.

1. **Início:** saída de bola no centro, time sorteado começa. Na saída, quem sai pode ter botões dentro do círculo central; os botões do adversário que cairiam dentro dele ficam com o centro em cima da linha do círculo.
2. **Turno:** o jogador escolhe um botão do seu time, mira e define a força; o botão empurra a bola.
3. **Posse:** o time mantém a vez enquanto seu botão tocar a bola, até o limite de toques seguidos (1 a 3, configurável); depois a vez passa.
4. **Perda da vez:** a vez passa se o botão errar a bola, se cometer falta (item 5), ou se o último toque na bola for de um botão adversário.
5. **Chute a gol:** permitido com a bola do meio-campo adversário em diante, chutando com qualquer botão; só ali aparece o botão "Pro Gol!". Falta (opção Faltas ligada): se o botão do peteleco, ou um companheiro empurrado por ele, acertar um adversário antes de alguém tocar a bola, o adversário ganha tiro livre no local do botão atingido: a bola fica onde ele levou a falta e ele vira o batedor, posicionado em volta da bola (gira 360°, até 30 s ou "Pronto", como no pênalti). Quem fez a falta volta para o seu lugar na formação e os botões perto da bola vão para o lugar livre mais perto, sem ficar um em cima do outro. Falta longe do lance (botão atingido a mais de 3,5 da bola no começo da jogada) dá cartão para o jogador, não para o botão: a 1ª é amarelo; a partir da 2ª, cada uma é vermelho e o botão que fez a falta sai até o fim da partida. Se o time ficar sem botões, é W.O.: a partida acaba, quem foi expulso fica com 0 e o adversário ganha 3 gols a mais. Falta no goleiro: se o botão do peteleco, ou um companheiro empurrado por ele, acertar o goleiro adversário antes de alguém tocar a bola, é tiro de meta para o time do goleiro (cartão pela mesma regra da falta longe do lance). Depois a vez começa normal: se a bola estiver no campo de ataque, o "Pro Gol!" fica disponível como em qualquer jogada (não é chute automático). O atacante anuncia o chute (botão "Pro Gol!"), o defensor tem até 30 s (ou até apertar "Pronto") para deslizar o goleiro na linha do gol, e só então o atacante chuta. Contra a IA, a IA posiciona o goleiro. *Mecânica a validar no protótipo.*
6. **Área e pênalti:** cada gol tem uma área do goleiro (4 x 1,5 no campo de 7 x 11, igual ao desenho das marcações). Falta do defensor dentro da própria área é pênalti: bola na marca, no meio do campo do defensor; quem sofreu a falta bate, os outros atacantes voltam para a formação e os defensores ficam na linha do meio-campo. No pênalti, primeiro o atacante posiciona o batedor num arco atrás da bola (30 s), depois o defensor ajusta o goleiro (30 s), e só então sai o chute. Pênalti escondido (10/11, com goleiro, na falta e na disputa): os dois têm que escolher um lado (batedor a pelo menos 20° do reto; goleiro a pelo menos metade do caminho até a trave; tempo esgotado no meio = vai para o lado mais perto). Quem escolheu primeiro fica sob uma faixa preta até o outro dar Pronto; então a faixa sai e o batedor chuta. Dois jogadores: o batedor escolhe primeiro (vale a confiança). Contra a IA, ela escolhe sempre primeiro, escondida, sorteando o lado (com o jogador batendo, o goleiro dela escolhe antes).
7. **Tiro de meta:** todos os botões dos dois times voltam para a formação inicial, os goleiros vão para o centro do gol e a bola fica na frente do goleiro de quem defendeu. O jogador primeiro posiciona a bola: arrasta ela só para os lados, dentro da largura da área, sem passar por cima de botão, e aperta Pronto. Depois chuta direto na bola, com a mesma mira e barra de força; os dois passos usam o mesmo tempo de mira. Conta como 1 toque. Também é bola do goleiro, com tiro de meta para o time dele, não importa quem jogou (menos no recuo, abaixo), quando a jogada termina com a bola parada na área em volta do goleiro: a caixa dele mais 0,15 para cada lado e 0,2 para a frente (10/11; era 0,3 e 0,4), e o espaço atrás dele até a linha do gol (e, se a bola estiver entre o goleiro e o gol, até o fundo do gol, desde que não tenha entrado inteira). Fora da faixa do goleiro, a bola parada na boca do gol segue em jogo. Recuo (18/11): quando a jogada do próprio time (ele tocou por último, sem "Pro Gol!") termina com a bola nessa área do próprio goleiro, não é tiro de meta e os toques não enchem. Sem toque sobrando, a bola fica onde parou e a vez passa normal. Com toque sobrando, a bola vai para a frente do goleiro, ninguém volta para a formação, e o goleiro posiciona e chuta como no tiro de meta, gastando 1 toque. No "Pro Gol!", quando o goleiro fica pronto (Pronto do defensor, da IA ou fim do tempo) com a bola nessa área (encostada ou atrás dele), também é bola do goleiro. O goleiro nunca empurra bola nem botão: ao deslizar, para encostado no que estiver no caminho.
8. **Gol:** bola cruza a linha inteira (depois ela para dentro da rede) durante um "Pro Gol!"; reinício no centro com o time que sofreu. Gol sem "Pro Gol!" é anulado e vira tiro de meta. Gol contra só quando o chute saiu de um botão do time que defende aquele gol (a jogada era dele), mesmo que a bola tenha batido num botão adversário antes de entrar; aí vale sem "Pro Gol!". Chute do atacante que desvia num defensor e entra é gol normal do atacante, e só vale com "Pro Gol!". Sem goleiro não existe "Pro Gol!": o gol vale se a bola estava no campo de ataque de quem jogou no começo da jogada; chutado do campo de defesa é anulado e vira tiro de meta.
9. **Paredes:** não há lateral nem escanteio; o campo é cercado por paredes e a bola rebate para dentro. Por padrão, gol (inclusive gol contra) só vale se a bola não tocar as paredes laterais (esquerda e direita) depois do chute; trave, fundo e rede não contam; gol anulado vira tiro de meta para quem defendeu. Essa regra é configurável.
10. **Fim:** um tempo único, com duração escolhida antes da partida: 3, 5, 8 ou 10 minutos (padrão 5). O relógio corre só durante a mira. Empate (09/11): com a opção Prorrogação (Desligado, 50% ou 100% do tempo; padrão Desligado), joga-se a prorrogação, relógio corrido, sem gol de ouro. Continuando empatado e com a opção Pênaltis ligada (padrão Desligado; some sem goleiro), disputa de pênaltis: 5 cobranças alternadas para cada time, depois morte súbita; cada time bate no seu gol de ataque, em campo só o batedor (os botões revezam) e o goleiro adversário, um toque. Placar da disputa na metade oposta à cobrança, virado para o Jogador 1: uma linha por time com 5 casas (bolinha preta; verde = gol, vermelha = erro) e o total; na morte súbita as casas zeram quando enchem. Vencer na prorrogação ou nos pênaltis é vitória (moedas e estatísticas); o Resultado mostra o empate e os gols dos pênaltis entre parênteses embaixo de cada número.

Timer de mira de 30 s por jogada mantém o ritmo.

### Opções da partida

Antes de cada partida o jogador pode ajustar estas regras; o padrão vale para o modo rápido.

| Opção | Valores | Padrão |
| --- | --- | --- |
| Duração | 3, 5, 8 ou 10 minutos | 5 minutos |
| Botões por time | 5 ou 3 | 3 |
| Goleiro | Ligado ou Desligado | Ligado |
| Toques seguidos por turno | 1, 2 ou 3 | 3 |
| Gol após tocar a parede | Anulado ou válido | Anulado |
| Ajuda de mira | Completa, curta ou desligada | Completa |
| Faltas | Ligado ou Desligado (desligado: bater em adversário é só física, sem tiro livre nem pênalti) | Ligado |
| Dificuldade | Fácil, Médio, Difícil ou Pro (só contra a IA) | Médio |
| Prorrogação | Desligado, 50% ou 100% do tempo (não aparece no torneio nem na liga) | Desligado |
| Pênaltis | Ligado ou Desligado (some sem goleiro; não aparece no torneio nem na liga) | Desligado |
| Torneio | Qual competição: Copa 8, 16 ou 32, Mundial 16 ou Liga 10 ou 20 (só nas Opções do torneio e da liga) | O primeiro da lista |
| Turnos | 1 ou 2 (só nas Opções da liga) | O do asset da liga |

**Equilíbrio do goleiro (**fechado em 09/11: largura 1,2, ajustável em keeperWidth): goleiro cobrindo cerca de metade da largura do gol, deslizando com velocidade limitada. Sem goleiro, o gol fica menor para não virar goleada: duas traves fecham a boca de 2,4 para 1,4 (a largura do gol menos a do goleiro), valor ajustável no teste. Meta do playtest: algo como 1 gol a cada 3 ou 4 chutes.

## Modos de jogo

| Modo | Descrição | Fase |
| --- | --- | --- |
| Amistoso vs IA | Partida única contra a IA em 4 níveis (Fácil, Médio, Difícil e Pro) | MVP |
| 2 jogadores local | Revezamento no mesmo computador ou celular | MVP |
| Tutorial | Ensina os controles a quem nunca jogou (substitui o antigo modo Treino, que saiu do MVP em 09/10). Implementado em 11/11: 6 lições curtas dentro da partida de verdade, contra a IA no Fácil, sem relógio nem timers (Peteleco, Vez e toques, Pro Gol!, Defender, Falta e tiro livre, Pênalti escondido); texto conforme o aparelho (toque, mouse, teclado, controle); Pular e Repetir; 15 moedas por lição na primeira vez e mais 10 ao completar todas (100 no total). Botão Tutorial no Jogar; no primeiro Jogar o Menu pergunta "Quer aprender a jogar?" (Sim / Agora não) | Implementado (11/11) |
| Campeonato | Competições contra a IA (a copa substituiu a torre do Mortal Kombat em 09/11): Torneio (Copa 8, 16 ou 32 em mata-mata, e Mundial 16 com fase de grupos) e Liga (10 ou 20 times, pontos corridos, 1 ou 2 turnos). Detalhes em Torneio e liga, abaixo | Implementado (09/11 a 17/11) |
| Online 1x1 | Partida por turnos pela internet | Futuro |

**Torneio e liga (mata-mata combinado em 09/11; grupos e liga depois):**

1. Menu: **Jogar abre Torneio, Liga, Partida única, Dois jogadores e Tutorial**.
2. Jogar → Torneio ou Liga. Com um torneio (ou uma liga) guardado em andamento: Continuar ou Novo (o novo abandona o guardado). Torneio e liga são guardados separados, dá para ter um de cada em andamento.
3. Opções da partida: escolhidas uma vez no começo e guardadas para todo o torneio, com o nível da IA. Goleiro, prorrogação (50% do tempo) e pênaltis ficam sempre ligados e não aparecem: no empate do jogo do jogador, prorrogação e depois pênaltis.
4. Chave: o jogador (Meu time) + 7 times sorteados do catálogo, em posições sorteadas. Só a primeira fase é sorteada; depois segue o padrão (vencedor do jogo 1 x vencedor do jogo 2...), e os perdedores das semifinais disputam o 3º lugar. Em cada jogo o de cima é o mandante: o jogador mandante joga na mesa de casa, visitante numa mesa de visitante sorteada.
5. Tela da chave: uma aba por fase (Quartas, Semi, Final; a Final tem também o 3º lugar), os jogos empilhados de dois em dois com o placar (pênaltis entre parênteses), o jogo do jogador em destaque, Voltar e Jogar. Os jogos dos outros times da fase são sorteados quando o jogador termina o dele (placares do asset Placares, cada placar com um peso de 0 a 10; empate no mata-mata = pênaltis sorteados).
6. Partida: Seleção de times só com esquema e camisas (os times e a mesa vêm da chave). Na pausa não há Reiniciar, e sair da partida (ou fechar o jogo no meio dela) é derrota e abandona o torneio.
7. Fim de jogo: vitória mostra o botão **Avançar** (a chave na próxima fase, ou o fim do torneio) e Sair (Menu; o torneio fica guardado para continuar). Perdeu nas quartas, acabou; perdeu na semifinal, joga o 3º lugar.

Fim do torneio: tela com o troféu (campeão, vice e 3º; arte do usuário, por enquanto taças provisórias), a colocação e as moedas (das partidas, como na partida única, mais o prêmio da competição, que pode ser dobrado assistindo a um anúncio). Cada torneio é um asset (Resources/Torneios) com times, prêmios, máximo de gols sorteados, prorrogação e troféus. Estatísticas: torneios jogados, títulos, vices, 3º lugares e abandonados, no geral e por nível. Times de cores parecidas: o visitante usa o outro uniforme.

Competições (cada uma é um asset em Resources/Torneios com formato, times, turnos, prêmios, peso no ranking, placares sorteados, prorrogação e troféus; escolhida na linha Torneio das Opções):

- **Copa 8, Copa 16 e Copa 32** (mata-mata): prêmios 50/25/10, 80/40/20 e 100/50/25 para campeão, vice e 3º; peso 1, 1,5 e 2.
- **Mundial 16** (copa com grupos): 4 grupos de 4, passam 2 de cada para o mata-mata; prêmios 120/60/30; peso 2.
- **Liga 10 e Liga 20** (pontos corridos, todos contra todos): prêmios do 1º ao 5º, Liga 10 com 125/60/40/20/10 (peso 2) e Liga 20 com 250/125/80/40/20 (peso 3). Linha Turnos (1 ou 2) nas Opções da liga: com 2 turnos o prêmio e o peso são multiplicados pelos campos do asset (padrão x2 e x1,5). Sair no meio de um jogo da liga é derrota por W.O. só daquele jogo, e a liga continua.
- **Peso no ranking:** multiplica os Pontos da semana do torneio (ver Play Games, em Física e tecnologia).

## Controles

O mesmo gesto serve para mouse e toque: arrastar para trás, como um estilingue, e soltar. Só no toque: um botão fica escolhido (no começo da vez, o mais perto da bola; só ele brilha, e sem escolha brilham todos os que podem jogar); tocar e soltar num botão escolhe ele, ou tira a escolha se ele já era o escolhido; arrastar, começando em qualquer lugar (até em cima de outro botão), mira o escolhido, medindo a força a partir de onde o dedo encostou. Assim tocar no botão nunca chuta sem querer e o botão encostado na lateral também tem espaço para puxar. Sem nenhum escolhido, arrastar começando num botão mira ele. O mouse não muda.

| Ação | Mouse (itch.io) | Toque (Android) |
| --- | --- | --- |
| Selecionar botão | Clique no botão | Toque no botão |
| Mirar e dar força | Arrastar para trás; distância = força | Arrastar com o dedo |
| Chutar | Soltar o botão do mouse | Tirar o dedo |
| Cancelar | Clique direito ou voltar ao centro | Arrastar de volta ao botão |
| Câmera | Sem zoom (descartado; a mesa sempre cabe na tela) | Sem zoom |

Durante a mira aparecem uma faixa de força em volta do botão (transparente, cresce da borda para fora, de verde a amarelo e vermelho, com um anel fraco marcando o máximo), uma mira de bolinhas andando na direção do botão (cresce com a força, bolinha maior na ponta) e, se ele for acertar a bola, uma linha de bolinhas menores com a direção prevista da bola, sem mostrar rebotes (em teste desde 29/10, desenhado por código até a arte). O tamanho dessa ajuda é a opção "Ajuda de mira" (completa = mira e linha da bola; curta = só a mira; desligada = só a faixa de força; padrão completa). Não há zoom. Joystick na tela (opção das Configurações, ligada por padrão, só no toque): ao encostar o dedo para mirar aparece um círculo transparente do tamanho do arrasto máximo, com uma bolinha seguindo o dedo; é só visual, a mira é a mesma.

**Teclado e controle (gamepad)** (decidido em 13/10; mouse e toque continuam como acima):

| Ação | Teclado | Controle |
| --- | --- | --- |
| Cursor entre os botões (começa no mais perto da bola; direita/esquerda da tela de quem joga) | A/D, setas | Analógico esq., D-pad |
| Escolher o botão / Pronto | E | A |
| Voltar um passo (cancela a mira ou solta o botão) | Q (ou botão direito) | B |
| Mirar | A/D gira | Analógico esq. ou D-pad giram (esquerda/direita) |
| Força | W/S | Analógico dir. (cima/baixo) |
| Chutar | Espaço | X |
| Goleiro, batedor e bola do tiro de meta | A/D, setas | Analógico esq., D-pad |
| Pro Gol! | C | LB ou RB |
| Pausa | Esc | Start |

Com teclado/controle só o botão do cursor brilha. A mira abre apontando para a bola com força média; o giro começa devagar e acelera segurando. Reiniciar fica só no menu de pausa. Cada jogador pausa com o Esc/Start do próprio dispositivo (ou o botão de pausa do HUD) e só quem pausou mexe no menu de pausa e tira a pausa; dispositivo que ninguém escolheu nos Controles não pausa. Menus: WASD/setas ou analógico/D-pad navegam (cima/baixo entre as opções; esquerda/direita muda o valor nas Opções da partida e na Seleção de times, ou anda entre botões), E/A confirma, Q/B volta. A opção escolhida ganha uma moldura amarela pulsando, só enquanto se usa teclado/controle. Tela Controles (depois de Um/Dois jogadores, só quando há mais de um dispositivo): Jogador 1 na esquerda, dispositivos detectados no meio (Teclado e mouse, Toque, Controle N), Jogador 2 na direita. Cada um leva o seu cartão para um lado com o próprio dispositivo (no mouse/toque, as setinhas do cartão) e confirma; Q/B desfaz e, no meio, volta. Em Dois jogadores, confirmar de novo com o outro lado vazio = os dois revezando no mesmo dispositivo. Com dispositivos diferentes cada jogador entra na sessão com o seu (os dois são Actors); no mesmo dispositivo a partida lê os dispositivos direto.

## Física e tecnologia

A física roda em 2D (Box2D da Unity), vista de cima, o que combina com a pixel art e simplifica o projeto.

- **Física:** Rigidbody2D sem gravidade, botões e bola com CircleCollider2D, goleiro com BoxCollider2D; PhysicsMaterial2D ajustável (atrito, quique) e Linear Drag para simular o feltro. A bola rebate nos botões parados como numa parede (o botão não sai do lugar); só o botão do peteleco empurra a bola. A bola não tem atrito: sai sempre na linha entre os centros do botão e da bola (a mesma da previsão da mira). No toque, a direção do peteleco é a de um instante antes (0,08 s) de soltar o dedo, para a tremida ao tirar o dedo não desviar o chute.
- **Paredes:** EdgeCollider2D em volta do campo; cada chute registra se a bola tocou parede, para a regra de gol anulado.
- **Fim de turno:** o turno acaba quando todas as velocidades ficam abaixo de um limiar por 0,3 s.
- **IA:** simula alguns chutes candidatos (ângulo x força), pontua o resultado e escolhe com ruído conforme a dificuldade. Níveis Fácil, Médio, Difícil e Pro (desde 09/11), escolhidos na linha Dificuldade das Opções da partida. Contra a IA o jogador fica embaixo, com o Meu time; na vez da IA o HUD mostra "IA pensando". A cada toque a IA tem um objetivo claro, numa ordem fixa, e cada objetivo tem uma conta só para saber se a jogada deu certo. 1) Marcar: dá pra chutar? Com goleiro, não tem conta de chance: se algum botão (qualquer um que pode jogar, não só os mais perto) tem caminho livre até a bola e da bola até a boca do gol, a bola está a distância de chute (3 no Fácil, 4,5 no Médio, 6 no Difícil, 6,5 no Pro) e o goleiro não consegue andar até a bola (ela viraria dele), anuncia o "Pro Gol!". Com toque sobrando e só um botão com ângulo, antes tenta deixar a bola onde dois botões tenham ângulo (o adversário não sabe de qual vem o chute); se não consegue, anuncia assim mesmo. Depois, com o goleiro já posicionado, a simulação escolhe qual botão chuta e para onde (o canto que o goleiro cobre pior); se nenhum chute entra, fica com o que leva a bola mais perto da boca do gol, para obrigar o goleiro a defender. O goleiro não conta como bloqueio na hora de anunciar, porque ainda vai se mexer. Sem goleiro, no ataque, chuta direto se alguma versão do chute entra. 2) Preparar o chute (tem toque sobrando): fica com a bola e a deixa onde algum botão tenha chute livre no toque seguinte, de preferência mais de um (o adversário não sabe de qual vem o chute para posicionar o goleiro); se não achar, avança ficando com a bola. Com 3 toques, o Médio e o Difícil imaginam também o toque seguinte das melhores jogadas (2 no Médio, 4 no Difícil, mais no Pro) para o chute ficar pronto e a bola não ficar sem saída. 3) Com a bola perto do próprio gol (menos de 3,5), Afastar vem antes de Preparar, mesmo com toque sobrando (a conta é o quanto a bola fica mais longe do gol); longe do próprio gol, no último toque (ou se não deu para preparar), Proteger (deixar a bola onde o adversário não tenha chute livre). Se nada serve, Fechar o gol: põe um botão entre a bola e o gol. Sempre: a nota de cada jogada é a da jogada perfeita; as versões com o erro de mira e de força do nível só servem para ver o risco. Na hora de jogar, o erro acontece com uma chance por nível (Fácil 60%, Médio 45%, Difícil 25%, Pro 12%). Falta certa e risco de gol contra nunca são aceitos, e a chance de falta tem limite por nível. No tiro de meta, simula o chute em várias posições da bola. Personalidade: o nível diz quão bem ela joga e o perfil mexe só na distância para anunciar o Pro Gol! e no risco de falta (Ofensivo anuncia de 0,5 mais longe e aceita mais falta; Defensivo só de 0,5 mais perto e evita falta). O perfil vem do estilo do esquema da IA (2-3 e 1-2 Ofensivo, 3-2 Defensivo, 2-2-1, 2-1 e Losango Equilibrado). Nos 25% finais do tempo o placar puxa o perfil: perdendo vai para o Ofensivo, ganhando para o Defensivo, mais forte quanto menos tempo falta e maior a diferença (2 gols ou mais no fim = total). O jogador não escolhe: vê o estilo do esquema da IA na Seleção de times. No editor, cada decisão vai para um CSV por partida em Logs/IA (não aparece no console), com o perfil e o ajuste. No "Pro Gol!" do adversário, o goleiro da IA pensa como o atacante: pega os chutes a gol mais prováveis (3 no Fácil, 6 no Médio, 10 no Difícil, 15 no Pro), simula cada um contra várias posições do goleiro e fica onde entram menos gols, com um erro conforme o nível.
- **Arquitetura:** construída sobre o \[Immersive Framework\](https://github.com/ImmersiveGames/com.immersive.framework) (com.immersive.framework, via OpenUPM). Routes para as telas (menu, seleção, partida, resultado), Activity para a partida, Pause e Reset do framework para pausar e reiniciar, save num arquivo JSON do Progression Save (backend JSON do framework, um slot com versão: música, nível da IA, opções da partida, últimas escolhas de cada jogador, estatísticas contra a IA, perfil do Meu time, moedas e extrato, itens comprados, torneio e liga em andamento, tutorial e conquistas), BGM via com.immersive.audio. Dentro da Activity da partida, uma máquina de estados própria (Saída, Mira, Movimento, Pro Gol!, Gol, Fim) e ScriptableObjects para times e opções da partida. Versão: sempre a mais recente publicada (hoje 1.1.0, a primeira Stable, desde 28/10; a 1.2.0 saiu em 15/11 e entra depois do teste aberto, mantendo o contorno do Leave no Menu), acompanhando as atualizações. Serviços do Google (só Android): Play Games (login automático, save na nuvem, 21 conquistas e o ranking semanal Pontos da semana), Firebase Analytics e Crashlytics (só dados das partidas e erros, sem dados pessoais), AdMob (anúncios) e Unity IAP (Remover anúncios). Pontos da semana: cada torneio terminado vale colocação x nível da IA (Fácil 0,5, Médio 1, Difícil 1,5, Pro 2) x peso da competição, mais o saldo de gols (até +3 por partida) e +10 por título logo depois de outro na mesma semana; a semana soma os 10 melhores torneios e vira de sábado para domingo (horário do Pacífico), igual ao ranking semanal do Google. Tudo isso aparece na aba Carreira do Meu time.
- **Input:** Input System 1.19 (já exigido pelo framework) com um único handler de ponteiro para mouse e toque, respeitando o input mode e o pause do framework.
- **Orientação:** campo sempre vertical, com um gol em cima e outro embaixo, nas duas plataformas. No PC a janela é larga e as laterais mostram HUD e moldura; a câmera encaixa a mesa na largura da tela (ScreenFit, desde 15/10, por causa de celulares compridos como o Moto G84) e os menus se esticam; o Pixel Perfect foi descartado.
- **WebGL:** build alvo abaixo de 20 MB; testar em Chrome e Firefox. Validar logo na fase 1 que o framework roda bem em WebGL.
- **Android:** mesmo projeto, build separada; 60 fps em aparelhos intermediários.

## Arte, áudio e UI

Pixel art top-down com cara de mesa de botão: feltro colorido cercado por placas ou outra borda, botões redondos com brilho de acrílico.

- **Times** (17/11): o catálogo são os Craques da Rua (em espanhol Cracks de la Calle, em inglês Street Stars), 40 times fictícios com nomes de pessoas, como Zé Botão FC, cada um com nome traduzido em português, inglês e espanhol, sigla de 3 letras e dois uniformes com combinações de cores sem repetir entre times. As camisas dos times da IA são sorteadas entre todos os padrões, mesmo os que o jogador ainda não comprou. Os times só de cores (Azul, Vermelho...) foram aposentados; Criar telas só cria times de cor se o catálogo estiver vazio.
- **Depois:** escudo dos times como uma camada a mais, e pacotes temáticos de camisas (ideias anotadas, sem plano). Nunca escudos de clubes reais (licença).
- **Dados do time:** nome, abreviação de 3 letras (usada só no placar, para não estourar e ficar padrão), cor do botão, cor do anel e escudo. Mensagens e Resultado usam o nome; os botões em campo só as cores.
- **Esquemas táticos** (contados de trás para a frente): com 5 botões 2-2-1 (equilibrado, padrão), 3-2 (defensivo), 2-3 (ofensivo) e 2-1-2 (losango); com 3 botões 2-1 (padrão) e 1-2 (ofensivo).
- **Seleção de times:** é o próprio campo, com os dois times no esquema escolhido. Em cada metade, à esquerda e na altura do meio daquele campo, as setas do time lado a lado com o nome e a abreviação à direita delas e, embaixo, as setas da do uniforme (Uniforme 1/2), no mesmo formato (sem escudo de prévia: os botões no campo já mostram o time); à direita, as setas do esquema. A metade de cima fica girada 180° (base do celular com 2 jogadores na mesma tela; o PC será ajustado depois); contra a IA, as opções da metade de cima ficam em pé, como as do jogador (time à esquerda, esquema à direita), e só os botões dela continuam virados. Na faixa do meio-campo, cada metade tem o seu Pronto (à direita de quem joga daquele lado) e o Voltar fica com o Jogador 1; a partida começa com os dois prontos (contra a IA, só o Pronto de baixo). Pronto marcado trava as setas daquele time e Q/B desmarca. Com dispositivos separados cada jogador tem o próprio cursor (Jogador 1 amarelo, Jogador 2 azul) e só mexe na sua metade, inclusive com mouse/toque; contra a IA ou revezando, um cursor só passa pelas duas metades. As Opções da partida são só do Jogador 1. Os dois não podem ter o mesmo time; contra a IA ela sorteia time, esquema e camisas, e dá para trocar. As camisas (padrão do botão e do goleiro) vêm do uniforme escolhido. As camisas são desenhadas em cinza e tingidas: corpo, contorno, sombra interna e brilho na cor principal (os tons de cinza da arte fazem o claro e o escuro), padrão na segunda cor e sombra preta semitransparente. O goleiro não tem contorno. No jogo o desenho dos botões nunca gira (mesmo girando na física), para o padrão ficar coerente na tela; o time de cima não é girado, então a luz e a frente do goleiro (virada para baixo da tela) são iguais nos dois times. A bola e os botões passam por cima do goleiro. A escolha vale na sessão. Uniforme 1 e 2 (11/11, no lugar da cor reserva): cada time (catálogo e Meu time) tem dois uniformes, cada um com cor principal, segunda cor, camisa do botão e camisa do goleiro (o 2 pode ter padrão diferente do 1). Na Seleção, as setas abaixo do time trocam o uniforme (não há mais setas de cor nem de camisa); contra a IA, ela escolhe sozinha o uniforme que não fica parecido com o do jogador. No Meu time, nome e sigla ficam em cima e, embaixo, as abas Uniforme 1 e Uniforme 2, cada uma com a prévia do botão e do goleiro e as opções. Saves e times antigos: a cor reserva vira o Uniforme 2; sem reserva, o 2 tem as cores invertidas. Vale na partida, na Seleção, no placar, no Resultado e na chave do torneio. Financeiro (11/11): o Meu time tem quatro abas (Time, Mesa, Financeiro e Carreira; a Carreira, que veio depois, tem Estatísticas, Pontos da semana e o Play Games com Conquistas e Ranking) e o saldo saiu do topo. A aba Financeiro mostra o saldo, o ganho total separado por origem (partidas, torneios, tutorial), o gasto total na loja e o extrato com as últimas 30 movimentações (data, descrição e valor, verde quando ganha e vermelho quando gasta). As moedas saíram das Estatísticas.
- **Áudio:** estalo do peteleco, batida entre botões (fraca/forte), bola na parede, apito (saída, falta, pênalti, gol anulado), apito final, cartão e torcida com fanfarra no gol, clique nos botões da interface, no estilo chiptune; música do menu (Abertura, Menu, Resultado) e da partida. Toca pelo com.immersive.audio e o BGM do framework (Route BGM). Por enquanto os sons são provisórios gerados por código; narração fica para depois.
- **HUD:** placar, relógio, contador de toques e de quem é a vez; na mira, barra de força e indicadores de direção do botão e da bola; no celular (versão principal, tela em pé) cada jogador tem uma faixa logo depois do seu gol (a de cima girada para o jogador de cima), dentro da moldura que a câmera sempre encaixa: relógio e pausa à esquerda; placar (abreviações, gols e cartões) no meio; vez, toques, timer da jogada (vermelho nos últimos 5 s) e o botão Pro Gol!/Pronto à direita. Mensagens no meio do campo, uma virada para cada jogador. Contra a IA as duas faixas são do jogador, em pé: a de cima em duas linhas, placar centralizado com os cartões do lado de fora de cada time e, embaixo, tempo de jogo à esquerda, vez/toques no meio e timer da jogada à direita; a de baixo só com Pausa e o botão Pro Gol!/Pronto, maiores e perto do polegar, com o meio livre (lugar de um controle virtual no futuro). Mensagem do jogo só a do jogador. Avisos importantes (falta, cartão, pênalti, gol, gol anulado, bola do goleiro) param o jogo por 3 s, sem pular: controles travados, relógio e timer parados, a IA espera; o aviso some junto com a pausa. As outras mensagens (vez, toques) ficam 3,5 s sem parar o jogo. Os tempos ficam nas opções da partida. Os botões da vez brilham. No gol, o texto GOOOOOL! ondula nas cores do time que marcou, com fogos de artifício. No PC, por enquanto, um painel à esquerda do campo (a orientação do PC será revista).
- **Telas:** fluxo: Abertura ("Clique para iniciar", "Toque para iniciar" no celular) → Menu com o título do jogo e os botões Jogar, Meu time, Loja, Configurações e Sair; Jogar abre Torneio, Liga, Partida única (contra a IA), Dois jogadores e Tutorial → Opções da partida (a Dificuldade Fácil/Médio/Difícil/Pro é a segunda opção e só aparece contra a IA; Voltar/Avançar) → Seleção de times → Jogo → Resultado ("Fim de Jogo"; o botão e o nome de cada time com o placar no meio, "W.O." quando for o caso; tabela dos dois times com Pro Gol!, conversão, posse de bola pela parte dos toques, toques na bola, toques errados, faltas, cartões e gols contra se teve; contra a IA, aviso de novo recorde de sequência a partir de 2 vitórias; Jogar de novo e Menu; numa partida que pagou moedas o Resultado tem só Avançar, que abre a página Premiação com as moedas subindo, Dobrar moedas por anúncio e os botões de seguir). As Estatísticas ficam na aba Carreira do Meu time. Também pausa (Continuar, Configurações, Reiniciar, Sair para o menu). Configurações (11/11, no Menu e na pausa, abre por cima): Volume geral, Música e Efeitos de 0% a 100%, guardados no aparelho (a opção Música saiu das Opções da partida); também Joystick na tela, Política de privacidade, Privacidade dos anúncios e Restaurar compras. Idioma (11/11) nessa tela: Automático (português se o aparelho estiver em português, senão inglês), Português, English ou Español; todos os textos do jogo traduzidos (Menu, partida, HUD, tutorial, loja, Meu time, estatísticas, torneio). Os textos ficam num arquivo único (Resources/Idiomas/textos.json) com português, inglês e espanhol, editado na janela Futebol de Botão/Idiomas/Textos; nomes de times e de itens também são traduzidos (abas Times e Itens da janela).

### Guia de arte: cenário

Atualizado em 23/11 (o guia de 10/10 descrevia a mesa antiga de madeira, que saiu em 20/11). As artes são feitas à mão no Aseprite; este guia diz o que desenhar e em que tamanho.

- Escala: 32 px por unidade do jogo, tiles de 32x32, filtro Point. Paleta: AAP-64 (vem com o Aseprite). Botões, camisas, feltro e bola são desenhados em branco ou cinza e o jogo aplica a cor por cima; as cores saem da paleta do jogo.
- A mesa é um tema em duas partes: a borda (asset Mesa, em Resources/Mesas) e o feltro (asset Feltro, em Resources/Feltros). Cada asset tem nome, preço em moedas, se já vem liberado e uma miniatura opcional para a Loja e o Meu time.
- Ordem das camadas (de baixo para cima): Fill da mesa (opcional, lado de fora), feltro, marcações, placas com a camada de cor, bola e botões, rede e trave.

| Peça | Formato | Observações |
| --- | --- | --- |
| Placas da borda (Mesa) | 6 peças: canto, parede (fundo e laterais), parede ao lado do gol grande, canto do fundo do gol grande, parede ao lado do gol pequeno e canto do fundo do gol pequeno | Desenhadas para a parede de cima; as outras giram ou espelham. Cada peça pode ter uma camada de cor (desenho branco por cima, pintado com a cor da trave escolhida). O asset também tem o Fill opcional (cobre o lado de fora da mesa no lugar do feltro), o recuo das paredes da física em pixels e a trave usada |
| Traves | Asset Trave: esquerda e direita do gol grande e do pequeno, a peça do meio (a mesma nos dois) e a rede de cada uma (opcional) | Desenhadas para o gol de cima; o de baixo gira 180°. A trave é pintada com a cor escolhida (branca, vermelha, amarela...), os dois gols sempre iguais, e a cor não é desbloqueável. A rede fica como está, por cima da bola. Uma trave pode servir para várias mesas |
| Feltro (Feltro) | Felt\_White, Felt\_White\_Gradient e Felt\_White\_Texture, tingidos por faixa | Padrões Liso (cor 1), Listrado (cor 1 e cor 2) e Meio a meio (cor 1 embaixo, cor 2 em cima), cada um com tile liso, gradiente ou textura. O feltro também define a cor das marcações e quais cores de trave combinam com ele. Campo de 7 x 11 tiles (224 x 352 px) |
| Marcações | Field\_Marks, sprite transparente de 224 x 352 px | Medidas abaixo; dá para desenhar um quarto e espelhar |
| Bola (Bola, em Resources/Bolas) | Quadros da bola, camada de cor de cada quadro (fundo branco, por baixo dos quadros), luz e sombra opcionais | O desenho é o padrão; a cor escolhida tinge a camada branca. Cada bola pode limitar as cores permitidas (lista vazia = todas da paleta) |

**Marcações** (em pixels, a partir do canto de cima à esquerda do feltro):

- Linhas com 2 px de espessura.
- Meio-campo nas linhas 175 e 176.
- Círculo central com centro em (112, 176) e cerca de 58 px de diâmetro.
- Áreas com 128 px de largura (x de 48 a 176) e 51 px de fundo a partir da linha do gol.
- Marca do pênalti a 88 px da linha do gol (y 88 e y 264), com uns 5 px.

**Mesas, feltros e bolas** (23/11):

- Mesas (9): Mesa Clássica (liberada); Placas, Placas B, Alambrado Baixo e Alambrado Alto (200 moedas); Madeira Alto e Madeira Baixo (220); Stone Wall e Stone Low (250).
- Feltros (9): Verde listrado, Verde listrado com gradiente, Azul liso e Amarelo liberados; Azul e roxo listrado e Roxo meio a meio (200); Black, Black listrado e Sand (250).
- Bolas (7, desde 20/11): Clássica liberada; Nice 26, Didas 26, Bruma e Wing (150); Didas 27 e Nice 27 (200). O padrão da bola é comprado na Loja e a cor é livre.
- No Meu time, a aba Mesa escolhe a mesa de casa: mesa, feltro, cor da trave, bola e cor da bola. Na Seleção de times, as setas da bola trocam o padrão e mantêm a cor. O visitante joga numa mesa e num feltro diferentes, sorteados entre os liberados dele; a IA sorteia a bola e a cor.

**Lembretes:**

- Para combinar com a perspectiva, botões e bola ganham uma bordinha de espessura e sombra. Tamanhos de referência: botão 22 px, bola 13 px, goleiro 32 x 8 px (esticado para a largura 1,2). Bola rápida (a partir de \~5 un/s, por chute ou batida) ganha um rastro branco meio transparente atrás dela, que cresce em 4 quadros até \~9 un/s e encolhe quando ela freia, girando nas mesmas 8 direções do desenho da bola.
- Área com 1,5 (48 px) desde 12/10; o gol continua com 2,4. A linha do gol é a das marcações (2 px, até a beira do feltro); o fundo do gol não tem linha, e o gol vale com a bola inteira além da beira do feltro.
- Telas (Abertura, Menu e Resultado): fundo com o feltro listrado (Felt\_White tingido de #1A7A3E e #24523B), na mesma escala da mesa. Os textos têm sombra para não se perder nas faixas.

## Monetização

Anúncios (decidido 13/11, só Android): na primeira abertura, uma tela neutra pergunta o ano de nascimento (fica só no aparelho, não pergunta de novo); menor de 13 recebe anúncio para crianças, não personalizado e classificação G; 13 a 17, abaixo da idade de consentimento e não personalizado; 18+, pedido de consentimento do Google quando a lei exige. Entre partidas: um anúncio ao sair do Resultado, no máximo um a cada 2 partidas, nunca durante o jogo nem no tutorial, e nenhum depois de um anúncio com prêmio na mesma tela. Com prêmio (o jogador escolhe assistir): Dobrar moedas no Resultado da partida, Dobrar prêmio no fim do torneio e Vender patrocínio na aba Financeiro do Meu time (5 moedas, até 3 por dia). A compra Remover anúncios (dinheiro de verdade, na Loja; Restaurar compras nas Configurações) tira os anúncios entre partidas, e os com prêmio continuam. Banner (13/11): no topo do Menu e do Resultado; sai com Remover anúncios. A tabela abaixo resume como ficou. Itens (implementados entre 28/10 e 09/11): padrões de botão para o jogador escolher, além das cores do time, desenhados pelo usuário, alguns grátis e os outros comprados com moedas na Loja. Só visual, sem mudar a física. Em camadas: a base em cinza leva a cor do time e o padrão em cinza (listras, faixa, estrela, borda) leva a segunda cor do time (o anel), para cada padrão servir para qualquer time; os dois times precisam continuar fáceis de distinguir em campo. Sem escudos ou marcas de clubes reais (licença). Loja (pensada em 28/10, implementada em 05/11, botão Loja no Menu): abas Botão, Goleiro, Bola, Mesa e Feltro; o que foi comprado passa a aparecer no Meu time e na Seleção de times. Depende do sistema de save (o que foi liberado fica guardado no aparelho) e, no Android, das compras no app; no itch não há compras. Moedas e desbloqueáveis (04/11, implementados em 05/11): o campo vira tema em duas partes, a borda (mesa) e o feltro (cor/padrão), e a bola também virou item (ver Guia de arte: cenário). Cada item é um asset com nome, arte, preço em moedas e se já vem liberado (a Mesa Clássica, a bola Clássica e quatro feltros vêm liberados); o save guarda as moedas, os itens liberados e o tema escolhido. As moedas só se ganham jogando, não há pacote de moedas à venda (dinheiro de verdade só para remover anúncios). Partida solo contra a IA terminada paga, no Médio, 10 pela vitória, 5 pelo empate e 2 pela derrota; Fácil paga metade, Difícil +20% e o nível Pro +50%, arredondando para cima (Fácil 5/3/1, Médio 10/5/2, Difícil 12/6/3, Pro 15/8/3). Partida abandonada e 2 jogadores não pagam. O torneio paga mais: por partida e um prêmio no fim, conforme o asset de cada competição (ver Torneio e liga).

| Formato | Onde aparece | Regra |
| --- | --- | --- |
| Intersticial | Ao sair do Resultado (ou da Premiação) | No máximo 1 a cada 2 partidas; nunca durante o jogo |
| Recompensado (opcional) | Premiação (Dobrar moedas), fim do torneio (Dobrar prêmio) e aba Financeiro (Vender patrocínio) | O jogador escolhe assistir para ganhar moedas (patrocínio: 5 moedas, até 3 por dia) |
| Banner | Topo do Menu e do Resultado (desde 13/11) | Nunca na tela de partida, que no retrato já é apertada |
| Remover anúncios | Loja (Restaurar compras nas Configurações) | Compra única; remove intersticial e banner, mantém o recompensado como opção |

SDK: Google AdMob (decidido em 12/11), com as regras de Famílias do Google Play; a compra usa o Unity IAP.

## Escopo do MVP e roadmap

O MVP é uma partida completa contra a IA ou entre 2 jogadores, jogável no navegador pelo itch.io.

1. **Protótipo de física:** mesa, 1 botão, bola, gol, goleiro e peteleco com mouse, com times de cores. Meta: o chute ser divertido sozinho. Abre direto no gameplay, mas já como uma Activity do framework.
2. **Regras e turnos:** times completos, toques, faltas, paredes, placar e relógio. Entra o fluxo mínimo com telas simples: abertura, menu (iniciar e opções da partida), partida e resultado, como Routes do framework.
3. **IA e modos:** IA em 3 níveis, 2 jogadores local.
4. **Polimento e itch.io:** arte, som, menus, build WebGL publicada.
5. **Android:** controles de toque, layout em retrato, decisão e integração dos anúncios, build na Play Store.
6. **Pós-lançamento:** tutorial, torneios e liga, 40 times, loja, idiomas e Play Games já feitos; falta avaliar o online. Teste aberto no Google Play desde 20/11 (versão 0.1.1, código 18, em análise em 23/11).

**Anotado em 14/10 para mais para a frente, e já implementado**:

- **Sistema de línguas:** português, inglês e espanhol (11/11).
- **Estatísticas do jogador:** só partidas contra a IA (2 jogadores não guarda), no total e por nível. Resultado: partidas, vitórias, empates, derrotas e abandonadas (número e %), sequência atual de vitórias e recorde. Gols: feitos, sofridos, contra, saldo, sofridos por gol feito e médias por partida. Jogo: chutes a gol (Pro Gol!), conversão, faltas, cartões, toques na bola e toques errados (total e por partida). Pênaltis: disputas vencidas e perdidas, gols feitos e sofridos (só disputas de pênaltis, não pênalti de falta). Abandonada = sair para o menu, reiniciar ou fechar o jogo no meio; zera a sequência. Tela Estatísticas (hoje na aba Carreira do Meu time) com abas Geral, Fácil, Médio, Difícil, Pro e Última partida, lista que rola e "Zerar estatísticas" (confirma e apaga tudo). Os números da IA só ficam na memória para a tela de Resultado. Detalhe das métricas: [Métricas das estatísticas](https://claude.ai/artifact/6wzpLNB75ZkTqQ9H9pUcU4).
- **Coleta de informações das partidas:** sem dados pessoais, só dados das partidas, para entender o desempenho do jogo e se as pessoas estão jogando. Feito com Firebase Analytics e Crashlytics.

## Riscos

| Risco | Mitigação |
| --- | --- |
| Goleiro forte demais, gols raros | Ajustar tamanho e velocidade do goleiro no protótipo; opção sem goleiro com gol menor |
| Física diferente entre WebGL e Android | Timestep fixo, testar nas duas builds desde a fase 1 |
| IA burra ou imbatível | Simulação de chutes com ruído ajustável por nível |
| Partidas lentas por excesso de mira | Timer de 30 s e relógio parado durante o movimento |
| Campo apertado no celular em retrato | Pixel art legível em tela pequena, HUD enxuto nos cantos ao lado dos gols |
| Dedo cobrindo o botão no celular | Arrasto pode começar em qualquer ponto após selecionar |
| Anúncios irritando o jogador | Nunca durante a partida, limite de frequência, compra para remover |
