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

var mirrorViewport = VrMirrorLayout.Viewport(1920, 1200);
if (mirrorViewport.Width != 1920 || mirrorViewport.Height != 1080 || mirrorViewport.Y != 60 ||
    VrMirrorLayout.Viewport(1200, 1080).Height != 675 ||
    VrMirrorLayout.FillScale(1000, 1000, mirrorViewport, 1) != 1.92f ||
    VrMirrorLayout.Zoom(1, -1) != 1 || VrMirrorLayout.Zoom(3, 1) != 3 ||
    VrMirrorLayout.Zoom(1, 1) <= 1)
    throw new Exception("VR desktop mirror framing or zoom is incorrect.");
Console.WriteLine("VR desktop 16:9 framing and zoom checks passed.");

if (!VrPointerMath.Intersect(Vector3.Zero, Quaternion.Identity, new(0, 0, -2), Quaternion.Identity, out var center) || Vector2.Distance(center, new(.5f, .5f)) > .0001f)
    throw new Exception("Pointer should hit the panel centre.");
if (VrPointerMath.Intersect(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI), new(0, 0, -2), Quaternion.Identity, out _))
    throw new Exception("Pointer facing away from the panel must not hit it.");
if (VrPointerMath.Intersect(new(2, 0, 0), Quaternion.Identity, new(0, 0, -2), Quaternion.Identity, out _))
    throw new Exception("Pointer outside panel bounds must not hit it.");
Console.WriteLine("VR pointer intersection checks passed.");

if (!VrCardInteraction.TryIntersect(new Vector3(0, -10, 0), Vector3.UnitY, Matrix4x4.Identity, 80, 50, out var cardUv, out var cardDistance) ||
    Vector2.Distance(cardUv, new Vector2(.5f, .5f)) > .0001f || MathF.Abs(cardDistance - 10) > .0001f ||
    VrCardInteraction.TryIntersect(new Vector3(0, -10, 0), -Vector3.UnitY, Matrix4x4.Identity, 80, 50, out _, out _) ||
    VrCardInteraction.TryIntersect(new Vector3(100, -10, 0), Vector3.UnitY, Matrix4x4.Identity, 80, 50, out _, out _))
    throw new Exception("The controller ray must hit only the visible postcard bounds in front of it.");
var movedCard = Matrix4x4.CreateRotationZ(MathF.PI / 2) * Matrix4x4.CreateTranslation(5, 0, 0);
if (!VrCardInteraction.TryIntersect(new Vector3(15, 0, 0), -Vector3.UnitX, movedCard, 80, 50, out cardUv, out _) ||
    Vector2.Distance(cardUv, new Vector2(.5f, .5f)) > .0001f)
    throw new Exception("Pointer intersection should follow a moved and rotated postcard.");
var cardDrag = new VrCardDrag();
if (!cardDrag.Begin(2, -Vector3.UnitZ, Vector2.Zero) || cardDrag.End() != 2)
    throw new Exception("A short trigger tap should select the pointed postcard.");
cardDrag.Begin(1, -Vector3.UnitZ, Vector2.Zero);
var cardTilt = cardDrag.Update(Vector3.Normalize(new Vector3(.3f, .1f, -1)));
if (!cardDrag.Moved || cardTilt.LengthSquared() < .001f || cardDrag.End() != -1)
    throw new Exception("Moving the controller while held should tilt the card without selecting it.");
Console.WriteLine("VR postcard ray, tap, and drag checks passed.");

var controlsLayout = new VrControlsLayout();
var controlsCenter = new Vector2(320, 180);
if (controlsLayout.HitTest(new(100, 50), controlsCenter) != 0 ||
    controlsLayout.HitTest(new(320, 50), controlsCenter) != 1 ||
    controlsLayout.HitTest(new(530, 50), controlsCenter) != 2 ||
    controlsLayout.HitTest(new(180, 315), controlsCenter) != 3 ||
    controlsLayout.HitTest(new(450, 315), controlsCenter) != 4 ||
    controlsLayout.HitTest(new(590, 15), controlsCenter) != -2 ||
    controlsLayout.HitTest(new(320, 170), controlsCenter) != -1 ||
    controlsLayout.PointerSide(1, new(250, 50), controlsCenter) != -1 ||
    controlsLayout.PointerSide(1, new(390, 50), controlsCenter) != 1)
    throw new Exception("VR controls guide pointer targets and arrows must match the visible settings.");
Console.WriteLine("VR controls guide pointer layout checks passed.");

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

