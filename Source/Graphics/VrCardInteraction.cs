using System.Numerics;

namespace Celeste64;

public static class VrCardInteraction
{
	/// <summary>Intersect a world-space controller ray with a transformed postcard.</summary>
	public static bool TryIntersect(Vector3 origin, Vector3 direction, Matrix4x4 cardTransform,
		float width, float height, out Vector2 uv, out float distance)
	{
		uv = default;
		distance = 0;
		if (width <= 0 || height <= 0 || !Matrix4x4.Invert(cardTransform, out var inverse)) return false;
		var localOrigin = Vector3.Transform(origin, inverse);
		var localDirection = Vector3.TransformNormal(direction, inverse);
		if (!float.IsFinite(localDirection.Y) || MathF.Abs(localDirection.Y) < .00001f) return false;
		distance = -localOrigin.Y / localDirection.Y;
		if (!float.IsFinite(distance) || distance <= 0) return false;
		var hit = localOrigin + localDirection * distance;
		uv = new(hit.X / width + .5f, .5f - hit.Z / height);
		return float.IsFinite(uv.X) && float.IsFinite(uv.Y) && uv.X >= 0 && uv.X <= 1 && uv.Y >= 0 && uv.Y <= 1;
	}
}

/// <summary>Distinguish a short pointer click from a deliberate card tilt.</summary>
public sealed class VrCardDrag
{
	private float startYaw, startPitch;
	private Vector2 startWobble;
	public bool Active { get; private set; }
	public bool Moved { get; private set; }
	public int CardIndex { get; private set; } = -1;

	public bool Begin(int cardIndex, Vector3 direction, Vector2 wobble)
	{
		if (cardIndex < 0 || !Angles(direction, out startYaw, out startPitch)) return false;
		CardIndex = cardIndex;
		startWobble = wobble;
		Moved = false;
		Active = true;
		return true;
	}

	public Vector2 Update(Vector3 direction)
	{
		if (!Active || !Angles(direction, out var yaw, out var pitch)) return startWobble;
		float dx = MathF.IEEERemainder(yaw - startYaw, MathF.Tau);
		float dy = pitch - startPitch;
		if (dx * dx + dy * dy > .05f * .05f) Moved = true;
		return new Vector2(Math.Clamp(startWobble.X + dx * 1.5f, -1, 1),
			Math.Clamp(startWobble.Y - dy * 1.5f, -1, 1));
	}

	public int End()
	{
		int tappedCard = Active && !Moved ? CardIndex : -1;
		Cancel();
		return tappedCard;
	}

	public void Cancel() { Active = false; CardIndex = -1; }

	private static bool Angles(Vector3 direction, out float yaw, out float pitch)
	{
		yaw = pitch = 0;
		if (!float.IsFinite(direction.X) || !float.IsFinite(direction.Y) || !float.IsFinite(direction.Z) || direction.LengthSquared() < .0001f) return false;
		direction = Vector3.Normalize(direction);
		yaw = MathF.Atan2(direction.X, -direction.Z);
		pitch = MathF.Asin(Math.Clamp(direction.Y, -1, 1));
		return true;
	}
}
