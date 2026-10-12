using UnityEngine;
namespace GeoSniper
{
    public sealed partial class GeoSniperGame
    {
        bool showingCampaignStory;
        int currentBriefingNodeId=-1;
        public void PlayBriefingAudio(int nodeId,bool forceRestart=false)
        {
            if(nodeId<0 || nodeId>=CampaignNodeGraph.Nodes.Length)return;
            if(currentBriefingNodeId==nodeId && !forceRestart)return;
            currentBriefingNodeId=nodeId;
            StoryNarrator.Play(gameObject,nodeId,false);
        }
        public void StopBriefingAudio(){StoryNarrator.Stop(gameObject);currentBriefingNodeId=-1;}
        public void ToggleBriefingAudio(int nodeId)
        {
            if(StoryNarrator.Playing(gameObject))StopBriefingAudio();else PlayBriefingAudio(nodeId,true);
        }
        void DrawCampaignScreen(float w,float h){DrawCampaignStory(w,h);}
    }
}
