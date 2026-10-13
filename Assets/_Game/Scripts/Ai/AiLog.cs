using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Registro das decisões da IA: uma linha no console por toque e, no editor, um CSV por partida em
    /// Logs/IA/partida-AAAAMMDD-HHMM-Nivel.csv (na pasta do projeto) com os fatos, os objetivos, a jogada, a nota esperada
    /// e o que aconteceu de verdade (onde a bola parou, último toque, avisos como gol ou falta).
    /// </summary>
    public sealed class AiLog : IDisposable
    {
        private static readonly CultureInfo Number = CultureInfo.InvariantCulture;
        private const string Header =
            "nivel;tempo;placar;toque;fatos;objetivos;objetivo;recusados;acao;botao;direcao;forca;nota;riscos;" +
            "bola_esperada;ultimo_toque_esperado;gol_esperado;simulacoes;ms;bola_real;ultimo_toque_real;avisos";

        private readonly TeamSide side;
        private readonly string level;
        private StreamWriter writer;
        private string pending;
        private readonly StringBuilder notices = new();

        public AiLog(TeamSide aiSide, AiDifficulty difficulty)
        {
            side = aiSide;
            level = difficulty != null ? difficulty.name.Replace(" (padrão)", string.Empty) : "?";
        }

        /// <summary>A IA decidiu: escreve no console e guarda a linha esperando o resultado real.</summary>
        public void Decided(AiDecision decision, AiSituation situation, MatchController match)
        {
            Flush();
            Debug.Log($"[IA] {level} {situation.State} toque {match.TouchesLeft}: {Describe(decision)} | objetivos: {decision.GoalsLine}" +
                      (decision.Rejected.Length > 0 ? $" | recusados: {decision.Rejected}" : string.Empty) +
                      $" | {situation.Describe()} | {decision.Simulations} simulações, {decision.Milliseconds:0.0} ms.");
#if UNITY_EDITOR
            var c = decision.Candidate;
            var e = decision.Expected;
            pending = string.Join(";",
                Field(level),
                Field(Mathf.FloorToInt(match.ClockSeconds).ToString(Number)),
                Field($"{match.Score(side)}x{match.Score(side == TeamSide.Bottom ? TeamSide.Top : TeamSide.Bottom)}"),
                Field(match.TouchesLeft.ToString(Number)),
                Field(situation.Describe()),
                Field(decision.GoalsLine),
                Field(decision.Goal != null ? decision.Goal.Name + (decision.NoGoalAccepted ? " (nenhum aceitou)" : string.Empty) : "nenhum"),
                Field(decision.Rejected),
                Field(decision.Action != null ? decision.Action.Name : "bater reto na bola"),
                Field(c.Disc != null ? c.Disc.name : decision.Action != null && decision.Action.IsInstant ? string.Empty : "bola"),
                Field(decision.Action != null && decision.Action.IsInstant ? string.Empty : string.Format(Number, "{0:0.00};{1:0.00}", c.Direction.x, c.Direction.y)),
                Field(decision.Action != null && decision.Action.IsInstant ? string.Empty : c.Power01.ToString("0.00", Number)),
                Field(decision.HasExpected ? decision.Verdict.Score.ToString("0", Number) : string.Empty),
                Field(Risks(decision.Verdict)),
                Field(decision.HasExpected ? Point(e.BallEnd) : string.Empty),
                Field(decision.HasExpected ? SideName(e.LastTouchSide) : string.Empty),
                Field(decision.HasExpected && e.GoalOf != null ? (e.GoalOf == side ? "contra" : "a favor") : string.Empty),
                Field(decision.Simulations.ToString(Number)),
                Field(decision.Milliseconds.ToString("0.0", Number)));
            notices.Clear();
            // Anúncio do Vai chutar não mexe nada: a linha sai na hora.
            if (decision.Action != null && decision.Action.IsInstant) Finish(match);
#endif
        }

        /// <summary>
        /// Jogada do jogador (no editor, para comparar com as da IA): a mesma linha, com objetivo "jogador", e o resultado
        /// real quando tudo parar.
        /// </summary>
        public void PlayerShot(Disc disc, Vector2 impulse, float maxImpulse, MatchController match)
        {
            Flush();
#if UNITY_EDITOR
            var c = Number;
            Vector2 ball = match.Ball.Body.position;
            var opponent = side == TeamSide.Bottom ? TeamSide.Top : TeamSide.Bottom;
            // A partida já contou o toque quando o peteleco sai (estado Moving): o toque desta jogada é um a mais.
            int touches = match.State == MatchState.Moving ? match.TouchesLeft + 1 : match.TouchesLeft;
            string facts = string.Join(" ",
                string.Format(c, "bola=({0:0.0};{1:0.0})", ball.x, ball.y),
                string.Format(c, "gol_jogador={0:0.0}", Vector2.Distance(ball, new Vector2(0f, FieldLayout.GoalLineY(opponent)))),
                string.Format(c, "gol_ia={0:0.0}", Vector2.Distance(ball, new Vector2(0f, FieldLayout.GoalLineY(side)))),
                $"toques={touches}");
            Vector2 direction = impulse.normalized;
            float power = maxImpulse > 0f ? impulse.magnitude / maxImpulse : 0f;
            pending = string.Join(";",
                Field(level),
                Field(Mathf.FloorToInt(match.ClockSeconds).ToString(Number)),
                Field($"{match.Score(side)}x{match.Score(opponent)}"),
                Field(touches.ToString(Number)),
                Field(facts),
                Field(string.Empty),
                Field("jogador"),
                Field(string.Empty),
                Field(disc != null ? "Peteleco" : "Chute na bola"),
                Field(disc != null ? disc.name : "bola"),
                Field(string.Format(Number, "{0:0.00};{1:0.00}", direction.x, direction.y)),
                Field(power.ToString("0.00", Number)),
                Field(string.Empty), Field(string.Empty), Field(string.Empty), Field(string.Empty), Field(string.Empty),
                Field(string.Empty), Field(string.Empty));
            notices.Clear();
#endif
        }

        /// <summary>Um aviso da partida (gol, falta, cartão...) durante a jogada da IA.</summary>
        public void Noticed(MatchNotice notice)
        {
            if (pending == null) return;
            notices.Append(notices.Length > 0 ? "," : string.Empty).Append(notice);
        }

        /// <summary>A jogada da IA terminou: completa a linha com o que aconteceu de verdade.</summary>
        public void Finish(MatchController match)
        {
            if (pending == null) return;
            var ball = match.Ball;
            Write(pending + ";" + string.Join(";",
                Field(Point(ball.Body.position)),
                Field(SideName(ball.LastTouchSide)),
                Field(notices.ToString())));
            pending = null;
        }

        /// <summary>A jogada não terminou normal (falta parou tudo, reinício): escreve o que tiver.</summary>
        private void Flush()
        {
            if (pending == null) return;
            Write(pending + ";;;" + Field(notices.ToString()));
            pending = null;
        }

        public void Dispose()
        {
            Flush();
            writer?.Dispose();
            writer = null;
        }

        private void Write(string line)
        {
#if UNITY_EDITOR
            try
            {
                if (writer == null)
                {
                    string folder = Path.Combine(Application.dataPath, "..", "Logs", "IA");
                    Directory.CreateDirectory(folder);
                    string path = Path.Combine(folder, $"partida-{DateTime.Now:yyyyMMdd-HHmm}-{FileSafe(level)}.csv");
                    bool exists = File.Exists(path);
                    writer = new StreamWriter(path, true, new UTF8Encoding(true)) { AutoFlush = true };
                    if (!exists) writer.WriteLine(Header);
                    Debug.Log($"[IA] Log da partida: {Path.GetFullPath(path)}");
                }
                writer.WriteLine(line);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[IA] Não deu para escrever o log: {exception.Message}");
                writer = null;
            }
#endif
        }

        private string Describe(AiDecision decision)
        {
            if (decision.Goal == null) return "nenhum objetivo achou jogada, bate reto na bola";
            var text = new StringBuilder();
            text.Append(decision.Goal.Name);
            if (decision.NoGoalAccepted) text.Append(" (nenhum objetivo aceitou)");
            text.Append(" → ").Append(decision.Action.Name);
            if (decision.Action.IsInstant) return text.ToString();
            text.Append(" com ").Append(decision.Candidate.Disc != null ? decision.Candidate.Disc.name : "a bola");
            text.Append(", nota ").Append(decision.Verdict.Score.ToString("0"));
            string risks = Risks(decision.Verdict);
            if (risks.Length > 0) text.Append(" (").Append(risks).Append(')');
            return text.ToString();
        }

        /// <summary>"falta certa" ou as chances com o erro do nível: "falta 29%, perde a bola 43%".</summary>
        private static string Risks(in AiVerdict verdict)
        {
            var text = new StringBuilder();
            if (verdict.Foul) text.Append("falta certa");
            else if (verdict.FoulChance > 0f) text.Append("falta ").Append(Percent(verdict.FoulChance));
            if (verdict.OwnGoalChance > 0f) text.Append(text.Length > 0 ? ", " : string.Empty).Append("gol contra ").Append(Percent(verdict.OwnGoalChance));
            if (verdict.LoseChance > 0f) text.Append(text.Length > 0 ? ", " : string.Empty).Append("perde a bola ").Append(Percent(verdict.LoseChance));
            return text.ToString();
        }

        private static string Percent(float chance) => Mathf.RoundToInt(chance * 100f).ToString(Number) + "%";

        private string SideName(TeamSide? touch) => touch == null ? "ninguem" : touch == side ? "IA" : "jogador";

        private static string Point(Vector2 point) => string.Format(Number, "{0:0.00};{1:0.00}", point.x, point.y);

        /// <summary>Nome do nível sem acento e espaço, para o nome do arquivo ("Difícil" → "Dificil").</summary>
        private static string FileSafe(string text)
        {
            var plain = new StringBuilder();
            foreach (char c in text.Normalize(NormalizationForm.FormD))
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c)) plain.Append(c);
            return plain.Length > 0 ? plain.ToString() : "IA";
        }

        private static string Field(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
    }
}
