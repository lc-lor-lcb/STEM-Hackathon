using System.Collections.Generic;
using SukimaWalker.Core;
using UnityEngine;

namespace SukimaWalker.Runtime
{
    // ランタイム状態をUnityのSpriteRenderer群として表示します。
    public sealed class StageRenderer : MonoBehaviour
    {
        private readonly List<GameObject> terrainTiles = new List<GameObject>();
        private readonly List<GameObject> framePieces = new List<GameObject>();
        private readonly Dictionary<string, GameObject> signboardObjects = new Dictionary<string, GameObject>();

        private StageVisualSettings visualSettings;
        private Sprite squareSprite;
        private GameObject characterObject;
        private SpriteRenderer characterRenderer;

        // 盤面、主人公、Frame、看板を作り直して表示します。
        public void Render(StageRuntimeState state)
        {
            Clear();
            visualSettings = Resources.Load<StageVisualSettings>("SukimaWalkerVisualSettings");
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
                    spriteRenderer.sprite = GetCellSprite(state.Terrain[x, y]);
                    spriteRenderer.color = ColorForCell(state.Terrain[x, y]);
                    spriteRenderer.sortingOrder = 0;
                    terrainTiles.Add(tile);
                }
            }

            characterObject = new GameObject("Character");
            characterObject.transform.SetParent(transform, false);
            characterObject.transform.localScale = Vector3.one;

            characterRenderer = characterObject.AddComponent<SpriteRenderer>();
            characterRenderer.sortingOrder = 10;

            CreateFramePieces();
            CreateSignboards(state);
            SyncFrame(state.Frame);
            SyncSignboards(state);
            SyncCharacter(state.Character);
        }

        // 主人公を1マスサイズで表示し、向きごとのSpriteへ切り替えます。
        public void SyncCharacter(CharacterRuntimeState character)
        {
            if (characterObject == null || characterRenderer == null)
            {
                return;
            }

            characterObject.transform.position = GridToWorldCenter(character.Cell.x + 0.5f, character.Cell.y + 0.5f, -0.1f);
            Sprite directionSprite = visualSettings != null ? visualSettings.GetCharacterSprite(character.Direction) : null;
            characterRenderer.sprite = directionSprite != null ? directionSprite : squareSprite;
            characterRenderer.color = directionSprite != null ? Color.white : new Color(1f, 0.72f, 0.2f, 1f);
            characterObject.transform.rotation = directionSprite != null ? Quaternion.identity : Quaternion.Euler(0f, 0f, RotationForDirection(character.Direction));
        }

        // 看板の現在セルと破棄状態を表示へ反映します。
        public void SyncSignboards(StageRuntimeState state)
        {
            foreach (SignboardRuntimeState signboard in state.Signboards)
            {
                if (!signboardObjects.TryGetValue(signboard.Id, out GameObject signboardObject))
                {
                    continue;
                }

                signboardObject.SetActive(!signboard.Destroyed);
                if (signboard.Destroyed)
                {
                    continue;
                }

                signboardObject.transform.position = GridToWorldCenter(signboard.X + 0.5f, signboard.Y + 0.5f, -0.15f);
                signboardObject.transform.rotation = Quaternion.identity;

                Transform arrow = signboardObject.transform.Find("Arrow");
                if (arrow != null)
                {
                    arrow.localRotation = Quaternion.Euler(0f, 0f, RotationForDirection(signboard.Direction));
                }
            }
        }

        // Frameの整数位置・整数サイズを、青い枠とハンドルとして表示します。
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
                characterRenderer = null;
            }

            foreach (GameObject framePiece in framePieces)
            {
                if (framePiece != null)
                {
                    Destroy(framePiece);
                }
            }

            framePieces.Clear();

            foreach (GameObject signboardObject in signboardObjects.Values)
            {
                if (signboardObject != null)
                {
                    Destroy(signboardObject);
                }
            }

            signboardObjects.Clear();
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

        private void CreateSignboards(StageRuntimeState state)
        {
            foreach (SignboardRuntimeState signboard in state.Signboards)
            {
                GameObject root = new GameObject($"Signboard_{signboard.Id}");
                root.transform.SetParent(transform, false);
                root.transform.localScale = Vector3.one * 0.78f;

                SpriteRenderer baseRenderer = root.AddComponent<SpriteRenderer>();
                baseRenderer.sprite = squareSprite;
                baseRenderer.color = new Color(0.05f, 0.66f, 0.78f, 1f);
                baseRenderer.sortingOrder = 8;

                GameObject arrow = new GameObject("Arrow");
                arrow.transform.SetParent(root.transform, false);
                arrow.transform.localPosition = new Vector3(0f, 0f, -0.01f);

                SpriteRenderer arrowRenderer = arrow.AddComponent<SpriteRenderer>();
                arrowRenderer.sprite = CreateArrowSprite();
                arrowRenderer.color = new Color(0.03f, 0.08f, 0.1f, 1f);
                arrowRenderer.sortingOrder = 9;

                signboardObjects[signboard.Id] = root;
            }
        }

        private Sprite GetCellSprite(CellType cellType)
        {
            Sprite configuredSprite = visualSettings != null ? visualSettings.GetCellSprite(cellType) : null;
            return configuredSprite != null ? configuredSprite : squareSprite;
        }

        private Color ColorForCell(CellType cellType)
        {
            if (visualSettings != null && visualSettings.GetCellSprite(cellType) != null)
            {
                return Color.white;
            }

            return FallbackColorForCell(cellType);
        }

        private static void SetRect(GameObject target, float gridX, float gridY, float width, float height)
        {
            target.transform.position = GridToWorldCenter(gridX, gridY, -0.2f);
            target.transform.localScale = new Vector3(width, height, 1f);
        }

        private static Color FallbackColorForCell(CellType cellType)
        {
            switch (cellType)
            {
                case CellType.Wall:
                    return new Color(0.12f, 0.14f, 0.17f, 1f);
                case CellType.Hole:
                    return new Color(0.18f, 0.18f, 0.24f, 1f);
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

        private static Sprite CreateArrowSprite()
        {
            const int size = 32;
            Texture2D texture = new Texture2D(size, size)
            {
                filterMode = FilterMode.Point
            };

            Color clear = new Color(1f, 1f, 1f, 0f);
            Color solid = Color.white;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool shaft = x >= 13 && x <= 18 && y >= 8 && y <= 24;
                    bool head = y >= 18 && Mathf.Abs(x - 15.5f) <= 25 - y;
                    texture.SetPixel(x, y, shaft || head ? solid : clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
