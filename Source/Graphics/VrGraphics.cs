namespace Celeste64;

public static class VrGraphics
{
	public static (int Width, int Height) RenderSize(bool clear, int recommendedWidth, int recommendedHeight, bool standalone = false)
	{
		if (!clear || recommendedWidth <= 0 || recommendedHeight <= 0) return (1024, 1024);
		float scale = standalone ? 1f : .65f;
		return (Math.Clamp((int)MathF.Ceiling(recommendedWidth * scale), 1024, 2048),
			Math.Clamp((int)MathF.Ceiling(recommendedHeight * scale), 1024, 2048));
	}
}
