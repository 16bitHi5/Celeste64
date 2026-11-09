using System.Numerics;

namespace Celeste64;

/// <summary>
/// Converts an OpenXR eye pose into the game's Z-up camera coordinates.
/// OpenXR poses and the reference position are in metres in the same space.
/// </summary>
public static class VrCamera
{
	/// <summary>Maps an OpenXR grip pose to the same world space used by the eyes.</summary>
	public static Matrix4x4 TrackedPose(Camera anchor, Vector3 position, Quaternion orientation, Vector3 referenceHeadPosition, float unitsPerMetre)
	{
		var forward = anchor.Forward;
		var right = Vector3.Cross(forward, Vector3.UnitZ);
		if (right.LengthSquared() < 0.000001f) right = Vector3.Cross(forward, Vector3.UnitY);
		right = Vector3.Normalize(right);
		var up = Vector3.Normalize(Vector3.Cross(right, forward));
		var basis = new Matrix4x4(right.X, right.Y, right.Z, 0, up.X, up.Y, up.Z, 0,
			-forward.X, -forward.Y, -forward.Z, 0, 0, 0, 0, 1);
		var worldPosition = anchor.Position + Vector3.TransformNormal(position - referenceHeadPosition, basis) * unitsPerMetre;
		return Matrix4x4.CreateFromQuaternion(orientation) * basis * Matrix4x4.CreateTranslation(worldPosition);
	}

	/// <summary>Creates an eye-level anchor while retaining the player's chosen camera heading.</summary>
	public static Camera FirstPersonAnchor(Camera camera, Vector3 playerPosition, Vector3 playerForward)
	{
		var heading = new Vector3(camera.Forward.X, camera.Forward.Y, 0);
		if (heading.LengthSquared() < 0.000001f) heading = new Vector3(playerForward.X, playerForward.Y, 0);
		if (heading.LengthSquared() < 0.000001f) heading = Vector3.UnitY;
		heading = Vector3.Normalize(heading);
		camera.Position = playerPosition + Vector3.UnitZ * 10;
		camera.LookAt = camera.Position + heading;
		camera.ViewUp = Vector3.UnitZ;
		camera.ProjectionOverride = null;
		return camera;
	}

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
