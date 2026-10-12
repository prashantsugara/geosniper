using GeoSniper;

int count = 0;
void Check(bool result, string name)
{
    if (!result) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name); count++;
}
float Pass(float x, float y, float z, float end = 20f) => EnemyReactionPolicy.FlightDistance(x, y, z, 0, 1.3f, 0, 0, 1.3f, end);
var noFlight = float.MaxValue;

Check(EnemyReactionPolicy.ClassifyShotThreat(Pass(1, 1.3f, 10), 10) == EnemyThreatReaction.TakeCover,
    "A bullet one metre from the chest provokes immediate evasive cover");
Check(EnemyReactionPolicy.ClassifyShotThreat(Pass(12, 1.3f, 10), 30) == EnemyThreatReaction.None,
    "A distant passing bullet does not give the whole street the shooter position");
Check(Pass(0, 1.3f, -1) == noFlight, "An enemy behind the muzzle is not suppressed by the forward flight");
Check(Pass(0, 1.3f, 8, 5) == noFlight, "An enemy behind the impact wall is not on the bullet segment");
Check(EnemyReactionPolicy.ClassifyShotThreat(Pass(0, 1.3f, 8, 5), 3, false) == EnemyThreatReaction.TakeCover,
    "A genuinely nearby impact still triggers cover without claiming a near pass");
Check(EnemyReactionPolicy.ClassifyShotThreat(noFlight, 12) == EnemyThreatReaction.Investigate,
    "An audible distant impact raises investigation rather than a visual target lock");
Check(EnemyReactionPolicy.ClassifyShotThreat(noFlight, 12, false) == EnemyThreatReaction.None,
    "An inaudible occluded impact does not leak information");
Check(EnemyReactionPolicy.FlightDistance(0, 0, 0, 1, 1, 1, 1, 1, 1) == noFlight,
    "Zero-length flight cannot manufacture suppression");
Check(EnemyReactionPolicy.FlightDistance(float.NaN, 0, 0, 0, 0, 0, 0, 0, 5) == noFlight,
    "Invalid bullet geometry is ignored");
Check(EnemyReactionPolicy.ClassifyShotThreat(-1, float.NaN) == EnemyThreatReaction.None,
    "Malformed distances cannot turn into a threat");
Check(EnemyReactionPolicy.ClassifyGunshot(75, 100, false) == EnemyThreatReaction.Investigate,
    "Hearing a concealed shooter leaves the bot investigating");
Check(EnemyReactionPolicy.ClassifyGunshot(75, 100, true) == EnemyThreatReaction.TakeCover,
    "Seeing the same shooter gives actionable combat information");
Check(EnemyReactionPolicy.ClassifyGunshot(30, 25, false) == EnemyThreatReaction.None,
    "Suppressed weapon noise remains bounded by its actual noise radius");

float normal = EnemyReactionPolicy.AcquisitionDelay(1f, 50, .5f);
Check(EnemyReactionPolicy.AcquisitionDelay(.75f, 50, .5f) < normal
    && EnemyReactionPolicy.AcquisitionDelay(1.5f, 50, .5f) > normal,
    "Hardcore reacquires faster than Standard while Casual gives a longer dodge window");
Check(EnemyReactionPolicy.AcquisitionDelay(1f, 50, .5f, true) < normal,
    "A prepared bot reacquires sooner while retaining a finite reaction window");
Check(EnemyReactionPolicy.AcquisitionDelay(1f, 110, .5f) > EnemyReactionPolicy.AcquisitionDelay(1f, 5, .5f),
    "Distant targets take longer to settle on than nearby targets");
Check(float.IsFinite(EnemyReactionPolicy.AcquisitionDelay(float.NaN, float.PositiveInfinity, float.NaN)),
    "Invalid difficulty input still produces a bounded reaction time");

bool Fire(bool visible = true, bool alive = true, bool armed = true, bool reloading = false,
    float stagger = 0f, float suppression = 0f, float acquisition = 0f, float angle = 0f)
    => EnemyReactionPolicy.CanFire(visible, alive, armed, reloading, stagger, suppression, acquisition, angle);
Check(Fire(), "An acquired visible target with clear settled aim can be fired at");
Check(!Fire(visible: false), "A burst cannot continue after the target breaks sight");
Check(!Fire(stagger: .1f), "A body-hit flinch interrupts firing mid-burst");
Check(!Fire(suppression: .1f), "A near-miss cover response interrupts firing mid-burst");
Check(!Fire(acquisition: .1f), "Reappearance does not allow instant reacquisition shots");
Check(!Fire(armed: false) && !Fire(reloading: true) && !Fire(alive: false),
    "Disarmed, reloading and dead bots cannot continue scheduled shots");
Check(!Fire(angle: 25), "Shots wait until the rifle turns onto the target");
Check(!Fire(stagger: float.NaN) && !Fire(angle: float.NaN), "Malformed aim state fails closed");

float repeated = 0;
for (int i = 0; i < 1000; i++) repeated = EnemyReactionPolicy.SuppressionSeconds(repeated, true);
Check(repeated <= 2.5f, "Rapid repeated near passes refresh suppression without stacking permanent lockout");
Check(EnemyReactionPolicy.SuppressionSeconds(3, false) >= 3, "A small new impact does not cancel existing suppression");
Check(EnemyReactionPolicy.AccuracyMultiplier(4, 0) < EnemyReactionPolicy.AccuracyMultiplier(0, 0),
    "Moving attackers have less precise fire than settled attackers");
Check(EnemyReactionPolicy.AccuracyMultiplier(0, 0, .4f) < EnemyReactionPolicy.AccuracyMultiplier(0, 0),
    "Surviving wounds reduce precision instead of leaving robotic perfect aim");
Check(EnemyReactionPolicy.AccuracyMultiplier(float.NaN, float.NaN, float.NaN) >= .1f,
    "Malformed state cannot produce negative or unbounded accuracy");
Console.WriteLine($"{count} enemy reaction checks passed.");
