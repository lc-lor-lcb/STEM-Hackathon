using System;
using System.Collections.Generic;
using System.IO;
using SukimaWalker.Core;
using UnityEngine;

namespace SukimaWalker.Runtime
{
    public static class StageRepository
    {
        public static string UserStageDirectory => Path.Combine(Application.persistentDataPath, "stages");
        public static string StreamingStageDirectory => Path.Combine(Application.streamingAssetsPath, "stages");

        public static StageData LoadFirstStage()
        {
            List<string> paths = ListStagePaths();
            if (paths.Count == 0)
            {
                Debug.LogWarning("No stage json found. Fallback stage will be used.");
                return StageData.CreateFallback();
            }

            return Load(paths[0]);
        }

        public static List<string> ListStagePaths()
        {
            List<string> paths = new List<string>();
            AddStagePaths(paths, UserStageDirectory);
            AddStagePaths(paths, StreamingStageDirectory);
            return paths;
        }

        public static StageData Load(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                StageData stageData = JsonUtility.FromJson<StageData>(json);
                return StageDataUtility.Normalize(stageData);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to load stage at {path}: {exception.Message}");
                return StageData.CreateFallback();
            }
        }

        public static string Save(StageData stageData)
        {
            stageData = StageDataUtility.Normalize(stageData);
            Directory.CreateDirectory(UserStageDirectory);

            string fileName = SanitizeFileName(stageData.id);
            string path = Path.Combine(UserStageDirectory, $"{fileName}.json");
            string json = JsonUtility.ToJson(stageData, true);
            File.WriteAllText(path, json);
            return path;
        }

        private static void AddStagePaths(List<string> paths, string directory)
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            string[] files = Directory.GetFiles(directory, "*.json");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            paths.AddRange(files);
        }

        private static string SanitizeFileName(string value)
        {
            string safe = string.IsNullOrWhiteSpace(value) ? "stage" : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                safe = safe.Replace(invalid, '_');
            }

            return safe;
        }
    }
}
