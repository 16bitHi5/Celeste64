using Celeste64;
using System.Numerics;

static void Near(Vector3 actual, Vector3 expected, string label)
{
    if (Vector3.Distance(actual, expected) > 0.0001f)
        throw new Exception($"{label}: {actual} != {expected}");
}

var anchor = new Camera { Position = new(10, 20, 30), LookAt = new(10, 21, 30) };
Camera Eye(Vector3 p, Quaternion q) => VrCamera.ForEye(anchor, p, q, new(0, 1.6f, 0), 10,
    -0.7f, 0.7f, -0.7f, 0.7f, 1, 800);

var left = Eye(new(-.032f, 1.6f, 0), Quaternion.Identity);
var right = Eye(new(.032f, 1.6f, 0), Quaternion.Identity);
Near(left.Position, new(9.68f, 20, 30), "left eye");
Near(right.Position, new(10.32f, 20, 30), "right eye");
Near(left.Normal, Vector3.UnitY, "forward");
Near(left.ViewUp, Vector3.UnitZ, "up");
Near(Eye(new(0, 1.7f, -.2f), Quaternion.Identity).Position, new(10, 22, 31), "head movement");
Near(Eye(new(0, 1.6f, 0), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2)).ViewUp,
    -Vector3.UnitX, "head roll");
if (left.ProjectionOverride is null || left.Projection == Matrix4x4.Identity)
    throw new Exception("Eye projection was not applied.");

var vertical = new Camera { LookAt = Vector3.UnitZ };
var verticalEye = VrCamera.ForEye(vertical, Vector3.Zero, Quaternion.Identity, Vector3.Zero, 10,
    -.7f, .7f, -.7f, .7f, 1, 800);
if (!float.IsFinite(verticalEye.ViewUp.X))
    throw new Exception("Vertical anchor produced an invalid eye camera.");

Console.WriteLine("VR eye offsets, movement, roll, projection, and vertical camera checks passed.");

if (!VrPointerMath.Intersect(Vector3.Zero, Quaternion.Identity, new(0, 0, -2), Quaternion.Identity, out var center) || Vector2.Distance(center, new(.5f, .5f)) > .0001f)
    throw new Exception("Pointer should hit the panel centre.");
if (VrPointerMath.Intersect(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI), new(0, 0, -2), Quaternion.Identity, out _))
    throw new Exception("Pointer facing away from the panel must not hit it.");
if (VrPointerMath.Intersect(new(2, 0, 0), Quaternion.Identity, new(0, 0, -2), Quaternion.Identity, out _))
    throw new Exception("Pointer outside panel bounds must not hit it.");
Console.WriteLine("VR pointer intersection checks passed.");

static void CheckListener(Camera camera, string label)
{
    if (!ListenerPose.TryFromCamera(camera, out _, out var forward, out var up) ||
        !float.IsFinite(forward.X) || !float.IsFinite(up.X) ||
        MathF.Abs(forward.Length() - 1) > .0001f || MathF.Abs(up.Length() - 1) > .0001f ||
        MathF.Abs(Vector3.Dot(forward, up)) > .0001f)
        throw new Exception($"{label} produced an invalid listener orientation.");
}
CheckListener(new Camera { Position = Vector3.Zero, LookAt = Vector3.Zero }, "coincident camera and target");
CheckListener(new Camera { Position = Vector3.Zero, LookAt = Vector3.UnitZ }, "vertical camera");
CheckListener(new Camera { Position = new(100, 40, 30), LookAt = new(100.00001f, 40, 30) }, "near-coincident camera");
if (ListenerPose.TryFromCamera(new Camera { Position = new(float.NaN, 0, 0) }, out _, out _, out _))
    throw new Exception("A nonfinite listener position must be rejected.");
var coincidentEye = VrCamera.ForEye(new Camera { Position = Vector3.One, LookAt = Vector3.One },
    Vector3.Zero, Quaternion.Identity, Vector3.Zero, 10, -.7f, .7f, -.7f, .7f, 1, 800);
