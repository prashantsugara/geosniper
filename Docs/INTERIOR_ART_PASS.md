# Interior art pass — 8 October 2026

Mapped playable buildings now use shared plaster, stone floor tile and oak surfaces, with a separate plaster finish under upper floors. Exterior facade finishes are preserved. Window jambs, headers and skirting give real openings visible depth. Desks have blotters, archive ledgers and drawer pulls; upper-floor cabinets have drawer faces and handles. Detailed interiors have warm emissive ceiling diffusers. These fixtures are visual sources, not additional real-time lights.

Small decorative pieces have disabled colliders and are batched with the existing architecture. The objective desktop centre, door openings and stair circulation are preserved. Materials are shared per sector; three original 512px texture swatches are reused across buildings. `Tools/create_interior_surfaces.py` reproduces them without downloaded artwork.

Evidence and sabotage extraction points now have a portable radio case, latches, antenna, green signal lamp and a thin perimeter marker in place of the broad green block. The case sits beside the pickup centre and has no blocking collider. Existing extraction timing and map guidance remain unchanged.

Validation: source compilation and the disposable Unity training-sector campaign checks, including 50 mission setups and indoor document/relay actions. Review images and report are saved in `output/interior-art/`. This is an incremental art pass; Android performance, multiple geographic sectors, full mission playthroughs and final lighting still require review before calling the result AAA quality.
