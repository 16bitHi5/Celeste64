# Madeline VR model working copy

`Madeline-VR.blend` is a copy of the original Celeste 64 player source model.
`player.png` and `badeline.png` are accompanying source textures, and
`player-original.glb` is the original in-game export for reference. The original game files remain in
`Content/Models` and `Content/Models/Sources`.

The exported character has a `Hands` mesh and separate left/right arm bones;
the hands are currently combined into one skinned mesh. The experimental VR
renderer separates the existing mesh into left and right pieces and attaches
them to controller grip poses. The copied Blender file can be edited independently;
changes to it are not automatically imported into the game.

Celeste characters and content belong to Maddy Makes Games, Inc.; see the
project's main README for the original creators' non-commercial fan-use terms.
