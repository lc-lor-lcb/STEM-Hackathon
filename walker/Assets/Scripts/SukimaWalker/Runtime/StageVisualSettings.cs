using SukimaWalker.Core;
using UnityEngine;

namespace SukimaWalker.Runtime
{
    [CreateAssetMenu(fileName = "SukimaWalkerVisualSettings", menuName = "Sukima Walker/Visual Settings")]
    public sealed class StageVisualSettings : ScriptableObject
    {
        public Sprite floorSprite;
        public Sprite wallSprite;
        public Sprite holeSprite;
        public Sprite goalSprite;
        public Sprite characterUpSprite;
        public Sprite characterDownSprite;
        public Sprite characterLeftSprite;
        public Sprite characterRightSprite;

        public Sprite GetCellSprite(CellType cellType)
        {
            switch (cellType)
            {
                case CellType.Wall:
                    return wallSprite;
                case CellType.Hole:
                    return holeSprite;
                case CellType.Goal:
                    return goalSprite;
                case CellType.Floor:
                default:
                    return floorSprite;
            }
        }

        public Sprite GetCharacterSprite(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:
                    return characterUpSprite;
                case Direction.Down:
                    return characterDownSprite;
                case Direction.Left:
                    return characterLeftSprite;
                case Direction.Right:
                    return characterRightSprite;
                default:
                    return characterRightSprite;
            }
        }
    }
}
