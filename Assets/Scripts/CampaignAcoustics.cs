using UnityEngine;
namespace GeoSniper
{
    // Four spatial voices and one cached echo keep campaign feedback bounded.
    public sealed class CampaignAcoustics : MonoBehaviour
    {
        static CampaignAcoustics active;
        readonly AudioSource[] voices=new AudioSource[4];
        readonly AudioClip[] impacts=new AudioClip[3];
        AudioClip echo,cityBed,roomBed;AudioSource citySource,roomSource;int voiceIndex;
        public void Initialize()
        {
            active=this;if(voices[0]!=null)return;
            for(int i=0;i<voices.Length;i++)
            {
                var obj=new GameObject("Campaign impact voice");obj.transform.SetParent(transform,false);
                voices[i]=obj.AddComponent<AudioSource>();voices[i].playOnAwake=false;
                voices[i].spatialBlend=1;voices[i].minDistance=3;voices[i].maxDistance=100;
                voices[i].dopplerLevel=0;voices[i].rolloffMode=AudioRolloffMode.Logarithmic;
            }
            for(int kind=0;kind<3;kind++)impacts[kind]=BuildSound(kind);
            echo=BuildSound(3);
            cityBed=BuildEnvironment(false);roomBed=BuildEnvironment(true);
            citySource=CreateBed("Distant city traffic",cityBed);roomSource=CreateBed("Interior room tone",roomBed);
        }
        AudioSource CreateBed(string name,AudioClip clip)
        {
            var obj=new GameObject(name);obj.transform.SetParent(transform,false);
            var source=obj.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.loop=true;source.clip=clip;source.volume=0;source.Play();return source;
        }
        static AudioClip BuildEnvironment(bool room)
        {
            const int rate=22050,length=12;var samples=new float[rate*length];var random=new System.Random(room?519:518);float low=0,mid=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate,n=(float)random.NextDouble()*2-1;
                low=Mathf.Lerp(low,n,room?.014f:.006f);mid=Mathf.Lerp(mid,n,.07f);
                float traffic=room?1:.6f+.4f*Mathf.Sin(t*Mathf.PI*2/length)*Mathf.Sin(t*Mathf.PI*2/length);
                float seam=Mathf.Clamp01(Mathf.Min(t,length-t)/.12f);
                samples[i]=Mathf.Clamp((low*(room?.45f:1.5f)+mid*(room?.10f:.18f))*traffic*seam*(room?5f:3f),-.65f,.65f);
            }
            var clip=AudioClip.Create(room?"Quiet interior air":"Distant road wash",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        public void UpdateEnvironment(UrbanPlayer player,bool playing,bool blocked,bool narration)
        {
            if(citySource==null || roomSource==null)return;
            bool inside=false;if(player!=null)foreach(var building in EnterableBuilding.All)if(building.Contains(player.transform.position)){inside=true;break;}
            float gain=playing && !blocked && Application.isFocused && player!=null && !player.Health.IsDead?(narration || RadioCommsChannel.IsChannelBusy?.35f:1f):0;
            float blend=1-Mathf.Exp(-3*Time.unscaledDeltaTime);
            citySource.volume=Mathf.Lerp(citySource.volume,(inside?.025f:.18f)*gain,blend);
            roomSource.volume=Mathf.Lerp(roomSource.volume,(inside?.16f:0)*gain,blend);
        }
        static AudioClip BuildSound(int kind)
        {
            const int rate=22050;float length=kind==3?1.5f:.32f;
            var samples=new float[Mathf.CeilToInt(rate*length)];var random=new System.Random(197+kind);float filtered=0;
            var delays=new[]{.16f,.31f,.52f};
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate,noise=(float)random.NextDouble()*2-1;filtered=Mathf.Lerp(filtered,noise,kind==3?.08f:.3f);
                if(kind==3)
                {
                    foreach(float delay in delays)
                    {float tail=t-delay;if(tail>=0)samples[i]+=filtered*Mathf.Exp(-tail*12)*.4f;}
                }
                else if(kind==1)samples[i]=(Mathf.Sin(t*2100)*.5f+Mathf.Sin(t*3470)*.25f+noise*.2f)*Mathf.Exp(-t*25);
                else samples[i]=(filtered*.8f+Mathf.Sin(t*(kind==2?180:100))*.2f)*Mathf.Exp(-t*(kind==2?12:24));
                samples[i]=Mathf.Clamp(samples[i],-.8f,.8f);
            }
            var clip=AudioClip.Create(kind==3?"Urban rifle reflections":"Campaign impact "+kind,samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        public static bool Impact(Vector3 point,byte kind)
        {
            if(active==null || kind>2)return false;
            var camera=Camera.main;if(camera==null || (camera.transform.position-point).sqrMagnitude>10000)return true;
            var voice=active.voices[active.voiceIndex++%4];voice.Stop();voice.transform.position=point;
            voice.pitch=1+(active.voiceIndex%3-1)*.035f;voice.PlayOneShot(active.impacts[kind],.55f);return true;
        }
        public static void RifleEcho(AudioSource source,bool suppressed)
        {
            if(active==null || source==null || source.mute)return;
            var origin=Camera.main!=null?Camera.main.transform.position:source.transform.position;
            bool indoors=Physics.Raycast(origin,Vector3.up,6,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            source.PlayOneShot(active.echo,suppressed?.025f:indoors?.09f:.22f);
        }
        public static void SetMuted(bool muted){if(active==null)return;foreach(var source in active.voices)if(source!=null)source.mute=muted;if(active.citySource!=null)active.citySource.mute=muted;if(active.roomSource!=null)active.roomSource.mute=muted;}
        void OnDestroy(){if(active==this)active=null;foreach(var clip in impacts)if(clip!=null)Destroy(clip);if(echo!=null)Destroy(echo);if(cityBed!=null)Destroy(cityBed);if(roomBed!=null)Destroy(roomBed);}
    }
}
