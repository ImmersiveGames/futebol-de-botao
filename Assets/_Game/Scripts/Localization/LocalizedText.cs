using UnityEngine;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Texto fixo de tela (o que o "Criar telas" ou a própria tela escreveu em português): mostra no idioma em uso e troca
    /// quando o idioma muda. Se o código trocar o texto depois (um valor, um placar), este componente deixa quieto.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [Tooltip("O texto em português (id em Resources/Idiomas/textos.json). Vazio: o que estiver no Text.")]
        [SerializeField] private string key;

        private Text text;
        private string applied;

        /// <summary>Põe no texto já escrito em português (telas montadas por código ou pelo "Criar telas").</summary>
        public static LocalizedText Attach(Text target)
        {
            if (target == null) return null;
            if (!target.TryGetComponent(out LocalizedText localized)) localized = target.gameObject.AddComponent<LocalizedText>();
            localized.key = Txt.IdOf(target.text);
            localized.text = target;
            localized.applied = target.text;
            localized.Apply();
            return localized;
        }

        private void Awake()
        {
            text = GetComponent<Text>();
            if (string.IsNullOrEmpty(key)) key = text.text;
            applied ??= text.text;
        }

        private void OnEnable()
        {
            Txt.Changed += Apply;
            Apply();
        }

        private void OnDisable() => Txt.Changed -= Apply;

        private void Apply()
        {
            if (text == null) text = GetComponent<Text>();
            if (text == null || string.IsNullOrEmpty(key)) return;
            // O código escreveu outra coisa (valor dinâmico): não é mais o texto fixo.
            if (text.text != applied && text.text != key) return;
            applied = Txt.Get(key);
            text.text = applied;
        }
    }
}
