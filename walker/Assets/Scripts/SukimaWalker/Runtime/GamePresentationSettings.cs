using UnityEngine;

namespace SukimaWalker.Runtime
{
    // 画面遷移や待機演出の「いつ発動するか」をInspectorから調整する設定です。
    [CreateAssetMenu(fileName = "SukimaWalkerPresentationSettings", menuName = "Sukima Walker/Presentation Settings")]
    public sealed class GamePresentationSettings : ScriptableObject
    {
        [SerializeField] private float gameplayInactivityTimeoutSeconds = 45f;
        [SerializeField] private float selectIdleDemoDelaySeconds = 20f;
        [SerializeField] private float previewPulseSpeed = 3f;
        [SerializeField] private float previewPulseAmount = 0.06f;
        [SerializeField] private float idleDemoCycleSeconds = 6f;
        [SerializeField] private float clearReturnDelaySeconds = 1.2f;
        [SerializeField] private Color completedStageTint = new Color(0.35f, 1f, 0.55f, 1f);
        [SerializeField] private Color lockedStageTint = new Color(0.75f, 0.78f, 0.85f, 1f);
        [SerializeField] private AnimationClip idleDemoClip = null;

        public float GameplayInactivityTimeoutSeconds => Mathf.Max(1f, gameplayInactivityTimeoutSeconds);
        public float SelectIdleDemoDelaySeconds => Mathf.Max(1f, selectIdleDemoDelaySeconds);
        public float PreviewPulseSpeed => Mathf.Max(0.1f, previewPulseSpeed);
        public float PreviewPulseAmount => Mathf.Clamp(previewPulseAmount, 0f, 0.25f);
        public float IdleDemoCycleSeconds => Mathf.Max(1f, idleDemoCycleSeconds);
        public float ClearReturnDelaySeconds => Mathf.Max(0f, clearReturnDelaySeconds);
        public Color CompletedStageTint => completedStageTint;
        public Color LockedStageTint => lockedStageTint;
        public AnimationClip IdleDemoClip => idleDemoClip;
    }
}
