using System.Collections.Generic;
using System.IO;
using SukimaWalker.Core;
using UnityEngine;

namespace SukimaWalker.Runtime
{
    // エディターで選べる配置ツールです。
    public enum StageEditorTool
    {
        Floor,
        Wall,
        Hole,
        Goal,
        Signboard,
        Character,
        Erase
    }

    // ゲーム内ステージエディターの入力、保存、バリデーションをまとめます。
    public sealed class StageEditorController
    {
        private const float PanelWidth = 320f;

        private StageData stageData;
        private StageEditorTool selectedTool = StageEditorTool.Floor;
        private Direction selectedDirection = Direction.Right;
        private bool renderDirty = true;
        private string widthInput;
        private string heightInput;
        private string difficultyInput;
        private string minWidthInput;
        private string minHeightInput;
        private string maxWidthInput;
        private string maxHeightInput;
        private string statusMessage = string.Empty;
        private Vector2 scroll;

        public StageData StageData => stageData;

        public StageEditorController(StageData source)
        {
            SetStage(source);
        }

        public void SetStage(StageData source)
        {
            stageData = StageDataUtility.Clone(source);
            SyncInputsFromStage();
            renderDirty = true;
            statusMessage = string.Empty;
        }

        public bool ConsumeRenderDirty()
        {
            bool wasDirty = renderDirty;
            renderDirty = false;
            return wasDirty;
        }

        public void SetStatus(string message)
        {
            statusMessage = message;
        }

        public void Update()
        {
            if (!Input.GetMouseButton(0) || Input.mousePosition.x <= PanelWidth)
            {
                return;
            }

            if (Camera.main == null)
            {
                return;
            }

            Vector3 world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            int x = Mathf.FloorToInt(world.x);
            int y = Mathf.FloorToInt(-world.y);
            if (x < 0 || y < 0 || x >= stageData.board_width || y >= stageData.board_height)
            {
                return;
            }

            ApplyTool(x, y);
        }

        public void DrawGui(System.Action<StageData> playStage)
        {
            GUILayout.BeginArea(new Rect(8f, 8f, PanelWidth - 16f, Screen.height - 16f), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);

            GUILayout.Label("Stage Editor");
            GUILayout.Space(4f);

            stageData.id = LabeledTextField("ID", stageData.id);
            stageData.title = LabeledTextField("Title", stageData.title);

            GUILayout.Space(8f);
            GUILayout.Label("Board");
            GUILayout.BeginHorizontal();
            widthInput = LabeledShortTextField("W", widthInput);
            heightInput = LabeledShortTextField("H", heightInput);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Apply Board Size"))
            {
                ApplyBoardSize();
            }

            GUILayout.BeginHorizontal();
            difficultyInput = LabeledShortTextField("***", difficultyInput);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Apply Stars"))
            {
                ApplyDifficulty();
            }

            GUILayout.Space(8f);
            GUILayout.Label("Palette");
            DrawToolButton(StageEditorTool.Floor, "Floor");
            DrawToolButton(StageEditorTool.Wall, "Wall");
            DrawToolButton(StageEditorTool.Hole, "Hole");
            DrawToolButton(StageEditorTool.Goal, "Goal");
            DrawToolButton(StageEditorTool.Signboard, "Signboard");
            DrawToolButton(StageEditorTool.Character, "Character");
            DrawToolButton(StageEditorTool.Erase, "Erase");

            GUILayout.Space(8f);
            GUILayout.Label("Direction");
            GUILayout.BeginHorizontal();
            DrawDirectionButton(Direction.Up, "Up");
            DrawDirectionButton(Direction.Down, "Down");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            DrawDirectionButton(Direction.Left, "Left");
            DrawDirectionButton(Direction.Right, "Right");
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("Frame");
            GUILayout.BeginHorizontal();
            minWidthInput = LabeledShortTextField("Min W", minWidthInput);
            minHeightInput = LabeledShortTextField("Min H", minHeightInput);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            maxWidthInput = LabeledShortTextField("Max W", maxWidthInput);
            maxHeightInput = LabeledShortTextField("Max H", maxHeightInput);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Apply Frame Settings"))
            {
                ApplyFrameSettings();
            }

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("New"))
            {
                SetStage(StageDataUtility.CreateEmpty(8, 5));
            }

