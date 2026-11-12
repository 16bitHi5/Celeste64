namespace Celeste64;

/// <summary>Experimental tracked hands using the original player's hand polygons.</summary>
internal sealed class VrHandRenderer : IDisposable
{
	private readonly SimpleModel left;
	private readonly SimpleModel right;
	private readonly SimpleModel bracelet;
	private readonly VrLedgeIndicator ledgeIndicator;
	private readonly VrWristStats wristStats;
	private ulong ledgeFrame = ulong.MaxValue;
	private Player? ledgePlayer;
	private bool showLeftLedge, showRightLedge;
	private Vec3 leftLanding, leftNormal, rightLanding, rightNormal;

	public VrHandRenderer()
	{
		ledgeIndicator = new VrLedgeIndicator();
		wristStats = new VrWristStats();
		var template = Assets.Models["player"];
		var geometry = VrHandGeometry.Extract(template.Root);
		left = Create(geometry.Left);
		right = Create(geometry.Right);
		var braceletGeometry = VrBraceletGeometry.Create();
		bracelet = new SimpleModel { CullMode = CullMode.None };
		bracelet.Materials.Add(new DefaultMaterial(Assets.Textures["white"]) { Effects = .25f });
		bracelet.Mesh.SetVertices(braceletGeometry.Vertices);
		bracelet.Mesh.SetIndices(braceletGeometry.Indices);
		bracelet.Parts.Add(new SimpleModel.Part(0, 0, braceletGeometry.Indices.Length));

		SimpleModel Create(VrHandGeometry.Hand hand)
		{
			var result = new SimpleModel { CullMode = CullMode.None };
			result.Materials.Add(template.Materials[geometry.MaterialIndex].Clone());
			result.Mesh.SetVertices(hand.Vertices);
			result.Mesh.SetIndices(hand.Indices);
			result.Parts.Add(new SimpleModel.Part(0, 0, hand.Indices.Length));
			return result;
		}
	}

	public void Render(ref RenderState state, Camera anchor, Vec3 referenceHeadPosition, Vec3 headWorldPosition, XrBridge.Hands hands, World world, Player player, Color hairColor, SpriteRenderer spriteRenderer)
	{
		var baseState = state;
		// Both eyes use the same tracked poses and world state. Compute the
		// collision-heavy ledge checks once per XR frame and share the result.
		if (ledgeFrame != Game.Instance.FirstPersonHandFrame || ledgePlayer != player)
		{
			ledgeFrame = Game.Instance.FirstPersonHandFrame;
			ledgePlayer = player;
			showLeftLedge = player.TryGetVrLedgeIndicator(hands.Left, hands.LeftValid != 0, anchor, referenceHeadPosition, out leftLanding, out leftNormal);
			showRightLedge = player.TryGetVrLedgeIndicator(hands.Right, hands.RightValid != 0, anchor, referenceHeadPosition, out rightLanding, out rightNormal);
		}
		if (showLeftLedge) ledgeIndicator.Render(ref state, leftLanding, leftNormal);
		if (showRightLedge) ledgeIndicator.Render(ref state, rightLanding, rightNormal);
		wristStats.BeginFrame();
		bracelet.Materials[0].Color = hairColor;
		bracelet.Materials[0].SilhouetteColor = hairColor;
		if (hands.LeftValid != 0) Draw(left, hands.Left, true);
		if (hands.RightValid != 0) Draw(right, hands.Right, false);
		wristStats.Render(ref state, spriteRenderer);
		void Draw(SimpleModel model, XrBridge.Pose pose, bool isLeft)
		{
			var handState = baseState;
			handState.ModelMatrix = VrCamera.TrackedPose(anchor, pose.Position, pose.Rotation, referenceHeadPosition, 10);
			handState.ModelMatrix.Translation = world.ConstrainVrHand(headWorldPosition, handState.ModelMatrix.Translation);
			model.Render(ref handState);
			// Keep the status color legible in areas with strong vertical fog.
			handState.VerticalFogColor = hairColor;
			bracelet.Render(ref handState);
			wristStats.Add(handState.ModelMatrix, headWorldPosition, isLeft);
		}
	}

	public void Dispose() { left.Mesh.Dispose(); right.Mesh.Dispose(); bracelet.Mesh.Dispose(); ledgeIndicator.Dispose(); wristStats.Dispose(); }
}
