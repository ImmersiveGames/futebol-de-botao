using System;
using System.Collections.Generic;
using System.IO;
using Immersive.Framework.ActivityFlow;
using Immersive.Framework.ActivityRestart;
using Immersive.Framework.Authoring;
using Immersive.Framework.GameFlow;
using Immersive.Framework.Pause;
using Immersive.Framework.PlayerParticipation;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FutebolDeBotao.Editor
{
    /// <summary>
    /// Monta o fluxo de telas da fase 2 sobre o Immersive Framework:
    /// Routes e cenas de Abertura, Menu e Resultado; na Partida, a tela de pausa, os triggers do framework
    /// e a Activity Content Contribution que faz a partida começar quando a Activity "Jogo" entra.
    /// Rode depois de "Criar cena da partida". Pode rodar de novo: as telas são recriadas.
    /// </summary>
    public static class ScreensBuilder
    {
        private const string AppFolder = "Assets/_Game/App";
        private const string ScenesFolder = "Assets/_Game/Scenes";
        private const string PersistentScenePath = ScenesFolder + "/PersistentContent.unity";
        private const string TitleScenePath = ScenesFolder + "/Abertura.unity";
        private const string MenuScenePath = ScenesFolder + "/Menu.unity";
        private const string MatchScenePath = ScenesFolder + "/Partida.unity";
        private const string ResultScenePath = ScenesFolder + "/Resultado.unity";
        private const string OptionsPath = "Assets/_Game/Data/OpcoesDaPartida.asset";
        private const string CircleSpritePath = "Assets/_Game/Art/Placeholder/Circle.png";
        private const string FieldSheetPath = "Assets/_Game/Art/Campo/Field_Sprites.png";
        private const string FieldMarksPath = "Assets/_Game/Art/Campo/Field_Marks.png";
        private const string BallSheetPath = "Assets/_Game/Art/Ball/Ball_SpriteSheet.png";
        /// <summary>Logo da Immersive só com o desenho (sem o texto), no canto da Abertura.</summary>
        private const string StudioLogoPath = "Assets/_Game/Art/logov3IconWhite.png";
        // Ícone do jogo em cima do título da Abertura (posto pelo usuário na cena em 2026-10-19).
        private const string GameLogoPath = "Assets/_Game/Art/Logo.png";
        /// <summary>Altura do logo na tela de referência (720 x 1280) e distância das bordas.</summary>
        private const float StudioLogoHeight = 84f;
        private const float StudioLogoMargin = 24f;
        private const string BrightFeltSprite = "Field_Braight";
        private const string DarkFeltSprite = "Field_Dark";

        private const string MatchSystemsName = "Partida (sistemas)";
        private const string MatchFrameworkName = "Framework (telas)";
        private const string PauseCanvasName = "Tela de pausa";
        private const string MatchHudName = "HUD da partida";

        /// <summary>Nome do jogo na Abertura e no Menu.</summary>
        private const string GameTitle = "Pro Gol!";
        private static readonly Vector2 ReferenceResolution = new(720f, 1280f);
        private static readonly Color Background = new(0.08f, 0.2f, 0.11f);
        private static readonly Color PanelColor = new(0f, 0f, 0f, 0.75f);
        private static readonly Color ButtonColor = new(0.95f, 0.95f, 0.9f);
        private static readonly Color TextColor = Color.white;
        private static readonly Color ButtonTextColor = new(0.1f, 0.1f, 0.1f);
        private static readonly Color TextShadowColor = new(0f, 0f, 0f, 0.6f);
        private static readonly Vector2 TextShadowDistance = new(3f, -3f);

        private static Font font;

        [MenuItem("Futebol de Botão/Criar telas")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            font = GameFont.Get();
            TeamsBuilder.CreateAssets();
            ButtonSpritesImport.Configure();

            var app = FindGameApplication();
            var matchRoute = FindMatchRoute();
            if (app == null || matchRoute == null) return;

            var activity = matchRoute.StartupActivity;
            if (activity == null)
            {
                Debug.LogError($"[Futebol de Botão] A Route '{matchRoute.name}' não tem Startup Activity. Ligue a Activity \"Jogo\" nela.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<MatchOptions>(OptionsPath) == null)
                Debug.LogWarning($"[Futebol de Botão] Não achei {OptionsPath}; o Menu vai usar as opções padrão do código.");

            // Trocar de cena descarrega assets que nenhuma cena usa, e a referência C# vira nula.
            // Por isso cada cena recarrega os assets pelo caminho (Load<T>) logo antes de usar.
            string appPath = AssetDatabase.GetAssetPath(app);
            string matchRoutePath = AssetDatabase.GetAssetPath(matchRoute);
            string activityPath = AssetDatabase.GetAssetPath(activity);
            string titleRoutePath = CreateRoute("Abertura", TitleScenePath);
            string menuRoutePath = CreateRoute("Menu", MenuScenePath);
            string resultRoutePath = CreateRoute("Resultado", ResultScenePath);
            AssetDatabase.SaveAssets();

            BuildTitleScene(menuRoutePath, titleRoutePath);
            BuildMenuScene(matchRoutePath, menuRoutePath);
            BuildResultScene(matchRoutePath, menuRoutePath, resultRoutePath);
            if (!SetupMatchScene(activityPath, resultRoutePath, menuRoutePath, matchRoutePath)) return;

            SetStartupRoute(Load<GameApplicationAsset>(appPath), Load<RouteAsset>(titleRoutePath));
            UpdateBuildScenes();
            AssetDatabase.SaveAssets();

            EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            Debug.Log("[Futebol de Botão] Telas criadas: Abertura → Menu (Como jogar, Opções, Seleção de times) → Partida → Resultado. " +
                      "Startup Route = 'Abertura'. Cenas adicionadas na lista da build.");
        }

        // ---- Assets do framework ----

        private static GameApplicationAsset FindGameApplication()
        {
            var guids = AssetDatabase.FindAssets("t:GameApplicationAsset");
            if (guids.Length == 0)
            {
                Debug.LogError("[Futebol de Botão] Nenhuma Game Application encontrada.");
                return null;
            }
            if (guids.Length > 1) Debug.LogWarning("[Futebol de Botão] Há mais de uma Game Application; usei a primeira encontrada.");
            return AssetDatabase.LoadAssetAtPath<GameApplicationAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static RouteAsset FindMatchRoute()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:RouteAsset"))
            {
                var route = AssetDatabase.LoadAssetAtPath<RouteAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (route != null && route.PrimaryScenePath == MatchScenePath) return route;
            }
            Debug.LogError($"[Futebol de Botão] Nenhuma Route com a cena {MatchScenePath}.");
            return null;
        }

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Debug.LogError($"[Futebol de Botão] Não consegui carregar {path}.");
            return asset;
        }

        /// <summary>Cria (ou atualiza) a Route e devolve o caminho do asset.</summary>
        private static string CreateRoute(string routeName, string scenePath)
        {
            string path = $"{AppFolder}/{routeName}.asset";
            var route = AssetDatabase.LoadAssetAtPath<RouteAsset>(path);
            if (route == null)
            {
                route = ScriptableObject.CreateInstance<RouteAsset>();
                AssetDatabase.CreateAsset(route, path);
            }

            var so = new SerializedObject(route);
            var id = so.FindProperty("routeId");
            if (string.IsNullOrWhiteSpace(id.stringValue)) id.stringValue = Guid.NewGuid().ToString("N");
            so.FindProperty("routeName").stringValue = routeName;
            so.FindProperty("primaryScenePath").stringValue = scenePath;
            so.FindProperty("primarySceneName").stringValue = Path.GetFileNameWithoutExtension(scenePath);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(route);
            return path;
        }

        private static void SetStartupRoute(GameApplicationAsset app, RouteAsset route)
        {
            var so = new SerializedObject(app);
            so.FindProperty("startupRoute").objectReferenceValue = route;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(app);
        }

        private static void UpdateBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var path in new[] { PersistentScenePath, TitleScenePath, MenuScenePath, MatchScenePath, ResultScenePath })
            {
                int index = scenes.FindIndex(s => s.path == path);
                if (index < 0) scenes.Add(new EditorBuildSettingsScene(path, true));
                else if (!scenes[index].enabled) scenes[index] = new EditorBuildSettingsScene(path, true);
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static T AddTrigger<T>(Transform parent, string objectName, Action<SerializedObject> configure) where T : Component
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            var trigger = go.AddComponent<T>();
            var so = new SerializedObject(trigger);
            configure(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            return trigger;
        }

        private static RouteRequestTrigger AddRouteTrigger(Transform parent, string objectName, string routePath, string reason)
        {
            var target = Load<RouteAsset>(routePath);
            var trigger = AddTrigger<RouteRequestTrigger>(parent, objectName, so =>
            {
                so.FindProperty("targetRoute").objectReferenceValue = target;
                so.FindProperty("reason").stringValue = reason;
            });
            if (trigger.TargetRoute == null)
                Debug.LogError($"[Futebol de Botão] '{objectName}' ficou sem Target Route ({routePath}).", trigger);
            return trigger;
        }

        // ---- Cenas ----

        private static void BuildTitleScene(string menuRoute, string ownRoute)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var canvas = CreateCanvas("Abertura", 0);
            CreateFieldBackground(canvas);

            CreateText(canvas, "Título", GameTitle, 72, new Vector2(0f, 160f), new Vector2(680f, 120f), FontStyle.Bold);
            var prompt = CreateText(canvas, "Aperte", TitleScreen.PromptText, 36, new Vector2(0f, -200f), new Vector2(680f, 120f));

            CreateStudioLogo(canvas);
            CreateGameLogo(canvas);

            var trigger = AddRouteTrigger(canvas, "Ir para o Menu", menuRoute, "abertura.menu");
            canvas.gameObject.AddComponent<TitleScreen>().Configure(trigger, prompt);

            AudioBuilder.AddRouteMusic(null, AudioBuilder.MenuMusic, Load<RouteAsset>(ownRoute));
            EditorSceneManager.SaveScene(scene, TitleScenePath);
        }

        private static void BuildMenuScene(string matchRoute, string ownRoute)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var options = AssetDatabase.LoadAssetAtPath<MatchOptions>(OptionsPath);
            var canvas = CreateCanvas("Menu", 0);
            CreateFieldBackground(canvas);
            var menu = canvas.gameObject.AddComponent<MenuScreen>();

            // Primeira tela do Menu: o título, o ícone e o logo da Immersive como na Abertura e, embaixo, Um jogador (contra a IA) ou Dois jogadores.
            var modePanel = CreateGroup(canvas, "Como jogar");
            CreateText(modePanel, "Título", GameTitle, 72, new Vector2(0f, 160f), new Vector2(680f, 120f), FontStyle.Bold);
            var onePlayer = CreateButton(modePanel, "Um jogador", "Um jogador", new Vector2(0f, -180f), new Vector2(420f, 90f), menu.PlayOnePlayer, ButtonStyle.Yellow);
            var twoPlayers = CreateButton(modePanel, "Dois jogadores", "Dois jogadores", new Vector2(0f, -300f), new Vector2(420f, 90f), menu.PlayTwoPlayers, ButtonStyle.Yellow);
            var quit = CreateButton(modePanel, "Sair", "Sair", new Vector2(0f, -420f), new Vector2(420f, 90f), menu.QuitApp, ButtonStyle.Red);
            quit.gameObject.AddComponent<HideOnWeb>();
            AddNavigator(modePanel, null, Item(onePlayer), Item(twoPlayers), Item(quit));
            CreateStudioLogo(modePanel);
            CreateGameLogo(modePanel);

            // Opções da partida (GDD), depois do modo; "Avançar" abre a Seleção de times
            var optionsPanel = CreateGroup(canvas, "Opções da partida");
            CreateText(optionsPanel, "Título", "Opções da partida", 52, new Vector2(0f, 440f), new Vector2(680f, 90f), FontStyle.Bold);
            var modeLabel = CreateText(optionsPanel, "Modo", "-", 30, new Vector2(0f, 370f), new Vector2(680f, 50f));
            var kinds = (MatchOptionKind[])Enum.GetValues(typeof(MatchOptionKind));
            var values = new Text[kinds.Length];
            var rows = new RectTransform[kinds.Length];
            var optionItems = new List<MenuEntry>();
            for (int i = 0; i < kinds.Length; i++)
            {
                float y = 290f - i * MenuScreen.OptionRowSpacing;
                var row = CreateGroup(optionsPanel, MenuScreen.Label(kinds[i]));
                rows[i] = (RectTransform)row;
                var name = CreateText(row, "Nome", MenuScreen.Label(kinds[i]), 30, new Vector2(-170f, y), new Vector2(300f, 70f), FontStyle.Normal, TextAnchor.MiddleLeft);
                var previous = CreateIntButton(row, "Anterior", "<", new Vector2(40f, y), new Vector2(70f, 70f), menu.Previous, i);
                values[i] = CreateText(row, "Valor", "-", 30, new Vector2(165f, y), new Vector2(170f, 70f), FontStyle.Bold);
                var next = CreateIntButton(row, "Próximo", ">", new Vector2(290f, y), new Vector2(70f, 70f), menu.Next, i);
                // Teclado/controle: esquerda/direita apertam as setas da linha.
                var item = row.gameObject.AddComponent<MenuEntry>();
                item.Configure(null, previous, next, name.rectTransform, (RectTransform)previous.transform,
                    values[i].rectTransform, (RectTransform)next.transform);
                optionItems.Add(item);
            }
            var optionsBack = CreateButton(optionsPanel, "Voltar", "Voltar", new Vector2(-140f, -450f), new Vector2(240f, 70f), menu.BackFromOptions, ButtonStyle.Red);
            var optionsNext = CreateButton(optionsPanel, "Avançar", "Avançar", new Vector2(140f, -450f), new Vector2(240f, 70f), menu.ShowSelection, ButtonStyle.Yellow);
            optionItems.Add(Item(optionsBack));
            optionItems.Add(Item(optionsNext));
            // Só o Jogador 1 mexe nas Opções.
            optionsPanel.gameObject.AddComponent<MenuNavigator>().Configure(optionItems.ToArray(), optionsBack, 0, 1);
            optionsPanel.gameObject.SetActive(false);

            // Controles: cada jogador escolhe o dispositivo (só com mais de um). Monta a UI sozinha ao abrir.
            var devicesPanel = CreateGroup(canvas, "Controles");
            var devices = devicesPanel.gameObject.AddComponent<DeviceSelectionScreen>();
            devices.Configure(menu);
            menu.SetDevices(devices);
            devicesPanel.gameObject.SetActive(false);

            // Seleção de times no campo desenhado ("Jogar" vai para a Partida). Monta a UI sozinha no Awake.
            var selectionPanel = CreateGroup(canvas, "Seleção de times");
            var selection = selectionPanel.gameObject.AddComponent<TeamSelectionScreen>();
            selection.Configure(menu, options, AssetDatabase.LoadAssetAtPath<Sprite>(CircleSpritePath), LoadTableSprites());
            menu.SetSelection(selection);
            selectionPanel.gameObject.SetActive(false);

            var trigger = AddRouteTrigger(canvas, "Ir para a Partida", matchRoute, "menu.iniciar");
            menu.Configure(options, trigger, modePanel.gameObject, optionsPanel.gameObject, modeLabel, values, rows);

            // Jogadores: o Menu tira todo mundo e faz o Join de quem vai jogar (escopo Route).
            var players = new GameObject("Jogadores");
            var menuObserver = AddObserver(players, LocalPlayerProvisioningConsumerScope.Route);
            var menuJoin = AddJoin(players, LocalPlayerProvisioningConsumerScope.Route);
            var menuLeaves = AddPerSlot<PlayerSessionLeaveCommandTrigger>(players.transform, "Leave", LocalPlayerProvisioningConsumerScope.Route, "menu.trocar_modo");
            var menuPlayers = players.AddComponent<MenuPlayers>();
            menuPlayers.Configure(menuObserver, menuJoin, menuLeaves);
            menu.SetPlayers(menuPlayers);

            AudioBuilder.AddRouteMusic(null, AudioBuilder.MenuMusic, Load<RouteAsset>(ownRoute));
            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }

        private static void BuildResultScene(string matchRoute, string menuRoute, string ownRoute)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var canvas = CreateCanvas("Resultado", 0);
            CreateFieldBackground(canvas);
            var result = canvas.gameObject.AddComponent<ResultScreen>();

            CreateText(canvas, "Título", "Fim de jogo", 64, new Vector2(0f, 320f), new Vector2(680f, 110f), FontStyle.Bold);
            var score = CreateText(canvas, "Placar", "Azul 0 x 0 Vermelho", 48, new Vector2(0f, 160f), new Vector2(680f, 90f), FontStyle.Bold);
            var verdict = CreateText(canvas, "Vencedor", "Empate!", 38, new Vector2(0f, 60f), new Vector2(680f, 80f));
            var again = CreateButton(canvas, "Jogar de novo", "Jogar de novo", new Vector2(0f, -120f), new Vector2(420f, 90f), result.PlayAgain, ButtonStyle.Yellow);
            var toMenu = CreateButton(canvas, "Menu", "Menu", new Vector2(0f, -240f), new Vector2(420f, 90f), result.BackToMenu, ButtonStyle.Red);
            AddNavigator(canvas, toMenu, Item(again), Item(toMenu));

            var againRoute = AddRouteTrigger(canvas, "Ir para a Partida", matchRoute, "resultado.jogar_de_novo");
            var toMenuRoute = AddRouteTrigger(canvas, "Ir para o Menu", menuRoute, "resultado.menu");
            result.Configure(score, verdict, againRoute, toMenuRoute);

            AudioBuilder.AddRouteMusic(null, AudioBuilder.MenuMusic, Load<RouteAsset>(ownRoute));
            EditorSceneManager.SaveScene(scene, ResultScenePath);
        }

        private static bool SetupMatchScene(string activityPath, string resultRoute, string menuRoute, string ownRoute)
        {
            var scene = EditorSceneManager.OpenScene(MatchScenePath, OpenSceneMode.Single);
            var activity = Load<ActivityAsset>(activityPath);
            if (activity == null) return false;

            var systems = FindRoot(scene, MatchSystemsName);
            var match = systems != null ? systems.GetComponent<MatchController>() : null;
            if (match == null)
            {
                Debug.LogError($"[Futebol de Botão] Não achei '{MatchSystemsName}' com o MatchController em {MatchScenePath}. Rode \"Criar cena da partida\" antes.");
                return false;
            }

            foreach (var oldName in new[] { MatchFrameworkName, PauseCanvasName, MatchHudName })
            {
                var old = FindRoot(scene, oldName);
                if (old != null) UnityEngine.Object.DestroyImmediate(old);
            }

            // A partida começa quando a Activity "Jogo" entra (e recomeça no Activity Restart).
            var contribution = systems.GetComponent<ActivityContentContribution>();
            if (contribution == null) contribution = systems.AddComponent<ActivityContentContribution>();
            var contributionSo = new SerializedObject(contribution);
            contributionSo.FindProperty("activity").objectReferenceValue = activity;
            contributionSo.FindProperty("localContentId").stringValue = "partida";
            contributionSo.ApplyModifiedPropertiesWithoutUndo();

            var framework = new GameObject(MatchFrameworkName).transform;
            var toResult = AddRouteTrigger(framework, "Ir para o Resultado", resultRoute, "partida.fim");
            var toMenu = AddRouteTrigger(framework, "Ir para o Menu", menuRoute, "partida.pausa.menu");
            var pause = AddTrigger<PauseRequestTrigger>(framework, "Pausa", so => so.FindProperty("reason").stringValue = "partida.pausa");
            var restart = AddTrigger<ActivityRestartTrigger>(framework, "Reiniciar partida", so =>
            {
                so.FindProperty("targetActivity").objectReferenceValue = activity;
                so.FindProperty("reason").stringValue = "partida.reiniciar";
            });

            var matchSo = new SerializedObject(match);
            matchSo.FindProperty("resultRoute").objectReferenceValue = toResult;
            matchSo.FindProperty("pauseTrigger").objectReferenceValue = pause;
            matchSo.ApplyModifiedPropertiesWithoutUndo();

            BuildPauseScreen(pause, restart, toMenu);

            // Jogadores: só o da vez joga; os outros (e o Jogador 1 na vez da IA) ficam bloqueados pelo framework.
            var players = new GameObject("Jogadores");
            players.transform.SetParent(framework, false);
            var matchObserver = AddObserver(players, LocalPlayerProvisioningConsumerScope.Activity);
            var testJoin = AddJoin(players, LocalPlayerProvisioningConsumerScope.Activity);
            var blocks = AddPerSlot<PlayerGameplayAvailabilityBlockTrigger>(players.transform, "Bloqueio", LocalPlayerProvisioningConsumerScope.Activity, "fora da vez");
            players.AddComponent<MatchPlayers>().Configure(matchObserver, match, blocks, testJoin);

            // HUD: cantos ao lado dos gols no celular (em pé), painel à esquerda no PC. Monta a UI sozinho no Awake.
            var hud = new GameObject(MatchHudName).AddComponent<MatchHud>();
            hud.Configure(match, ButtonSpritesImport.HudPanel());

            // Música da partida e sons (peteleco, batidas, apito, gol, cartão). Sem os cues ("Criar áudio"), fica sem som.
            AudioBuilder.AddMatchAudio(framework, match, Load<RouteAsset>(ownRoute));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return true;
        }

        private static void BuildPauseScreen(PauseRequestTrigger pause, ActivityRestartTrigger restart, RouteRequestTrigger toMenu)
        {
            var canvas = CreateCanvas(PauseCanvasName, 100);
            var pauseMenu = canvas.gameObject.AddComponent<PauseMenu>();

            // O adaptador do framework mostra e esconde o painel; o PauseMenu fica no Canvas, que nunca é desligado.
            var panel = CreatePanel(canvas, "Painel", PanelColor, Vector2.zero, Vector2.one);
            // O framework exige o CanvasGroup no mesmo objeto do Surface Root.
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            CreateText(panel.transform, "Título", "Pausa", 64, new Vector2(0f, 260f), new Vector2(600f, 110f), FontStyle.Bold);
            var resume = CreateButton(panel.transform, "Continuar", "Continuar", new Vector2(0f, 120f), new Vector2(420f, 90f), pauseMenu.Resume, ButtonStyle.Yellow);
            var music = CreateButton(panel.transform, "Música", "Música: Ligado", new Vector2(0f, 0f), new Vector2(420f, 90f), pauseMenu.ToggleMusic);
            var restartButton = CreateButton(panel.transform, "Reiniciar", "Reiniciar partida", new Vector2(0f, -120f), new Vector2(420f, 90f), pauseMenu.Restart, ButtonStyle.Red);
            var menuButton = CreateButton(panel.transform, "Menu", "Sair para o menu", new Vector2(0f, -240f), new Vector2(420f, 90f), pauseMenu.BackToMenu, ButtonStyle.Red);
            pauseMenu.Configure(pause, restart, toMenu, music.GetComponentInChildren<Text>());
            // Quem pausou mexe no menu (Esc/Start do próprio dispositivo, ou o mouse/toque dele).
            var pauseNavigator = panel.gameObject.AddComponent<MenuNavigator>();
            pauseNavigator.Configure(new[] { Item(resume), Item(music), Item(restartButton), Item(menuButton) }, resume);
            pauseNavigator.FollowPauser();
            panel.gameObject.SetActive(false);

            var adapter = canvas.gameObject.AddComponent<UnityPauseSurfaceAdapter>();
            var so = new SerializedObject(adapter);
            so.FindProperty("canvasGroup").objectReferenceValue = group;
            so.FindProperty("surfaceRoot").objectReferenceValue = panel.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static PlayerSessionObserver AddObserver(GameObject target, LocalPlayerProvisioningConsumerScope scope)
        {
            var observer = target.AddComponent<PlayerSessionObserver>();
            var so = new SerializedObject(observer);
            so.FindProperty("scope").intValue = (int)scope;
            so.ApplyModifiedPropertiesWithoutUndo();
            return observer;
        }

        private static PlayerSessionJoinTrigger AddJoin(GameObject target, LocalPlayerProvisioningConsumerScope scope)
        {
            var join = target.AddComponent<PlayerSessionJoinTrigger>();
            var so = new SerializedObject(join);
            so.FindProperty("scope").intValue = (int)scope;
            so.ApplyModifiedPropertiesWithoutUndo();
            return join;
        }

        /// <summary>Um componente por vaga (Jogador 1 e 2), cada um num filho, porque o framework só aceita um por objeto.</summary>
        private static T[] AddPerSlot<T>(Transform parent, string label, LocalPlayerProvisioningConsumerScope scope, string reason) where T : Component
        {
            var paths = new[] { PlayersBuilder.Slot1Path, PlayersBuilder.Slot2Path };
            var result = new T[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                var child = new GameObject($"{label} Jogador {i + 1}");
                child.transform.SetParent(parent, false);
                result[i] = child.AddComponent<T>();
                var so = new SerializedObject(result[i]);
                so.FindProperty("scope").intValue = (int)scope;
                so.FindProperty("playerSlot").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PlayerSlotProfile>(paths[i]);
                so.FindProperty("reason").stringValue = reason;
                so.ApplyModifiedPropertiesWithoutUndo();
                if (so.FindProperty("playerSlot").objectReferenceValue == null)
                    Debug.LogWarning($"[Futebol de Botão] Não achei {paths[i]}. Rode \"Criar jogadores\" antes.");
            }
            return result;
        }

        private static GameObject FindRoot(Scene scene, string objectName)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == objectName) return root;
            return null;
        }

        // ---- UI (uGUI, visual provisório) ----

        private static Transform CreateCanvas(string objectName, int sortingOrder)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            // Expand: a tela de referência (720x1280) cabe inteira em qualquer celular; em tela comprida sobra em cima e embaixo.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            go.AddComponent<GraphicRaycaster>();
            return go.transform;
        }

        /// <summary>Botão que o teclado/controle alcança (Confirmar aperta).</summary>
        private static MenuEntry Item(Button button)
        {
            var item = button.gameObject.AddComponent<MenuEntry>();
            item.Configure(button);
            return item;
        }

        /// <summary>Navegação do painel pelo teclado/controle, na ordem dos itens; Q/B aperta <paramref name="back"/>.</summary>
        private static void AddNavigator(Transform panel, Button back, params MenuEntry[] items) =>
            panel.gameObject.AddComponent<MenuNavigator>().Configure(items, back);

        private static Transform CreateGroup(Transform parent, string objectName)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform, Vector2.zero, Vector2.one);
            return go.transform;
        }

        private static Image CreatePanel(Transform parent, string objectName, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform, anchorMin, anchorMax);
            var image = go.AddComponent<Image>();
            image.color = color;
            return image;
        }

        /// <summary>
        /// Fundo das telas: faixas horizontais do feltro (clara e escura, a do meio clara), na mesma escala da mesa
        /// na partida. Sem a arte do campo, fica o verde liso.
        /// </summary>
        private static void CreateFieldBackground(Transform canvas)
        {
            var background = CreatePanel(canvas, "Fundo", Background, Vector2.zero, Vector2.one);
            var sprites = new Dictionary<string, Sprite>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(FieldSheetPath))
                if (asset is Sprite sprite) sprites[sprite.name] = sprite;
            if (!sprites.TryGetValue(BrightFeltSprite, out var bright) || !sprites.TryGetValue(DarkFeltSprite, out var dark))
            {
                Debug.LogWarning($"[Futebol de Botão] Sem {BrightFeltSprite}/{DarkFeltSprite} em {FieldSheetPath}: telas com fundo liso.");
                return;
            }

            // Altura da tela de referência = 2 x OrthoSize tiles na partida.
            float tile = ReferenceResolution.y / (FrameworkCameraBuilder.OrthoSize * 2f);
            // Cobre também o celular comprido: com o Expand a tela passa de 1280 de altura (20:9 = 1600).
            const float coverHeight = 2000f;
            int rows = Mathf.CeilToInt(coverHeight / tile / 2f) * 2 + 1;
            for (int row = 0; row < rows; row++)
            {
                int fromMiddle = row - rows / 2;
                var go = new GameObject($"Faixa {row + 1}", typeof(RectTransform));
                go.transform.SetParent(background.transform, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(1f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, fromMiddle * tile);
                rect.sizeDelta = new Vector2(0f, tile);
                var image = go.AddComponent<Image>();
                image.sprite = fromMiddle % 2 == 0 ? bright : dark;
                image.type = Image.Type.Tiled;
                // Image.Tiled desenha cada tile com (pixels / PPU) x 100 unidades; o multiplicador acerta para "tile".
                image.pixelsPerUnitMultiplier = 100f / tile;
                image.raycastTarget = false;
            }
        }

        /// <summary>Ícone do jogo acima do título (Abertura e primeira tela do Menu).</summary>
        private static void CreateGameLogo(Transform canvas)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GameLogoPath);
            if (sprite == null) return;
            var go = new GameObject("Logo do jogo", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            Place((RectTransform)go.transform, new Vector2(0f, 380f), new Vector2(400f, 400f));
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
        }

        /// <summary>
        /// Logo da Immersive no canto inferior direito, pequeno e um pouco transparente. A imagem é grande e vai ser
        /// mostrada bem menor: filtro bilinear com mipmaps para não serrilhar.
        /// </summary>
        private static void CreateStudioLogo(Transform canvas)
        {
            if (AssetImporter.GetAtPath(StudioLogoPath) is TextureImporter importer &&
                (importer.filterMode != FilterMode.Bilinear || !importer.mipmapEnabled))
            {
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(StudioLogoPath);
            if (sprite == null)
            {
                Debug.LogWarning($"[Futebol de Botão] Sem o logo em {StudioLogoPath}: Abertura sem logo.");
                return;
            }

            var go = new GameObject("Logo Immersive", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-StudioLogoMargin, StudioLogoMargin);
            float aspect = sprite.rect.width / sprite.rect.height;
            rect.sizeDelta = new Vector2(StudioLogoHeight * aspect, StudioLogoHeight);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = new Color(1f, 1f, 1f, 0.85f);
            image.raycastTarget = false;
        }

        /// <summary>Um recorte da Field_Sprites pelo nome, ou nulo (com aviso) se não existir.</summary>
        /// <summary>Recortes da mesa, das marcações e da bola para o campo desenhado da seleção de times.</summary>
        private static Sprite[] LoadTableSprites()
        {
            var sprites = new List<Sprite>();
            foreach (var path in new[] { FieldSheetPath, FieldMarksPath, BallSheetPath })
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is Sprite sprite) sprites.Add(sprite);
            return sprites.ToArray();
        }

        private static Text CreateText(Transform parent, string objectName, string content, int size, Vector2 position, Vector2 boxSize,
            FontStyle style = FontStyle.Normal, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, position, boxSize);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = TextColor;
            text.raycastTarget = false;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = TextShadowColor;
            shadow.effectDistance = TextShadowDistance;
            return text;
        }

        private static Button CreateButton(Transform parent, string objectName, string label, Vector2 position, Vector2 boxSize, UnityAction action,
            ButtonStyle style = ButtonStyle.Green)
        {
            var button = CreateButtonBase(parent, objectName, label, position, boxSize, style);
            UnityEventTools.AddPersistentListener(button.onClick, action);
            return button;
        }

        private static Button CreateIntButton(Transform parent, string objectName, string label, Vector2 position, Vector2 boxSize,
            UnityAction<int> action, int argument, ButtonStyle style = ButtonStyle.Green)
        {
            var button = CreateButtonBase(parent, objectName, label, position, boxSize, style);
            UnityEventTools.AddIntPersistentListener(button.onClick, action, argument);
            return button;
        }

        /// <summary>Botão com a moldura da folha de botões (<see cref="ButtonSkin"/>); sem a folha, retângulo claro com texto escuro.</summary>
        private static Button CreateButtonBase(Transform parent, string objectName, string label, Vector2 position, Vector2 boxSize, ButtonStyle style)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, position, boxSize);
            var image = go.AddComponent<Image>();
            image.color = ButtonColor;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var text = CreateText(go.transform, "Texto", label, 32, Vector2.zero, boxSize, FontStyle.Bold);
            text.color = ButtonTextColor;
            UnityEngine.Object.DestroyImmediate(text.GetComponent<Shadow>());
            Stretch((RectTransform)text.transform, Vector2.zero, Vector2.one);
            ButtonSkin.Apply(button, style);
            return button;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
