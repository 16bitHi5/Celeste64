using System.Numerics;

namespace Celeste64;

/// <summary>Smoothed, bounded release velocity from physical hand pulls.</summary>
public sealed class VrClimbMomentum
{
	private Vector3 velocity;
	public void Reset() => velocity = Vector3.Zero;

	public void Sample(Vector3 displacement, float elapsedSeconds)
	{
		if (!float.IsFinite(elapsedSeconds) || elapsedSeconds <= 0 ||
			!float.IsFinite(displacement.X) || !float.IsFinite(displacement.Y) || !float.IsFinite(displacement.Z))
		{
			Reset();
			return;
		}
		// Hand movement is in game units. Gain provides a useful hop against the game's gravity.
		var target = displacement / elapsedSeconds * 5;
		var horizontal = new Vector2(target.X, target.Y);
		if (horizontal.LengthSquared() > 110 * 110) horizontal = Vector2.Normalize(horizontal) * 110;
		target = new Vector3(horizontal, Math.Clamp(target.Z, -80, 130));
		float blend = 1 - MathF.Exp(-elapsedSeconds / .06f);
		velocity = Vector3.Lerp(velocity, target, blend);
	}

	public Vector3 Release() => velocity;
}
