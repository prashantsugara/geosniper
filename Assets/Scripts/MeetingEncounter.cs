using System;

namespace GeoSniper
{
    // Intro encounter pacing is independent of frame rate and pauses with the map.
    public sealed class MeetingEncounter
    {
        public float Elapsed {get;private set;}
        public float Observation {get;private set;}
        public bool Identified {get;private set;}
        public bool ShotFired {get;private set;}
        public void Tick(float dt,bool observing,bool paused)
        {
            if(paused || Identified) return;
            dt=Math.Max(0,dt);Elapsed+=dt;
            if(Elapsed<8) return;
            float observationTime=Math.Min(dt,Elapsed-8);
            Observation=observing?Math.Min(3,Observation+observationTime):Math.Max(0,Observation-dt*.35f);
            Identified=Observation>=3;
        }
        public void Fire() {if(Identified) ShotFired=true;}
    }
}
