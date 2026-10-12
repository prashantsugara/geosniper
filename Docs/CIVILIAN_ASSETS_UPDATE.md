# Civilian asset update — 7 October 2026

Three realistic rigged Sketchfab models are now active: Carla and Eric by Renderpeople, and Doctor by Nasser. All original listings permit commercial use under CC BY 4.0 with attribution. Source details and modification records are in Store/SketchfabCivilianLicenses.json; in-game Civilian Credits provides clickable source and license links.

The game now selects male/female residents, story witnesses/VIPs, doctors, reporters and shopkeepers. Mina and Nia use Carla. Other witnesses use Eric unless the briefing identifies a doctor. Reporter and shopkeeper roles adapt these base characters using original camera/badge/apron accessories; they are not separate downloaded models. Civilians retain their existing patrol, flee, cower, rescue and escort behavior.

Texture atlases are capped at 1024px and repeated doctor atlases are shared. Models contain about 20k, 20.5k and 26k triangles respectively. Imported T/A poses are relaxed before the existing procedural gait animates them. FBX conversion preserves skinning; authored animation is not used. The stylized Casual Man candidate is retained outside Resources in Assets/ThirdParty/CivilianAlternates, without use in gameplay.

Validation: runtime and editor C# compilation passed. Isolated Unity checks passed for five roles, textured skinned meshes, leg recognition, grounded human scale, female witness selection and locomotion pose changes. Visual captures are in output/civilians. The first three campaign missions, navigation, nonblocking rank-up, ad protection and legacy magazine migration also passed after the civilian change. No Android APK/AAB was built, and phone frame rate, memory, thermal behavior and release retention remain unverified.
