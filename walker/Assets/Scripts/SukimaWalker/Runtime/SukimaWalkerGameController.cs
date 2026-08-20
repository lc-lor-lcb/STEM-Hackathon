using SukimaWalker.Core;
using UnityEngine;

namespace SukimaWalker.Runtime
{
    public sealed class SukimaWalkerGameController : MonoBehaviour
    {
        [SerializeField] private float characterSpeed = 3f;

        private enum AppMode
        {
            Play,
            TestPlay,
            Editor
        }

        private AppMode mode;
        private StageData currentStageData;
        private GameSimulation simulation;
        private StageRenderer stageRenderer;
        private FrameInputReader frameInputReader;
        private StageEditorController editorController;
        private StageData editingStageData;

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

        private void Awake()
        {
            currentStageData = StageLoader.LoadFirstStreamingStage();
            frameInputReader = new FrameInputReader();

            stageRenderer = gameObject.AddComponent<StageRenderer>();
            editorController = new StageEditorController(currentStageData);
            StartPlay(currentStageData);
        }

        private void Update()
        {
            if (mode == AppMode.Editor)
            {
                editorController.Update();
                if (editorController.ConsumeRenderDirty())
                {
                    RenderEditorStage();
                }

                return;
            }

            FrameInputCommand frameInput = frameInputReader.Capture(simulation.State.Frame);
            simulation.Tick(Time.deltaTime, frameInput);
            stageRenderer.SyncTerrainEffects(simulation.State);
            stageRenderer.SyncFrame(simulation.State.Frame);
            stageRenderer.SyncSignboards(simulation.State);
            stageRenderer.SyncCharacter(simulation.State.Character);
            UpdatePlayCamera(simulation.State);

            if (mode == AppMode.TestPlay && !simulation.State.Character.IsAlive)
            {
                ReturnToEditorAfterTestPlay("Test play ended: character died.");
            }
            else if (mode == AppMode.TestPlay && simulation.State.Character.HasCleared)
            {
                ReturnToEditorAfterTestPlay("Test play ended: stage cleared.");
            }
        }

        private void OnGUI()
        {
            if (mode == AppMode.Play || mode == AppMode.TestPlay)
            {
                GUILayout.BeginArea(new Rect(8f, 8f, 160f, 48f), GUI.skin.box);
                if (mode == AppMode.Play && GUILayout.Button("Editor"))
                {
                    OpenEditor();
                }

                if (mode == AppMode.TestPlay && GUILayout.Button("Back To Editor"))
                {
                    ReturnToEditorAfterTestPlay("Test play ended.");
                }

                GUILayout.EndArea();
                return;
            }

            editorController.DrawGui(StartTestPlayFromEditor);
        }

        private void OpenEditor()
        {
            currentStageData = simulation != null ? simulation.State.Source : currentStageData;
            editorController.SetStage(currentStageData);
            mode = AppMode.Editor;
            RenderEditorStage();
        }

        private void StartTestPlayFromEditor(StageData stageData)
        {
            editingStageData = StageDataUtility.Clone(stageData);
            simulation = new GameSimulation(editingStageData, characterSpeed, false);
            mode = AppMode.TestPlay;
            stageRenderer.Render(simulation.State);
            ConfigurePlayCamera(simulation.State);
        }

        private void StartPlay(StageData stageData)
        {
            currentStageData = StageDataUtility.Clone(stageData);
            simulation = new GameSimulation(currentStageData, characterSpeed);
            mode = AppMode.Play;
            stageRenderer.Render(simulation.State);
            ConfigurePlayCamera(simulation.State);
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

        private static void ConfigureEditorCamera(StageRuntimeState state)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

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

            Vector3 target = StageRenderer.GridToWorldCenter(state.Character.Position.x, state.Character.Position.y, -10f);
            float minX = halfWidth;
            float maxX = state.Width - halfWidth;
            float minY = -state.Height + halfHeight;
            float maxY = -halfHeight;

            if (minX > maxX)
            {
                target.x = boardCenter.x;
            }
            else
            {
                target.x = Mathf.Clamp(target.x, minX, maxX);
            }

            if (minY > maxY)
            {
                target.y = boardCenter.y;
            }
            else
            {
                target.y = Mathf.Clamp(target.y, minY, maxY);
            }

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
    }
}
