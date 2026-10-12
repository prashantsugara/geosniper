using UnityEngine;
namespace GeoSniper
{
    // Bundled narration works on Android without a speech service or network request.
    public sealed class StoryNarrator : MonoBehaviour
    {
        AudioSource source;
        AudioClip clip;
        bool outcomeClip;
        public static bool Enabled {get=>PlayerPrefs.GetInt("GeoSniper.StoryNarration",1)==1;set{PlayerPrefs.SetInt("GeoSniper.StoryNarration",value?1:0);PlayerPrefs.Save();}}
        public static bool Playing(GameObject owner){var narrator=owner.GetComponent<StoryNarrator>();return narrator!=null && narrator.source!=null && narrator.source.isPlaying;}
        public static void Play(GameObject owner,int nodeId,bool outcome)
        {
            if(owner==null)return;
            var narrator=owner.GetComponent<StoryNarrator>() ?? owner.AddComponent<StoryNarrator>();
            narrator.Release();narrator.outcomeClip=outcome;if(!Enabled)return;
            narrator.clip=Resources.Load<AudioClip>("Audio/Story/"+(outcome?"Outcome_":"Mission_")+nodeId.ToString("00"));
            if(narrator.clip==null)return;
            if(narrator.source==null){narrator.source=owner.AddComponent<AudioSource>();narrator.source.playOnAwake=false;narrator.source.spatialBlend=0;narrator.source.ignoreListenerPause=true;narrator.source.volume=.9f;}
            narrator.source.clip=narrator.clip;narrator.source.Play();
        }
        public static void Stop(GameObject owner){if(owner!=null)owner.GetComponent<StoryNarrator>()?.Release();}
        public static void PlayRadio(GameObject owner,string key)
        {
            if(owner==null || !Enabled)return;
            var narrator=owner.GetComponent<StoryNarrator>() ?? owner.AddComponent<StoryNarrator>();narrator.Release();narrator.outcomeClip=false;
            narrator.clip=Resources.Load<AudioClip>("Audio/Story/Radio_"+key);if(narrator.clip==null)return;
            if(narrator.source==null){narrator.source=owner.AddComponent<AudioSource>();narrator.source.playOnAwake=false;narrator.source.spatialBlend=0;narrator.source.volume=.9f;}
            narrator.source.clip=narrator.clip;narrator.source.Play();
        }
        public static void StopOutcome(GameObject owner){var narrator=owner!=null?owner.GetComponent<StoryNarrator>():null;if(narrator!=null && narrator.outcomeClip)narrator.Release();}
        void Release(){if(source!=null){source.Stop();source.clip=null;}if(clip!=null){Resources.UnloadAsset(clip);clip=null;}}
        void OnDisable()=>Release();
        void OnDestroy()=>Release();
    }
}
