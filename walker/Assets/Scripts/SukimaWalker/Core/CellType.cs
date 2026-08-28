namespace SukimaWalker.Core
{
    public enum CellType
    {
        Floor,
        Wall,
        Hole,
        Goal
    }

    public static class CellTypeExtensions
    {
        public static CellType FromSymbol(char symbol)
        {
            switch (symbol)
            {
                case '#':
                    return CellType.Wall;
                case 'O':
                    return CellType.Hole;
                case 'G':
                    return CellType.Goal;
                case '.':
                default:
                    return CellType.Floor;
            }
        }
    }
}
