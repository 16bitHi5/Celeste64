using System.Numerics;

namespace Celeste64;

public static class VrPointerMath
{
    // Returns normalized top-left-origin coordinates on the 1.6m x 0.9m UI panel.
    public static bool Intersect(Vector3 origin, Quaternion aim, Vector3 panelPosition, Quaternion panelRotation, out Vector2 point)
    {
        point = default;
        var inverse = Quaternion.Inverse(panelRotation);
        var localOrigin = Vector3.Transform(origin - panelPosition, inverse);
        var localDirection = Vector3.Transform(Vector3.Transform(-Vector3.UnitZ, aim), inverse);
        if (localDirection.Z >= -0.0001f) return false;
        float distance = -localOrigin.Z / localDirection.Z;
        if (distance <= 0) return false;
        var hit = localOrigin + localDirection * distance;
        point = new(hit.X / 1.6f + .5f, .5f - hit.Y / .9f);
        return point.X >= 0 && point.X <= 1 && point.Y >= 0 && point.Y <= 1;
    }
}
