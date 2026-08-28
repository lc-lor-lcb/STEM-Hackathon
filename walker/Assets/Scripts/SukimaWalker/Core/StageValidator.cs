using System.Collections.Generic;

namespace SukimaWalker.Core
{
    public static class StageValidator
    {
        public static List<string> Validate(StageData source)
        {
            bool characterMissing = source == null || source.character == null;
            StageData stageData = StageDataUtility.Normalize(StageDataUtility.Clone(source));
            List<string> warnings = new List<string>();

            if (!IsOuterWallClosed(stageData))
            {
                warnings.Add("Outer border is not fully closed by walls.");
            }

            if (characterMissing)
            {
                warnings.Add("Character start is missing.");
            }

            if (!HasGoal(stageData))
            {
                warnings.Add("At least one goal cell is required.");
            }

            if (stageData.character != null)
            {
                char terrain = StageDataUtility.GetTerrainSymbol(stageData, stageData.character.start_x, stageData.character.start_y);
                if (terrain == '#' || terrain == 'O')
                {
                    warnings.Add("Character start is on a wall or hole.");
                }
            }

            foreach (SignboardData signboard in stageData.signboards)
            {
                char terrain = StageDataUtility.GetTerrainSymbol(stageData, signboard.x, signboard.y);
                if (terrain != '.')
                {
                    warnings.Add($"Signboard {signboard.id} is not on a floor cell.");
                }
            }

            return warnings;
        }

        private static bool IsOuterWallClosed(StageData stageData)
        {
            for (int x = 0; x < stageData.board_width; x++)
            {
                if (StageDataUtility.GetTerrainSymbol(stageData, x, 0) != '#' || StageDataUtility.GetTerrainSymbol(stageData, x, stageData.board_height - 1) != '#')
                {
                    return false;
                }
            }

            for (int y = 0; y < stageData.board_height; y++)
            {
                if (StageDataUtility.GetTerrainSymbol(stageData, 0, y) != '#' || StageDataUtility.GetTerrainSymbol(stageData, stageData.board_width - 1, y) != '#')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasGoal(StageData stageData)
        {
            for (int y = 0; y < stageData.board_height; y++)
            {
                for (int x = 0; x < stageData.board_width; x++)
                {
                    if (StageDataUtility.GetTerrainSymbol(stageData, x, y) == 'G')
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
