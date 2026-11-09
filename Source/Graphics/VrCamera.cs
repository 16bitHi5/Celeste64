using System.Numerics;

namespace Celeste64;

/// <summary>
/// Converts an OpenXR eye pose into the game's Z-up camera coordinates.
/// OpenXR poses and the reference position are in metres in the same space.
/// </summary>
public static class VrCamera
{
	public static Camera ForEye(Camera anchor, Vector3 eyePosition, Quaternion eyeOrientation,
		Vector3 referenceHeadPosition, float unitsPerMetre,
		float angleLeft, float angleRight, float angleDown, float angleUp,
		float nearPlane, float farPlane)
	{
		if (unitsPerMetre <= 0 || nearPlane <= 0 || farPlane <= nearPlane)
			throw new ArgumentOutOfRangeException(nameof(unitsPerMetre), "Scale and clipping planes must be positive and ordered.");

		var forward = anchor.Forward;
		var right = Vector3.Cross(forward, Vector3.UnitZ);
		if (right.LengthSquared() < 0.000001f)
			right = Vector3.Cross(forward, Vector3.UnitY);
		right = Vector3.Normalize(right);
		var up = Vector3.Normalize(Vector3.Cross(right, forward));

		Vector3 ToWorld(Vector3 v) => right * v.X + up * v.Y - forward * v.Z;
		var eyeForward = ToWorld(Vector3.Transform(-Vector3.UnitZ, eyeOrientation));
		var eyeUp = ToWorld(Vector3.Transform(Vector3.UnitY, eyeOrientation));
		var position = anchor.Position + ToWorld(eyePosition - referenceHeadPosition) * unitsPerMetre;

		var left = MathF.Tan(angleLeft) * nearPlane;
		var rightEdge = MathF.Tan(angleRight) * nearPlane;
		var bottom = MathF.Tan(angleDown) * nearPlane;
		var top = MathF.Tan(angleUp) * nearPlane;
		if (!(left < rightEdge && bottom < top))
			throw new ArgumentException("Eye field of view is invalid.");

		anchor.Position = position;
		anchor.LookAt = position + eyeForward;
		anchor.ViewUp = eyeUp;
		anchor.NearPlane = nearPlane;
		anchor.FarPlane = farPlane;
		anchor.ProjectionOverride = Matrix4x4.CreatePerspectiveOffCenter(left, rightEdge, bottom, top, nearPlane, farPlane);
		return anchor;
	}
}
