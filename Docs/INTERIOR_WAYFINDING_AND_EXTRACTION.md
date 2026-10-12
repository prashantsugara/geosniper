# Interior wayfinding and extraction

Accessible floors now have a ground-floor/floor-number sign with stairs/roof or street-exit guidance. Signs sit against an existing wall above head height; colliders remain disabled. Existing HQ floor briefings remain.

Document stations carry a blue archive identification sign and stacked indexed files. Relay stations carry an amber electrical-control sign and circuit conduits. These details survive document collection and do not add colliders.

Timed extraction uses a compact progress banner for the opening operation (4 seconds), relay missions (6 seconds), and The Leak (12 seconds). Leaving the zone resets progress. Taking damage also resets The Leak's extraction progress; its existing one-second recovery delay remains.

Relay extraction sends living, reachable guards toward the exit once per mission. Existing bounded Leak response waves remain; no extra enemies are spawned by this pass. First-mission difficulty remains unchanged.

Verification: C# runtime/editor compilation and the disposable Unity campaign regression scene. Android appearance and performance still require the user's device playtest.

## Level 5 setup correction

Single-storey objective security previously required guards inside the building while rejecting the objective floor, leaving no possible placement. Single-storey sites now admit supported, reachable nearby approaches. Multi-storey sites still exclude objective floors, but sample a finer floor grid and existing evidence-station positions so compact floors are not missed by the old 3/5/8/12 metre rings. Guard candidates retain physical support, reachability and minimum player distance checks.

The broad opening-sightline regression failed twice in mission 2 (one tank obstruction, one building obstruction). That separate issue is not covered by the focused indoor run and remains unresolved.
