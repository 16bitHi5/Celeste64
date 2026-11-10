using System.Numerics;

namespace Celeste64;

public static class VrLedgeGeometry
{
	// Upward normals within 60 degrees of vertical: approximately 30–150
	// degrees when measured from a horizontal axis with straight up at 90.
	public static bool IsStandable(Vector3 normal) =>
		float.IsFinite(normal.X) && float.IsFinite(normal.Y) && float.IsFinite(normal.Z) &&
		normal.LengthSquared() > .0001f && normal.Z / MathF.Sqrt(normal.LengthSquared()) >= .5f;

	public static float PlaneHeight(Vector3 floor, Vector3 normal, Vector2 at) =>
		floor.Z - (normal.X * (at.X - floor.X) + normal.Y * (at.Y - floor.Y)) / normal.Z;

	/// <summary>Require floor underneath the center and perimeter of the player's feet.</summary>
	public static bool HasFootSupport(Vector3 floor, Vector3 normal, Func<Vector3, Vector3?> sampleFloor)
	{
		if (!IsStandable(normal)) return false;
		const float radius = 2.5f;
		const float diagonal = radius * .70710678f;
		ReadOnlySpan<Vector2> offsets = [Vector2.Zero, new(radius, 0), new(-radius, 0),
			new(0, radius), new(0, -radius), new(diagonal, diagonal), new(-diagonal, diagonal),
			new(diagonal, -diagonal), new(-diagonal, -diagonal)];
		foreach (var offset in offsets)
		{
			var xy = new Vector2(floor.X, floor.Y) + offset;
			float expected = PlaneHeight(floor, normal, xy);
			var sample = sampleFloor(new Vector3(xy, expected));
			if (sample is not { } hit || !float.IsFinite(hit.Z) || MathF.Abs(hit.Z - expected) > 1)
				return false;
		}
		return true;
	}
}
