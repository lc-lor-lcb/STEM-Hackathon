using System.Collections.Generic;
using UnityEngine;

namespace SukimaWalker.Core
{
    public sealed class GameSimulation
    {
        private readonly float characterSpeed;
        private readonly bool restartOnDeath;

        public StageRuntimeState State { get; }

        public GameSimulation(StageData stageData, float characterSpeed, bool restartOnDeath = true)
        {
            this.characterSpeed = characterSpeed;
            this.restartOnDeath = restartOnDeath;
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

            Vector2Int pointerDelta = new Vector2Int(
                Mathf.RoundToInt(frameInput.CurrentPointer.x - frameInput.StartPointer.x),
                Mathf.RoundToInt(frameInput.CurrentPointer.y - frameInput.StartPointer.y));

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
            List<SignboardMove> moves = new List<SignboardMove>();

            int leftDelta = frame.Position.x - frame.PreviousPosition.x;
            int rightDelta = frame.Position.x + frame.Size.x - frame.PreviousPosition.x - frame.PreviousSize.x;
            int topDelta = frame.Position.y - frame.PreviousPosition.y;
            int bottomDelta = frame.Position.y + frame.Size.y - frame.PreviousPosition.y - frame.PreviousSize.y;

            if (leftDelta > 0)
            {
                CollectSignboardPushes(moves, Direction.Right, frame.PreviousPosition.x, frame.PreviousPosition.x + leftDelta, frame.Position.y, frame.Position.y + frame.Size.y);
            }

            if (rightDelta < 0)
            {
                int previousRight = frame.PreviousPosition.x + frame.PreviousSize.x;
                CollectSignboardPushes(moves, Direction.Left, previousRight + rightDelta, previousRight, frame.Position.y, frame.Position.y + frame.Size.y);
            }

            if (topDelta > 0)
            {
                CollectSignboardPushes(moves, Direction.Down, frame.Position.x, frame.Position.x + frame.Size.x, frame.PreviousPosition.y, frame.PreviousPosition.y + topDelta);
            }

            if (bottomDelta < 0)
            {
                int previousBottom = frame.PreviousPosition.y + frame.PreviousSize.y;
                CollectSignboardPushes(moves, Direction.Up, frame.Position.x, frame.Position.x + frame.Size.x, previousBottom + bottomDelta, previousBottom);
            }

            ApplySignboardMoves(moves);
        }

        private void ResolveFrameEdgeCharacterCollision()
        {
            CharacterRuntimeState character = State.Character;
            Vector2Int nextCell = character.Cell + DirectionToCellStep(character.Direction);
            if (State.Frame.Contains(character.Cell) && !State.Frame.Contains(nextCell))
            {
                character.Direction = character.Direction.Opposite();
                character.MoveProgress = 0f;
            }
        }

        private void MoveCharacter(float deltaTime)
        {
            CharacterRuntimeState character = State.Character;
            character.EnteredNewCellThisFrame = false;
            character.MoveProgress += Mathf.Max(0f, deltaTime * character.Speed);

            if (character.MoveProgress < 1f)
            {
                return;
            }

            character.MoveProgress -= Mathf.Floor(character.MoveProgress);
            Vector2Int nextCell = character.Cell + DirectionToCellStep(character.Direction);
            if (State.GetTerrain(nextCell) == CellType.Wall)
            {
                character.Direction = character.Direction.Opposite();
                character.MoveProgress = 0f;
                return;
            }

            character.Cell = nextCell;
            character.EnteredNewCellThisFrame = true;
        }

        private void EvaluateEnteredCellTerrainEffects()
        {
            CharacterRuntimeState character = State.Character;
            if (!character.EnteredNewCellThisFrame)
            {
                return;
            }

            CellType terrain = State.GetTerrain(character.Cell);
            if (terrain == CellType.Wall)
            {
                character.Direction = character.Direction.Opposite();
                character.MoveProgress = 0f;
                return;
            }

            if (terrain == CellType.Hole)
            {
                character.IsAlive = false;
                return;
            }

            SignboardRuntimeState signboard = State.GetActiveSignboardAt(character.Cell);
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
            if (!State.Character.IsAlive && restartOnDeath)
            {
                Restart();
            }
        }

