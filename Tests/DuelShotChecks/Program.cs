using GeoSniper.Duel;
using UnityEngine;

int passed = 0;
void Check(bool success, string message)
{
    if (!success) throw new Exception("FAIL: " + message);
    passed++;
}
bool Near(float a, float b, float tolerance = .001f) => Math.Abs(a - b) < tolerance;
Vector3 Unit(Vector3 value) => value / (float)Math.Sqrt(Vector3.Dot(value, value));
var standing = new DuelHistoricalPose(0, new Vector3(0, 0, 0), 0, 0, false);
var crouched = new DuelHistoricalPose(0, new Vector3(0, 0, 0), 0, 0, true);
Vector3 forward = new(0, 0, 1);

var history = new DuelPoseHistory();
Check(!history.TrySample(0, 0, out _), "An unobserved player cannot be hit by rewind");
history.Record(10, new Vector3(0, 0, 0), 350, 350, false);
history.Record(10.2, new Vector3(20, 0, 0), 10, 10, true, false);
Check(history.TrySample(10.2, .1f, out var interpolated) && Near(interpolated.Position.x, 10, .002f), "Moving targets interpolate between host observations");
Check(Near(interpolated.Yaw, 360, .002f) && Near(interpolated.Pitch, 360, .002f), "Aim angles wrap through zero without spinning backwards");
Check(interpolated.Alive, "Death status is not applied before the recorded death time");
Check(history.TrySample(10.2, 0, out var dead) && !dead.Alive, "Current dead pose remains dead");
Check(!history.Record(10.1, new Vector3(100, 0, 0), 0, 0, false), "Delayed state cannot overwrite newer history");
Check(!history.Record(10.3, new Vector3(float.NaN, 0, 0), 0, 0, false), "Nonfinite positions are excluded from history");
Check(!history.TrySample(10.451, 0, out _), "Stale positions older than250ms cannot remain hittable");
Check(!history.TrySample(10.1, 0, out _), "History does not accept a future host observation");
history.Clear();
for (int i = 0; i < 64; i++) history.Record(i * .01, new Vector3(i, 0, 0), 0, 0, false);
Check(history.Count == 32, "Long rounds retain bounded history memory");
Check(history.TrySample(.63, 10, out var clamped) && Near(clamped.Position.x, 38, .002f), "A claimed ten-second rewind is clamped to250ms");
history.Record(.63, new Vector3(99, 0, 0), 0, 0, false);
Check(history.Count == 32 && history.TrySample(.63, 0, out var replaced) && Near(replaced.Position.x, 99), "A same-frame observation replaces rather than duplicating history");

var validator = new DuelShotValidator();
validator.ResetRound(1);
bool Shoot(int round, int id, int rifle, double time, out DuelValidatedShot shot) =>
    validator.TryAccept(round, id, rifle, standing.EyePosition, forward, standing, time, out shot, out _);
Check(Shoot(1, 1, 0, 0, out var accepted), "A live player's camera ray is accepted");
Check(!Shoot(1, 1, 0, .6, out _), "Reliable retransmission cannot apply a shot twice");
Check(!Shoot(2, 999, 0, .6, out _) && Shoot(1, 2, 0, .6, out _), "A stale or future round cannot consume active-round identifiers");
Check(!Shoot(1, 3, 2, .7, out _) && Shoot(1, 4, 2, 1.15, out _), "Switching to a rapid rifle cannot bypass the previous rifle's chamber");
Check(Shoot(1, 5, 1, 1.48, out _) && !Shoot(1, 6, 0, 1.8, out _), "Switching away from the bolt action preserves its cooldown");
Check(!Shoot(1, 6, 0, 2.6, out _), "A rejected identifier cannot be retried after its cooldown expires");
Check(!Shoot(1, 7, 0, 1, out _), "Logical shot timestamps cannot run backwards");
Check(!Shoot(1, 8, 0, double.NaN, out _) && !Shoot(1, 9, 0, -1, out _), "Nonfinite and negative shot times are rejected");
Check(!Shoot(1, 10, 3, 3, out _), "An unknown rifle cannot choose its own statistics");

