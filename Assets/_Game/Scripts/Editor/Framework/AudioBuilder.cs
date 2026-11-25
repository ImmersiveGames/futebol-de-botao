using System.Collections.Generic;
using System.IO;
using Immersive.Audio.Authoring;
using Immersive.Audio.Contracts;
using Immersive.Audio.Unity.Hosts;
using Immersive.Framework.Audio;
using Immersive.Framework.Authoring;
using Immersive.Framework.RouteLifecycle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FutebolDeBotao.Editor
{
    /// <summary>
    /// Áudio com o com.immersive.audio e o BGM do framework:
    /// cues dos sons em Assets/_Game/Audio/Cues, o Audio Runtime (AudioRuntimeHost + BGM Director) no Persistent Content,
    /// a música de cada Route (Route BGM Binding) e os sons da partida (<see cref="MatchSounds"/>).
    /// "Criar áudio" gera os sons provisórios que faltam (<see cref="ChiptuneSounds"/>), cria os assets e o Audio Runtime e
    /// depois roda "Criar telas", que liga a música e os sons nas cenas.
    /// Músicas de verdade ficam em Assets/_Game/Audio/Musicas: samba_menu.wav e batucada_partida.wav (se existirem) entram no
    /// lugar das provisórias do menu e da partida, e as do rádio (<see cref="RadioSongs"/>) viram cues radio_* e a lista
    /// Resources/Audio/Radio (<see cref="RadioPlaylist"/>), que o <see cref="MatchRadio"/> toca na partida.
    /// </summary>
    public static class AudioBuilder
    {
        private const string AudioFolder = "Assets/_Game/Audio";
        private const string ClipsFolder = AudioFolder + "/Sons";
        private const string CuesFolder = AudioFolder + "/Cues";
        private const string MusicFolder = AudioFolder + "/Musicas";
        private const string PlaylistPath = "Assets/_Game/Resources/" + RadioPlaylist.ResourcePath + ".asset";
        private const string MenuMusicFile = "samba_menu";
        private const string MatchMusicFile = "batucada_partida";
        private const string DefaultsPath = AudioFolder + "/PadroesDeAudio.asset";
        private const string PersistentScenePath = "Assets/_Game/Scenes/PersistentContent.unity";
        private const string AudioRuntimeName = "Áudio";

        public const string MenuMusic = "musica_menu";
        public const string MatchMusic = "musica_partida";

        // Sons da partida e o clique dos botões: nome do arquivo em Sons/, volume.
        private static readonly (string clip, float volume)[] SfxCues =
        {
            ("peteleco", 0.8f),
            ("batida_fraca", 0.6f),
            ("batida_forte", 0.9f),
            ("bola_parede", 0.7f),
            ("apito", 0.7f),
            ("apito_final", 0.8f),
            ("gol", 1f),
            ("fogos", 1f),
            ("cartao", 0.8f),
            ("clique", 0.6f)
        };

        // Músicas do rádio, na ordem do seletor (depois da Batucada, que é a música da partida): arquivo em Musicas/
        // (= id gravado no save), nome mostrado, desbloqueio.
        private static readonly (string file, string name, RadioUnlock unlock)[] RadioSongs =
        {
            ("rock", "Rock", RadioUnlock.LeagueTitle),
            ("funk", "Funk", RadioUnlock.Free),
            ("sertanejo", "Sertanejo", RadioUnlock.Free),
            ("frevo", "Frevo", RadioUnlock.TournamentTitle),
            ("forro", "Forró", RadioUnlock.Free),
            ("axe", "Axé", RadioUnlock.Free),
            ("carimbo", "Carimbó", RadioUnlock.Free),
            ("boi", "Toada de boi", RadioUnlock.Free)
        };

        [MenuItem("Futebol de Botão/Criar áudio")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var defaults = CreateAssets();
            if (defaults == null) return;
            if (!SetupPersistentContent(defaults)) return;
            Debug.Log("[Futebol de Botão] Áudio criado (cues e Audio Runtime no Persistent Content). Ligando música e sons nas telas...");
            ScreensBuilder.Build();
        }

        private static AudioDefaultsAsset CreateAssets()
        {
            Directory.CreateDirectory(CuesFolder);
            // Sons provisórios (chiptune) para os que ainda não existem; um som de verdade com o mesmo nome fica.
            int created = ChiptuneSounds.WriteMissing(ClipsFolder);
            AssetDatabase.Refresh();
            if (created > 0)
            {
                ConfigureImports();
                Debug.Log($"[Futebol de Botão] {created} sons provisórios criados em {ClipsFolder}. Faça o commit dos .wav e .meta.");
            }

            var defaults = LoadOrCreate<AudioDefaultsAsset>(DefaultsPath);
            if (defaults == null)
            {
                Debug.LogError($"[Futebol de Botão] Não consegui criar {DefaultsPath}.");
                return null;
            }

            foreach (var (clip, volume) in SfxCues)
            {
                var cue = Cue<AudioSfxCueAsset>(clip, "sfx", volume, AudioBusKeys.Sfx);
                if (cue == null) return null;
                Set(cue, so =>
                {
                    so.FindProperty("executionMode").intValue = (int)AudioSfxExecutionMode.Direct;
                    so.FindProperty("playbackMode").intValue = (int)AudioPlaybackMode.Global;
                    so.FindProperty("loopMode").intValue = (int)AudioLoopMode.Off;
                });
            }

            ConfigureMusicImports();
            foreach (var (music, file) in new[] { (MenuMusic, MenuMusicFile), (MatchMusic, MatchMusicFile) })
            {
                // A música de verdade (Musicas/) no lugar da provisória (Sons/), quando já está no projeto.
                var real = AssetDatabase.LoadAssetAtPath<AudioClip>($"{MusicFolder}/{file}.wav");
                if (BgmCue(music, real, radio: music == MatchMusic) == null) return null;
            }
            BuildRadio();

            AssetDatabase.SaveAssets();
            return defaults;
        }

        /// <summary>Cue de música em loop. As do rádio (e a Batucada, que também está nele) somem e entram mais devagar ao trocar.</summary>
        private static AudioBgmCueAsset BgmCue(string cueName, AudioClip clip = null, bool radio = false)
        {
            var cue = Cue<AudioBgmCueAsset>(cueName, "bgm", 0.5f, AudioBusKeys.Bgm, clip);
            if (cue == null) return null;
            Set(cue, so =>
            {
                so.FindProperty("loopMode").intValue = (int)AudioLoopMode.On;
                so.FindProperty("fadeInSeconds").floatValue = radio ? 1.2f : 0.8f;
                so.FindProperty("fadeOutSeconds").floatValue = radio ? 1.5f : 0.6f;
            });
            return cue;
        }

        /// <summary>
        /// Lista do rádio: a Batucada (cue da partida) e as músicas de <see cref="RadioSongs"/> que já estão em Musicas/.
        /// Itens já na lista mantêm nome, ordem e desbloqueio do Inspector; só os cues são religados e os novos entram no fim.
        /// </summary>
        private static void BuildRadio()
        {
            var playlist = LoadOrCreate<RadioPlaylist>(PlaylistPath);
            var wanted = new List<(string id, string name, RadioUnlock unlock, AudioBgmCueAsset cue)>
            {
                ("batucada", "Batucada", RadioUnlock.Free, LoadCue<AudioBgmCueAsset>(MatchMusic))
            };
            var missing = new List<string>();
            foreach (var (file, songName, unlock) in RadioSongs)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{MusicFolder}/{file}.wav");
                if (clip == null)
                {
                    missing.Add(file);
                    continue;
                }
                wanted.Add((file, songName, unlock, BgmCue("radio_" + file, clip, radio: true)));
            }
            foreach (var (id, songName, unlock, cue) in wanted)
            {
                var track = playlist.tracks.Find(t => t != null && t.id == id);
                if (track == null)
                {
                    track = new RadioTrack { id = id, displayName = songName, unlock = unlock };
                    playlist.tracks.Add(track);
                }
                track.cue = cue;
            }
            EditorUtility.SetDirty(playlist);
            if (missing.Count > 0)
                Debug.LogWarning($"[Futebol de Botão] Rádio: faltam em {MusicFolder} as músicas {string.Join(", ", missing)} (.wav). Elas ficam fora até chegarem.");
        }

        /// <summary>Músicas de verdade: estéreo, lidas aos poucos do disco (são várias e longas).</summary>
        private static void ConfigureMusicImports()
        {
            if (!AssetDatabase.IsValidFolder(MusicFolder)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { MusicFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not AudioImporter importer) continue;
                var settings = importer.defaultSampleSettings;
                if (!importer.forceToMono && settings.loadType == AudioClipLoadType.Streaming &&
                    settings.compressionFormat == AudioCompressionFormat.Vorbis) continue;
                importer.forceToMono = false;
                importer.loadInBackground = true;
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.6f;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }

        /// <summary>Cue com o som de Sons/ com o mesmo nome, ou com <paramref name="clipOverride"/> (música de Musicas/).</summary>
        private static T Cue<T>(string clipName, string kind, float volume, string bus, AudioClip clipOverride = null) where T : AudioCueAsset
        {
            var clip = clipOverride != null ? clipOverride : AssetDatabase.LoadAssetAtPath<AudioClip>($"{ClipsFolder}/{clipName}.wav");
            if (clip == null)
            {
                Debug.LogError($"[Futebol de Botão] Faltou o som {ClipsFolder}/{clipName}.wav.");
                return null;
            }

            // Volume só no cue novo: depois quem manda é o Inspector (o Criar áudio não desfaz o ajuste).
            bool created = AssetDatabase.LoadAssetAtPath<T>(CuePath(clipName)) == null;
            var cue = LoadOrCreate<T>(CuePath(clipName));
            Set(cue, so =>
            {
                so.FindProperty("cueId").stringValue = $"{kind}.futebol.{clipName}";
                so.FindProperty("clip").objectReferenceValue = clip;
                if (created) so.FindProperty("volume").floatValue = volume;
                so.FindProperty("pitch").floatValue = 1f;
                so.FindProperty("routingBus").stringValue = bus;
            });
            return cue;
        }

        /// <summary>Mono; a música fica comprimida na memória e carrega em segundo plano.</summary>
        private static void ConfigureImports()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { ClipsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not AudioImporter importer) continue;
                bool music = Path.GetFileNameWithoutExtension(path).StartsWith("musica");
                importer.forceToMono = true;
                importer.loadInBackground = music;
                var settings = importer.defaultSampleSettings;
                settings.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = music ? 0.7f : 1f;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }

        private static string CuePath(string clipName) => $"{CuesFolder}/{clipName}.asset";

        private static T LoadCue<T>(string clipName) where T : AudioCueAsset => AssetDatabase.LoadAssetAtPath<T>(CuePath(clipName));

        /// <summary>Audio Runtime no Persistent Content: toca a música entre as telas e os sons da partida.</summary>
        private static bool SetupPersistentContent(AudioDefaultsAsset defaults)
        {
            var scene = EditorSceneManager.OpenScene(PersistentScenePath, OpenSceneMode.Single);

            GameObject root = null;
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == AudioRuntimeName) root = go;
            if (root == null)
            {
                root = new GameObject(AudioRuntimeName);
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            var host = root.GetComponent<AudioRuntimeHost>();
            if (host == null) host = root.AddComponent<AudioRuntimeHost>();
            Set(host, so =>
            {
                so.FindProperty("defaults").objectReferenceValue = defaults;
                so.FindProperty("playbackRoot").objectReferenceValue = root.transform;
                so.FindProperty("ensurePersistentListener").boolValue = true;
            });

            var director = root.GetComponent<FrameworkBgmDirector>();
            if (director == null) director = root.AddComponent<FrameworkBgmDirector>();
            Set(director, so => so.FindProperty("audioRuntimeHost").objectReferenceValue = host);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // Sem os padrões o host não toca nada (FailedMissingDefaults).
            if (host.Defaults == null)
            {
                Debug.LogError($"[Futebol de Botão] O Audio Runtime ficou sem os padrões. Arraste {DefaultsPath} no campo Defaults do objeto \"{AudioRuntimeName}\" no Persistent Content.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Música da Route desta cena (o Director do Persistent Content é injetado pelo framework). O binding precisa estar
        /// num Route Content Contribution da Route da cena: sem ele o framework não avisa a entrada na Route e a música não toca.
        /// </summary>
        public static void AddRouteMusic(Transform parent, string music, RouteAsset route)
        {
            var cue = LoadCue<AudioBgmCueAsset>(music);
            if (cue == null) return; // "Criar áudio" ainda não rodou.

            var go = new GameObject("Música");
            if (parent != null) go.transform.SetParent(parent, false);
            if (go.GetComponentInParent<RouteContentContribution>(true) == null)
            {
                if (route == null) Debug.LogWarning($"[Futebol de Botão] Música '{music}' sem Route: ela não vai tocar.");
                var content = go.AddComponent<RouteContentContribution>();
                Set(content, so =>
                {
                    so.FindProperty("route").objectReferenceValue = route;
                    so.FindProperty("localContentId").stringValue = $"musica.{music}";
                });
            }
            var binding = go.AddComponent<RouteBgmAuthoring>();
            Set(binding, so =>
            {
                so.FindProperty("routeBgm").objectReferenceValue = cue;
                so.FindProperty("policy").intValue = (int)FrameworkBgmRoutePolicy.PlayOwn;
            });
            AddUiSounds(parent);
        }

        /// <summary>Clique dos botões da cena (vai junto com a música, que toda cena tem).</summary>
        private static void AddUiSounds(Transform parent)
        {
            var click = LoadCue<AudioSfxCueAsset>("clique");
            if (click == null) return;
            var go = new GameObject("Sons dos botões");
            if (parent != null) go.transform.SetParent(parent, false);
            go.AddComponent<UiSounds>().Configure(click);
        }

        /// <summary>Música e sons da partida.</summary>
        public static void AddMatchAudio(Transform parent, MatchController match, RouteAsset route)
        {
            AddRouteMusic(parent, MatchMusic, route);

            // Rádio: troca a música da Route pela escolhida nas Configurações (lista em Resources/Audio/Radio).
            var radio = new GameObject("Rádio");
            radio.transform.SetParent(parent, false);
            radio.AddComponent<MatchRadio>().Configure(LoadCue<AudioBgmCueAsset>(MatchMusic));

            var flick = LoadCue<AudioSfxCueAsset>("peteleco");
            if (flick == null) return;
            var go = new GameObject("Sons da partida");
            go.transform.SetParent(parent, false);
            go.AddComponent<MatchSounds>().Configure(match,
                flick,
                LoadCue<AudioSfxCueAsset>("batida_fraca"),
                LoadCue<AudioSfxCueAsset>("batida_forte"),
                LoadCue<AudioSfxCueAsset>("bola_parede"),
                LoadCue<AudioSfxCueAsset>("apito"),
                LoadCue<AudioSfxCueAsset>("apito_final"),
                LoadCue<AudioSfxCueAsset>("gol"),
                LoadCue<AudioSfxCueAsset>("cartao"),
                LoadCue<AudioSfxCueAsset>("fogos"));
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void Set(Object target, System.Action<SerializedObject> configure)
        {
            var so = new SerializedObject(target);
            configure(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
