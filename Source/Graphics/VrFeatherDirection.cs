using System.Numerics;

namespace Celeste64;

public static class VrFeatherDirection
{
	/// <summary>Stick input takes priority; otherwise look horizontally to steer.</summary>
	public static Vector2 Resolve(Vector2 stickDirection, Vector3 headForward, Vector2 fallback)
	{
		if (float.IsFinite(stickDirection.X) && float.IsFinite(stickDirection.Y) && stickDirection.LengthSquared() > .0001f)
			return Vector2.Normalize(stickDirection);
		var horizontal = new Vector2(headForward.X, headForward.Y);
		if (float.IsFinite(horizontal.X) && float.IsFinite(horizontal.Y) && horizontal.LengthSquared() > .0001f)
			return Vector2.Normalize(horizontal);
		return fallback;
	}
}
