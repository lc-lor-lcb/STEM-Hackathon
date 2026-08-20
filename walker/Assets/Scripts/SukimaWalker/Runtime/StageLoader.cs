using SukimaWalker.Core;

namespace SukimaWalker.Runtime
{
    public static class StageLoader
    {
        public static StageData LoadFirstStreamingStage()
        {
            return StageRepository.LoadFirstStage();
        }
    }
}
