# Visual target: custom Geo Sniper advert

## Primary target confirmed 10 October 2026

The user identified the five images on https://play.google.com/store/apps/details?id=com.geosniper.game as the desired game appearance. All five were inspected directly in the screenshot viewer. These take precedence over the advert when their art direction differs. Retain landscape gameplay; the store images' portrait framing is not an orientation requirement.

- Night city: solid weathered masonry, varied occupied windows, warm street pools, layered roofs and mechanical detail, dark reflective rifle metal, blue sky and distant atmospheric depth.
- Mission map: satellite/aerial imagery with a restrained cyan route, distinct insertion/target/extraction markers and readable dark briefing panels. The current vector map is not equivalent to this imagery.
- Duel: believable roof perches, textured parapets, warm windows against a cool dusk sky, detailed gloves and rifle, sparse combat controls.
- Results: a clean mission-complete panel, route summary, rewards and one prominent next-mission action over a city backdrop.
- Day city: warm low-angle sunlight, cool shadows, varied building heights and materials, roof equipment, trees and a city silhouette with depth.

The open-frame building repetition was the main structural mismatch. The revised pilot now uses solid façades, recessed occupied/unlit glazing and explicit retained firing/vault apertures, plus restrained shop signs and fine roof/weapon surface grain. Street, entrance and roof gameplay-component views were inspected in the controlled fixture and cached real sector. One bounded street is selected in the initial playable sector; streamed sectors retain their earlier architecture. The pilot remains below the store target in interiors, vehicles, skyline, overall lighting and map imagery. Increasing density, adding empty distant towers or recolouring the scene does not satisfy this direction. Neon remains a small accent; natural warm/cool lighting is the primary direction.

Satellite imagery requires a suitable imagery source and attribution; it is a separate implementation dependency, not a palette change. Preserve Android light/draw/texture budgets and validate on-device performance before claiming parity with the store imagery.

The victory aim reset was subsequently patched and checked in the street study; complete bullet-camera/result transitions still need broader regression validation. See STREET_STUDY.md for the inspected captures and measured limitations.

## Earlier advert direction

Reference: output/ads/geosniper_ad_custom.mp4. Landscape gameplay remains required.

The reference combines a dense blue night city, warm ground lighting, restrained cyan/magenta signs, layered rooftop silhouettes and a dark green reconnaissance interface. It is a visual direction, not proof of the runtime game's graphical quality or retention.

Implemented initial passes: dark vector map palette and readable light labels; darker park/special land-use colours; warm street lamps with some cooler LEDs; rooftop HVAC grille geometry batched into the existing architectural mesh. Clockwise footprints now receive the same rooftop detail as counterclockwise footprints.

Remaining quality gates: actual Android screenshots at day/night/rain; sustained device frame rate and loading time; complete campaign and stair traversal; firearm animation; art consistency at street and scope distance. The explicit victory aim/FOV reset passes the staged Play-mode check; full bullet-camera/result transitions still need broader regression coverage. Full satellite imagery and the reference's futuristic skyline have not been implemented. No claim of AAA parity or improved retention is established by this pass.

Runtime/editor compile passed. A separate Unity render checks the map raster; this does not validate the complete world on a phone.
