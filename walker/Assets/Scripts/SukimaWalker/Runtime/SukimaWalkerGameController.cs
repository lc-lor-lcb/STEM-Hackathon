using System.Collections.Generic;
using System.IO;
using SukimaWalker.Core;
using UnityEngine;

namespace SukimaWalker.Runtime
{
    // ゲーム全体の画面状態を管理し、セレクト、プレイ、エディターを切り替えます。
    public sealed class SukimaWalkerGameController : MonoBehaviour
    {
        [SerializeField] private float characterSpeed = 1.5f;

        private enum AppMode
        {
            Select,
            Play,
            TestPlay,
            Editor
        }

        private AppMode mode;
        private StageData currentStageData;
        private StageData editingStageData;
        private GameSimulation simulation;
        private StageRenderer stageRenderer;
        private FrameInputReader frameInputReader;
        private StageEditorController editorController;
        private GamePresentationSettings presentationSettings;
        private readonly List<StageData> selectableStages = new List<StageData>();

        private int selectedStageIndex;
        private bool gameplayStarted;
        private float gameplayIdleTimer;
        private float selectIdleTimer;
        private float selectDemoTimer;
        private float clearTimer = -1f;
        private Vector2 lastMousePosition;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<SukimaWalkerGameController>() != null)
            {
                return;
            }

