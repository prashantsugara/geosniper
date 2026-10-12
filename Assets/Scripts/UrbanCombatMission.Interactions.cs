using UnityEngine;
namespace GeoSniper
{
    public sealed partial class UrbanCombatMission
    {
        float interactionHealth=100;
        void AnimateStoryInteraction(StoryPoint point,float progress)
        {
            if(point.Visual==null)return;
            if(activeContract.Type==CampaignContractType.Sabotage)
            {
                float pull=Mathf.SmoothStep(0,1,Mathf.Clamp01((progress-.65f)/.35f));
                var lever=point.Visual.transform.Find("Disconnect lever");
                if(lever!=null)lever.localRotation=Quaternion.Euler(Mathf.Lerp(-25,25,pull),0,0);
                var indicator=point.Visual.transform.Find("Power indicator");
                if(indicator!=null)indicator.GetComponent<Renderer>().sharedMaterial.color=progress>0?new Color(.95f,.62f,.12f):Color.green;
            }
            else
            {
                float lift=Mathf.SmoothStep(0,1,Mathf.Clamp01((progress-.4f)/.6f));
                Vector3 toward=player.transform.position-point.RestPosition;toward.y=0;
                point.Visual.transform.position=point.RestPosition+Vector3.up*(lift*.22f)+toward.normalized*(lift*.3f);
                point.Visual.transform.rotation=point.RestRotation*Quaternion.Euler(-lift*18,0,lift*5);
            }
        }
        string InteractionPhaseLabel(float progress)=>activeContract.Type==CampaignContractType.Rescue?"Keep the witness covered while releasing them.":activeContract.Type==CampaignContractType.Sabotage?
            progress<.65f?"CHECKING POWER FEED — remain at the panel":"THROWING BREAKER — disconnecting relay":
            progress<.4f?"VERIFYING DISPATCH FILE":"SECURING DOCUMENTS — remain close";
    }
}
