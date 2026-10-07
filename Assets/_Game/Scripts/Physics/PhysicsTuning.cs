using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>
    /// Números de física do protótipo. Ajuste com o jogo rodando para achar a sensação do peteleco.
    /// </summary>
    [CreateAssetMenu(menuName = "Futebol de Botão/Physics Tuning", fileName = "PhysicsTuning")]
    public sealed class PhysicsTuning : ScriptableObject
    {
        [Header("Botão")]
        [Min(0.01f)] public float discMass = 1f;
        [Min(0f)] public float discLinearDamping = 2.2f;
        [Min(0f)] public float discAngularDamping = 4f;

        [Header("Bola")]
        [Min(0.01f)] public float ballMass = 0.35f;
        [Min(0f)] public float ballLinearDamping = 1.4f;

        [Header("Material")]
        [Range(0f, 1f)] public float bounciness = 0.75f;
        [Min(0f)] public float friction = 0.1f;

        [Header("Peteleco")]
        [Tooltip("Impulso máximo aplicado ao botão com a força no máximo.")]
        [Min(0f)] public float maxImpulse = 9f;
        [Tooltip("Distância do arrasto (em unidades do mundo) que corresponde à força máxima.")]
        [Min(0.1f)] public float maxDragDistance = 2.5f;
        [Tooltip("Arrastos menores que isso cancelam o peteleco.")]
        [Min(0f)] public float minDragDistance = 0.15f;

        [Header("Paredes (elástico)")]
        [Tooltip("Multiplica a velocidade com que bola e botões saem da parede. 1 = sem ganho.")]
        [Min(0f)] public float wallBounceBoost = 1.25f;
        [Tooltip("Velocidade mínima de saída depois de bater na parede.")]
        [Min(0f)] public float wallMinBounceSpeed = 1.2f;
        [Tooltip("Velocidade com que a parede empurra de volta quem fica encostado nela.")]
        [Min(0f)] public float wallPushSpeed = 0.8f;

        [Header("Fim do movimento")]
        [Tooltip("Velocidade abaixo da qual um corpo é considerado parado.")]
        [Min(0f)] public float restSpeed = 0.05f;
        [Tooltip("Tempo que todos precisam ficar parados para o turno acabar.")]
        [Min(0f)] public float restTime = 0.3f;

        [Header("Goleiro")]
        [Min(0f)] public float goalkeeperSpeed = 3f;
    }
}
