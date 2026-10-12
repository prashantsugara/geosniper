# Battlefield lobby

Original background generated with the built-in imagegen tool, then copied into `Assets/Resources/Textures/WarLobbyBackground.png`. The original generated output is retained under the Codex generated-images directory.

The image is rendered as one unlit camera-aligned backdrop behind the live 3D sniper. The plate covers the camera at different aspect ratios. The sniper has amber key lighting and cyan rim lighting, with a soft contact shadow. Dark navy translucent panels keep the existing three lobby actions readable. The old hangar and circular pedestal were removed. Texture import disables mipmaps and uses normal Unity texture compression, with a 2048-pixel maximum.

This is lobby environment art; the buildings and vehicles pictured are part of the backdrop, and do not change playable maps.

## Final generation prompt

Generate a polished original battlefield background plate for a mobile 3D sniper game's lobby, widescreen 16:9 composition, ideally 2048x1152. Cinematic realistic game environment at blue-hour sunset: a ruined modern city skyline, burnt-orange sunset breaking through cobalt-blue and purple storm clouds, smoke columns rising from distant buildings, small fires among rubble, an abandoned olive military jeep and concrete barricades on the far left, broken asphalt in the foreground reflecting amber firelight and cyan sky. Strong rich colorful atmosphere, believable detail, restrained explosions in the distant left skyline, a small distant helicopter silhouette in the upper left. The foreground middle-left at about 40% width must be empty, spacious, and relatively calm for a live 3D sniper character to stand there; low eye-level camera around 1.2m high. Leave the rightmost 35% darker, low-detail smoky blue for interface cards; leave the top edge darker for white title text. No people, no soldiers, no weapons in the foreground, no bodies, no gore, no text, no logos, no interface, no watermark. Atmospheric depth, warm amber and cool teal lighting, visually exciting without noisy clutter. This is environment art only, not a screenshot of a UI.

## Validation

C# compilation and isolated Unity previews at wide and compact sizes. Device GPU performance still requires an Android check.
