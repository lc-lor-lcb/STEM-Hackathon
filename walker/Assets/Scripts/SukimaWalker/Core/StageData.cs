using System;
using System.Collections.Generic;
using UnityEngine;

namespace SukimaWalker.Core
{
    [Serializable]
    public sealed class StageData
    {
        public string id;
        public string title;
        public int board_width;
        public int board_height;
        public string[] grid;
        public SignboardData[] signboards;
        public CharacterData character;
        public FrameData frame;

        public static StageData CreateFallback()
        {
            return new StageData
            {
                id = "fallback",
                title = "Fallback Test",
                board_width = 8,
                board_height = 5,
                grid = new[]
                {
                    "########",
                    "#.O....#",
                    "#...O..#",
                    "#.....G#",
                    "########"
                },
                signboards = new[]
                {
                    new SignboardData
                    {
                        id = "A",
                        x = 3,
                        y = 2,
                        direction = "UP"
                    }
                },
                character = new CharacterData
                {
                    start_x = 1,
                    start_y = 2,
                    start_direction = "RIGHT"
                },
                frame = new FrameData
                {
                    min_width = 1,
                    min_height = 1,
                    max_width = 2,
                    max_height = 2
                }
            };
        }
    }

    [Serializable]
    public sealed class SignboardData
    {
        public string id;
        public int x;
        public int y;
        public string direction;
    }

    [Serializable]
    public sealed class CharacterData
    {
        public int start_x;
        public int start_y;
        public string start_direction;
    }

    [Serializable]
    public sealed class FrameData
    {
        public float min_width;
        public float min_height;
        public float max_width;
        public float max_height;
    }

    public sealed class StageRuntimeState
    {
        public readonly StageData Source;
        public readonly CellType[,] Terrain;
        public readonly List<SignboardRuntimeState> Signboards = new List<SignboardRuntimeState>();
        public readonly CharacterRuntimeState Character = new CharacterRuntimeState();
        public readonly FrameRuntimeState Frame = new FrameRuntimeState();

        public int Width => Source.board_width;
        public int Height => Source.board_height;

        public StageRuntimeState(StageData source, float characterSpeed)
        {
            Source = StageDataUtility.Normalize(source);
            Terrain = BuildTerrain(Source);
            Reset(characterSpeed);
        }

        public void Reset(float characterSpeed)
        {
            Signboards.Clear();
            if (Source.signboards != null)
            {
                for (int i = 0; i < Source.signboards.Length; i++)
                {
                    SignboardData signboard = Source.signboards[i];
                    Signboards.Add(new SignboardRuntimeState
                    {
                        Id = string.IsNullOrWhiteSpace(signboard.id) ? $"Signboard_{i}" : signboard.id,
                        X = signboard.x,
                        Y = signboard.y,
                        Direction = DirectionExtensions.FromString(signboard.direction),
                        Destroyed = false
                    });
                }
            }

            Character.Cell = new Vector2Int(Source.character.start_x, Source.character.start_y);
            Character.Direction = DirectionExtensions.FromString(Source.character.start_direction);
            Character.Speed = characterSpeed;
            Character.MoveProgress = 0f;
            Character.IsAlive = true;
            Character.HasCleared = false;
            Character.EnteredNewCellThisFrame = false;

            int minWidth = Mathf.Max(1, Source.frame != null ? Mathf.RoundToInt(Source.frame.min_width) : 1);
            int minHeight = Mathf.Max(1, Source.frame != null ? Mathf.RoundToInt(Source.frame.min_height) : 1);
            int maxWidth = Mathf.Max(minWidth, Source.frame != null ? Mathf.RoundToInt(Source.frame.max_width) : 2);
            int maxHeight = Mathf.Max(minHeight, Source.frame != null ? Mathf.RoundToInt(Source.frame.max_height) : 2);

            Frame.MinSize = new Vector2Int(minWidth, minHeight);
            Frame.MaxSize = new Vector2Int(maxWidth, maxHeight);
            Frame.Size = new Vector2Int(
                Mathf.Clamp(Mathf.Max(minWidth, Width - 2), minWidth, Mathf.Min(maxWidth, Width)),
                Mathf.Clamp(Mathf.Max(minHeight, Height - 2), minHeight, Mathf.Min(maxHeight, Height)));
            Frame.Position = new Vector2Int(
                Mathf.Clamp((Width - Frame.Size.x) / 2, 0, Width - Frame.Size.x),
                Mathf.Clamp((Height - Frame.Size.y) / 2, 0, Height - Frame.Size.y));
            Frame.PreviousPosition = Frame.Position;
            Frame.PreviousSize = Frame.Size;
        }

        public CellType GetTerrain(Vector2Int cell)
        {
            if (cell.x < 0 || cell.y < 0 || cell.x >= Width || cell.y >= Height)
            {
                return CellType.Wall;
            }

            return Terrain[cell.x, cell.y];
        }

        public SignboardRuntimeState GetActiveSignboardAt(Vector2Int cell)
        {
            foreach (SignboardRuntimeState signboard in Signboards)
            {
                if (!signboard.Destroyed && signboard.X == cell.x && signboard.Y == cell.y)
                {
                    return signboard;
                }
            }

            return null;
        }

        private static CellType[,] BuildTerrain(StageData source)
        {
            CellType[,] terrain = new CellType[source.board_width, source.board_height];
            for (int y = 0; y < source.board_height; y++)
            {
                string row = source.grid != null && y < source.grid.Length ? source.grid[y] : string.Empty;
                for (int x = 0; x < source.board_width; x++)
                {
                    char symbol = x < row.Length ? row[x] : '.';
                    terrain[x, y] = CellTypeExtensions.FromSymbol(symbol);
                }
            }

            return terrain;
        }
    }

    public sealed class CharacterRuntimeState
    {
        public Vector2Int Cell;
        public Direction Direction;
        public float Speed;
        public float MoveProgress;
        public bool IsAlive;
        public bool HasCleared;
        public bool EnteredNewCellThisFrame;
    }

    public sealed class SignboardRuntimeState
    {
        public string Id;
        public int X;
        public int Y;
        public Direction Direction;
        public bool Destroyed;
    }

    public sealed class FrameRuntimeState
    {
        public Vector2Int Position;
        public Vector2Int Size;
        public Vector2Int MinSize;
        public Vector2Int MaxSize;
        public Vector2Int PreviousPosition;
        public Vector2Int PreviousSize;

        public bool Contains(Vector2Int cell)
        {
            return cell.x >= Position.x
                && cell.y >= Position.y
                && cell.x < Position.x + Size.x
                && cell.y < Position.y + Size.y;
        }
    }
}
