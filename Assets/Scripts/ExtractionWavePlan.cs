using UnityEngine;

namespace GeoSniper
{
    // Counts only active mission time; callers pause Tick with briefing/map/failure.
    public sealed class ExtractionWavePlan
    {
        public const int TotalWaves=3;
        public int Wave {get;private set;}=1;
        public int Pending {get;private set;}
        public int ActiveCap {get;}
        public float Elapsed {get;private set;}
        public float Remaining=>Mathf.Max(0,duration-Elapsed);
        public float NextWaveIn=>Mathf.Max(0,duration*Wave/TotalWaves-Elapsed,6f-sinceArrival);
        public bool ReadyForExtraction=>Wave==TotalWaves && Pending==0 && Remaining<=0 && sinceArrival>=6f;
        readonly float duration;
        float sinceArrival;
        public ExtractionWavePlan(float holdSeconds,int threatBudget)
        {duration=Mathf.Max(25f,holdSeconds);ActiveCap=Mathf.Clamp(threatBudget,4,8);}
        public void Tick(float delta)
        {if(delta<=0 || !float.IsFinite(delta))return;Elapsed+=delta;sinceArrival+=delta;}
        public bool StartNextWave(int alive)
        {
            if(Wave>=TotalWaves || Pending>0 || NextWaveIn>0 || alive>=ActiveCap)return false;
            Wave++;Pending=Mathf.Clamp(ActiveCap/2+(Wave==TotalWaves?1:0),2,4);return true;
        }
        public bool CanSpawn(int alive)=>Pending>0 && alive<ActiveCap;
        public void RecordSpawn()
        {if(Pending<=0)return;Pending--;sinceArrival=0;}
    }
}
