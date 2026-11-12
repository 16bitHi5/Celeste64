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

	/// <summary>Place the collection camera in front of the direction the player was looking.</summary>
	public static Camera CollectionView(Camera anchor, Vector3 playerPosition, Vector3 headForward, Vector3? cameraPosition = null)
	{
		var flat = new Vector3(headForward.X, headForward.Y, 0);
		if (!float.IsFinite(flat.LengthSquared()) || flat.LengthSquared() < .000001f)
			flat = new Vector3(anchor.Forward.X, anchor.Forward.Y, 0);
		if (flat.LengthSquared() < .000001f) flat = Vector3.UnitY;
		flat = Vector3.Normalize(flat);
		var position = cameraPosition ?? playerPosition + flat * 36 + Vector3.UnitZ * 8;
		var aim = Vector3.Normalize(playerPosition + Vector3.UnitZ * 8 - position);
		var baseForward = anchor.Forward;
		// The eye's tracked rotation is applied on top of the anchor. Compensate
		// for the current head orientation so the initial view actually aims at Madeline.
		float headYaw = MathF.Atan2(flat.Y, flat.X);
		float baseYaw = MathF.Atan2(baseForward.Y, baseForward.X);
		float yaw = MathF.Atan2(aim.Y, aim.X) - MathF.IEEERemainder(headYaw - baseYaw, MathF.Tau);
		float headPitch = MathF.Asin(Math.Clamp(headForward.Z, -1, 1));
		float basePitch = MathF.Asin(Math.Clamp(baseForward.Z, -1, 1));
		float pitch = Math.Clamp(MathF.Asin(Math.Clamp(aim.Z, -1, 1)) - (headPitch - basePitch), -1.4f, 1.4f);
		var direction = new Vector3(MathF.Cos(yaw) * MathF.Cos(pitch), MathF.Sin(yaw) * MathF.Cos(pitch), MathF.Sin(pitch));
		anchor.Position = position;
		anchor.LookAt = position + direction;
		anchor.ViewUp = Vector3.UnitZ;
		return anchor;
	}

	/// <summary>Choose a clear path for the celebration camera, preferring the direction the player looked.</summary>
	public static Vector3 CollectionPosition(Vector3 start, Vector3 playerPosition, Vector3 headForward,
		Func<Vector3, Vector3, float, float> clearDistance)
	{
		var heading = new Vector2(headForward.X, headForward.Y);
		if (!float.IsFinite(heading.LengthSquared()) || heading.LengthSquared() < .000001f)
			heading = Vector2.UnitY;
		heading = Vector2.Normalize(heading);
		float baseAngle = MathF.Atan2(heading.Y, heading.X);
		// Try nearby directions first, but choose the longest safe path if none
		// has enough room to frame the character comfortably.
		ReadOnlySpan<float> turns = [0, MathF.PI / 4, -MathF.PI / 4, MathF.PI / 2,
			-MathF.PI / 2, 3 * MathF.PI / 4, -3 * MathF.PI / 4, MathF.PI];
		var best = start;
		float bestDistance = -1;
		foreach (float turn in turns)
		{
			float angle = baseAngle + turn;
			var desired = playerPosition + new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0) * 36 + Vector3.UnitZ * 8;
			var delta = desired - start;
			float distance = delta.Length();
			if (distance < .001f) continue;
			var direction = delta / distance;
			var side = Vector3.Normalize(new Vector3(-direction.Y, direction.X, 0));
			float available = distance;
			// Sample around the camera as well as its center to leave space for
			// both eyes and the near plane during the entire transition.
			foreach (var offset in new[] { Vector3.Zero, side * 2, -side * 2, Vector3.UnitZ * 2, -Vector3.UnitZ * 2 })
				available = MathF.Min(available, Math.Clamp(clearDistance(start + offset, direction, distance), 0, distance));
			if (available < distance) available = MathF.Max(0, available - 2);
			if (available > bestDistance)
			{
				bestDistance = available;
				best = start + direction * available;
			}
			if (available >= 30) break;
		}
		return best;
	}

	/// <summary>Blend the VR anchor to a collection camera without changing eye separation.</summary>
	public static Camera CollectionAnchor(Camera firstPerson, Camera thirdPerson, float amount)
	{
		float t = Math.Clamp(amount, 0, 1);
		t = t * t * (3 - 2 * t);
		var position = Vector3.Lerp(firstPerson.Position, thirdPerson.Position, t);
		var from = firstPerson.Forward;
		var to = thirdPerson.Forward;
		float yawFrom = MathF.Atan2(from.Y, from.X);
		float yawTo = MathF.Atan2(to.Y, to.X);
		float yaw = yawFrom + MathF.IEEERemainder(yawTo - yawFrom, MathF.Tau) * t;
		float pitchFrom = MathF.Asin(Math.Clamp(from.Z, -1, 1));
		float pitchTo = MathF.Asin(Math.Clamp(to.Z, -1, 1));
		float pitch = pitchFrom + (pitchTo - pitchFrom) * t;
		var direction = new Vector3(MathF.Cos(yaw) * MathF.Cos(pitch), MathF.Sin(yaw) * MathF.Cos(pitch), MathF.Sin(pitch));
		firstPerson.Position = position;
		firstPerson.LookAt = position + direction;
		firstPerson.ViewUp = Vector3.UnitZ;
		return firstPerson;
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
