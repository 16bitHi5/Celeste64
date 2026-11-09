namespace Celeste64;

/// <summary>Experimental tracked hands using the original player's hand polygons.</summary>
internal sealed class VrHandRenderer : IDisposable
{
	private readonly SimpleModel left;
	private readonly SimpleModel right;

	public VrHandRenderer()
	{
		var template = Assets.Models["player"];
		var geometry = VrHandGeometry.Extract(template.Root);
		left = Create(geometry.Left);
		right = Create(geometry.Right);

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

	public void Render(ref RenderState state, Camera anchor, Vec3 referenceHeadPosition, Vec3 headWorldPosition, XrBridge.Hands hands, World world)
	{
		var baseState = state;
		if (hands.LeftValid != 0) Draw(left, hands.Left);
		if (hands.RightValid != 0) Draw(right, hands.Right);
		void Draw(SimpleModel model, XrBridge.Pose pose)
		{
			var handState = baseState;
			handState.ModelMatrix = VrCamera.TrackedPose(anchor, pose.Position, pose.Rotation, referenceHeadPosition, 10);
			handState.ModelMatrix.Translation = world.ConstrainVrHand(headWorldPosition, handState.ModelMatrix.Translation);
			model.Render(ref handState);
		}
	}

	public void Dispose() { left.Mesh.Dispose(); right.Mesh.Dispose(); }
}
