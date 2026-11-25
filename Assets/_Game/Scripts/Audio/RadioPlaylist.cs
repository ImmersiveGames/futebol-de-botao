using System;
using System.Collections.Generic;
using Immersive.Audio.Authoring;
using UnityEngine;

namespace FutebolDeBotao
{
    /// <summary>Como uma música do rádio é liberada.</summary>
    public enum RadioUnlock
    {
        /// <summary>Já vem liberada.</summary>
        Free,
        /// <summary>Ganhar um torneio (qualquer tamanho, qualquer nível).</summary>
        TournamentTitle,
        /// <summary>Ganhar uma liga.</summary>
        LeagueTitle
    }

    /// <summary>Uma música do rádio da partida.</summary>
    [Serializable]
    public sealed class RadioTrack
    {
        [Tooltip("Identificador gravado no save (não muda depois de publicado).")]
        public string id;
        [Tooltip("Nome mostrado no jogo (texto em português; a tradução vem de Resources/Idiomas).")]
        public string displayName;
        public AudioBgmCueAsset cue;
        public RadioUnlock unlock = RadioUnlock.Free;
    }

    /// <summary>
    /// Músicas do rádio da partida, na ordem do seletor (Resources/Audio/Radio). O "Criar áudio" monta a lista com os cues
    /// das músicas em Assets/_Game/Audio/Musicas; depois o Inspector pode mudar nome, ordem e desbloqueio.
    /// </summary>
    [CreateAssetMenu(fileName = "Radio", menuName = "Futebol de Botão/Rádio")]
    public sealed class RadioPlaylist : ScriptableObject
    {
        public const string ResourcePath = "Audio/Radio";

        public List<RadioTrack> tracks = new();
    }
}
