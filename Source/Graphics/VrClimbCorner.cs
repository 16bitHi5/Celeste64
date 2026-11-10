using System.Numerics;

namespace Celeste64;

/// <summary>Chooses when a hand contact should change the active climbing face.</summary>
public static class VrClimbCorner
{
	public static bool ShouldSwitch(Vector3 currentNormal, Vector3 handNormal, float handDistance, float currentDistance, bool newGrip)
	{
		if (!float.IsFinite(handDistance) || handDistance < 0 || handDistance > 3.5f ||
			float.IsNaN(currentDistance) || currentDistance < 0) return false;
		float turn = Vector3.Dot(currentNormal, handNormal);
		return float.IsFinite(turn) && turn < .98f && (newGrip || handDistance + .5f < currentDistance);
	}
}
