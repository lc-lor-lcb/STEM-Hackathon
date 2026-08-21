using SukimaWalker.Core;
using UnityEngine;

namespace SukimaWalker.Runtime
{
    // マウス操作をFrame移動・リサイズ用の入力コマンドへ変換します。
    public sealed class FrameInputReader
    {
        private const float HitSlop = 0.25f;

        private FrameDragMode activeMode = FrameDragMode.None;
        private Vector2 startPointer;
        private Vector2Int startPosition;
        private Vector2Int startSize;

        // ドラッグ開始位置と現在位置をグリッド座標で返します。
        public FrameInputCommand Capture(FrameRuntimeState frame)
        {
            if (Camera.main == null)
            {
                return FrameInputCommand.None;
            }

            Vector2 pointer = MouseToGrid(Camera.main);

            if (Input.GetMouseButtonDown(0))
            {
                activeMode = HitTest(frame, pointer);
                startPointer = pointer;
                startPosition = frame.Position;
                startSize = frame.Size;
            }

            if (!Input.GetMouseButton(0) || activeMode == FrameDragMode.None)
            {
                if (Input.GetMouseButtonUp(0))
                {
                    activeMode = FrameDragMode.None;
                }

                return FrameInputCommand.None;
            }

            if (Input.GetMouseButtonUp(0))
            {
                activeMode = FrameDragMode.None;
                return FrameInputCommand.None;
            }

            return new FrameInputCommand
            {
                IsActive = true,
                Mode = activeMode,
                StartPointer = startPointer,
                CurrentPointer = pointer,
                StartPosition = startPosition,
                StartSize = startSize
            };
        }

        private static Vector2 MouseToGrid(Camera camera)
        {
            Vector3 world = camera.ScreenToWorldPoint(Input.mousePosition);
            return new Vector2(world.x, -world.y);
        }

        private static FrameDragMode HitTest(FrameRuntimeState frame, Vector2 pointer)
        {
            float left = frame.Position.x;
            float right = frame.Position.x + frame.Size.x;
            float top = frame.Position.y;
            float bottom = frame.Position.y + frame.Size.y;

            bool nearLeft = Mathf.Abs(pointer.x - left) <= HitSlop;
            bool nearRight = Mathf.Abs(pointer.x - right) <= HitSlop;
            bool nearTop = Mathf.Abs(pointer.y - top) <= HitSlop;
            bool nearBottom = Mathf.Abs(pointer.y - bottom) <= HitSlop;
            bool withinHorizontal = pointer.x >= left - HitSlop && pointer.x <= right + HitSlop;
            bool withinVertical = pointer.y >= top - HitSlop && pointer.y <= bottom + HitSlop;

            if (nearLeft && nearTop)
            {
                return FrameDragMode.TopLeft;
            }

            if (nearRight && nearTop)
            {
                return FrameDragMode.TopRight;
            }

            if (nearLeft && nearBottom)
            {
                return FrameDragMode.BottomLeft;
            }

            if (nearRight && nearBottom)
            {
                return FrameDragMode.BottomRight;
            }

            if (nearLeft && withinVertical)
            {
                return FrameDragMode.Left;
            }

            if (nearRight && withinVertical)
            {
                return FrameDragMode.Right;
            }

            if (nearTop && withinHorizontal)
            {
                return FrameDragMode.Top;
            }

            if (nearBottom && withinHorizontal)
            {
                return FrameDragMode.Bottom;
            }

            if (pointer.x > left && pointer.x < right && pointer.y > top && pointer.y < bottom)
            {
                return FrameDragMode.Move;
            }

            return FrameDragMode.None;
        }
    }
}