validator.ResetRound(3);
Check(!validator.TryAccept(3, 1, 0, new Vector3(20, 1.55f, 0), forward, standing, 0, out _, out _), "A peer cannot fire from across the map");
Check(!validator.TryAccept(3, 2, 0, standing.EyePosition, new Vector3(0, 0, 100), standing, 0, out _, out _), "A direction cannot scale range or geometry");
Check(!validator.TryAccept(3, 3, 0, standing.EyePosition, new Vector3(float.NaN, 0, 1), standing, 0, out _, out _), "A nonfinite direction cannot damage a player");
Check(!validator.TryAccept(3, 4, 0, standing.EyePosition, forward, new DuelHistoricalPose(0, standing.Position, 0, 0, false, false), 0, out _, out _), "An eliminated player cannot fire");
Check(validator.GetRemainingAmmo(0, 0) == 5, "Rejected requests do not consume ammunition");
Check(validator.TryAccept(3, 5, 0, standing.EyePosition + new Vector3(.15f, -.1f, 1.5f), forward, standing, 0, out _, out _), "A visual muzzle offset near the camera remains valid");
Check(Near(crouched.EyePosition.y, .85f), "Crouch origin follows the real first-person camera");

validator.ResetRound(4);
for (int i = 0; i < 5; i++) Check(Shoot(4, i + 1, 0, i * .55, out _), "Five legitimate Barrett shots are accepted");
Check(!Shoot(4, 6, 0, 2.75, out _) && validator.GetRemainingAmmo(0, 2.75) == 0, "A sixth shot cannot bypass magazine capacity");
Check(!validator.TryBeginReload(4, 0, 2.3), "Reload cannot start before the chamber cycle ends");
Check(validator.TryBeginReload(4, 0, 2.75), "An empty magazine accepts a declared reload");
Check(!Shoot(4, 7, 2, 4, out _), "Switching rifles cannot shoot during a reload");
Check(validator.GetRemainingAmmo(0, 4.94) == 0 && validator.IsReloading(4.94), "A reload does not refill ammunition early");
Check(Shoot(4, 8, 0, 4.95, out _) && validator.GetRemainingAmmo(0, 4.95) == 4, "A completed reload refills exactly one magazine");
Check(validator.GetRemainingAmmo(1, 4.95) == 20 && validator.GetRemainingAmmo(2, 4.95) == 30, "Every rifle has separate ammunition");
Check(!validator.TryBeginReload(5, 0, 6), "A stale round cannot refill an active magazine");
validator.ResetRound(6);
Check(Shoot(6, 1, 1, 1, out _) && Shoot(6, 2, 1, 2.05, out _) && Shoot(6, 3, 1, 3.10, out _), "A reliable batch preserves valid logical shot cadence");

validator.ResetRound(7);
Check(Shoot(7, 1, 0, 10, out _) && validator.ShiftClock(-.5), "Reconnect can shift an existing cadence clock");
Check(!Shoot(7, 2, 2, 9.7, out _) && Shoot(7, 3, 0, 10.05, out _), "A reconnect preserves the remaining rifle chamber time");
Check(validator.HighestShotId == 3 && validator.GetRemainingAmmo(0, 10.05) == 3, "Clock correction preserves request deduplication and ammunition");
Check(!validator.ShiftClock(double.NaN) && !validator.ShiftClock(double.PositiveInfinity)
    && !validator.ShiftClock(180.01) && !validator.ShiftClock(-180.01), "Malformed clock corrections are rejected");
Check(Shoot(7, 4, 0, 10.6, out _), "Rejected clock corrections cannot mutate the rifle timeline");
validator.ResetRound(8);
Check(Shoot(8, 1, 2, 10, out _) && validator.TryBeginReload(8, 2, 10.32)
    && validator.ShiftClock(-1), "Reconnect can shift an in-progress reload");
Check(validator.IsReloading(10.419) && validator.GetRemainingAmmo(2, 10.419) == 29,
    "Clock correction preserves the remaining reload time without granting ammunition");
Check(Shoot(8, 2, 2, 10.42, out _) && validator.GetRemainingAmmo(2, 10.42) == 29,
    "The shifted reload completes at the corresponding synchronized time");
validator.ResetRound(9);
Check(validator.ShiftClock(180) && validator.ShiftClock(-180) && Shoot(9, 1, 1, 0, out _),
    "Clock correction preserves a fresh-round empty cadence history");
Check(Near(validator.GetShotCooldownRemaining(.2), .85f), "Reconnect snapshots expose the remaining bolt-action chamber delay");
Check(Near(validator.GetShotCooldownRemaining(-100), 1.05f)
    && validator.GetShotCooldownRemaining(double.NaN) == 0, "Cooldown snapshots remain finite and bounded");
