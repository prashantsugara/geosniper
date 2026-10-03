using System;

namespace GeoSniper
{
    public enum ContractPhase { Briefing, Active, Extracting, Complete, Failed }

    // Deterministic mission rules; scene code supplies observations rather than changing rewards.
    public sealed class FirstContactContract
    {
        public const float DurationSeconds=300;
        public ContractPhase Phase { get; private set; }=ContractPhase.Briefing;
        public float Remaining { get; private set; }=DurationSeconds;
        public float ExtractionHold { get; private set; }
        public int Shots { get; private set; }
        public int Hits { get; private set; }
        public bool Scouted { get; private set; }
        public bool Detected { get; private set; }
        public bool TargetDown { get; private set; }
        public string Failure { get; private set; }="";
        public bool Running => Phase==ContractPhase.Active || Phase==ContractPhase.Extracting;
        public int Accuracy => Shots==0?0:(int)Math.Round(100.0*Hits/Shots);
        public int BonusXP => Phase!=ContractPhase.Complete?0:(Scouted?25:0)+(!Detected?50:0)+(Accuracy>=75?25:0);
        public int BonusCredits => BonusXP/5;
        public void Start() { if(Phase==ContractPhase.Briefing) Phase=ContractPhase.Active; }
        public void Scout() { if(Running) Scouted=true; }
        public void Shot() { if(Running) Shots++; }
        public void ResolveShot(bool hit) { if(Running && hit && Hits<Shots) Hits++; }
        public void EliminateTarget()
        {
            if(Phase!=ContractPhase.Active) return;
            TargetDown=true;Phase=ContractPhase.Extracting;ExtractionHold=0;
        }
        public void Fail(string reason)
        {
            if(Phase==ContractPhase.Complete || Phase==ContractPhase.Failed) return;
            Failure=reason;Phase=ContractPhase.Failed;
        }
        public void Tick(float seconds,bool paused,bool dead,bool detected,bool insideExtraction)
        {
            if(!Running) return;
            if(dead) {Fail("You were downed. Try another approach.");return;}
            if(paused) return;
            Detected |= detected;
            seconds=Math.Max(0,seconds);
            Remaining=Math.Max(0,Remaining-seconds);
            if(Remaining<=0) {Fail("The extraction window closed.");return;}
            if(Phase!=ContractPhase.Extracting) return;
            // Extraction needs two uninterrupted seconds inside the zone.
            ExtractionHold=insideExtraction?Math.Min(2,ExtractionHold+seconds):0;
            if(ExtractionHold>=2) Phase=ContractPhase.Complete;
        }
    }
}
