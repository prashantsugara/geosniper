using System.Collections.Generic;
using UnityEngine;
namespace GeoSniper
{
    public sealed partial class GeoSniperGame
    {
        float deploymentStartedAt;
        bool deploymentTracked;
        string deploymentMapMode;
        internal static string DeploymentFailureReason(System.Exception error,string phase)
        {
            if(error is System.OutOfMemoryException)return "memory_exhausted";
            if(error.Message=="NO SAFE DRY LAND IN LOADED SECTOR")return "no_safe_spawn";
            if(error.Message=="CACHED_SECTOR_MISSING")return "cache_missing";
            if(error.Message=="RENDER_SHADER_MISSING")return "shader_missing";
            string step=phase=="MAP DOWNLOAD"?"map":phase=="LOCATION"?"location":phase=="WORLD"?"world":phase=="PLAYER"?"player":"startup";
            return step+(error is System.TimeoutException?"_timeout":"_error");
        }
        void BeginDeploymentTracking(string mapMode)
        {
            EndDeploymentTracking("deployment_abandoned","replaced");
            deploymentStartedAt=Time.realtimeSinceStartup;deploymentTracked=true;deploymentMapMode=mapMode;
            TrackDeployment("deployment_started","");
        }
        void TrackDeployment(string eventName,string reason)
        {
            if(!deploymentTracked)return;
            GameAnalyticsManager.TrackCustomEvent(eventName,new Dictionary<string,object>
            {
                {"stage_index",stageIndexToStart+1},{"map_mode",deploymentMapMode},
                {"load_phase",startupStage},{"duration_sec",Mathf.RoundToInt(Time.realtimeSinceStartup-deploymentStartedAt)},
                {"attempt_id",GameAnalyticsManager.CurrentDeploymentAttempt},{"is_retry",loadFailed?1:0},
                {"reason",reason}
            });
        }
        void EndDeploymentTracking(string eventName,string reason="")
        {TrackDeployment(eventName,reason);deploymentTracked=false;}
        void OnApplicationQuit()
        {EndDeploymentTracking("deployment_abandoned","app_quit");}
    }
}
