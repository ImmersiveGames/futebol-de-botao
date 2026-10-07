using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>Placar final da última partida, lido pela tela de Resultado.</summary>
    public readonly struct MatchResult
    {
        public MatchResult(int bottom, int top)
        {
            Bottom = bottom;
            Top = top;
        }

        public int Bottom { get; }
        public int Top { get; }
    }

    /// <summary>
    /// Dados que passam de uma tela para outra enquanto o jogo está aberto: as opções escolhidas no Menu
    /// e o placar da última partida. As opções são uma cópia em memória do asset padrão, que não é alterado.
    /// Salvar entre sessões fica para a fase 4 (Progression Save).
    /// </summary>
    public static class MatchSession
    {
        private static MatchOptions options;

        public static MatchResult? LastResult { get; set; }

        /// <summary>Opções da sessão; na primeira vez copia <paramref name="defaults"/> (ou o padrão do código).</summary>
        public static MatchOptions Options(MatchOptions defaults)
        {
            if (options != null) return options;
            options = defaults != null ? Object.Instantiate(defaults) : MatchOptions.CreateDefault();
            options.name = "Opções da sessão";
            options.hideFlags = HideFlags.DontSave;
            return options;
        }

        // Com o "Enter Play Mode" sem recarregar o domínio, os estáticos sobrevivem entre execuções no editor.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            options = null;
            LastResult = null;
        }
    }
}
