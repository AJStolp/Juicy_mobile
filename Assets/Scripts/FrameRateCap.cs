using UnityEngine;

// Caps the frame rate. Without it the editor's Game view renders as fast as
// the machine allows (v-sync is off in both quality levels), which heats the
// laptop for nothing, and iPhones would default to 30 frames per second.
public static class FrameRateCap
{
    public const int Target = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply()
    {
        Application.targetFrameRate = Target;
    }
}
