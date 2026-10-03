namespace GeoSniper
{
    public sealed class HitConfirmation
    {
        float until;
        public bool Headshot { get; private set; }
        public bool Confirm(float now,float damage,bool headshot)
        {
            if (!float.IsFinite(now) || !float.IsFinite(damage) || damage<=0) return false;
            Headshot=headshot;
            until=now+(headshot?.45f:.28f);
            return true;
        }
        public float Remaining(float now) => float.IsFinite(now) && now<until ? until-now : 0;
    }
}