            if (GUILayout.Button("Save"))
            {
                SaveCurrentStage();
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Test Play Current"))
            {
                playStage(StageDataUtility.Clone(stageData));
            }

            GUILayout.Space(8f);
            GUILayout.Label("Load");
            foreach (string path in StageRepository.ListStagePaths())
            {
                if (GUILayout.Button(Path.GetFileNameWithoutExtension(path)))
                {
                    SetStage(StageRepository.Load(path));
                }
            }

            DrawValidation();

            if (!string.IsNullOrWhiteSpace(statusMessage))
            {
                GUILayout.Space(8f);
                GUILayout.Label(statusMessage);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // 盤面セルへのドラッグ/クリックを、現在選択中のツールとして反映します。
        private void ApplyTool(int x, int y)
        {
            StageDataUtility.Normalize(stageData);

            if (selectedTool == StageEditorTool.Signboard)
            {
                if (StageDataUtility.GetTerrainSymbol(stageData, x, y) != '.')
                {
                    statusMessage = "Signboards can be placed only on floor cells.";
                    return;
                }

                RemoveSignboardAt(x, y);
                List<SignboardData> signboards = new List<SignboardData>(stageData.signboards);
                signboards.Add(new SignboardData
                {
                    id = NextSignboardId(signboards.Count),
                    x = x,
                    y = y,
                    direction = StageDataUtility.DirectionToSymbol(selectedDirection)
                });
                stageData.signboards = signboards.ToArray();
            }
            else if (selectedTool == StageEditorTool.Character)
            {
                if (StageDataUtility.GetTerrainSymbol(stageData, x, y) == '#' || StageDataUtility.GetTerrainSymbol(stageData, x, y) == 'O')
                {
                    statusMessage = "Character start cannot be placed on wall or hole cells.";
                    return;
                }

                stageData.character.start_x = x;
                stageData.character.start_y = y;
                stageData.character.start_direction = StageDataUtility.DirectionToSymbol(selectedDirection);
            }
            else
            {
                char symbol = SymbolForTool(selectedTool);
                StageDataUtility.SetTerrainSymbol(stageData, x, y, symbol);
                if (symbol != '.')
                {
                    RemoveSignboardAt(x, y);
                }
                else if (selectedTool == StageEditorTool.Erase)
                {
                    RemoveSignboardAt(x, y);
                }
            }

            statusMessage = string.Empty;
            renderDirty = true;
        }

        // 盤面サイズを変更し、外周壁と既存配置をできる範囲で保ちます。
        private void ApplyBoardSize()
        {
            if (int.TryParse(widthInput, out int width) && int.TryParse(heightInput, out int height))
            {
                stageData = StageDataUtility.Resize(stageData, width, height);
                SyncInputsFromStage();
                renderDirty = true;
            }
        }

        private void ApplyDifficulty()
        {
            if (int.TryParse(difficultyInput, out int difficulty))
            {
                stageData.difficulty = Mathf.Clamp(difficulty, 1, 5);
                SyncInputsFromStage();
                renderDirty = true;
            }
        }

        // Frameの最小/最大サイズをステージデータへ保存します。
        private void ApplyFrameSettings()
        {
            if (!float.TryParse(minWidthInput, out float minWidth)
                || !float.TryParse(minHeightInput, out float minHeight)
                || !float.TryParse(maxWidthInput, out float maxWidth)
                || !float.TryParse(maxHeightInput, out float maxHeight))
            {
                return;
            }

            stageData.frame.min_width = Mathf.Max(1f, minWidth);
            stageData.frame.min_height = Mathf.Max(1f, minHeight);
            stageData.frame.max_width = Mathf.Max(stageData.frame.min_width, maxWidth);
            stageData.frame.max_height = Mathf.Max(stageData.frame.min_height, maxHeight);
            StageDataUtility.Normalize(stageData);
            SyncInputsFromStage();
            renderDirty = true;
        }

        // JSONスキーマのステージファイルとして保存します。
        private void SaveCurrentStage()
        {
            StageDataUtility.Normalize(stageData);
            string path = StageRepository.Save(stageData);
            statusMessage = $"Saved: {path}";
        }

        // 保存前・テスト前に見るべき問題を警告として出します。
        private void DrawValidation()
        {
            List<string> warnings = StageValidator.Validate(stageData);
            GUILayout.Space(8f);
            GUILayout.Label("Validation");
            if (warnings.Count == 0)
            {
                GUILayout.Label("OK");
                return;
            }

            foreach (string warning in warnings)
            {
                GUILayout.Label($"- {warning}");
            }
        }

        private void DrawToolButton(StageEditorTool tool, string label)
        {
            bool isSelected = selectedTool == tool;
            if (GUILayout.Toggle(isSelected, label, "Button") && !isSelected)
            {
                selectedTool = tool;
            }
        }

        private void DrawDirectionButton(Direction direction, string label)
        {
            bool isSelected = selectedDirection == direction;
            if (GUILayout.Toggle(isSelected, label, "Button") && !isSelected)
            {
                selectedDirection = direction;
            }
        }

        private static string LabeledTextField(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(54f));
            value = GUILayout.TextField(value ?? string.Empty);
            GUILayout.EndHorizontal();
            return value;
        }

        private static string LabeledShortTextField(string label, string value)
        {
            GUILayout.Label(label, GUILayout.Width(48f));
            return GUILayout.TextField(value ?? string.Empty, GUILayout.Width(54f));
        }

        private void SyncInputsFromStage()
        {
            StageDataUtility.Normalize(stageData);
            widthInput = stageData.board_width.ToString();
            heightInput = stageData.board_height.ToString();
            difficultyInput = stageData.difficulty.ToString();
            minWidthInput = stageData.frame.min_width.ToString("0.##");
            minHeightInput = stageData.frame.min_height.ToString("0.##");
            maxWidthInput = stageData.frame.max_width.ToString("0.##");
            maxHeightInput = stageData.frame.max_height.ToString("0.##");
        }

        private void RemoveSignboardAt(int x, int y)
        {
            List<SignboardData> signboards = new List<SignboardData>();
            foreach (SignboardData signboard in stageData.signboards)
            {
                if (signboard.x != x || signboard.y != y)
                {
                    signboards.Add(signboard);
                }
            }

            stageData.signboards = signboards.ToArray();
        }

        private static string NextSignboardId(int count)
        {
            return ((char)('A' + count % 26)).ToString();
        }

        private static char SymbolForTool(StageEditorTool tool)
        {
            switch (tool)
            {
                case StageEditorTool.Wall:
                    return '#';
                case StageEditorTool.Hole:
                    return 'O';
                case StageEditorTool.Goal:
                    return 'G';
                case StageEditorTool.Floor:
                case StageEditorTool.Erase:
                default:
                    return '.';
            }
        }
    }
}
