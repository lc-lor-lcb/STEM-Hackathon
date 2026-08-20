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
            FrameRuntimeState frame = State.Frame;
            float previousLeft = frame.PreviousPosition.x;
            float previousRight = frame.PreviousPosition.x + frame.PreviousSize.x;
            float previousTop = frame.PreviousPosition.y;
            float previousBottom = frame.PreviousPosition.y + frame.PreviousSize.y;
            float currentLeft = frame.Position.x;
            float currentRight = frame.Position.x + frame.Size.x;
            float currentTop = frame.Position.y;
            float currentBottom = frame.Position.y + frame.Size.y;

            foreach (SignboardRuntimeState signboard in State.Signboards)
            {
                if (signboard.Destroyed)
                {
                    continue;
                }

                bool pushed = false;
                if (currentRight > previousRight && previousRight <= signboard.X && currentRight >= signboard.X && RangesOverlap(currentTop, currentBottom, signboard.Y, signboard.Y + 1f))
                {
                    pushed = TryPushSignboard(signboard, Direction.Right);
                    if (!pushed)
                    {
                        StopFrameAtRightEdge(signboard.X);
                    }
                }
                else if (currentLeft < previousLeft && previousLeft >= signboard.X + 1f && currentLeft <= signboard.X + 1f && RangesOverlap(currentTop, currentBottom, signboard.Y, signboard.Y + 1f))
                {
                    pushed = TryPushSignboard(signboard, Direction.Left);
                    if (!pushed)
                    {
                        StopFrameAtLeftEdge(signboard.X + 1f);
                    }
                }
                else if (currentBottom > previousBottom && previousBottom <= signboard.Y && currentBottom >= signboard.Y && RangesOverlap(currentLeft, currentRight, signboard.X, signboard.X + 1f))
                {
                    pushed = TryPushSignboard(signboard, Direction.Down);
                    if (!pushed)
                    {
                        StopFrameAtBottomEdge(signboard.Y);
                    }
                }
                else if (currentTop < previousTop && previousTop >= signboard.Y + 1f && currentTop <= signboard.Y + 1f && RangesOverlap(currentLeft, currentRight, signboard.X, signboard.X + 1f))
                {
                    pushed = TryPushSignboard(signboard, Direction.Up);
                    if (!pushed)
                    {
                        StopFrameAtTopEdge(signboard.Y + 1f);
                    }
                }

                currentLeft = frame.Position.x;
                currentRight = frame.Position.x + frame.Size.x;
                currentTop = frame.Position.y;
                currentBottom = frame.Position.y + frame.Size.y;
            }
        }

        private void RecalculateFrameDisabledHoles()
        {
            State.DisabledHoles.Clear();
            for (int y = 0; y < State.Height; y++)
            {
                for (int x = 0; x < State.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (State.GetTerrain(cell) == CellType.Hole && IsCellCenterCoveredByFrame(cell))
                    {
                        State.DisabledHoles.Add(cell);
                    }
                }
            }
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

        private bool TryPushSignboard(SignboardRuntimeState signboard, Direction direction)
        {
            Vector2Int destination = new Vector2Int(signboard.X, signboard.Y) + DirectionToCellStep(direction);

            if (destination == StageRuntimeState.CellFromPosition(State.Character.Position))
            {
                return false;
            }

            if (State.GetActiveSignboardAt(destination) != null)
            {
                return false;
            }

            CellType destinationTerrain = State.GetTerrain(destination);
            if (destinationTerrain == CellType.Wall)
            {
                return false;
            }

            if (destinationTerrain == CellType.Hole && !IsCellCenterCoveredByFrame(destination))
            {
                signboard.Destroyed = true;
                return true;
            }

            signboard.X = destination.x;
            signboard.Y = destination.y;
            return true;
        }

        private bool IsCellCenterCoveredByFrame(Vector2Int cell)
        {
            FrameRuntimeState frame = State.Frame;
            Vector2 center = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
            return center.x >= frame.Position.x
                && center.x <= frame.Position.x + frame.Size.x
                && center.y >= frame.Position.y
                && center.y <= frame.Position.y + frame.Size.y;
        }

        private void StopFrameAtRightEdge(float rightEdge)
        {
            FrameRuntimeState frame = State.Frame;
            if (Mathf.Approximately(frame.PreviousSize.x, frame.Size.x))
            {
                frame.Position.x = Mathf.Clamp(rightEdge - frame.Size.x, 0f, State.Width - frame.Size.x);
            }
            else
            {
                frame.Size.x = Mathf.Clamp(rightEdge - frame.Position.x, frame.MinSize.x, frame.MaxSize.x);
            }
        }

        private void StopFrameAtLeftEdge(float leftEdge)
        {
            FrameRuntimeState frame = State.Frame;
            float currentRight = frame.Position.x + frame.Size.x;
            if (Mathf.Approximately(frame.PreviousSize.x, frame.Size.x))
            {
                frame.Position.x = Mathf.Clamp(leftEdge, 0f, State.Width - frame.Size.x);
            }
            else
            {
                frame.Position.x = Mathf.Clamp(leftEdge, 0f, currentRight - frame.MinSize.x);
                frame.Size.x = Mathf.Clamp(currentRight - frame.Position.x, frame.MinSize.x, frame.MaxSize.x);
            }
        }

        private void StopFrameAtBottomEdge(float bottomEdge)
        {
            FrameRuntimeState frame = State.Frame;
            if (Mathf.Approximately(frame.PreviousSize.y, frame.Size.y))
            {
                frame.Position.y = Mathf.Clamp(bottomEdge - frame.Size.y, 0f, State.Height - frame.Size.y);
            }
            else
            {
                frame.Size.y = Mathf.Clamp(bottomEdge - frame.Position.y, frame.MinSize.y, frame.MaxSize.y);
            }
        }

        private void StopFrameAtTopEdge(float topEdge)
        {
            FrameRuntimeState frame = State.Frame;
            float currentBottom = frame.Position.y + frame.Size.y;
            if (Mathf.Approximately(frame.PreviousSize.y, frame.Size.y))
            {
                frame.Position.y = Mathf.Clamp(topEdge, 0f, State.Height - frame.Size.y);
            }
            else
            {
                frame.Position.y = Mathf.Clamp(topEdge, 0f, currentBottom - frame.MinSize.y);
                frame.Size.y = Mathf.Clamp(currentBottom - frame.Position.y, frame.MinSize.y, frame.MaxSize.y);
            }
        }

        private static bool RangesOverlap(float minA, float maxA, float minB, float maxB)
        {
            return minA < maxB && maxA > minB;
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
                if (!State.IsHoleDisabled(character.CurrentCell))
                {
                    character.IsAlive = false;
                    return;
                }
            }

            SignboardRuntimeState signboard = State.GetActiveSignboardAt(character.CurrentCell);
            if (signboard != null)
            {
                character.Direction = signboard.Direction;
            }

            if (terrain == CellType.Goal)
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