var roomAnchor = new Camera { Position = new(0, 0, 10), LookAt = new(0, 1, 10) };
var roomReference = new Vector3(0, 1.6f, 0);
var movedHead = new Vector3(.2f, 1.8f, -.1f);
if (!VrRoomScale.TryMovement(roomAnchor, movedHead, roomReference, 10, out var roomMove))
    throw new Exception("Ordinary headset movement should move the player.");
Near(roomMove, new Vector3(2, 1, 0), "room-scale player movement");
Near(VrRoomScale.Recenter(roomReference, movedHead), new Vector3(.2f, 1.6f, -.1f), "room-scale reference preserves height");
if (VrRoomScale.TryMovement(roomAnchor, new Vector3(1, 1.6f, 0), roomReference, 10, out _) ||
    VrRoomScale.TryMovement(roomAnchor, new Vector3(float.NaN, 1.6f, 0), roomReference, 10, out _))
    throw new Exception("Large tracking resets or invalid poses must not teleport the player.");
var turnedRoom = new Camera { Position = Vector3.Zero, LookAt = -Vector3.UnitX };
if (!VrRoomScale.TryMovement(turnedRoom, new Vector3(0, 1.6f, -.2f), roomReference, 10, out roomMove))
    throw new Exception("Head movement should work after turning.");
Near(roomMove, new Vector3(-2, 0, 0), "room-scale movement follows camera heading");
Console.WriteLine("VR room-scale body alignment checks passed.");

var collectionFirst = new Camera { Position = Vector3.Zero, LookAt = Vector3.UnitY };
var collectionThird = new Camera { Position = new(0, -20, 10), LookAt = new(0, 0, 5) };
Near(VrCamera.CollectionAnchor(collectionFirst, collectionThird, 0).Position, collectionFirst.Position, "collection start");
Near(VrCamera.CollectionAnchor(collectionFirst, collectionThird, 1).Position, collectionThird.Position, "collection end");
Near(VrCamera.CollectionAnchor(collectionFirst, collectionThird, .5f).Position, new(0, -10, 5), "collection midpoint");
var opposite = new Camera { Position = Vector3.One, LookAt = Vector3.One - Vector3.UnitY };
CheckListener(VrCamera.CollectionAnchor(collectionFirst, opposite, .5f), "opposite collection views");
var quarter = VrCamera.CollectionAnchor(collectionFirst, opposite, .25f).Forward;
var threeQuarter = VrCamera.CollectionAnchor(collectionFirst, opposite, .75f).Forward;
if (Vector3.Dot(quarter, threeQuarter) > .9f || MathF.Abs(quarter.X) < .01f || MathF.Abs(threeQuarter.X) < .01f)
    throw new Exception("Collection rotation should turn smoothly between opposite views.");
var headTurn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, .65f);
Camera TrackedEye(Camera c) => VrCamera.ForEye(c, Vector3.Zero, headTurn, Vector3.Zero, 10,
    -.7f, .7f, -.7f, .7f, 1, 800);
var gaze = TrackedEye(collectionFirst).Forward;
var frontView = VrCamera.CollectionView(collectionFirst, Vector3.Zero, gaze);
if (Vector3.Dot(Vector3.Normalize(new Vector3(frontView.Position.X, frontView.Position.Y, 0)),
    Vector3.Normalize(new Vector3(gaze.X, gaze.Y, 0))) < .999f ||
    Vector3.Dot(TrackedEye(frontView).Forward, Vector3.Normalize(new Vector3(0, 0, 8) - frontView.Position)) < .999f)
    throw new Exception("Collection camera should be in front of the tracked gaze and aim back at the player.");
Console.WriteLine("VR collection camera blending checks passed.");

var cameraStart = new Vector3(0, 0, 10);
var clearCollection = VrCamera.CollectionPosition(cameraStart, Vector3.Zero, Vector3.UnitY,
    (_, _, distance) => distance);
Near(clearCollection, new Vector3(0, 36, 8), "unobstructed collection camera");
var alternateCollection = VrCamera.CollectionPosition(cameraStart, Vector3.Zero, Vector3.UnitY,
    (_, direction, distance) => MathF.Abs(direction.X) < .1f && direction.Y > 0 ? 5 : distance);
if (MathF.Abs(alternateCollection.X) < 10 || Vector3.Distance(alternateCollection, cameraStart) < 30)
    throw new Exception("Collection camera should choose an open direction when the preferred path is blocked.");
