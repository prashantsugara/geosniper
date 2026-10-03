using UnityEngine;

namespace GeoSniper
{
    public enum DifficultyLevel { Casual, Standard, Hardcore }
    [CreateAssetMenu(menuName="GeoSniper/Difficulty Profile")]
    public sealed class DifficultyProfile : ScriptableObject
    {
        [Min(.1f)] public float enemyHealth = 1f;
        [Min(.1f)] public float enemyDamage = 1f;
        [Min(.1f)] public float enemySpeed = 1f;
        [Min(.1f)] public float detectionSpeed = 1f;
        [Min(.1f)] public float reactionTime = 1f;
        [Min(.1f)] public float missionTime = 1f;
        [Min(.1f)] public float vipHealth = 1f;
        [Min(1)] public int stealthAlertLimit = 4;
        [Min(.1f)] public float duelLockSeconds = 3.2f;
        [Min(.1f)] public float counterSniperLockSeconds = 2.2f;
        public static DifficultyLevel Selected
        {
            get => (DifficultyLevel)Mathf.Clamp(PlayerPrefs.GetInt("GeoSniper.Difficulty",1),0,2);
            set => PlayerPrefs.SetInt("GeoSniper.Difficulty",Mathf.Clamp((int)value,0,2));
        }
        // Each mission owns a snapshot; lobby settings cannot retune a running fight.
        public static DifficultyProfile Create(DifficultyLevel level)
        {
            var authored=Resources.Load<DifficultyProfile>("Difficulty/"+level);
            if(authored!=null) return Instantiate(authored);
            var p=CreateInstance<DifficultyProfile>(); p.name=level.ToString();
            if(level==DifficultyLevel.Casual)
            {
                p.enemyHealth=.75f; p.enemyDamage=.65f; p.enemySpeed=.8f;
                p.detectionSpeed=.65f; p.reactionTime=1.5f; p.missionTime=1.35f;
                p.vipHealth=1.5f; p.stealthAlertLimit=6;
                p.duelLockSeconds=4.8f; p.counterSniperLockSeconds=3.3f;
            }
            else if(level==DifficultyLevel.Hardcore)
            {
                p.enemyHealth=1.5f; p.enemyDamage=1.35f; p.enemySpeed=1.15f;
                p.detectionSpeed=1.4f; p.reactionTime=.75f; p.missionTime=.8f;
                p.vipHealth=.65f; p.stealthAlertLimit=2;
                p.duelLockSeconds=2.4f; p.counterSniperLockSeconds=1.65f;
            }
            return p;
        }
    }
    public sealed class MissionAlertState
    {
        public int Level { get; private set; }
        private float lastRaiseTime = -10f;
        public void Raise()
        {
            if (Time.time - lastRaiseTime < 2.5f) return;
            lastRaiseTime = Time.time;
            Level = Mathf.Min(100, Level + 1);
        }
        public void Reset() { Level = 0; lastRaiseTime = -10f; }
    }
}
