# Character animation pass

Implemented 2026-09-30 for the two shipped enemy rigs and civilian locomotion fallback.

- Regular infantry now use an 18-round magazine and a two-second reload. The final round interrupts a burst, stops aiming and firing, and sends the soldier toward available cover. Crouched soldiers remain down until the reload finishes.
- The weapon anchor lowers the rifle and moves the support hand toward its magazine through the reload, then returns to the normal two-hand grip. The procedural fallback animator also lowers its rifle. Counter-snipers retain their existing lock and firing behavior.
- Imported soldiers lean slightly into turns and vary their idle head scan. Civilians can glance at another nearby idle civilian and make a small hand gesture. Their existing flee and cower behavior remains the priority during danger.

Compilation and the isolated Unity review suite passed, including reload and grip checks on both shipped enemy rigs. Rendered reload poses were inspected. This pass uses procedural poses because no authored reload clip ships with these rigs. Android visual quality, frame rate and combat balance still need device review; no APK was built.