            GameObject gameObject = new GameObject("SukimaWalkerGame");
            gameObject.AddComponent<SukimaWalkerGameController>();
        }

        // 起動時に設定とステージ一覧を読み、最初はセレクト画面を表示します。
        private void Awake()
        {
            presentationSettings = Resources.Load<GamePresentationSettings>("SukimaWalkerPresentationSettings");
            if (presentationSettings == null)
            {
                presentationSettings = ScriptableObject.CreateInstance<GamePresentationSettings>();
            }

            frameInputReader = new FrameInputReader();
            stageRenderer = gameObject.AddComponent<StageRenderer>();
            LoadStageList();

            currentStageData = selectableStages.Count > 0 ? selectableStages[0] : StageData.CreateFallback();
            editorController = new StageEditorController(currentStageData);
            lastMousePosition = Input.mousePosition;
            OpenSelect();
        }

        // 現在の画面に応じて、入力、無操作時間、プレイ進行を更新します。
        private void Update()
        {
            bool interacted = ConsumeInteractionSignal();
            if (mode == AppMode.Select)
            {
                UpdateSelectIdle(interacted);
                return;
            }

            if (mode == AppMode.Editor)
            {
                editorController.Update();
                if (editorController.ConsumeRenderDirty())
                {
                    RenderEditorStage();
                }

                return;
            }

            if (interacted)
            {
                gameplayIdleTimer = 0f;
            }
            else
            {
                gameplayIdleTimer += Time.deltaTime;
                if (gameplayIdleTimer >= presentationSettings.GameplayInactivityTimeoutSeconds)
                {
                    OpenSelect();
                    return;
                }
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                RetryStage();
            }

            FrameInputCommand frameInput = frameInputReader.Capture(simulation.State.Frame);
            if (frameInput.IsActive)
            {
                gameplayStarted = true;
                gameplayIdleTimer = 0f;
            }

            simulation.Tick(gameplayStarted ? Time.deltaTime : 0f, frameInput);
            SyncRuntimeView();
            UpdatePlayCamera(simulation.State);

            if (mode == AppMode.TestPlay && !simulation.State.Character.IsAlive)
            {
                ReturnToEditorAfterTestPlay("Test play ended: character died.");
                return;
            }

            if (simulation.State.Character.HasCleared)
            {
                if (mode == AppMode.Play)
                {
                    MarkStageCompleted(currentStageData);
                    clearTimer = clearTimer < 0f ? presentationSettings.ClearReturnDelaySeconds : clearTimer - Time.deltaTime;
                    if (clearTimer <= 0f)
                    {
                        OpenSelect();
                    }
                }
                else if (mode == AppMode.TestPlay)
                {
                    ReturnToEditorAfterTestPlay("Test play ended: stage cleared.");
                }
            }
        }

        // 画面上のボタンやサムネイルを、なるべく記号だけで描きます。
        private void OnGUI()
        {
            if (mode == AppMode.Select)
            {
                DrawStageSelectGui();
                return;
            }

            if (mode == AppMode.Play || mode == AppMode.TestPlay)
            {
                DrawPlayGui();
                return;
            }

            editorController.DrawGui(StartTestPlayFromEditor);
        }

        // ステージ選択画面では、サムネイル、星、クリア印、待機デモを表示します。
        private void DrawStageSelectGui()
        {
            float x = 24f;
            float y = 24f;
            for (int i = 0; i < selectableStages.Count; i++)
            {
                Rect rect = new Rect(x, y, 132f, 108f);
                bool focused = rect.Contains(Event.current.mousePosition) || i == selectedStageIndex;
                DrawStageCard(rect, selectableStages[i], focused);

                if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                {
                    selectedStageIndex = i;
                    StartPlay(selectableStages[i]);
                    Event.current.Use();
                }

                x += 144f;
                if (x + 132f > Screen.width)
                {
                    x = 24f;
                    y += 120f;
                }
            }

            Rect editorRect = new Rect(Screen.width - 58f, 18f, 40f, 40f);
            if (GUI.Button(editorRect, "E"))
            {
                OpenEditorFromSelect();
            }

            if (selectIdleTimer >= presentationSettings.SelectIdleDemoDelaySeconds && selectableStages.Count > 0)
            {
                Rect demoRect = new Rect(Screen.width - 220f, Screen.height - 170f, 180f, 132f);
                DrawStageCard(demoRect, selectableStages[selectedStageIndex], true);
            }
        }

        // プレイ中は即時リトライとエディター復帰だけを小さな記号ボタンで出します。
        private void DrawPlayGui()
        {
            if (GUI.Button(new Rect(8f, 8f, 42f, 42f), "R"))
            {
                RetryStage();
            }

            if (mode == AppMode.Play && GUI.Button(new Rect(58f, 8f, 42f, 42f), "E"))
            {
                OpenEditorFromPlay();
            }

            if (mode == AppMode.TestPlay && GUI.Button(new Rect(58f, 8f, 42f, 42f), "<"))
            {
                ReturnToEditorAfterTestPlay("Test play ended.");
            }
        }

        // 1枚のステージカードを、盤面プレビューと記号だけで描画します。
        private void DrawStageCard(Rect rect, StageData stageData, bool focused)
        {
            float pulse = focused ? 1f + Mathf.Sin(Time.time * presentationSettings.PreviewPulseSpeed) * presentationSettings.PreviewPulseAmount : 1f;
            Rect pulsed = ScaleRect(rect, pulse);
            GUI.color = IsStageCompleted(stageData) ? presentationSettings.CompletedStageTint : presentationSettings.LockedStageTint;
            GUI.Box(pulsed, GUIContent.none);
            GUI.color = Color.white;

            DrawMiniBoard(new Rect(pulsed.x + 8f, pulsed.y + 8f, pulsed.width - 16f, pulsed.height - 32f), stageData);
            DrawDifficulty(new Rect(pulsed.x + 8f, pulsed.yMax - 22f, 90f, 18f), stageData.difficulty);

            if (IsStageCompleted(stageData))
            {
                GUI.Label(new Rect(pulsed.xMax - 26f, pulsed.y + 4f, 22f, 22f), "o");
            }
        }

        // ステージデータのgridを小さな四角に置き換えてサムネイル化します。
        private static void DrawMiniBoard(Rect rect, StageData stageData)
        {
            StageDataUtility.Normalize(stageData);
            float cellSize = Mathf.Min(rect.width / stageData.board_width, rect.height / stageData.board_height);
            float startX = rect.x + (rect.width - cellSize * stageData.board_width) * 0.5f;
            float startY = rect.y + (rect.height - cellSize * stageData.board_height) * 0.5f;

            for (int y = 0; y < stageData.board_height; y++)
            {
                for (int x = 0; x < stageData.board_width; x++)
                {
                    GUI.color = ColorForPreview(StageDataUtility.GetTerrainSymbol(stageData, x, y));
                    GUI.DrawTexture(new Rect(startX + x * cellSize, startY + y * cellSize, cellSize - 1f, cellSize - 1f), Texture2D.whiteTexture);
                }
            }

            GUI.color = Color.white;
        }

        // 難易度は数字ではなく星の数として見せます。
        private static void DrawDifficulty(Rect rect, int difficulty)
        {
            difficulty = Mathf.Clamp(difficulty, 1, 5);
            for (int i = 0; i < 5; i++)
            {
                GUI.color = i < difficulty ? new Color(1f, 0.82f, 0.2f, 1f) : new Color(0.2f, 0.22f, 0.26f, 1f);
                GUI.DrawTexture(new Rect(rect.x + i * 16f, rect.y, 12f, 12f), Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
        }

        // セレクトで放置された時は、フォーカスを周期的に移してデモっぽく動かします。
        private void UpdateSelectIdle(bool interacted)
        {
            if (interacted)
            {
                selectIdleTimer = 0f;
                selectDemoTimer = 0f;
                return;
            }

            selectIdleTimer += Time.deltaTime;
            if (selectIdleTimer < presentationSettings.SelectIdleDemoDelaySeconds || selectableStages.Count == 0)
            {
                return;
            }

            selectDemoTimer += Time.deltaTime;
            if (selectDemoTimer >= presentationSettings.IdleDemoCycleSeconds)
            {
                selectDemoTimer = 0f;
                selectedStageIndex = (selectedStageIndex + 1) % selectableStages.Count;
            }
        }

        // 保存済みステージと同梱ステージを集め、セレクト用に保持します。
        private void LoadStageList()
        {
            selectableStages.Clear();
            foreach (string path in StageRepository.ListStagePaths())
            {
                selectableStages.Add(StageRepository.Load(path));
            }

            if (selectableStages.Count == 0)
            {
                selectableStages.Add(StageData.CreateFallback());
            }
        }

        // 選んだステージを本編プレイとして開始します。
        private void StartPlay(StageData stageData)
        {
            currentStageData = StageDataUtility.Clone(stageData);
            simulation = new GameSimulation(currentStageData, characterSpeed);
            mode = AppMode.Play;
            gameplayStarted = false;
            gameplayIdleTimer = 0f;
            clearTimer = -1f;
            stageRenderer.Render(simulation.State);
            ConfigurePlayCamera(simulation.State);
        }

        // エディターから現在の編集内容をテストプレイします。
        private void StartTestPlayFromEditor(StageData stageData)
        {
            editingStageData = StageDataUtility.Clone(stageData);
            simulation = new GameSimulation(editingStageData, characterSpeed, false);
            mode = AppMode.TestPlay;
            gameplayStarted = false;
            gameplayIdleTimer = 0f;
            clearTimer = -1f;
            stageRenderer.Render(simulation.State);
            ConfigurePlayCamera(simulation.State);
        }

        // 現在のステージを初期状態へ戻し、最初の操作待ちに戻します。
        private void RetryStage()
        {
            simulation.Restart();
            gameplayStarted = false;
            gameplayIdleTimer = 0f;
            clearTimer = -1f;
            SyncRuntimeView();
        }

        private void OpenSelect()
        {
            LoadStageList();
            selectedStageIndex = Mathf.Clamp(selectedStageIndex, 0, selectableStages.Count - 1);
            mode = AppMode.Select;
            selectIdleTimer = 0f;
            selectDemoTimer = 0f;
            StageRuntimeState previewState = new StageRuntimeState(selectableStages[selectedStageIndex], characterSpeed);
            stageRenderer.Render(previewState);
            ConfigureEditorCamera(previewState);
        }

        private void OpenEditorFromSelect()
        {
            currentStageData = selectableStages[Mathf.Clamp(selectedStageIndex, 0, selectableStages.Count - 1)];
            editorController.SetStage(currentStageData);
            mode = AppMode.Editor;
            RenderEditorStage();
        }

        private void OpenEditorFromPlay()
        {
            editorController.SetStage(currentStageData);
            mode = AppMode.Editor;
            RenderEditorStage();
        }

        private void RenderEditorStage()
        {
            StageRuntimeState previewState = new StageRuntimeState(editorController.StageData, characterSpeed);
            stageRenderer.Render(previewState);
            ConfigureEditorCamera(previewState);
        }

        private void ReturnToEditorAfterTestPlay(string message)
        {
            editorController.SetStage(editingStageData);
            editorController.SetStatus(message);
            mode = AppMode.Editor;
            RenderEditorStage();
        }

        private void SyncRuntimeView()
        {
            stageRenderer.SyncFrame(simulation.State.Frame);
            stageRenderer.SyncSignboards(simulation.State);
            stageRenderer.SyncCharacter(simulation.State.Character);
        }

        private bool ConsumeInteractionSignal()
        {
            bool movedMouse = ((Vector2)Input.mousePosition - lastMousePosition).sqrMagnitude > 1f;
            bool interacted = Input.anyKeyDown || Input.GetMouseButtonDown(0) || movedMouse;
            lastMousePosition = Input.mousePosition;
            return interacted;
        }

        private static void ConfigureEditorCamera(StageRuntimeState state)
        {
            Camera camera = EnsureMainCamera();
            Vector3 boardCenter = StageRenderer.BoardCenterWorld(state);
            camera.transform.position = new Vector3(boardCenter.x, boardCenter.y, -10f);
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(state.Height * 0.62f, state.Width * 0.62f / camera.aspect, 4f);
            camera.backgroundColor = new Color(0.2f, 0.24f, 0.3f, 1f);
        }

        private static void ConfigurePlayCamera(StageRuntimeState state)
        {
            Camera camera = EnsureMainCamera();
            camera.orthographic = true;
            camera.orthographicSize = 4f;
            camera.backgroundColor = new Color(0.2f, 0.24f, 0.3f, 1f);
            UpdatePlayCamera(state);
        }

        private static void UpdatePlayCamera(StageRuntimeState state)
        {
            Camera camera = EnsureMainCamera();
            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * camera.aspect;

            Vector3 boardCenter = StageRenderer.BoardCenterWorld(state);
            bool boardFits = state.Width <= halfWidth * 2f && state.Height <= halfHeight * 2f;
            if (boardFits)
            {
                camera.transform.position = new Vector3(boardCenter.x, boardCenter.y, -10f);
                return;
            }

            Vector3 target = StageRenderer.GridToWorldCenter(state.Character.Cell.x + 0.5f, state.Character.Cell.y + 0.5f, -10f);
            float minX = halfWidth;
            float maxX = state.Width - halfWidth;
            float minY = -state.Height + halfHeight;
            float maxY = -halfHeight;

            target.x = minX > maxX ? boardCenter.x : Mathf.Clamp(target.x, minX, maxX);
            target.y = minY > maxY ? boardCenter.y : Mathf.Clamp(target.y, minY, maxY);
            camera.transform.position = new Vector3(target.x, target.y, -10f);
        }

        private static Camera EnsureMainCamera()
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                return camera;
            }

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            return camera;
        }

        private static Rect ScaleRect(Rect rect, float scale)
        {
            Vector2 center = rect.center;
            rect.width *= scale;
            rect.height *= scale;
            rect.center = center;
            return rect;
        }

        private static Color ColorForPreview(char symbol)
        {
            switch (symbol)
            {
                case '#':
                    return new Color(0.12f, 0.14f, 0.17f, 1f);
                case 'O':
                    return new Color(0.18f, 0.18f, 0.24f, 1f);
                case 'G':
                    return new Color(0.25f, 0.8f, 0.42f, 1f);
                default:
                    return new Color(0.78f, 0.82f, 0.88f, 1f);
            }
        }

        private static bool IsStageCompleted(StageData stageData)
        {
            return PlayerPrefs.GetInt($"SukimaWalker.Completed.{stageData.id}", 0) == 1;
        }

        private static void MarkStageCompleted(StageData stageData)
        {
            PlayerPrefs.SetInt($"SukimaWalker.Completed.{stageData.id}", 1);
            PlayerPrefs.Save();
        }
    }
}
