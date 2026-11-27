# Pro Gol! — o que cadastrar no Firebase (Definições personalizadas)

Onde: Firebase Console > Analytics > **Definições personalizadas** (ou Google Analytics > Administrador > Definições personalizadas).
Cada linha abaixo é um cadastro. Para todos: **Escopo = Evento**. O "Parâmetro do evento" tem que ser digitado exatamente como está.
Só aparecem nos relatórios os dados que chegarem DEPOIS do cadastro (não é retroativo), então vale cadastrar logo.

## Dimensões personalizadas (textos) — botão "Criar dimensões personalizadas"

| Nome da dimensão | Parâmetro do evento | Valores |
|---|---|---|
| Modo | mode | ia, 2j, torneio, tutorial |
| Nível | level | easy, medium, hard, pro, - |
| Resultado | result | win, loss, draw |
| Prorrogação | extra_time | yes, no |
| Disputa de pênaltis | shootout | won, lost, no |
| W.O. | walkover | yes, no |
| Goleiro | goalkeeper | yes, no |
| Faltas ligadas | fouls_on | yes, no |
| Pênaltis ligados | penalties_on | yes, no |
| Primeira partida vs IA | first_ai_match | yes, no |
| Parte do abandono | period | regular, extra_time, shootout |
| Local do premiado | placement | dobrar, dobrar_torneio, patrocinio |
| Assistiu até o fim | completed | yes, no |
| Comprou na visita | bought | yes, no |
| Mudou uniforme | uniform | yes, no |
| Mudou mesa | table | yes, no |
| Mudou nome | team_name | yes, no |
| Abandonou torneio | abandoned | yes, no |
| Desafio | challenge | F1-F13, M1-M12, D1-D9, S1-S13 |
| Desafio semanal | weekly | yes, no |
| Desafio trocado | from | id do desafio que saiu |
| Desafio novo | to | id do desafio que entrou |

(item_name, virtual_currency_name e value da loja são parâmetros padrão do Google: não precisa cadastrar.)

## Métricas personalizadas (números) — botão "Criar métricas personalizadas", Unidade = Padrão

| Nome da métrica | Parâmetro do evento |
|---|---|
| Gols pró | goals_for |
| Gols sofridos | goals_against |
| Tempo real (s) | real_seconds |
| Pro Gol! pedidos | progol_called |
| Pro Gol! convertidos | progol_scored |
| Pro Gol! pedidos (adversário) | opp_progol_called |
| Pro Gol! convertidos (adversário) | opp_progol_scored |
| Faltas | fouls |
| Faltas (adversário) | opp_fouls |
| Cartões | cards |
| Cartões (adversário) | opp_cards |
| Botões por time | discs |
| Minutos da partida | minutes |
| Toques por vez | touches |
| Prorrogação % | extra_time_pct |
| Minuto do abandono | minute |
| Lição | lesson |
| Times no torneio | teams |
| Colocação no torneio | position |
| Fase eliminada | eliminated_stage |
| Intersticiais na sessão | session_count |
| Dias seguidos | days |

Total: 22 dimensões + 22 métricas (limite do Firebase: 50 de cada).
