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
        public readonly HashSet<Vector2Int> DisabledHoles = new HashSet<Vector2Int>();
        public readonly CharacterRuntimeState Character = new CharacterRuntimeState();
        public readonly FrameRuntimeState Frame = new FrameRuntimeState();

        public int Width => Source.board_width;
        public int Height => Source.board_height;

        public StageRuntimeState(StageData source, float characterSpeed)
        {
            Source = source;
            Terrain = BuildTerrain(source);
            Reset(characterSpeed);
        }

        public void Reset(float characterSpeed)
        {
            Signboards.Clear();
            DisabledHoles.Clear();
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

            Character.Position = new Vector2(Source.character.start_x + 0.5f, Source.character.start_y + 0.5f);
            Character.Direction = DirectionExtensions.FromString(Source.character.start_direction);
            Character.Speed = characterSpeed;
            Character.IsAlive = true;
            Character.HasCleared = false;
            Character.CurrentCell = new Vector2Int(Source.character.start_x, Source.character.start_y);
            Character.EnteredNewCellThisFrame = false;
            Character.PendingWallHit = false;

            Frame.MinSize = new Vector2(
                Source.frame != null && Source.frame.min_width > 0f ? Source.frame.min_width : 1f,
                Source.frame != null && Source.frame.min_height > 0f ? Source.frame.min_height : 1f);
            Frame.MaxSize = new Vector2(
                Source.frame != null && Source.frame.max_width > 0f ? Source.frame.max_width : 2f,
                Source.frame != null && Source.frame.max_height > 0f ? Source.frame.max_height : 2f);
            Frame.MinSize = Vector2.Max(Frame.MinSize, Vector2.one);
            Frame.MaxSize = Vector2.Max(Frame.MaxSize, Frame.MinSize);
            Frame.Size = new Vector2(
                Mathf.Min(Frame.MaxSize.x, Mathf.Max(Frame.MinSize.x, Width - 2f)),
                Mathf.Min(Frame.MaxSize.y, Mathf.Max(Frame.MinSize.y, Height - 2f)));
            Frame.Position = new Vector2(
                Mathf.Clamp((Width - Frame.Size.x) * 0.5f, 0f, Width - Frame.Size.x),
                Mathf.Clamp((Height - Frame.Size.y) * 0.5f, 0f, Height - Frame.Size.y));
            Frame.PreviousPosition = Frame.Position;
            Frame.PreviousSize = Frame.Size;
            Frame.CharacterWasTouchingEdge = false;
        }

        public CellType GetTerrain(Vector2Int cell)
        {
            if (cell.x < 0 || cell.y < 0 || cell.x >= Width || cell.y >= Height)
            {
                return CellType.Wall;
            }

            return Terrain[cell.x, cell.y];
        }

        public bool IsHoleDisabled(Vector2Int cell)
        {
            return DisabledHoles.Contains(cell);
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

        public static Vector2Int CellFromPosition(Vector2 position)
        {
            return new Vector2Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y));
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
        public Vector2 Position;
        public Direction Direction;
        public float Speed;
        public bool IsAlive;
        public bool HasCleared;
        public Vector2Int CurrentCell;
        public bool EnteredNewCellThisFrame;
        public bool PendingWallHit;
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
        public Vector2 Position;
        public Vector2 Size;
        public Vector2 MinSize;
        public Vector2 MaxSize;
        public Vector2 PreviousPosition;
        public Vector2 PreviousSize;
        public bool CharacterWasTouchingEdge;

        public Rect Rect => new Rect(Position.x, Position.y, Size.x, Size.y);
    }
}
