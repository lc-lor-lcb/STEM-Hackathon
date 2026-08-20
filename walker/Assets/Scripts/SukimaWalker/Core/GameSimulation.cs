using UnityEngine;

namespace SukimaWalker.Core
{
    public sealed class GameSimulation
    {
        private const float WallContactEpsilon = 0.001f;
        private const float CharacterCollisionRadius = 0.28f;

        private readonly float characterSpeed;

        public StageRuntimeState State { get; }

        public GameSimulation(StageData stageData, float characterSpeed)
        {
            this.characterSpeed = characterSpeed;
            State = new StageRuntimeState(stageData, characterSpeed);
        }

        public void Tick(float deltaTime, FrameInputCommand frameInput)
        {
            if (!State.Character.IsAlive || State.Character.HasCleared)
            {
                return;
            }

            ApplyPlayerInputAndUpdateFrame(frameInput);
            PushSignboardsByFrameEdges();
            RecalculateFrameDisabledHoles();
            ResolveFrameEdgeCharacterCollision();
            MoveCharacter(deltaTime);
            EvaluateEnteredCellTerrainEffects();
            ResolveDeathOrClear();
        }

        public void Restart()
        {
            State.Reset(characterSpeed);
        }

        private void ApplyPlayerInputAndUpdateFrame(FrameInputCommand frameInput)
        {
            FrameRuntimeState frame = State.Frame;
            frame.PreviousPosition = frame.Position;
            frame.PreviousSize = frame.Size;

            if (!frameInput.IsActive || frameInput.Mode == FrameDragMode.None)
            {
                return;
            }

            Vector2 pointerDelta = frameInput.CurrentPointer - frameInput.StartPointer;

            if (frameInput.Mode == FrameDragMode.Move)
            {
                frame.Position = ClampFramePosition(frameInput.StartPosition + pointerDelta, frame.Size);
                return;
            }

            ApplyResize(frame, frameInput, pointerDelta);
        }

        private void PushSignboardsByFrameEdges()
        {
            // Step 7 adds signboard pushing here.
        }

        private void RecalculateFrameDisabledHoles()
        {
            // Step 8 adds disabled-hole recalculation here.
        }

        private void ResolveFrameEdgeCharacterCollision()
        {
            bool isTouchingEdge = IsCharacterTouchingAnyFrameEdge(State.Character.Position, State.Frame);
            if (isTouchingEdge && !State.Frame.CharacterWasTouchingEdge)
            {
                State.Character.Direction = State.Character.Direction.Opposite();
            }

            State.Frame.CharacterWasTouchingEdge = isTouchingEdge;
        }

        private void ApplyResize(FrameRuntimeState frame, FrameInputCommand frameInput, Vector2 pointerDelta)
        {
            bool left = frameInput.Mode == FrameDragMode.Left || frameInput.Mode == FrameDragMode.TopLeft || frameInput.Mode == FrameDragMode.BottomLeft;
            bool right = frameInput.Mode == FrameDragMode.Right || frameInput.Mode == FrameDragMode.TopRight || frameInput.Mode == FrameDragMode.BottomRight;
            bool top = frameInput.Mode == FrameDragMode.Top || frameInput.Mode == FrameDragMode.TopLeft || frameInput.Mode == FrameDragMode.TopRight;
            bool bottom = frameInput.Mode == FrameDragMode.Bottom || frameInput.Mode == FrameDragMode.BottomLeft || frameInput.Mode == FrameDragMode.BottomRight;

            float x = frameInput.StartPosition.x;
            float y = frameInput.StartPosition.y;
            float width = frameInput.StartSize.x;
            float height = frameInput.StartSize.y;

            if (left)
            {
                float fixedRight = frameInput.StartPosition.x + frameInput.StartSize.x;
                width = Mathf.Clamp(frameInput.StartSize.x - pointerDelta.x, frame.MinSize.x, frame.MaxSize.x);
                x = fixedRight - width;
                if (x < 0f)
                {
                    x = 0f;
                    width = fixedRight;
                }
            }
            else if (right)
            {
                width = Mathf.Clamp(frameInput.StartSize.x + pointerDelta.x, frame.MinSize.x, frame.MaxSize.x);
            }

            if (top)
            {
                float fixedBottom = frameInput.StartPosition.y + frameInput.StartSize.y;
                height = Mathf.Clamp(frameInput.StartSize.y - pointerDelta.y, frame.MinSize.y, frame.MaxSize.y);
                y = fixedBottom - height;
                if (y < 0f)
                {
                    y = 0f;
                    height = fixedBottom;
                }
            }
            else if (bottom)
            {
                height = Mathf.Clamp(frameInput.StartSize.y + pointerDelta.y, frame.MinSize.y, frame.MaxSize.y);
            }

            width = Mathf.Clamp(width, frame.MinSize.x, Mathf.Min(frame.MaxSize.x, State.Width - x));
            height = Mathf.Clamp(height, frame.MinSize.y, Mathf.Min(frame.MaxSize.y, State.Height - y));

            frame.Position = ClampFramePosition(new Vector2(x, y), new Vector2(width, height));
            frame.Size = new Vector2(width, height);
        }

