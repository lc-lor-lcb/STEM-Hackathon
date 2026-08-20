using SukimaWalker.Core;
using UnityEngine;

namespace SukimaWalker.Runtime
{
    public sealed class SukimaWalkerGameController : MonoBehaviour
    {
        [SerializeField] private float characterSpeed = 3f;

        private GameSimulation simulation;
        private StageRenderer stageRenderer;
        private FrameInputReader frameInputReader;

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
            StageData stageData = StageLoader.LoadFirstStreamingStage();
            simulation = new GameSimulation(stageData, characterSpeed);
            frameInputReader = new FrameInputReader();

            stageRenderer = gameObject.AddComponent<StageRenderer>();
            stageRenderer.Render(simulation.State);

            ConfigureCamera(simulation.State);
        }

        private void Update()
        {
            FrameInputCommand frameInput = frameInputReader.Capture(simulation.State.Frame);
            simulation.Tick(Time.deltaTime, frameInput);
            stageRenderer.SyncTerrainEffects(simulation.State);
            stageRenderer.SyncFrame(simulation.State.Frame);
            stageRenderer.SyncSignboards(simulation.State);
            stageRenderer.SyncCharacter(simulation.State.Character);
        }

        private static void ConfigureCamera(StageRuntimeState state)
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
    }
}