var enclosedCollection = VrCamera.CollectionPosition(cameraStart, Vector3.Zero, Vector3.UnitY,
    (_, _, _) => 6);
if (Vector3.Distance(enclosedCollection, cameraStart) > 4.01f)
    throw new Exception("Collection camera must stop before nearby obstacles even when every path is blocked.");
var shiftedView = VrCamera.CollectionView(collectionFirst, Vector3.Zero, gaze, alternateCollection);
if (Vector3.Dot(TrackedEye(shiftedView).Forward, Vector3.Normalize(new Vector3(0, 0, 8) - shiftedView.Position)) < .999f)
    throw new Exception("An alternate collection view must still aim the tracked gaze at the player.");
Console.WriteLine("VR collection camera obstruction and alternate-view checks passed.");

var ledge = new Vector3(0, 0, 10);
if (!VrLedgeGeometry.HasFootSupport(ledge, Vector3.UnitZ, point => new Vector3(point.X, point.Y, 10)))
    throw new Exception("A broad flat ledge should support a mantle.");
if (VrLedgeGeometry.HasFootSupport(ledge, Vector3.UnitZ, point => MathF.Abs(point.X) < 1 ? new Vector3(point.X, point.Y, 10) : null))
    throw new Exception("A narrow lip should not support a mantle.");
if (VrLedgeGeometry.HasFootSupport(ledge, Vector3.UnitZ, point => new Vector3(point.X, point.Y, point.Y > 1 ? 7 : 10)))
    throw new Exception("A drop under the edge of the feet should not support a mantle.");
if (VrLedgeGeometry.HasFootSupport(ledge, Vector3.UnitZ, point => new Vector3(point.X, point.Y, float.NaN)))
    throw new Exception("An invalid floor height should not support a mantle.");
var slope = Vector3.Normalize(new Vector3(-1, 0, 1));
if (!VrLedgeGeometry.IsStandable(slope) || !VrLedgeGeometry.IsStandable(new Vector3(.866f, 0, .5f)) ||
    VrLedgeGeometry.IsStandable(Vector3.UnitX) || VrLedgeGeometry.IsStandable(-Vector3.UnitZ) ||
    VrLedgeGeometry.IsStandable(new Vector3(float.NaN, 0, 1)))
    throw new Exception("The supported ledge cone should include upward slopes, but reject walls and ceilings.");
if (!VrLedgeGeometry.HasFootSupport(ledge, slope, point => new Vector3(point.X, point.Y, 10 + point.X)) ||
    VrLedgeGeometry.HasFootSupport(ledge, slope, point => point.X > 1 ? null : new Vector3(point.X, point.Y, 10 + point.X)))
    throw new Exception("Slope support should follow the plane but reject a narrow edge.");
Console.WriteLine("VR ledge support footprint checks passed.");

if (!Utils.PlaneTriangleIntersection(new Plane(Vector3.UnitZ, 1),
    new Vector3(10, 10, 1), new Vector3(10, 9, 0), new Vector3(11, 10, 0), out var vertexA, out var vertexB))
    throw new Exception("A horizontal slice should touch the top triangle vertex.");
var vertexContact = Utils.ClosestPointOnSegment(new Vector2(vertexA.X, vertexA.Y), new Vector2(vertexB.X, vertexB.Y), new Vector2(10, 10));
if (!float.IsFinite(vertexContact.X) || !float.IsFinite(vertexContact.Y) || Vector2.Distance(vertexContact, new Vector2(10, 10)) > .001f)
    throw new Exception("A collision slice at a triangle tip must produce a finite contact point.");
if (Vector2.Distance(Utils.ClosestPointOnSegment(new(1, 2), new(1, 2), new(20, 30)), new(1, 2)) > .001f ||
    Vector2.Distance(Utils.ClosestPointOnSegment(new(1, 2), new(3, 2), new(2, 8)), new(2, 2)) > .001f)
    throw new Exception("Closest wall contact must handle both point and ordinary segments.");
Console.WriteLine("VR ledge-tip collision checks passed.");

if (!VrClimbCorner.ShouldSwitch(Vector3.UnitY, Vector3.UnitX, 1, 1, true) ||
    !VrClimbCorner.ShouldSwitch(Vector3.UnitY, -Vector3.UnitY, 1, 1, true))
    throw new Exception("A nearby perpendicular wall should allow a corner hand transfer.");
if (VrClimbCorner.ShouldSwitch(Vector3.UnitY, Vector3.UnitY, 1, 5, true) ||
    VrClimbCorner.ShouldSwitch(Vector3.UnitY, Vector3.UnitX, 4, 5, true))
    throw new Exception("Corner transfer accepted a same-face or distant wall.");
