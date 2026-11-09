namespace Celeste64;

/// <summary>Creates the finite orthonormal listener orientation required by FMOD.</summary>
public static class ListenerPose
{
	public static bool TryFromCamera(Camera camera, out Vec3 position, out Vec3 forward, out Vec3 up)
	{
		position = camera.Position;
		forward = camera.Forward;
		up = camera.Up;
		if (!Finite(position) || !Finite(forward) || !Finite(up)) return false;
		float forwardLength = forward.LengthSquared();
		if (!float.IsFinite(forwardLength) || forwardLength < 0.000001f) return false;
		forward /= MathF.Sqrt(forwardLength);
		up -= forward * Vec3.Dot(up, forward);
		float upLength = up.LengthSquared();
		if (!float.IsFinite(upLength) || upLength < 0.000001f) return false;
		up /= MathF.Sqrt(upLength);
		return true;
	}

	private static bool Finite(Vec3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}
