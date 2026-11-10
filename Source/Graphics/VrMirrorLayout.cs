namespace Celeste64;

/// <summary>Desktop-only framing; never changes the headset render.</summary>
public static class VrMirrorLayout
{
	public static RectInt Viewport(int width, int height)
	{
		int w = Math.Min(width, (int)(height * (16f / 9f)));
		int h = Math.Min(height, (int)(w * (9f / 16f)));
		return new RectInt((width - w) / 2, (height - h) / 2, w, h);
	}

	public static float Zoom(float current, float wheel) =>
		Math.Clamp(current * MathF.Pow(1.15f, wheel), 1f, 3f);

	public static float FillScale(int sourceWidth, int sourceHeight, RectInt viewport, float zoom) =>
		Math.Max(viewport.Width / (float)sourceWidth, viewport.Height / (float)sourceHeight) * zoom;
}