        private Vector2 ClampFramePosition(Vector2 position, Vector2 size)
        {
            return new Vector2(
                Mathf.Clamp(position.x, 0f, Mathf.Max(0f, State.Width - size.x)),
                Mathf.Clamp(position.y, 0f, Mathf.Max(0f, State.Height - size.y)));
        }

        private static bool IsCharacterTouchingAnyFrameEdge(Vector2 characterPosition, FrameRuntimeState frame)
        {
            float left = frame.Position.x;
            float right = frame.Position.x + frame.Size.x;
            float top = frame.Position.y;
            float bottom = frame.Position.y + frame.Size.y;

            float radiusSqr = CharacterCollisionRadius * CharacterCollisionRadius;
            return DistanceToSegmentSqr(characterPosition, new Vector2(left, top), new Vector2(right, top)) <= radiusSqr
                || DistanceToSegmentSqr(characterPosition, new Vector2(left, bottom), new Vector2(right, bottom)) <= radiusSqr
                || DistanceToSegmentSqr(characterPosition, new Vector2(left, top), new Vector2(left, bottom)) <= radiusSqr
                || DistanceToSegmentSqr(characterPosition, new Vector2(right, top), new Vector2(right, bottom)) <= radiusSqr;
        }

        private static float DistanceToSegmentSqr(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSqr = segment.sqrMagnitude;
            if (lengthSqr <= Mathf.Epsilon)
            {
                return (point - start).sqrMagnitude;
            }

            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSqr);
            Vector2 closest = start + segment * t;
            return (point - closest).sqrMagnitude;
        }

        private void MoveCharacter(float deltaTime)
        {
            CharacterRuntimeState character = State.Character;
            character.PendingWallHit = false;
            character.EnteredNewCellThisFrame = false;

            float distance = Mathf.Max(0f, character.Speed * deltaTime);
            if (distance <= 0f)
            {
                return;
            }

            Vector2Int currentCell = StageRuntimeState.CellFromPosition(character.Position);
            Vector2Int nextCell = currentCell + DirectionToCellStep(character.Direction);
            float distanceToNextBoundary = DistanceToNextCellBoundary(character.Position, currentCell, character.Direction);

            if (State.GetTerrain(nextCell) == CellType.Wall && distance >= distanceToNextBoundary)
            {
                character.Position = PositionJustBeforeBoundary(character.Position, currentCell, character.Direction);
                character.CurrentCell = currentCell;
                character.PendingWallHit = true;
                return;
            }

            character.Position += character.Direction.ToGridVector() * distance;

            Vector2Int newCell = StageRuntimeState.CellFromPosition(character.Position);
            character.EnteredNewCellThisFrame = newCell != character.CurrentCell;
            character.CurrentCell = newCell;
        }

        private void EvaluateEnteredCellTerrainEffects()
        {
            CharacterRuntimeState character = State.Character;

            if (character.PendingWallHit)
            {
                character.Direction = character.Direction.Opposite();
                character.PendingWallHit = false;
                return;
            }

            if (!character.EnteredNewCellThisFrame)
            {
                return;
            }

            CellType terrain = State.GetTerrain(character.CurrentCell);
            if (terrain == CellType.Wall)
            {
                character.Direction = character.Direction.Opposite();
            }
            else if (terrain == CellType.Hole)
            {
                character.IsAlive = false;
            }
            else if (terrain == CellType.Goal)
            {
                character.HasCleared = true;
            }
        }

        private void ResolveDeathOrClear()
        {
            if (!State.Character.IsAlive)
            {
                Restart();
            }
        }

        private static Vector2Int DirectionToCellStep(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:
                    return Vector2Int.down;
                case Direction.Down:
                    return Vector2Int.up;
                case Direction.Left:
                    return Vector2Int.left;
                case Direction.Right:
                    return Vector2Int.right;
                default:
                    return Vector2Int.right;
            }
        }

        private static float DistanceToNextCellBoundary(Vector2 position, Vector2Int currentCell, Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:
                    return position.y - currentCell.y;
                case Direction.Down:
                    return currentCell.y + 1f - position.y;
                case Direction.Left:
                    return position.x - currentCell.x;
                case Direction.Right:
                    return currentCell.x + 1f - position.x;
                default:
                    return 0f;
            }
        }

        private static Vector2 PositionJustBeforeBoundary(Vector2 position, Vector2Int currentCell, Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:
                    return new Vector2(position.x, currentCell.y + WallContactEpsilon);
                case Direction.Down:
                    return new Vector2(position.x, currentCell.y + 1f - WallContactEpsilon);
                case Direction.Left:
                    return new Vector2(currentCell.x + WallContactEpsilon, position.y);
                case Direction.Right:
                    return new Vector2(currentCell.x + 1f - WallContactEpsilon, position.y);
                default:
                    return position;
            }
        }
    }
}