if (!VrClimbCorner.ShouldSwitch(Vector3.UnitY, Vector3.UnitX, 2, .5f, true) ||
    !VrClimbCorner.ShouldSwitch(Vector3.UnitY, Vector3.UnitX, .5f, 1.5f, false) ||
    VrClimbCorner.ShouldSwitch(Vector3.UnitY, Vector3.UnitX, 1, 1.2f, false))
    throw new Exception("A held hand should transfer only when the adjacent face is clearly closer.");
Console.WriteLine("VR climbing corner contact checks passed.");

if (VrFeatherDirection.Resolve(Vector2.Zero, new Vector3(2, 0, 8), -Vector2.UnitY) != Vector2.UnitX ||
    VrFeatherDirection.Resolve(-Vector2.UnitY, Vector3.UnitX, Vector2.UnitX) != -Vector2.UnitY ||
    VrFeatherDirection.Resolve(Vector2.Zero, Vector3.UnitZ, -Vector2.UnitY) != -Vector2.UnitY)
    throw new Exception("Feather steering should follow horizontal gaze, respect stick input, and handle a vertical gaze.");
Console.WriteLine("VR feather gaze and stick steering checks passed.");

var releaseGate = new VrGrabReleaseGate();
releaseGate.BlockHeld(true, true);
releaseGate.Update(true, true, true, true);
if (!releaseGate.LeftBlocked || !releaseGate.RightBlocked)
    throw new Exception("Held triggers must stay blocked after a wall jump.");
releaseGate.Update(true, false, false, false);
if (releaseGate.LeftBlocked || !releaseGate.RightBlocked)
    throw new Exception("Releasing one trigger should unblock it independently; tracking loss is not a release.");
releaseGate.Update(true, true, true, false);
if (releaseGate.LeftBlocked || releaseGate.RightBlocked)
    throw new Exception("Each trigger should be available after its own release.");
releaseGate.BlockHeld(false, true);
releaseGate.Reset();
if (releaseGate.LeftBlocked || releaseGate.RightBlocked)
    throw new Exception("Leaving first-person VR should clear the grab gate.");
Console.WriteLine("VR wall-jump grab release checks passed.");

using (var savedSettings = new MemoryStream())
{
    var settings = new Save { VrFirstPerson = true, VrFirstPersonStickClimb = true };
    Save.Serialize(savedSettings, settings);
    var restored = Save.Deserialize(System.Text.Encoding.UTF8.GetString(savedSettings.ToArray()));
    if (restored is null || !restored.VrFirstPerson || !restored.VrFirstPersonStickClimb)
        throw new Exception("First-person stick climbing must survive saving and loading.");
    var oldSave = Save.Deserialize("{\"VrFirstPerson\":true}");
    if (oldSave is null || oldSave.VrFirstPersonStickClimb)
        throw new Exception("Older saves should retain hand climbing by default.");
}
Console.WriteLine("VR climbing mode save compatibility checks passed.");

var searchDirectory = new DirectoryInfo(AppContext.BaseDirectory);
while (searchDirectory != null && !File.Exists(Path.Combine(searchDirectory.FullName, "Content", "Models", "player.glb")))
    searchDirectory = searchDirectory.Parent;
if (searchDirectory == null) throw new Exception("Could not find the player model for hand extraction checks.");
var playerModel = SharpGLTF.Schema2.ModelRoot.Load(Path.Combine(searchDirectory.FullName, "Content", "Models", "player.glb"));
var vrLogoPath = Path.Combine(searchDirectory.FullName, "Content", "Models", "logo_vr.glb");
var vrLogo = SharpGLTF.Schema2.ModelRoot.Load(vrLogoPath);
if (vrLogo.DefaultScene == null || !vrLogo.LogicalNodes.Any(n => n.Name == "Cube.003" && n.Mesh != null) ||
    vrLogo.LogicalImages.Count == 0 || vrLogo.LogicalImages.Any(i => i.Content.Content.IsEmpty) ||
    vrLogo.LogicalMeshes.SelectMany(m => m.Primitives).Any(p => p.GetVertexAccessor("POSITION") == null ||
        p.GetVertexAccessor("NORMAL") == null || p.GetVertexAccessor("TEXCOORD_0") == null))
    throw new Exception("VR title logo must include the new mesh, embedded texture, normals, and UVs.");
