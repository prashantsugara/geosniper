# Combat readability pass

Guards refresh physical line of sight when damaged before deciding to hold an angle or take cover. A stale visibility flag cannot give a guard a return-fire decision through a wall. Damage and nearby incoming shots create a bounded three-second pressure window. Healthy guards otherwise settle at a firing angle for a varied interval, rather than constantly shuffling; pressure or close range shortens the pause. Investigation movement is faster, while search scans and successive nearby probes vary and remain bounded to the last reported area. Probe safety includes support, reachability and spacing from teammates. Squad flank distances use individual side preference and variable distances.

Confirmed damage owns the impact sound. Body and head clips are generated once per camera and reused on a dedicated source, so slight pitch variation does not alter rifle reports or narration. Duplicate confirmation and cinematic replay pings are removed. Duel feedback plays on host-confirmed damage. Training targets retain their existing confirmation sound.

The centre hit marker briefly contracts after impact. A lethal hit retains gold when its same-frame damage callback arrives after the kill callback; a later nonlethal hit is white even if the previous kill banner still exists.

Verification: C# compile and disposable Unity checks exercise wall-blocked damage reactions, exposed close detection, pressure-dependent angle holds, physical bullets, cancelled bursts, safe movement, squad separation, clip/source reuse, kill/body marker ordering, scope regression and campaign setup/indoor actions. These checks do not replace Android device playtesting or establish a retention improvement.
