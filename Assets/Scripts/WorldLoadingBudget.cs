using UnityEngine;
namespace GeoSniper
{
    internal static class WorldLoadingBudget
    {
        internal const float ProgressTimeout=120f,MaximumDuration=600f;
        internal static float Deadline(float started,float progressedAt)
            =>Mathf.Min(started+MaximumDuration,progressedAt+ProgressTimeout);
    }
}
