using System.Numerics;

namespace Celeste64;

/// <summary>Tracks independent wall grabs in OpenXR reference-space metres.</summary>
public sealed class VrHandClimb
{
	public readonly record struct Hand(Vector3 Position, bool Tracked, bool Held, bool TouchingWall);
	private bool leftGrabbed, rightGrabbed;
	private Vector3 lastLeft, lastRight;
	public bool IsGrabbing => leftGrabbed || rightGrabbed;
	public bool LeftGrabbed => leftGrabbed;
	public bool RightGrabbed => rightGrabbed;

	public void Reset() { leftGrabbed = rightGrabbed = false; }

	/// <summary>Returns the player's reference-space displacement in metres.</summary>
	public Vector3 Update(Hand left, Hand right)
	{
		Vector3 movement = Vector3.Zero;
		int count = 0;
		UpdateHand(left, ref leftGrabbed, ref lastLeft, ref movement, ref count);
		UpdateHand(right, ref rightGrabbed, ref lastRight, ref movement, ref count);
		return count == 0 ? Vector3.Zero : movement / count;
	}

	private static void UpdateHand(Hand hand, ref bool grabbed, ref Vector3 previous, ref Vector3 movement, ref int count)
	{
		if (!hand.Held || !hand.Tracked || !Finite(hand.Position)) { grabbed = false; return; }
		if (!grabbed)
		{
			if (!hand.TouchingWall) return;
			grabbed = true;
			previous = hand.Position;
		}
		var delta = previous - hand.Position;
		previous = hand.Position;
		// A sudden tracking reset should not teleport the player.
		if (delta.LengthSquared() > .35f * .35f) return;
		movement += delta;
		count++;
	}

	private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}