CheckListener(coincidentEye, "VR eye with coincident anchor");
Console.WriteLine("Degenerate camera and audio listener checks passed.");

if (VrGraphics.RenderSize(false, 2528, 2704) != (1024, 1024) ||
    VrGraphics.RenderSize(true, 2528, 2704) != (1644, 1758) ||
    VrGraphics.RenderSize(true, 10000, 10000) != (2048, 2048) ||
    VrGraphics.RenderSize(true, 0, 0) != (1024, 1024))
    throw new Exception("VR quality render sizes were not selected correctly.");
Console.WriteLine("VR quality selection checks passed.");

var thirdPersonAnchor = new Camera { Position = new(0, -20, 30), LookAt = new(0, 0, 10) };
var firstPersonAnchor = VrCamera.FirstPersonAnchor(thirdPersonAnchor, new(5, 7, 3), -Vector3.UnitX);
Near(firstPersonAnchor.Position, new(5, 7, 13), "first-person eye height");
Near(firstPersonAnchor.Forward, Vector3.UnitY, "first-person horizontal heading");
var firstPersonEye = VrCamera.ForEye(firstPersonAnchor, new(.032f, 1.6f, 0), Quaternion.Identity,
    new(0, 1.6f, 0), 10, -.7f, .7f, -.7f, .7f, 1, 800);
Near(firstPersonEye.Position, new(5.32f, 7, 13), "first-person right-eye offset");
var handTransform = VrCamera.TrackedPose(firstPersonAnchor, new(.25f, 1.4f, -.5f), Quaternion.Identity,
    new(0, 1.6f, 0), 10);
Near(Vector3.Transform(Vector3.Zero, handTransform), new(7.5f, 12, 11), "tracked hand position");
var verticalAnchor = VrCamera.FirstPersonAnchor(new Camera { LookAt = Vector3.UnitZ }, Vector3.Zero, -Vector3.UnitX);
Near(verticalAnchor.Forward, -Vector3.UnitX, "first-person fallback heading");
Console.WriteLine("First-person anchor and eye-offset checks passed.");

var searchDirectory = new DirectoryInfo(AppContext.BaseDirectory);
while (searchDirectory != null && !File.Exists(Path.Combine(searchDirectory.FullName, "Content", "Models", "player.glb")))
    searchDirectory = searchDirectory.Parent;
if (searchDirectory == null) throw new Exception("Could not find the player model for hand extraction checks.");
var playerModel = SharpGLTF.Schema2.ModelRoot.Load(Path.Combine(searchDirectory.FullName, "Content", "Models", "player.glb"));
var hands = VrHandGeometry.Extract(playerModel);
if (hands.Left.Indices.Length != 132 || hands.Right.Indices.Length != 132 ||
    hands.Left.Indices.Any(i => i < 0 || i >= hands.Left.Vertices.Length) ||
    hands.Right.Indices.Any(i => i < 0 || i >= hands.Right.Vertices.Length))
    throw new Exception("Original player hand triangles were not separated correctly.");
Console.WriteLine("Original left and right hand mesh extraction checks passed.");

if (args.Contains("--audio-integration"))
{
    Audio.Init();
    try
    {
        var normalListener = new Camera { Position = Vector3.Zero, LookAt = Vector3.UnitY };
        var coincidentListener = new Camera { Position = Vector3.One, LookAt = Vector3.One };
        var verticalListener = new Camera { Position = Vector3.Zero, LookAt = Vector3.UnitZ };
        var invalidListener = new Camera { Position = new(float.NaN, 0, 0) };
        Audio.SetListener(normalListener);
        Audio.SetListener(coincidentListener);
        Audio.SetListener(verticalListener);
        Audio.SetListener(invalidListener);
        Audio.SetListener(normalListener);
        Console.WriteLine("FMOD accepted repaired listener poses and skipped an invalid position.");
    }
    finally { Audio.Shutdown(); }
}
