using UnityEngine;

namespace SukimaWalker.Core
{
    public enum FrameDragMode
    {
        None,
        Move,
        Left,
        Right,
        Top,
        Bottom,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    public struct FrameInputCommand
    {
        public bool IsActive;
        public FrameDragMode Mode;
        public Vector2 StartPointer;
        public Vector2 CurrentPointer;
        public Vector2Int StartPosition;
        public Vector2Int StartSize;

        public static FrameInputCommand None => new FrameInputCommand
        {
            IsActive = false,
            Mode = FrameDragMode.None
        };
    }
}
