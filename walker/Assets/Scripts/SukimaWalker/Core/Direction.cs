using UnityEngine;

namespace SukimaWalker.Core
{
    public enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }

    public static class DirectionExtensions
    {
        public static Direction Opposite(this Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:
                    return Direction.Down;
                case Direction.Down:
                    return Direction.Up;
                case Direction.Left:
                    return Direction.Right;
                case Direction.Right:
                    return Direction.Left;
                default:
                    return Direction.Right;
            }
        }

        public static Vector2 ToGridVector(this Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:
                    return Vector2.down;
                case Direction.Down:
                    return Vector2.up;
                case Direction.Left:
                    return Vector2.left;
                case Direction.Right:
                    return Vector2.right;
                default:
                    return Vector2.right;
            }
        }

        public static Direction FromString(string value, Direction fallback = Direction.Right)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            switch (value.Trim().ToUpperInvariant())
            {
                case "UP":
                    return Direction.Up;
                case "DOWN":
                    return Direction.Down;
                case "LEFT":
                    return Direction.Left;
                case "RIGHT":
                    return Direction.Right;
                default:
                    return fallback;
            }
        }
    }
}
