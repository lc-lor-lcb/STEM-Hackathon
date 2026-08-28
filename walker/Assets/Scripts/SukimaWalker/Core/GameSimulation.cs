using System.Collections.Generic;
using UnityEngine;

namespace SukimaWalker.Core
{
    // すきまウォーカーのコアルールを、描画やUIから独立して1フレーム分進める処理です。
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

        // ステージを初期状態へ戻します。
        public void Restart()
        {
            State.Reset(characterSpeed);
        }

        // 1フレームの処理順です。Frame入力、主人公のFrame反射、主人公移動、地形効果の順で評価します。
        public void Tick(float deltaTime, FrameInputCommand frameInput)
        {
            State.Character.EnteredNewCellThisFrame = false;
            if (!State.Character.IsAlive || State.Character.HasCleared)
            {
                return;
            }

            ApplyPlayerInputAndUpdateFrame(frameInput);
            ResolveFrameEdgeCharacterCollision();
            MoveCharacter(deltaTime);
            EvaluateEnteredCellTerrainEffects();
            ResolveDeathOrClear();
        }

        // プレイヤーのドラッグ入力を、整数グリッドのFrame移動/リサイズへ変換して1マスずつ試します。
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
                Vector2Int targetPosition = ClampFramePosition(frameInput.StartPosition + pointerDelta, frame.Size);
                StepFrameMoveTo(targetPosition);
                return;
            }

            StepFrameResizeTo(BuildResizeTarget(frameInput, pointerDelta));
        }

        // Frameの平行移動は、目的地へ一気に飛ばさず、1マスずつ成功/失敗を判定します。
        private void StepFrameMoveTo(Vector2Int targetPosition)
        {
            while (State.Frame.Position.x < targetPosition.x && TryMoveFrameOneCell(Direction.Right))
            {
            }

            while (State.Frame.Position.x > targetPosition.x && TryMoveFrameOneCell(Direction.Left))
            {
            }

            while (State.Frame.Position.y < targetPosition.y && TryMoveFrameOneCell(Direction.Down))
            {
            }

            while (State.Frame.Position.y > targetPosition.y && TryMoveFrameOneCell(Direction.Up))
            {
            }
        }

        // Frameのリサイズも、各辺を1マスずつ動かして、看板や主人公を置き去りにしない範囲で止めます。
        private void StepFrameResizeTo(FrameRect target)
        {
            while (State.Frame.Position.x < target.Left && TryShrinkLeftEdge())
            {
            }

            while (State.Frame.Position.x > target.Left && TryExpandLeftEdge())
            {
            }

            while (FrameRight > target.Right && TryShrinkRightEdge())
            {
            }

            while (FrameRight < target.Right && TryExpandRightEdge())
            {
            }

            while (State.Frame.Position.y < target.Top && TryShrinkTopEdge())
            {
            }

            while (State.Frame.Position.y > target.Top && TryExpandTopEdge())
            {
            }

            while (FrameBottom > target.Bottom && TryShrinkBottomEdge())
            {
            }

            while (FrameBottom < target.Bottom && TryExpandBottomEdge())
            {
            }
        }

        // Frame全体を1マス動かします。押し出される辺にいる看板だけを押し、主人公が外れるなら動きません。
        private bool TryMoveFrameOneCell(Direction direction)
        {
            FrameRuntimeState frame = State.Frame;
            switch (direction)
            {
                case Direction.Right:
                    FrameRect movedRightFrame = new FrameRect(frame.Position.x + 1, frame.Position.y, FrameRight + 1, FrameBottom);
                    if (FrameRight >= State.Width || !TryReleaseColumn(frame.Position.x, Direction.Right, movedRightFrame))
                    {
                        return false;
                    }

                    frame.Position += Vector2Int.right;
                    return true;

                case Direction.Left:
                    FrameRect movedLeftFrame = new FrameRect(frame.Position.x - 1, frame.Position.y, FrameRight - 1, FrameBottom);
                    if (frame.Position.x <= 0 || !TryReleaseColumn(FrameRight - 1, Direction.Left, movedLeftFrame))
                    {
                        return false;
                    }

                    frame.Position += Vector2Int.left;
                    return true;

                case Direction.Down:
                    FrameRect movedDownFrame = new FrameRect(frame.Position.x, frame.Position.y + 1, FrameRight, FrameBottom + 1);
                    if (FrameBottom >= State.Height || !TryReleaseRow(frame.Position.y, Direction.Down, movedDownFrame))
                    {
                        return false;
                    }

                    frame.Position += Vector2Int.up;
                    return true;

                case Direction.Up:
                    FrameRect movedUpFrame = new FrameRect(frame.Position.x, frame.Position.y - 1, FrameRight, FrameBottom - 1);
                    if (frame.Position.y <= 0 || !TryReleaseRow(FrameBottom - 1, Direction.Up, movedUpFrame))
                    {
                        return false;
                    }

                    frame.Position += Vector2Int.down;
                    return true;

                default:
                    return false;
            }
        }

        // 左辺を右へ縮めます。外れる列にいる看板は右へ押し、主人公がいるなら縮みません。
        private bool TryShrinkLeftEdge()
        {
            FrameRuntimeState frame = State.Frame;
            FrameRect resizedFrame = new FrameRect(frame.Position.x + 1, frame.Position.y, FrameRight, FrameBottom);
            if (frame.Size.x <= frame.MinSize.x || !TryReleaseColumn(frame.Position.x, Direction.Right, resizedFrame))
            {
                return false;
            }

            frame.Position += Vector2Int.right;
            frame.Size += Vector2Int.left;
            return true;
        }

        // 右辺を左へ縮めます。外れる列にいる看板は左へ押し、主人公がいるなら縮みません。
        private bool TryShrinkRightEdge()
        {
            FrameRuntimeState frame = State.Frame;
            FrameRect resizedFrame = new FrameRect(frame.Position.x, frame.Position.y, FrameRight - 1, FrameBottom);
            if (frame.Size.x <= frame.MinSize.x || !TryReleaseColumn(FrameRight - 1, Direction.Left, resizedFrame))
            {
                return false;
            }

            frame.Size += Vector2Int.left;
            return true;
        }

        // 上辺を下へ縮めます。外れる行にいる看板は下へ押し、主人公がいるなら縮みません。
        private bool TryShrinkTopEdge()
        {
            FrameRuntimeState frame = State.Frame;
            FrameRect resizedFrame = new FrameRect(frame.Position.x, frame.Position.y + 1, FrameRight, FrameBottom);
            if (frame.Size.y <= frame.MinSize.y || !TryReleaseRow(frame.Position.y, Direction.Down, resizedFrame))
            {
                return false;
            }

            frame.Position += Vector2Int.up;
            frame.Size += Vector2Int.down;
            return true;
        }

        // 下辺を上へ縮めます。外れる行にいる看板は上へ押し、主人公がいるなら縮みません。
        private bool TryShrinkBottomEdge()
        {
            FrameRuntimeState frame = State.Frame;
            FrameRect resizedFrame = new FrameRect(frame.Position.x, frame.Position.y, FrameRight, FrameBottom - 1);
            if (frame.Size.y <= frame.MinSize.y || !TryReleaseRow(FrameBottom - 1, Direction.Up, resizedFrame))
            {
                return false;
            }

            frame.Size += Vector2Int.down;
            return true;
        }

        // 左辺を左へ広げます。拡大は何かを押し出さないので、盤面と最大サイズだけを見ます。
        private bool TryExpandLeftEdge()
        {
            FrameRuntimeState frame = State.Frame;
            if (frame.Position.x <= 0 || frame.Size.x >= frame.MaxSize.x)
            {
                return false;
            }

            frame.Position += Vector2Int.left;
            frame.Size += Vector2Int.right;
            return true;
        }

        // 右辺を右へ広げます。拡大は何かを押し出さないので、盤面と最大サイズだけを見ます。
        private bool TryExpandRightEdge()
        {
            FrameRuntimeState frame = State.Frame;
            if (FrameRight >= State.Width || frame.Size.x >= frame.MaxSize.x)
            {
                return false;
            }

            frame.Size += Vector2Int.right;
            return true;
        }

        // 上辺を上へ広げます。拡大は何かを押し出さないので、盤面と最大サイズだけを見ます。
        private bool TryExpandTopEdge()
        {
            FrameRuntimeState frame = State.Frame;
            if (frame.Position.y <= 0 || frame.Size.y >= frame.MaxSize.y)
            {
                return false;
            }

            frame.Position += Vector2Int.down;
            frame.Size += Vector2Int.up;
            return true;
        }

        // 下辺を下へ広げます。拡大は何かを押し出さないので、盤面と最大サイズだけを見ます。
        private bool TryExpandBottomEdge()
        {
            FrameRuntimeState frame = State.Frame;
            if (FrameBottom >= State.Height || frame.Size.y >= frame.MaxSize.y)
            {
                return false;
            }

            frame.Size += Vector2Int.up;
            return true;
        }

        // Frameから外れようとしている列を処理します。主人公は押さず、看板だけを押せる時に押します。
        private bool TryReleaseColumn(int x, Direction pushDirection, FrameRect frameAfterStep)
        {
            FrameRuntimeState frame = State.Frame;
            Vector2Int characterCell = State.Character.Cell;
            if (characterCell.x == x && characterCell.y >= frame.Position.y && characterCell.y < FrameBottom)
            {
                return false;
            }

            List<SignboardRuntimeState> targets = new List<SignboardRuntimeState>();
            foreach (SignboardRuntimeState signboard in State.Signboards)
            {
                if (signboard.Destroyed)
                {
                    continue;
                }

                if (signboard.X == x && signboard.Y >= frame.Position.y && signboard.Y < FrameBottom)
                {
                    targets.Add(signboard);
                }
            }

            SortSignboardsForPush(targets, pushDirection);
            return TryPushSignboards(targets, pushDirection, frameAfterStep);
        }

        // Frameから外れようとしている行を処理します。主人公は押さず、看板だけを押せる時に押します。
        private bool TryReleaseRow(int y, Direction pushDirection, FrameRect frameAfterStep)
        {
            FrameRuntimeState frame = State.Frame;
            Vector2Int characterCell = State.Character.Cell;
            if (characterCell.y == y && characterCell.x >= frame.Position.x && characterCell.x < FrameRight)
            {
                return false;
            }

            List<SignboardRuntimeState> targets = new List<SignboardRuntimeState>();
            foreach (SignboardRuntimeState signboard in State.Signboards)
            {
                if (signboard.Destroyed)
                {
                    continue;
                }

                if (signboard.Y == y && signboard.X >= frame.Position.x && signboard.X < FrameRight)
                {
                    targets.Add(signboard);
                }
            }

            SortSignboardsForPush(targets, pushDirection);
            return TryPushSignboards(targets, pushDirection, frameAfterStep);
        }

        // 複数看板を同時に押す時、奥の看板を先に計画してから手前を動かせるようにします。
        private bool TryPushSignboards(List<SignboardRuntimeState> targets, Direction direction, FrameRect frameAfterStep)
        {
            if (targets.Count == 0)
            {
                return true;
            }

            HashSet<Vector2Int> occupiedCells = BuildSignboardCellSet();
            HashSet<SignboardRuntimeState> plannedSignboards = new HashSet<SignboardRuntimeState>();
            List<SignboardMove> plannedMoves = new List<SignboardMove>();

            foreach (SignboardRuntimeState signboard in targets)
            {
                if (!TryPlanSignboardPush(signboard, direction, frameAfterStep, occupiedCells, plannedSignboards, plannedMoves))
                {
                    return false;
                }
            }

            foreach (SignboardMove move in plannedMoves)
            {
                move.Signboard.PreviousX = move.Signboard.X;
                move.Signboard.PreviousY = move.Signboard.Y;
                move.Signboard.X = move.Destination.x;
                move.Signboard.Y = move.Destination.y;
                move.Signboard.Destroyed = move.Destroyed;
            }

            return true;
        }

        // 看板1枚が押せるかを再帰的に計画します。隣の看板が先に動ける場合は、その空きを使えます。
        private bool TryPlanSignboardPush(
            SignboardRuntimeState signboard,
            Direction direction,
            FrameRect frameAfterStep,
            HashSet<Vector2Int> occupiedCells,
            HashSet<SignboardRuntimeState> plannedSignboards,
            List<SignboardMove> plannedMoves)
        {
            if (signboard.Destroyed || plannedSignboards.Contains(signboard))
            {
                return true;
            }

            Vector2Int current = new Vector2Int(signboard.X, signboard.Y);
            Vector2Int destination = current + DirectionToCellStep(direction);

            if (destination == State.Character.Cell || State.GetTerrain(destination) == CellType.Wall)
            {
                return false;
            }

            SignboardRuntimeState blockingSignboard = State.GetActiveSignboardAt(destination);
            if (blockingSignboard != null && blockingSignboard != signboard)
            {
                if (!TryPlanSignboardPush(blockingSignboard, direction, frameAfterStep, occupiedCells, plannedSignboards, plannedMoves))
                {
                    return false;
                }
            }

            occupiedCells.Remove(current);
            bool fallsIntoHole = State.GetTerrain(destination) == CellType.Hole;
            if (!fallsIntoHole && !frameAfterStep.Contains(destination))
            {
                return false;
            }

            if (!fallsIntoHole && occupiedCells.Contains(destination))
            {
                return false;
            }

            if (!fallsIntoHole)
            {
                occupiedCells.Add(destination);
            }

            plannedSignboards.Add(signboard);
            plannedMoves.Add(new SignboardMove(signboard, destination, fallsIntoHole));
            return true;
        }

        // 押す方向から見て奥側の看板を先に処理するための並び替えです。
        private static void SortSignboardsForPush(List<SignboardRuntimeState> signboards, Direction pushDirection)
        {
            signboards.Sort((a, b) =>
            {
                switch (pushDirection)
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

        // 現在生きている看板のセル集合を作ります。
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

        // 主人公の次セルがFrame外なら、Frame辺に触れた扱いでその場で180度反転します。
        private void ResolveFrameEdgeCharacterCollision()
        {
            CharacterRuntimeState character = State.Character;
            if (!State.Frame.Contains(character.Cell))
            {
                return;
            }

            Vector2Int nextCell = character.Cell + DirectionToCellStep(character.Direction);
            if (!State.Frame.Contains(nextCell))
            {
                character.Direction = character.Direction.Opposite();
                character.MoveProgress = 0f;
            }
        }

        // 主人公を1マス単位で進めます。壁に当たった時は移動せず180度反転します。
        private void MoveCharacter(float deltaTime)
        {
            CharacterRuntimeState character = State.Character;
            character.MoveProgress += Mathf.Max(0f, deltaTime) * character.Speed;
            if (character.MoveProgress < 1f)
            {
                return;
            }

            character.MoveProgress -= 1f;
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

        // 新しいセルに入った時だけ、穴、ゴール、看板の効果を評価します。
        private void EvaluateEnteredCellTerrainEffects()
        {
            CharacterRuntimeState character = State.Character;
            if (!character.EnteredNewCellThisFrame)
            {
                return;
            }

            CellType terrain = State.GetTerrain(character.Cell);
            if (terrain == CellType.Hole)
            {
                character.IsAlive = false;
                return;
            }

            if (terrain == CellType.Goal)
            {
                character.HasCleared = true;
                return;
            }

            SignboardRuntimeState signboard = State.GetActiveSignboardAt(character.Cell);
            if (signboard != null)
            {
                character.Direction = signboard.Direction;
            }
        }

        // 死亡時の自動リスタート設定だけをここで処理します。
        private void ResolveDeathOrClear()
        {
            if (!State.Character.IsAlive && restartOnDeath)
            {
                Restart();
            }
        }

        // リサイズ入力から、最終的に目指すFrame矩形をmin/maxと盤面内に収めて作ります。
        private FrameRect BuildResizeTarget(FrameInputCommand frameInput, Vector2Int pointerDelta)
        {
            int left = frameInput.StartPosition.x;
            int top = frameInput.StartPosition.y;
            int right = frameInput.StartPosition.x + frameInput.StartSize.x;
            int bottom = frameInput.StartPosition.y + frameInput.StartSize.y;

            bool resizeLeft = frameInput.Mode == FrameDragMode.Left
                || frameInput.Mode == FrameDragMode.TopLeft
                || frameInput.Mode == FrameDragMode.BottomLeft;
            bool resizeRight = frameInput.Mode == FrameDragMode.Right
                || frameInput.Mode == FrameDragMode.TopRight
                || frameInput.Mode == FrameDragMode.BottomRight;
            bool resizeTop = frameInput.Mode == FrameDragMode.Top
                || frameInput.Mode == FrameDragMode.TopLeft
                || frameInput.Mode == FrameDragMode.TopRight;
            bool resizeBottom = frameInput.Mode == FrameDragMode.Bottom
                || frameInput.Mode == FrameDragMode.BottomLeft
                || frameInput.Mode == FrameDragMode.BottomRight;

            if (resizeLeft)
            {
                left += pointerDelta.x;
            }

            if (resizeRight)
            {
                right += pointerDelta.x;
            }

            if (resizeTop)
            {
                top += pointerDelta.y;
            }

            if (resizeBottom)
            {
                bottom += pointerDelta.y;
            }

            FrameRuntimeState frame = State.Frame;
            int minWidth = frame.MinSize.x;
            int minHeight = frame.MinSize.y;
            int maxWidth = Mathf.Min(frame.MaxSize.x, State.Width);
            int maxHeight = Mathf.Min(frame.MaxSize.y, State.Height);

            left = Mathf.Clamp(left, 0, State.Width - minWidth);
            right = Mathf.Clamp(right, minWidth, State.Width);
            top = Mathf.Clamp(top, 0, State.Height - minHeight);
            bottom = Mathf.Clamp(bottom, minHeight, State.Height);

            if (right - left < minWidth)
            {
                if (resizeLeft && !resizeRight)
                {
                    left = right - minWidth;
                }
                else
                {
                    right = left + minWidth;
                }
            }

            if (bottom - top < minHeight)
            {
                if (resizeTop && !resizeBottom)
                {
                    top = bottom - minHeight;
                }
                else
                {
                    bottom = top + minHeight;
                }
            }

            if (right - left > maxWidth)
            {
                if (resizeLeft && !resizeRight)
                {
                    left = right - maxWidth;
                }
                else
                {
                    right = left + maxWidth;
                }
            }

            if (bottom - top > maxHeight)
            {
                if (resizeTop && !resizeBottom)
                {
                    top = bottom - maxHeight;
                }
                else
                {
                    bottom = top + maxHeight;
                }
            }

            left = Mathf.Clamp(left, 0, State.Width - minWidth);
            right = Mathf.Clamp(right, left + minWidth, State.Width);
            top = Mathf.Clamp(top, 0, State.Height - minHeight);
            bottom = Mathf.Clamp(bottom, top + minHeight, State.Height);

            return new FrameRect(left, top, right, bottom);
        }

        // Frame左上座標を、現在サイズのまま盤面内に収めます。
        private Vector2Int ClampFramePosition(Vector2Int position, Vector2Int size)
        {
            return new Vector2Int(
                Mathf.Clamp(position.x, 0, State.Width - size.x),
                Mathf.Clamp(position.y, 0, State.Height - size.y));
        }

        // ゲーム内の上下左右を、Y下向きのグリッド座標に変換します。
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

        private int FrameRight => State.Frame.Position.x + State.Frame.Size.x;

        private int FrameBottom => State.Frame.Position.y + State.Frame.Size.y;

        private struct FrameRect
        {
            public readonly int Left;
            public readonly int Top;
            public readonly int Right;
            public readonly int Bottom;

            public FrameRect(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public bool Contains(Vector2Int cell)
            {
                return cell.x >= Left
                    && cell.y >= Top
                    && cell.x < Right
                    && cell.y < Bottom;
            }
        }

        private struct SignboardMove
        {
            public readonly SignboardRuntimeState Signboard;
            public readonly Vector2Int Destination;
            public readonly bool Destroyed;

            public SignboardMove(SignboardRuntimeState signboard, Vector2Int destination, bool destroyed)
            {
                Signboard = signboard;
                Destination = destination;
                Destroyed = destroyed;
            }
        }
    }
}
