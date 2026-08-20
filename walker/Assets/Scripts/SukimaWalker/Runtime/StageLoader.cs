using System.IO;
using SukimaWalker.Core;
using UnityEngine;

namespace SukimaWalker.Runtime
{
    public static class StageLoader
    {
        public static StageData LoadFirstStreamingStage()
        {
            string stageDirectory = Path.Combine(Application.streamingAssetsPath, "stages");
            if (!Directory.Exists(stageDirectory))
            {
                Debug.LogWarning($"Stage directory not found: {stageDirectory}. Fallback stage will be used.");
                return StageData.CreateFallback();
            }

            string[] files = Directory.GetFiles(stageDirectory, "*.json");
            if (files.Length == 0)
            {
                Debug.LogWarning($"No stage json found in {stageDirectory}. Fallback stage will be used.");
                return StageData.CreateFallback();
            }

            string json = File.ReadAllText(files[0]);
            StageData stageData = JsonUtility.FromJson<StageData>(json);
            return stageData ?? StageData.CreateFallback();
        }
    }
}
