namespace Celeste64;

public static class VrGraphics
{
	public static (int Width, int Height) RenderSize(bool clear, int recommendedWidth, int recommendedHeight)
	{
		if (!clear || recommendedWidth <= 0 || recommendedHeight <= 0) return (1024, 1024);
		return (Math.Clamp((int)MathF.Ceiling(recommendedWidth * .65f), 1024, 2048),
			Math.Clamp((int)MathF.Ceiling(recommendedHeight * .65f), 1024, 2048));
	}
}
