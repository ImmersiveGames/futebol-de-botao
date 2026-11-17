using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FutebolDeBotao
{
    /// <summary>
    /// Letras ondulando (sobem e descem numa onda da esquerda para a direita) e alternando entre duas cores.
    /// Usado no "GOL!" da mensagem do campo. Desligado, o texto fica como era. Fica antes do <see cref="Outline"/>
    /// no objeto, para o contorno acompanhar as letras.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public sealed class TextWave : BaseMeshEffect
    {
        [Tooltip("Altura da onda, em pixels do texto.")]
        public float amplitude = 7f;
        [Tooltip("Velocidade da onda (radianos por segundo).")]
        public float speed = 9f;
        [Tooltip("Defasagem entre uma letra e a seguinte (radianos).")]
        public float letterPhase = 0.9f;

        private Color first = Color.white;
        private Color second = Color.white;
        private readonly List<UIVertex> vertices = new();

        public void SetColors(Color a, Color b)
        {
            first = a;
            second = b;
            graphic.SetVerticesDirty();
        }

        private void Update()
        {
            // O texto é refeito a cada quadro enquanto ondula.
            if (graphic != null) graphic.SetVerticesDirty();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive()) return;
            vertices.Clear();
            helper.GetUIVertexStream(vertices);
            float time = Time.unscaledTime * speed;
            // Texto legado: 6 vértices (2 triângulos) por letra visível, na ordem do texto.
            for (int letter = 0; letter * 6 < vertices.Count; letter++)
            {
                float lift = Mathf.Sin(time - letter * letterPhase) * amplitude;
                var color = letter % 2 == 0 ? first : second;
                for (int i = letter * 6; i < letter * 6 + 6 && i < vertices.Count; i++)
                {
                    var vertex = vertices[i];
                    vertex.position.y += lift;
                    color.a = vertex.color.a / 255f;
                    vertex.color = color;
                    vertices[i] = vertex;
                }
            }
            helper.Clear();
            helper.AddUIVertexTriangleStream(vertices);
        }
    }
}
