# Armory showcase images

Replaced the low-resolution 512 x 288 card source in the main preview with eight 2048 x 768 studio renders of the existing gameplay models. These use a slight three-quarter angle, warm key/cool fill/rim lighting and darker untextured metal finishes. Authored weapon surfaces retain their textures, with reduced normal-map strength and studio gloss so wood does not look metallic. Cards fit the complete render without the old crop.

The showcase textures have mipmaps disabled, a 2048 maximum import size and high-quality compression. The same assets serve the small browse cards. No additional live 3D camera or per-frame model rendering is needed on Android.

Reproduce using ArmoryShowcaseRenderer.Run in the disposable .utmp/review-unity project. Existing weapon-card assets remain fallback sources.