validator.ResetRound(10);
Check(Shoot(10, 1, 2, 5, out _) && validator.TryBeginReload(10, 2, 5.32), "A partially used magazine can start a snapshot-test reload");
validator.GetReloadState(5.5, out int reloadingWeapon, out float reloadRemaining);
Check(reloadingWeapon == 2 && Near(reloadRemaining, .92f) && validator.GetRemainingAmmo(2, 5.5) == 29,
    "Reconnect snapshots distinguish remaining reload time from ammunition already available");
validator.ShiftClock(1);
validator.GetReloadState(6.5, out reloadingWeapon, out reloadRemaining);
Check(reloadingWeapon == 2 && Near(reloadRemaining, .92f), "Snapshot reload time survives synchronization to a new clock");
validator.GetReloadState(7.421, out reloadingWeapon, out reloadRemaining);
Check(reloadingWeapon == -1 && reloadRemaining == 0 && validator.GetRemainingAmmo(2, 7.421) == 30,
    "Expired reload snapshots complete the magazine once and clear the reload marker");
validator.GetReloadState(double.NaN, out reloadingWeapon, out reloadRemaining);
Check(reloadingWeapon == -1 && reloadRemaining == 0, "Invalid snapshot clocks cannot produce nonfinite packet fields");

DuelValidatedShot RayTo(Vector3 origin, Vector3 destination, int rifle = 1) => new(1, 1, rifle, origin, Unit(destination - origin), 0);
var target = new DuelHistoricalPose(0, new Vector3(0, 0, 216), 0, 0, false);
Check(DuelShotValidator.TryIntersect(RayTo(standing.EyePosition, target.Position + new Vector3(0, 1.65f, 0)), target, out var head) && head.Headshot && Near(head.Damage, 100), "A216m headshot uses the calibrated head volume and lethal host damage");
Check(DuelShotValidator.TryIntersect(RayTo(standing.EyePosition, target.Position + new Vector3(0, .9f, 0), 1), target, out var torso) && torso.Zone == 1 && Near(torso.Damage, 85), "A torso shot uses the rifle's authoritative base damage");
var crouchingTarget = new DuelHistoricalPose(0, target.Position, 0, 0, true);
Check(!DuelShotValidator.TryIntersect(new DuelValidatedShot(1, 1, 1, new Vector3(0, 1.55f, 0), forward, 0), crouchingTarget, out _), "A crouched target can avoid a standing-head-height shot");
Check(DuelShotValidator.TryIntersect(RayTo(standing.EyePosition, target.Position + new Vector3(0, .95f, 0)), crouchingTarget, out var crouchHit) && crouchHit.Headshot, "Crouching moves the authoritative head volume");
Check(!DuelShotValidator.TryIntersect(new DuelValidatedShot(1, 1, 1, new Vector3(.5f, .74f, 0), forward, 0), target, out _), "A nearby miss does not become a torso hit");
Check(!DuelShotValidator.TryIntersect(new DuelValidatedShot(1, 1, 1, standing.EyePosition, new Vector3(0, 0, -1), 0), target, out _), "The target behind the shot direction cannot be hit");
Check(!DuelShotValidator.TryIntersect(accepted, new DuelHistoricalPose(0, target.Position, 0, 0, false, false), out _), "A dead target cannot be hit again");
var distant = new DuelHistoricalPose(0, new Vector3(0, 0, 1300), 0, 0, false);
Check(!DuelShotValidator.TryIntersect(RayTo(standing.EyePosition, distant.Position + new Vector3(0, 1.65f, 0)), distant, out _), "Shots beyond weapon range cannot damage a player");
var longRange = new DuelHistoricalPose(0, new Vector3(0, 0, 1199), 0, 0, false);
Check(!DuelShotValidator.TryIntersect(new DuelValidatedShot(1, 1, 1, new Vector3(.3f, 1.65f, 0), forward, 0), longRange, out _), "Long-range floating-point cancellation cannot enlarge a head hitbox");
Check(Near(DuelShotValidator.DamageFor(0, 0), 72) && Near(DuelShotValidator.DamageFor(1, 2, .65f), 29.25f), "Limb and glass penetration use host multipliers");
Check(Near(DuelShotValidator.DamageFor(1, 1, 9), 85) && DuelShotValidator.DamageFor(1, 1, float.NaN) == 0, "Damage scales cannot boost a rifle or become nonfinite");
Console.WriteLine($"PASS: {passed} host shot, ammunition, history and geometry checks.");
