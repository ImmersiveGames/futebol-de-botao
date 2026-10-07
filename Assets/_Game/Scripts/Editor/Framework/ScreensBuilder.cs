using System;
using System.Collections.Generic;
using System.IO;
using Immersive.Framework.ActivityFlow;
using Immersive.Framework.ActivityRestart;
using Immersive.Framework.Authoring;
using Immersive.Framework.GameFlow;
using Immersive.Framework.Pause;
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

        private const string MatchSystemsName = "Partida (sistemas)";
        private const string MatchFrameworkName = "Framework (telas)";
        private const string PauseCanvasName = "Tela de pausa";

        private static readonly Vector2 ReferenceResolution = new(720f, 1280f);
        private static readonly Color Background = new(0.08f, 0.2f, 0.11f);
        private static readonly Color PanelColor = new(0f, 0f, 0f, 0.75f);
        private static readonly Color ButtonColor = new(0.95f, 0.95f, 0.9f);
        private static readonly Color TextColor = Color.white;
        private static readonly Color ButtonTextColor = new(0.1f, 0.1f, 0.1f);

        private static Font font;

        [MenuItem("Futebol de Botão/Criar telas")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var app = FindGameApplication();
            var matchRoute = FindMatchRoute();
            if (app == null || matchRoute == null) return;

            var activity = matchRoute.StartupActivity;
            if (activity == null)
            {
                Debug.LogError($"[Futebol de Botão] A Route '{matchRoute.name}' não tem Startup Activity. Ligue a Activity \"Jogo\" nela.");
                return;
            }

            var options = AssetDatabase.LoadAssetAtPath<MatchOptions>(OptionsPath);
            if (options == null)
                Debug.LogWarning($"[Futebol de Botão] Não achei {OptionsPath}; o Menu vai usar as opções padrão do código.");

            var titleRoute = LoadOrCreateRoute("Abertura", TitleScenePath);
            var menuRoute = LoadOrCreateRoute("Menu", MenuScenePath);
            var resultRoute = LoadOrCreateRoute("Resultado", ResultScenePath);
            AssetDatabase.SaveAssets();

            BuildTitleScene(menuRoute);
            BuildMenuScene(matchRoute, options);
            BuildResultScene(matchRoute, menuRoute);
            if (!SetupMatchScene(activity, resultRoute, menuRoute)) return;

            SetStartupRoute(app, titleRoute);
            UpdateBuildScenes();
            AssetDatabase.SaveAssets();

            EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            Debug.Log("[Futebol de Botão] Telas criadas: Abertura → Menu → Partida → Resultado. " +
                      $"Startup Route = '{titleRoute.name}'. Cenas adicionadas na lista da build.");
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

        private static RouteAsset LoadOrCreateRoute(string routeName, string scenePath)
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
            return route;
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

        private static RouteRequestTrigger AddRouteTrigger(Transform parent, string objectName, RouteAsset target, string reason) =>
            AddTrigger<RouteRequestTrigger>(parent, objectName, so =>
            {
                so.FindProperty("targetRoute").objectReferenceValue = target;
                so.FindProperty("reason").stringValue = reason;
            });

        // ---- Cenas ----

        private static void BuildTitleScene(RouteAsset menuRoute)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var canvas = CreateCanvas("Abertura", 0);
            CreatePanel(canvas, "Fundo", Background, Vector2.zero, Vector2.one);

            CreateText(canvas, "Título", "Futebol de Botão", 72, new Vector2(0f, 160f), new Vector2(680f, 120f), FontStyle.Bold);
            var prompt = CreateText(canvas, "Aperte", "Aperte qualquer botão\nou toque na tela", 36, new Vector2(0f, -200f), new Vector2(680f, 120f));

            var trigger = AddRouteTrigger(canvas, "Ir para o Menu", menuRoute, "abertura.menu");
            canvas.gameObject.AddComponent<TitleScreen>().Configure(trigger, prompt);

            EditorSceneManager.SaveScene(scene, TitleScenePath);
        }

        private static void BuildMenuScene(RouteAsset matchRoute, MatchOptions options)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var canvas = CreateCanvas("Menu", 0);
            CreatePanel(canvas, "Fundo", Background, Vector2.zero, Vector2.one);
            var menu = canvas.gameObject.AddComponent<MenuScreen>();

            // Principal
            var main = CreateGroup(canvas, "Principal");
            CreateText(main, "Título", "Futebol de Botão", 64, new Vector2(0f, 300f), new Vector2(680f, 110f), FontStyle.Bold);
            CreateButton(main, "Iniciar", "Iniciar", new Vector2(0f, 40f), new Vector2(420f, 90f), menu.StartMatch);
            CreateButton(main, "Opções", "Opções da partida", new Vector2(0f, -80f), new Vector2(420f, 90f), menu.ShowOptions);

            // Opções da partida (GDD)
            var optionsPanel = CreateGroup(canvas, "Opções da partida");
            CreateText(optionsPanel, "Título", "Opções da partida", 52, new Vector2(0f, 420f), new Vector2(680f, 90f), FontStyle.Bold);
            var kinds = (MatchOptionKind[])Enum.GetValues(typeof(MatchOptionKind));
            var values = new Text[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                float y = 280f - i * 100f;
                var row = CreateGroup(optionsPanel, MenuScreen.Label(kinds[i]));
                CreateText(row, "Nome", MenuScreen.Label(kinds[i]), 30, new Vector2(-170f, y), new Vector2(300f, 70f), FontStyle.Normal, TextAnchor.MiddleLeft);
                CreateIntButton(row, "Anterior", "<", new Vector2(40f, y), new Vector2(70f, 70f), menu.Previous, i);
                values[i] = CreateText(row, "Valor", "-", 30, new Vector2(165f, y), new Vector2(170f, 70f), FontStyle.Bold);
                CreateIntButton(row, "Próximo", ">", new Vector2(290f, y), new Vector2(70f, 70f), menu.Next, i);
            }
            CreateButton(optionsPanel, "Voltar", "Voltar", new Vector2(0f, -400f), new Vector2(320f, 90f), menu.ShowMain);
            optionsPanel.gameObject.SetActive(false);

            var trigger = AddRouteTrigger(canvas, "Ir para a Partida", matchRoute, "menu.iniciar");
            menu.Configure(options, trigger, main.gameObject, optionsPanel.gameObject, values);

            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }

        private static void BuildResultScene(RouteAsset matchRoute, RouteAsset menuRoute)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var canvas = CreateCanvas("Resultado", 0);
            CreatePanel(canvas, "Fundo", Background, Vector2.zero, Vector2.one);
            var result = canvas.gameObject.AddComponent<ResultScreen>();

            CreateText(canvas, "Título", "Fim de jogo", 64, new Vector2(0f, 320f), new Vector2(680f, 110f), FontStyle.Bold);
            var score = CreateText(canvas, "Placar", "Azul 0 x 0 Vermelho", 48, new Vector2(0f, 160f), new Vector2(680f, 90f), FontStyle.Bold);
            var verdict = CreateText(canvas, "Vencedor", "Empate!", 38, new Vector2(0f, 60f), new Vector2(680f, 80f));
            CreateButton(canvas, "Jogar de novo", "Jogar de novo", new Vector2(0f, -120f), new Vector2(420f, 90f), result.PlayAgain);
            CreateButton(canvas, "Menu", "Menu", new Vector2(0f, -240f), new Vector2(420f, 90f), result.BackToMenu);

            var again = AddRouteTrigger(canvas, "Ir para a Partida", matchRoute, "resultado.jogar_de_novo");
            var menu = AddRouteTrigger(canvas, "Ir para o Menu", menuRoute, "resultado.menu");
            result.Configure(score, verdict, again, menu);

            EditorSceneManager.SaveScene(scene, ResultScenePath);
        }

        private static bool SetupMatchScene(ActivityAsset activity, RouteAsset resultRoute, RouteAsset menuRoute)
        {
            var scene = EditorSceneManager.OpenScene(MatchScenePath, OpenSceneMode.Single);

            var systems = FindRoot(scene, MatchSystemsName);
            var match = systems != null ? systems.GetComponent<MatchController>() : null;
            if (match == null)
            {
                Debug.LogError($"[Futebol de Botão] Não achei '{MatchSystemsName}' com o MatchController em {MatchScenePath}. Rode \"Criar cena da partida\" antes.");
                return false;
            }

            foreach (var oldName in new[] { MatchFrameworkName, PauseCanvasName })
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
            matchSo.FindProperty("restartTrigger").objectReferenceValue = restart;
            matchSo.FindProperty("pauseTrigger").objectReferenceValue = pause;
            matchSo.ApplyModifiedPropertiesWithoutUndo();

            BuildPauseScreen(pause, restart, toMenu);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return true;
        }

        private static void BuildPauseScreen(PauseRequestTrigger pause, ActivityRestartTrigger restart, RouteRequestTrigger toMenu)
        {
            var canvas = CreateCanvas(PauseCanvasName, 100);
            var group = canvas.gameObject.AddComponent<CanvasGroup>();
            var pauseMenu = canvas.gameObject.AddComponent<PauseMenu>();
            pauseMenu.Configure(pause, restart, toMenu);

            // O adaptador do framework mostra e esconde o painel; o PauseMenu fica no Canvas, que nunca é desligado.
            var panel = CreatePanel(canvas, "Painel", PanelColor, Vector2.zero, Vector2.one);
            CreateText(panel.transform, "Título", "Pausa", 64, new Vector2(0f, 260f), new Vector2(600f, 110f), FontStyle.Bold);
            CreateButton(panel.transform, "Continuar", "Continuar", new Vector2(0f, 80f), new Vector2(420f, 90f), pauseMenu.Resume);
            CreateButton(panel.transform, "Reiniciar", "Reiniciar partida", new Vector2(0f, -40f), new Vector2(420f, 90f), pauseMenu.Restart);
            CreateButton(panel.transform, "Menu", "Sair para o menu", new Vector2(0f, -160f), new Vector2(420f, 90f), pauseMenu.BackToMenu);
            panel.gameObject.SetActive(false);

            var adapter = canvas.gameObject.AddComponent<UnityPauseSurfaceAdapter>();
            var so = new SerializedObject(adapter);
            so.FindProperty("canvasGroup").objectReferenceValue = group;
            so.FindProperty("surfaceRoot").objectReferenceValue = panel.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
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
            scaler.matchWidthOrHeight = 1f;
            go.AddComponent<GraphicRaycaster>();
            return go.transform;
        }

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
            return text;
        }

        private static Button CreateButton(Transform parent, string objectName, string label, Vector2 position, Vector2 boxSize, UnityAction action)
        {
            var button = CreateButtonBase(parent, objectName, label, position, boxSize);
            UnityEventTools.AddPersistentListener(button.onClick, action);
            return button;
        }

        private static Button CreateIntButton(Transform parent, string objectName, string label, Vector2 position, Vector2 boxSize,
            UnityAction<int> action, int argument)
        {
            var button = CreateButtonBase(parent, objectName, label, position, boxSize);
            UnityEventTools.AddIntPersistentListener(button.onClick, action, argument);
            return button;
        }

        private static Button CreateButtonBase(Transform parent, string objectName, string label, Vector2 position, Vector2 boxSize)
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
            Stretch((RectTransform)text.transform, Vector2.zero, Vector2.one);
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
