using UnityEngine;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Texto que não cabe na caixa diminui a fonte sozinho (até <see cref="MinRatio"/> do tamanho), em vez de estourar o
    /// botão ou o rótulo. Quem cabe continua do tamanho escrito. Pensado para as traduções maiores que o português.
    /// </summary>
    public static class TextFit
    {
        public const float MinRatio = 0.6f;

        public static Text Shrink(Text text)
        {
            if (text == null) return null;
            text.resizeTextMaxSize = text.fontSize;
            text.resizeTextMinSize = Mathf.Max(10, Mathf.RoundToInt(text.fontSize * MinRatio));
            text.resizeTextForBestFit = true;
            return text;
        }
    }
}
