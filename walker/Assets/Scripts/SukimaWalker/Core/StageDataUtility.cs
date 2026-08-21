using System.Collections.Generic;
using UnityEngine;

namespace SukimaWalker.Core
{
    public static class StageDataUtility
    {
        public static StageData Clone(StageData source)
        {
            StageData clone = source == null
                ? StageData.CreateFallback()
                : JsonUtility.FromJson<StageData>(JsonUtility.ToJson(source, false));
            return Normalize(clone);
        }

        public static StageData Normalize(StageData source)
        {
            if (source == null)
            {
                source = StageData.CreateFallback();
            }

            source.board_width = Mathf.Max(3, source.board_width);
            source.board_height = Mathf.Max(3, source.board_height);

            if (string.IsNullOrWhiteSpace(source.id))
            {
                source.id = "stage";
            }

            if (string.IsNullOrWhiteSpace(source.title))
            {
                source.title = source.id;
            }

            source.difficulty = Mathf.Clamp(source.difficulty <= 0 ? 1 : source.difficulty, 1, 5);

            string[] normalizedGrid = new string[source.board_height];
            for (int y = 0; y < source.board_height; y++)
            {
                string row = source.grid != null && y < source.grid.Length ? source.grid[y] ?? string.Empty : string.Empty;
                char[] cells = new char[source.board_width];
                for (int x = 0; x < source.board_width; x++)
                {
                    cells[x] = x < row.Length ? NormalizeTerrainSymbol(row[x]) : '.';
                }

                normalizedGrid[y] = new string(cells);
            }

            source.grid = normalizedGrid;
            source.signboards = source.signboards ?? new SignboardData[0];
            source.character = source.character ?? new CharacterData
            {
                start_x = 1,
                start_y = 1,
                start_direction = "RIGHT"
            };
            source.frame = source.frame ?? new FrameData
            {
                min_width = 1,
                min_height = 1,
                max_width = 2,
                max_height = 2
            };

            source.character.start_x = Mathf.Clamp(source.character.start_x, 0, source.board_width - 1);
            source.character.start_y = Mathf.Clamp(source.character.start_y, 0, source.board_height - 1);
            source.character.start_direction = DirectionToSymbol(DirectionExtensions.FromString(source.character.start_direction));
            source.frame.min_width = Mathf.Max(1f, source.frame.min_width);
            source.frame.min_height = Mathf.Max(1f, source.frame.min_height);
            source.frame.max_width = Mathf.Max(source.frame.min_width, source.frame.max_width);
            source.frame.max_height = Mathf.Max(source.frame.min_height, source.frame.max_height);

            for (int i = 0; i < source.signboards.Length; i++)
            {
                SignboardData signboard = source.signboards[i];
                signboard.id = string.IsNullOrWhiteSpace(signboard.id) ? $"Signboard_{i}" : signboard.id;
                signboard.x = Mathf.Clamp(signboard.x, 0, source.board_width - 1);
                signboard.y = Mathf.Clamp(signboard.y, 0, source.board_height - 1);
                signboard.direction = DirectionToSymbol(DirectionExtensions.FromString(signboard.direction));
            }

            return source;
        }

        public static StageData CreateEmpty(int width, int height)
        {
            width = Mathf.Max(3, width);
            height = Mathf.Max(3, height);
            string[] grid = new string[height];
            for (int y = 0; y < height; y++)
            {
                char[] row = new char[width];
                for (int x = 0; x < width; x++)
                {
                    row[x] = x == 0 || y == 0 || x == width - 1 || y == height - 1 ? '#' : '.';
                }

                grid[y] = new string(row);
            }

            return new StageData
            {
                id = "new_stage",
                title = "New Stage",
                difficulty = 1,
                board_width = width,
                board_height = height,
                grid = grid,
                signboards = new SignboardData[0],
                character = new CharacterData
                {
                    start_x = 1,
                    start_y = 1,
                    start_direction = "RIGHT"
                },
                frame = new FrameData
                {
                    min_width = 1,
                    min_height = 1,
                    max_width = Mathf.Max(1, width / 3),
                    max_height = Mathf.Max(1, height / 3)
                }
            };
        }

        public static StageData Resize(StageData source, int width, int height)
        {
            source = Normalize(Clone(source));
            width = Mathf.Max(3, width);
            height = Mathf.Max(3, height);

            string[] resized = new string[height];
            for (int y = 0; y < height; y++)
            {
                char[] row = new char[width];
                for (int x = 0; x < width; x++)
                {
                    if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                    {
                        row[x] = '#';
                    }
                    else if (y < source.grid.Length && x < source.grid[y].Length)
                    {
                        row[x] = NormalizeTerrainSymbol(source.grid[y][x]);
                    }
                    else
                    {
                        row[x] = '.';
                    }
                }

                resized[y] = new string(row);
            }

            source.board_width = width;
            source.board_height = height;
            source.grid = resized;
            source.character.start_x = Mathf.Clamp(source.character.start_x, 1, width - 2);
            source.character.start_y = Mathf.Clamp(source.character.start_y, 1, height - 2);

            List<SignboardData> kept = new List<SignboardData>();
            foreach (SignboardData signboard in source.signboards)
            {
                if (signboard.x > 0 && signboard.y > 0 && signboard.x < width - 1 && signboard.y < height - 1)
                {
                    kept.Add(signboard);
                }
            }

            source.signboards = kept.ToArray();
            source.frame.max_width = Mathf.Clamp(source.frame.max_width, source.frame.min_width, width);
            source.frame.max_height = Mathf.Clamp(source.frame.max_height, source.frame.min_height, height);
            return Normalize(source);
        }

        public static char GetTerrainSymbol(StageData stageData, int x, int y)
        {
            stageData = Normalize(stageData);
            return stageData.grid[y][x];
        }

        public static void SetTerrainSymbol(StageData stageData, int x, int y, char symbol)
        {
            stageData = Normalize(stageData);
            char[] row = stageData.grid[y].ToCharArray();
            row[x] = NormalizeTerrainSymbol(symbol);
            stageData.grid[y] = new string(row);
        }

        public static string DirectionToSymbol(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:
                    return "UP";
                case Direction.Down:
                    return "DOWN";
                case Direction.Left:
                    return "LEFT";
                case Direction.Right:
                    return "RIGHT";
                default:
                    return "RIGHT";
            }
        }

        private static char NormalizeTerrainSymbol(char symbol)
        {
            return symbol == '#' || symbol == 'O' || symbol == 'G' ? symbol : '.';
        }
    }
}
