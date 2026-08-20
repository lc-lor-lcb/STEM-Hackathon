using System.Collections.Generic;
using SukimaWalker.Core;
using UnityEngine;

namespace SukimaWalker.Runtime
{
    public sealed class StageRenderer : MonoBehaviour
    {
        private readonly List<GameObject> terrainTiles = new List<GameObject>();
        private readonly List<GameObject> framePieces = new List<GameObject>();
        private Sprite squareSprite;
        private GameObject characterObject;

        public void Render(StageRuntimeState state)
        {
            Clear();
            squareSprite = CreateSquareSprite();

            for (int y = 0; y < state.Height; y++)
            {
                for (int x = 0; x < state.Width; x++)
                {
                    GameObject tile = new GameObject($"Cell_{x}_{y}");
                    tile.transform.SetParent(transform, false);
                    tile.transform.position = GridToWorldCenter(x + 0.5f, y + 0.5f, 0f);
                    tile.transform.localScale = Vector3.one * 0.96f;

                    SpriteRenderer spriteRenderer = tile.AddComponent<SpriteRenderer>();
                    spriteRenderer.sprite = squareSprite;
                    spriteRenderer.color = ColorForCell(state.Terrain[x, y]);
                    spriteRenderer.sortingOrder = 0;
                    terrainTiles.Add(tile);
                }
            }

            characterObject = new GameObject("Character");
            characterObject.transform.SetParent(transform, false);
            characterObject.transform.localScale = Vector3.one * 0.55f;

            SpriteRenderer characterRenderer = characterObject.AddComponent<SpriteRenderer>();
            characterRenderer.sprite = squareSprite;
            characterRenderer.color = new Color(1f, 0.72f, 0.2f, 1f);
            characterRenderer.sortingOrder = 10;

            CreateFramePieces();
            SyncFrame(state.Frame);
            SyncCharacter(state.Character);
        }

        public void SyncCharacter(CharacterRuntimeState character)
        {
            if (characterObject == null)
            {
                return;
            }

            characterObject.transform.position = GridToWorldCenter(character.Position.x, character.Position.y, -0.1f);
            characterObject.transform.rotation = Quaternion.Euler(0f, 0f, RotationForDirection(character.Direction));
        }

        public void SyncFrame(FrameRuntimeState frame)
        {
            if (framePieces.Count != 12)
            {
                return;
            }

            const float thickness = 0.08f;
            const float handleSize = 0.18f;
            float left = frame.Position.x;
            float right = frame.Position.x + frame.Size.x;
            float top = frame.Position.y;
            float bottom = frame.Position.y + frame.Size.y;
            float midX = (left + right) * 0.5f;
            float midY = (top + bottom) * 0.5f;

            SetRect(framePieces[0], midX, top, frame.Size.x + thickness, thickness);
            SetRect(framePieces[1], midX, bottom, frame.Size.x + thickness, thickness);
            SetRect(framePieces[2], left, midY, thickness, frame.Size.y + thickness);
            SetRect(framePieces[3], right, midY, thickness, frame.Size.y + thickness);

            SetRect(framePieces[4], left, top, handleSize, handleSize);
            SetRect(framePieces[5], right, top, handleSize, handleSize);
            SetRect(framePieces[6], left, bottom, handleSize, handleSize);
            SetRect(framePieces[7], right, bottom, handleSize, handleSize);
            SetRect(framePieces[8], midX, top, handleSize, handleSize);
            SetRect(framePieces[9], midX, bottom, handleSize, handleSize);
            SetRect(framePieces[10], left, midY, handleSize, handleSize);
            SetRect(framePieces[11], right, midY, handleSize, handleSize);
        }

        public static Vector3 GridToWorldCenter(float gridX, float gridY, float z)
        {
            return new Vector3(gridX, -gridY, z);
        }

        public static Vector3 BoardCenterWorld(StageRuntimeState state)
        {
            return GridToWorldCenter(state.Width * 0.5f, state.Height * 0.5f, 0f);
        }

        private void Clear()
        {
            foreach (GameObject tile in terrainTiles)
            {
                if (tile != null)
                {
                    Destroy(tile);
                }
            }

            terrainTiles.Clear();

            if (characterObject != null)
            {
                Destroy(characterObject);
                characterObject = null;
            }

            foreach (GameObject framePiece in framePieces)
            {
                if (framePiece != null)
                {
                    Destroy(framePiece);
                }
            }

            framePieces.Clear();
        }

        private void CreateFramePieces()
        {
            for (int i = 0; i < 12; i++)
            {
                GameObject piece = new GameObject($"FramePiece_{i}");
                piece.transform.SetParent(transform, false);

                SpriteRenderer spriteRenderer = piece.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = squareSprite;
                spriteRenderer.color = i < 4 ? new Color(0.15f, 0.45f, 1f, 1f) : new Color(0.08f, 0.22f, 0.62f, 1f);
                spriteRenderer.sortingOrder = 20;

                framePieces.Add(piece);
            }
        }

        private static void SetRect(GameObject target, float gridX, float gridY, float width, float height)
        {
            target.transform.position = GridToWorldCenter(gridX, gridY, -0.2f);
            target.transform.localScale = new Vector3(width, height, 1f);
        }

        private static Color ColorForCell(CellType cellType)
        {
            switch (cellType)
            {
                case CellType.Wall:
                    return new Color(0.12f, 0.14f, 0.17f, 1f);
                case CellType.Hole:
                    return new Color(0.05f, 0.05f, 0.07f, 1f);
                case CellType.Goal:
                    return new Color(0.25f, 0.8f, 0.42f, 1f);
                case CellType.Floor:
                default:
                    return new Color(0.78f, 0.82f, 0.88f, 1f);
            }
        }

        private static float RotationForDirection(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:
                    return 0f;
                case Direction.Down:
                    return 180f;
                case Direction.Left:
                    return 90f;
                case Direction.Right:
                    return -90f;
                default:
                    return 0f;
            }
        }

        private static Sprite CreateSquareSprite()
        {
            Texture2D texture = new Texture2D(1, 1)
            {
                filterMode = FilterMode.Point
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