var logoTemplate = new SkinnedTemplate(vrLogo);
if (logoTemplate.Vertices.Count == 0 || logoTemplate.Indices.Count == 0)
    throw new Exception("The game must be able to load the exported VR title logo.");
Console.WriteLine("VR title logo model and embedded texture checks passed.");
var hands = VrHandGeometry.Extract(playerModel);
if (hands.Left.Indices.Length != 132 || hands.Right.Indices.Length != 132 ||
    hands.Left.Indices.Any(i => i < 0 || i >= hands.Left.Vertices.Length) ||
    hands.Right.Indices.Any(i => i < 0 || i >= hands.Right.Vertices.Length))
    throw new Exception("Original player hand triangles were not separated correctly.");
Console.WriteLine("Original left and right hand mesh extraction checks passed.");

var bracelet = VrBraceletGeometry.Create();
if (bracelet.Vertices.Length != 12 * 6 || bracelet.Indices.Length != 12 * 6 * 6 ||
    bracelet.Indices.Any(i => i < 0 || i >= bracelet.Vertices.Length) ||
    bracelet.Vertices.Any(v => !float.IsFinite(v.Pos.X) || !float.IsFinite(v.Pos.Y) || !float.IsFinite(v.Pos.Z) ||
        MathF.Abs(v.Normal.Length() - 1) > .001f || new Vector2(v.Pos.X, v.Pos.Z).Length() < .34f ||
        v.Pos.Y < .28f || v.Pos.Y > .56f))
    throw new Exception("Wrist bracelet geometry must form a valid ring with a clear center.");
Console.WriteLine("VR wrist bracelet geometry checks passed.");

var climber = new VrHandClimb();
var idle = new VrHandClimb.Hand(Vector3.Zero, true, false, false);
var leftGrab = new VrHandClimb.Hand(new(0, 1, 0), true, true, true);
Near(climber.Update(leftGrab, idle), Vector3.Zero, "initial grab");
if (!climber.IsGrabbing) throw new Exception("A hand touching a wall should grab it.");
var leftPull = leftGrab with { Position = new(0, .9f, 0), TouchingWall = false };
Near(climber.Update(leftPull, idle), new(0, .1f, 0), "left hand pull");
var rightGrab = new VrHandClimb.Hand(new(.2f, 1, 0), true, true, true);
climber.Update(leftPull, rightGrab);
var rightPull = rightGrab with { Position = new(.2f, .9f, 0), TouchingWall = false };
Near(climber.Update(idle, rightPull), new(0, .1f, 0), "switch to right hand");
for (int i = 0; i < 10000; i++) climber.Update(idle, rightPull);
if (!climber.IsGrabbing) throw new Exception("A held hand must not expire with time.");
Near(climber.Update(idle, rightPull with { Position = new(.2f, 5, 0) }), Vector3.Zero, "tracking jump protection");
climber.Update(idle, idle);
if (climber.IsGrabbing) throw new Exception("Both released hands must let go.");
climber.Update(idle, rightGrab with { TouchingWall = false });
if (climber.IsGrabbing) throw new Exception("A hand away from a wall must not grab.");
Console.WriteLine("Independent hand grabs, pulls, switching, unlimited hold, and tracking checks passed.");

var bridgeType = typeof(Game).Assembly.GetType("Celeste64.XrBridge")!;
var nativeHandsType = bridgeType.GetNestedType("Hands", System.Reflection.BindingFlags.NonPublic)!;
if (System.Runtime.InteropServices.Marshal.SizeOf(nativeHandsType) != 72)
    throw new Exception("The managed tracked-hands layout does not match the native bridge.");
Console.WriteLine("Tracked-hands native layout check passed.");

var momentum = new VrClimbMomentum();
for (int i = 0; i < 20; i++) momentum.Sample(new Vector3(0, .1f, .2f), .02f);
var fling = momentum.Release();
if (fling.Y <= 0 || fling.Z <= 0 || fling.Z > 130 || new Vector2(fling.X, fling.Y).Length() > 110.001f)
    throw new Exception("A hand pull must produce bounded momentum in the pull direction.");
for (int i = 0; i < 100; i++) momentum.Sample(Vector3.Zero, .02f);
if (momentum.Release().Length() > .01f) throw new Exception("Momentum should decay when the hands stop moving.");
momentum.Sample(new Vector3(float.NaN, 0, 0), .02f);
if (momentum.Release() != Vector3.Zero) throw new Exception("Invalid tracking samples must clear momentum.");
Console.WriteLine("Hand pull momentum, limits, and stopping checks passed.");

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
