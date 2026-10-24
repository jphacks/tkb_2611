Cabin Field v001 for Unity

Copy this folder's FBX and Cabin_ColorAtlas_v001.png together into Unity Assets.
Iceberg_With_Cabin_v001.fbx: static field and placed cabin.
Cabin_Standalone_v001.fbx: standalone cabin, ground-centred pivot, door faces local -Y.
Cabin_Standalone_v001.glb: optional embedded-texture format; needs a glTF importer.

If Unity does not bind the color map automatically, create a material using
Cabin_ColorAtlas_v001.png as Base Map (or Standard Albedo) and assign it to
Cabin_Atlas_v001 on the imported model. UV0 is included in both FBX files.
For baked lighting, enable Generate Lightmap UVs in Model Import Settings.
Set a static Mesh Collider on WalkSurface_Snow_Continuous and a Box Collider
around the cabin as appropriate for your game.

See Penguin/Cabin_Field_v001_README.md for the full contents and validation.
