using System.Collections.Generic;
using UnityEngine;

namespace SukimaWalker.Core
{
    // ゲーム本編とエディターのテストプレイで共有する、1マス単位の進行ルールです。
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

        // 1フレーム分の入力、Frame、看板、主人公の順に評価します。
        public void Tick(float deltaTime, FrameInputCommand frameInput)
        {
            if (!State.Character.IsAlive || State.Character.HasCleared)
            {
                return;
            }

            ApplyPlayerInputAndUpdateFrame(frameInput);
            PushSignboardsByFrameEdges();
            ConstrainFrameToContainedActors();
            ResolveFrameEdgeCharacterCollision();
            MoveCharacter(deltaTime);
            EvaluateEnteredCellTerrainEffects();
            ResolveDeathOrClear();
        }

        // ステージ開始時と即時リトライ時に、全ランタイム状態を初期値へ戻します。
        public void Restart()
        {
            State.Reset(characterSpeed);
        }

        // Frameを整数マスにスナップして動かし、主人公を置き去りにしない範囲へ先に制限します。
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
                ConstrainFrameToContainedCharacter();
                return;
            }

            ApplyResize(frame, frameInput, pointerDelta);
            ConstrainFrameToContainedCharacter();
        }

        // 動いてきたFrameの辺が内側の看板に重なった時だけ、看板を1マス押し出します。
        private void PushSignboardsByFrameEdges()
        {
            FrameRuntimeState frame = State.Frame;
            CachePreviousSignboardCells();

            int leftDelta = frame.Position.x - frame.PreviousPosition.x;
            int rightDelta = frame.Position.x + frame.Size.x - frame.PreviousPosition.x - frame.PreviousSize.x;
            int topDelta = frame.Position.y - frame.PreviousPosition.y;
            int bottomDelta = frame.Position.y + frame.Size.y - frame.PreviousPosition.y - frame.PreviousSize.y;

            if (leftDelta > 0)
            {
                ProcessSignboardPushes(Direction.Right, frame.PreviousPosition.x, frame.PreviousPosition.x + leftDelta, frame.Position.y, frame.Position.y + frame.Size.y);
            }

            if (rightDelta < 0)
            {
                int previousRight = frame.PreviousPosition.x + frame.PreviousSize.x;
                ProcessSignboardPushes(Direction.Left, previousRight + rightDelta, previousRight, frame.Position.y, frame.Position.y + frame.Size.y);
            }

            if (topDelta > 0)
            {
                ProcessSignboardPushes(Direction.Down, frame.Position.x, frame.Position.x + frame.Size.x, frame.PreviousPosition.y, frame.PreviousPosition.y + topDelta);
            }

            if (bottomDelta < 0)
            {
                int previousBottom = frame.PreviousPosition.y + frame.PreviousSize.y;
                ProcessSignboardPushes(Direction.Up, frame.Position.x, frame.Position.x + frame.Size.x, previousBottom + bottomDelta, previousBottom);
            }
        }

        // 主人公が次に進むマスがFrame外なら、壁と同じくその場で180度反転します。
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

        // 主人公を一定テンポで1マスだけ進めます。判定は常に整数マスで行います。
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

        // 新しく入ったマスの地形、穴、看板、ゴールを評価します。穴は常に有効です。
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

        // リサイズ時に固定辺を保ちながら、min/maxと盤面内に収めます。
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

        // 押す方向の奥から手前へ看板を処理し、1体ごとに占有セルを更新します。
        private void ProcessSignboardPushes(Direction direction, int minX, int maxX, int minY, int maxY)
        {
            HashSet<Vector2Int> occupiedCells = BuildSignboardCellSet();
            Vector2Int characterCell = State.Character.Cell;
            List<SignboardRuntimeState> candidates = new List<SignboardRuntimeState>();
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

                candidates.Add(signboard);
            }

            SortSignboardsForPush(candidates, direction);

            foreach (SignboardRuntimeState signboard in candidates)
            {
                Vector2Int cell = new Vector2Int(signboard.X, signboard.Y);
                Vector2Int destination = cell + DirectionToCellStep(direction);
                if (destination == characterCell || occupiedCells.Contains(destination) || State.GetTerrain(destination) == CellType.Wall)
                {
                    continue;
                }

                occupiedCells.Remove(cell);
                if (State.GetTerrain(destination) == CellType.Hole)
                {
                    signboard.Destroyed = true;
                    continue;
                }

                signboard.X = destination.x;
                signboard.Y = destination.y;
                occupiedCells.Add(destination);
            }
        }

        // 前フレームでFrame内にいた主人公と看板を、移動後のFrameにも必ず含めます。
        private void ConstrainFrameToContainedActors()
        {
            ConstrainFrameToCells(GetActorsContainedByPreviousFrame());
        }

        // 「さっきまでFrame内にいたもの」だけを保護対象にして、Frame外のものは無理に取り込みません。
        private List<Vector2Int> GetActorsContainedByPreviousFrame()
        {
            FrameRuntimeState previousFrame = new FrameRuntimeState
            {
                Position = State.Frame.PreviousPosition,
                Size = State.Frame.PreviousSize
            };
            List<Vector2Int> cells = new List<Vector2Int>();

            if (previousFrame.Contains(State.Character.Cell))
            {
                cells.Add(State.Character.Cell);
            }

            foreach (SignboardRuntimeState signboard in State.Signboards)
            {
                if (signboard.Destroyed)
                {
                    continue;
                }

                Vector2Int previousCell = new Vector2Int(signboard.PreviousX, signboard.PreviousY);
                if (previousFrame.Contains(previousCell))
                {
                    cells.Add(new Vector2Int(signboard.X, signboard.Y));
                }
            }

            return cells;
        }

        private void CachePreviousSignboardCells()
        {
            foreach (SignboardRuntimeState signboard in State.Signboards)
            {
                signboard.PreviousX = signboard.X;
                signboard.PreviousY = signboard.Y;
            }
        }

        // 主人公だけはFrameに押されないため、看板押し出しより前にFrame側を止めます。
        private void ConstrainFrameToContainedCharacter()
        {
            FrameRuntimeState previousFrame = new FrameRuntimeState
            {
                Position = State.Frame.PreviousPosition,
                Size = State.Frame.PreviousSize
            };

            if (!previousFrame.Contains(State.Character.Cell))
            {
                return;
            }

            ConstrainFrameToCells(new List<Vector2Int> { State.Character.Cell });
        }

        private void ConstrainFrameToCells(List<Vector2Int> protectedCells)
        {
            if (protectedCells.Count == 0)
            {
                return;
            }

            FrameRuntimeState frame = State.Frame;
            int minProtectedX = protectedCells[0].x;
            int maxProtectedX = protectedCells[0].x;
            int minProtectedY = protectedCells[0].y;
            int maxProtectedY = protectedCells[0].y;

            foreach (Vector2Int cell in protectedCells)
            {
                minProtectedX = Mathf.Min(minProtectedX, cell.x);
                maxProtectedX = Mathf.Max(maxProtectedX, cell.x);
                minProtectedY = Mathf.Min(minProtectedY, cell.y);
                maxProtectedY = Mathf.Max(maxProtectedY, cell.y);
            }

            int requiredWidth = maxProtectedX - minProtectedX + 1;
            int requiredHeight = maxProtectedY - minProtectedY + 1;
            frame.Size = new Vector2Int(
                Mathf.Clamp(Mathf.Max(frame.Size.x, requiredWidth), frame.MinSize.x, Mathf.Min(frame.MaxSize.x, State.Width)),
                Mathf.Clamp(Mathf.Max(frame.Size.y, requiredHeight), frame.MinSize.y, Mathf.Min(frame.MaxSize.y, State.Height)));

            int minAllowedX = Mathf.Max(0, maxProtectedX - frame.Size.x + 1);
            int maxAllowedX = Mathf.Min(State.Width - frame.Size.x, minProtectedX);
            int minAllowedY = Mathf.Max(0, maxProtectedY - frame.Size.y + 1);
            int maxAllowedY = Mathf.Min(State.Height - frame.Size.y, minProtectedY);

            frame.Position = new Vector2Int(
                Mathf.Clamp(frame.Position.x, minAllowedX, maxAllowedX),
                Mathf.Clamp(frame.Position.y, minAllowedY, maxAllowedY));
        }

        private static void SortSignboardsForPush(List<SignboardRuntimeState> signboards, Direction direction)
        {
            signboards.Sort((a, b) =>
            {
                switch (direction)
                {
                    case Direction.Right:
                        return b.X.CompareTo(a.X);
                    case Direction.Left:
                        return a.X.CompareTo(b.X);
                    case Direction.Down:
                        return b.Y.CompareTo(a.Y);
                    case Direction.Up:
                        return a.Y.CompareTo(b.Y);
                    default:
                        return 0;
                }
            });
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

    }
}
