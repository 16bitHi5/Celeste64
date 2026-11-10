using System.Numerics;

namespace Celeste64;

public static class VrRoomScale
{
	/// <summary>Map horizontal headset translation in metres to the player's world plane.</summary>
	public static bool TryMovement(Camera anchor, Vector3 head, Vector3 reference, float unitsPerMetre, out Vector3 movement)
	{
		movement = Vector3.Zero;
		float x = head.X - reference.X, z = head.Z - reference.Z;
		if (!float.IsFinite(x) || !float.IsFinite(z) || !float.IsFinite(unitsPerMetre) || unitsPerMetre <= 0 ||
			x * x + z * z > .5f * .5f) return false;
		var forward = new Vector3(anchor.Forward.X, anchor.Forward.Y, 0);
		if (!float.IsFinite(forward.LengthSquared()) || forward.LengthSquared() < .000001f) return false;
		forward = Vector3.Normalize(forward);
		var right = Vector3.Cross(forward, Vector3.UnitZ);
		movement = (right * x - forward * z) * unitsPerMetre;
		return true;
	}

	/// <summary>Recenter horizontally while preserving physical head height.</summary>
	public static Vector3 Recenter(Vector3 reference, Vector3 head) => new(head.X, reference.Y, head.Z);
}
