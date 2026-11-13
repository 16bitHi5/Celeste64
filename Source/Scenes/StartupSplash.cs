using System.Diagnostics;

namespace Celeste64;

/// <summary>Displays the startup credits before the player can interact.</summary>
public sealed class StartupSplash : Scene
{
	private const double FadeSeconds = 0.2;
	private const double HoldSeconds = 1;
	private const double DisplaySeconds = FadeSeconds * 2 + HoldSeconds;
	private readonly Batcher batch;
	private long previousFrame;
	private double visibleSeconds;

	public StartupSplash() => batch = new(GraphicsDevice);

	public override void Update()
	{
		if (visibleSeconds >= DisplaySeconds && !Game.IsMidTransition)
		{
			Log.Info("Startup credits finished");
			Game.Goto(new Transition { Mode = Transition.Modes.Replace, Scene = () => new Titlescreen(), FromBlack = new AngledWipe() });
		}
	}

	public override void Render(Target target)
	{
		long now = Stopwatch.GetTimestamp();
		if (previousFrame == 0)
			Log.Info("Startup credits shown");
		else
			visibleSeconds += Math.Min(Stopwatch.GetElapsedTime(previousFrame, now).TotalSeconds, 0.1);
		previousFrame = now;

		float opacity = (float)Math.Clamp(Math.Min(visibleSeconds / FadeSeconds, (DisplaySeconds - visibleSeconds) / FadeSeconds), 0, 1);
		var color = Color.White * opacity;
		target.Clear(Color.Black);
		batch.PushSampler(new(TextureFilter.Linear, TextureWrap.Clamp, TextureWrap.Clamp));
		var fmod = Assets.Textures["Branding/FMOD"];
		float fmodScale = Math.Min(target.Width * .65f / fmod.Width, target.Height * .45f / fmod.Height);
		batch.Image(fmod, target.Bounds.Center, new Vec2(fmod.Width / 2f, fmod.Height / 2f), Vec2.One * fmodScale, 0, color);
		batch.PopSampler();
		batch.Render(target);
		batch.Clear();
	}

	public override void Disposed() => batch.Dispose();
}
