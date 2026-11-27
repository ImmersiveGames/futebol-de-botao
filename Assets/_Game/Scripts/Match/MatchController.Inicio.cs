using System.Collections.Generic;
using Immersive.Framework.ActivityFlow;
using Immersive.Framework.GameFlow;
using Immersive.Framework.Pause;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FutebolDeBotao
{
    /// <summary>Começo da partida: Activity do framework, peças, opções, gol sem goleiro, IA, tutorial e saída. Parte da <see cref="MatchController"/>.</summary>
    public sealed partial class MatchController
    {
        // ---- Activity do framework ----

        public void OnActivityContentEntered(ActivityContentLifecycleContext context)
        {
            EnsureInitialized();
            StartMatch();
        }

        public void OnActivityContentExited(ActivityContentLifecycleContext context)
        {
            if (!initialized) return;
            // Saiu antes do fim (menu ou reiniciar): partida abandonada. Terminada, já foi gravada e não conta de novo.
            PlayerStats.Abandon();
            if (analyticsMatchOpen) LogAbandon();
            analyticsMatchOpen = false;
            EndShot();
            monitor.StopWatching();
            monitor.FreezeAll();
            message = string.Empty;
            Enter(MatchState.Waiting);
        }

        private void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;

            ball = FindAnyObjectByType<Ball>();
            keepers = FindObjectsByType<Goalkeeper>();
            aim = FindAnyObjectByType<AimController>();
            aimVisuals = FindAnyObjectByType<AimVisuals>();
            monitor = FindAnyObjectByType<MotionMonitor>();
            keeperControl = FindAnyObjectByType<GoalkeeperControl>();
            if (keeperControl == null) keeperControl = gameObject.AddComponent<GoalkeeperControl>();

            CollectDiscs();

            aim.Flicked += OnFlicked;
            aim.BallKicked += OnBallKicked;
            monitor.Settled += OnSettled;
            foreach (var goal in FindObjectsByType<GoalTrigger>()) goal.BallEntered += OnBallEntered;
            foreach (var list in discs.Values)
                foreach (var disc in list)
                {
                    disc.Fouled += OnFouled;
                    disc.HitOpponent += OnHitOpponent;
                    disc.HitOpponentKeeper += OnHitOpponentKeeper;
                }

            Enter(MatchState.Waiting);
        }

        private void CollectDiscs()
        {
            discs[TeamSide.Bottom] = new List<Disc>();
            discs[TeamSide.Top] = new List<Disc>();
            foreach (var disc in FindObjectsByType<Disc>()) discs[disc.Side].Add(disc);
            foreach (var list in discs.Values) list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            allDiscs.Clear();
            allDiscs.AddRange(discs[TeamSide.Bottom]);
            allDiscs.AddRange(discs[TeamSide.Top]);
        }

        private void ApplyOptions()
        {
            foreach (var list in discs.Values)
                for (int i = 0; i < list.Count; i++)
                    list[i].gameObject.SetActive(i < options.discsPerTeam);

            foreach (var keeper in keepers) keeper.gameObject.SetActive(options.hasGoalkeeper);
            ApplyGoalWidth();
            if (options.hasGoalkeeper)
                foreach (var keeper in keepers) keeper.SetWidth(options.keeperWidth, GoalWidth);
            if (aimVisuals != null) aimVisuals.Mode = options.aimAssist;
            PaintTeams();
        }

        /// <summary>Largura da boca do gol nesta partida (menor sem goleiro).</summary>
        public float GoalWidth { get; private set; } = FieldLayout.GoalWidth;

        private readonly List<GameObject> goalPosts = new();

        /// <summary>
        /// Sem goleiro o gol fica menor: dois blocos de parede, um de cada lado, da linha de fundo até a rede de trás
        /// (copiados da parede do fundo).
        /// </summary>
        private void ApplyGoalWidth()
        {
            GoalWidth = options.hasGoalkeeper
                ? FieldLayout.GoalWidth
                : Mathf.Clamp(options.goalWidthWithoutKeeper, 0.6f, FieldLayout.GoalWidth);
            float post = (FieldLayout.GoalWidth - GoalWidth) * 0.5f;

            if (goalPosts.Count == 0 && post > 0.001f)
            {
                foreach (var wall in FindObjectsByType<Wall>())
                {
                    // As paredes do fundo, ao lado do gol: viram o molde das traves daquele lado do campo.
                    var position = wall.transform.position;
                    if (!wall.PushesBack || Mathf.Abs(position.y) < FieldLayout.HalfHeight || Mathf.Abs(position.x) > FieldLayout.HalfWidth) continue;
                    if (position.x > 0f) continue;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var copy = Instantiate(wall.gameObject, wall.transform.parent);
                        copy.name = $"Trave sem goleiro {(position.y < 0f ? "baixo" : "cima")} {(side < 0f ? "esquerda" : "direita")}";
                        copy.SetActive(false);
                        copy.transform.position = new Vector3(side, Mathf.Sign(position.y), position.z);
                        goalPosts.Add(copy);
                    }
                }
            }

            foreach (var copy in goalPosts)
            {
                var position = copy.transform.position;
                float side = Mathf.Sign(position.x);
                float end = Mathf.Sign(position.y);
                copy.transform.position = new Vector3(side * (GoalWidth * 0.5f + post * 0.5f),
                    end * (FieldLayout.HalfHeight + FieldLayout.GoalDepth * 0.5f), position.z);
                var scale = copy.transform.localScale;
                copy.transform.localScale = new Vector3(Mathf.Max(post, 0.001f), FieldLayout.GoalDepth, scale.z);
                copy.SetActive(post > 0.001f);
            }

            var repulsion = FindAnyObjectByType<WallRepulsion>();
            if (repulsion != null) repulsion.RefreshWalls();

            var art = FindAnyObjectByType<TableArt>();
            if (art != null) art.SetSmallGoals(!options.hasGoalkeeper);
        }

        public void StartMatch()
        {
            options = MatchSession.Options(options);
            ApplyOptions();
            ConfigureAi();
            resultRequested = false;
            holdTimer = 0f;
            restoreControls = false;
            score[0] = score[1] = 0;
            playerTrailed = false;
            playerSetPieceGoals = playerWallGoals = 0;
            farFouls[0] = farFouls[1] = 0;
            numbers[0] = new SaveData.MatchNumbers();
            numbers[1] = new SaveData.MatchNumbers();
            touchPending = false;
            period = Period.Regular;
            foreach (var list in shootoutKicks) list.Clear();
            clock = options.DurationSeconds;
            ConfigureTutorial();
            analyticsMatchOpen = tutorial == null;
            if (tutorial != null)
            {
                // Tutorial: sem relógio e sem estatísticas; a lição monta a jogada.
                tutorial.Begin();
                return;
            }
            PlayerStats.BeginMatch();
            GameAnalytics.MatchStart(options);
            analyticsStartTime = Time.realtimeSinceStartup;
            // Início: time sorteado dá a saída.
            KickOff(Random.value < 0.5f ? TeamSide.Bottom : TeamSide.Top);
        }

        /// <summary>Partida do tutorial (aberta pelo Menu): liga o <see cref="TutorialDirector"/>; senão tira.</summary>
        private void ConfigureTutorial()
        {
            if (!FutebolDeBotao.Tutorial.Active)
            {
                if (tutorial != null) Destroy(tutorial);
                tutorial = null;
                return;
            }
            if (tutorial == null) tutorial = gameObject.AddComponent<TutorialDirector>();
            tutorial.Configure(this);
        }

        /// <summary>Contra a IA, o time de cima joga sozinho no nível do Menu; em 2 jogadores, ninguém.</summary>
        private void ConfigureAi()
        {
            bool fromMenu = MatchSession.VsAi.HasValue;
            bool vsAi = MatchSession.VsAi ?? aiPlaysTop;
            if (!vsAi)
            {
                if (ai != null) Destroy(ai);
                ai = null;
                return;
            }

            // No tutorial a IA é sempre a do Fácil.
            var difficulty = FutebolDeBotao.Tutorial.Active ? AiDifficulty.Load(AiLevel.Easy)
                : fromMenu || aiDifficulty == null ? AiDifficulty.Load(MatchSession.AiLevel) : aiDifficulty;
            if (ai == null) ai = gameObject.AddComponent<AiPlayer>();
            ai.Configure(this, TeamSide.Top, difficulty);
        }

        private void KickOff(TeamSide side)
        {
            Enter(MatchState.KickOff);
            EndShot();
            monitor.StopWatching();

            foreach (var pair in discs)
                for (int i = 0; i < pair.Value.Count && i < options.discsPerTeam; i++)
                    pair.Value[i].PlaceAt(FormationPosition(i, pair.Key));
            foreach (var keeper in keepers)
                if (keeper.isActiveAndEnabled) keeper.ResetToCenter();
            ball.ResetTo(Vector2.zero);
            KeepOutOfCenterCircle(side.Opponent());

            GiveTurn(side);
            message = Txt.Format("Saída do {0}", TeamName(side));
            Noticed?.Invoke(MatchNotice.KickOff);
            Enter(MatchState.Aim);
        }
    }
}