        private void ApplyResize(FrameRuntimeState frame, FrameInputCommand frameInput, Vector2Int pointerDelta)
        {
            bool left = frameInput.Mode == FrameDragMode.Left || frameInput.Mode == FrameDragMode.TopLeft || frameInput.Mode == FrameDragMode.BottomLeft;
            bool right = frameInput.Mode == FrameDragMode.Right || frameInput.Mode == FrameDragMode.TopRight || frameInput.Mode == FrameDragMode.BottomRight;
            bool top = frameInput.Mode == FrameDragMode.Top || frameInput.Mode == FrameDragMode.TopLeft || frameInput.Mode == FrameDragMode.TopRight;
            bool bottom = frameInput.Mode == FrameDragMode.Bottom || frameInput.Mode == FrameDragMode.BottomLeft || frameInput.Mode == FrameDragMode.BottomRight;

            int x = frameInput.StartPosition.x;
            int y = frameInput.StartPosition.y;
            int width = frameInput.StartSize.x;
            int height = frameInput.StartSize.y;

            if (left)
            {
                int fixedRight = frameInput.StartPosition.x + frameInput.StartSize.x;
                x = Mathf.Clamp(frameInput.StartPosition.x + pointerDelta.x, 0, fixedRight - frame.MinSize.x);
                width = fixedRight - x;
            }
            else if (right)
            {
                width = frameInput.StartSize.x + pointerDelta.x;
            }

            if (top)
            {
                int fixedBottom = frameInput.StartPosition.y + frameInput.StartSize.y;
                y = Mathf.Clamp(frameInput.StartPosition.y + pointerDelta.y, 0, fixedBottom - frame.MinSize.y);
                height = fixedBottom - y;
            }
            else if (bottom)
            {
                height = frameInput.StartSize.y + pointerDelta.y;
            }

            width = Mathf.Clamp(width, frame.MinSize.x, Mathf.Min(frame.MaxSize.x, State.Width - x));
            height = Mathf.Clamp(height, frame.MinSize.y, Mathf.Min(frame.MaxSize.y, State.Height - y));
            frame.Position = ClampFramePosition(new Vector2Int(x, y), new Vector2Int(width, height));
            frame.Size = new Vector2Int(width, height);
        }

        private void CollectSignboardPushes(List<SignboardMove> moves, Direction direction, int minX, int maxX, int minY, int maxY)
        {
            HashSet<Vector2Int> occupiedCells = BuildSignboardCellSet();
            Vector2Int characterCell = State.Character.Cell;
            foreach (SignboardRuntimeState signboard in State.Signboards)
            {
                if (signboard.Destroyed)
                {
                    continue;
                }

                Vector2Int cell = new Vector2Int(signboard.X, signboard.Y);
                if (cell.x < minX || cell.x >= maxX || cell.y < minY || cell.y >= maxY)
                {
                    continue;
                }

                Vector2Int destination = cell + DirectionToCellStep(direction);
                if (destination == characterCell || occupiedCells.Contains(destination) || State.GetTerrain(destination) == CellType.Wall)
                {
                    continue;
                }

                moves.Add(new SignboardMove
                {
                    Signboard = signboard,
                    Destination = destination,
                    DestroyOnArrival = State.GetTerrain(destination) == CellType.Hole
                });
            }
        }

        private void ApplySignboardMoves(List<SignboardMove> moves)
        {
            foreach (SignboardMove move in moves)
            {
                if (move.Signboard.Destroyed)
                {
                    continue;
                }

                if (move.DestroyOnArrival)
                {
                    move.Signboard.Destroyed = true;
                    continue;
                }

                move.Signboard.X = move.Destination.x;
                move.Signboard.Y = move.Destination.y;
            }
        }

        private HashSet<Vector2Int> BuildSignboardCellSet()
        {
            HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();
            foreach (SignboardRuntimeState signboard in State.Signboards)
            {
                if (!signboard.Destroyed)
                {
                    occupiedCells.Add(new Vector2Int(signboard.X, signboard.Y));
                }
            }

            return occupiedCells;
        }

        private Vector2Int ClampFramePosition(Vector2Int position, Vector2Int size)
        {
            return new Vector2Int(
                Mathf.Clamp(position.x, 0, Mathf.Max(0, State.Width - size.x)),
                Mathf.Clamp(position.y, 0, Mathf.Max(0, State.Height - size.y)));
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

        private struct SignboardMove
        {
            public SignboardRuntimeState Signboard;
            public Vector2Int Destination;
            public bool DestroyOnArrival;
        }
    }
}
