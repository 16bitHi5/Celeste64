using System.Diagnostics;

namespace Celeste64;

/// <summary>Displays the startup credits before the player can interact.</summary>
public sealed class StartupSplash : Scene
{
	private const double FadeSeconds = 1;
	private const double HoldSeconds = 2;
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
		float width = target.Width;
		float height = target.Height;
		var color = Color.White * opacity;
		target.Clear(Color.Black);
		batch.PushSampler(new(TextureFilter.Linear, TextureWrap.Clamp, TextureWrap.Clamp));
		var mark = Assets.Textures["Branding/Subpixel"];
		float markScale = Math.Min(width * .24f / mark.Width, height * .33f / mark.Height);
		batch.Image(mark, new Vec2(width * .5f, height * .24f), new Vec2(mark.Width / 2f, mark.Height / 2f), Vec2.One * markScale, 0, color);
		UI.Text(batch, "VR MOD BY MARK MATTA", new Vec2(width * .5f, height * .47f), new Vec2(.5f, .5f), color);
		UI.Text(batch, "BASED ON CELESTE 64", new Vec2(width * .5f, height * .58f), new Vec2(.5f, .5f), color);
		UI.Text(batch, "BY THE CELESTE TEAM", new Vec2(width * .5f, height * .65f), new Vec2(.5f, .5f), color);
		var fmod = Assets.Textures["Branding/FMOD"];
		float fmodScale = Math.Min(width * .30f / fmod.Width, height * .12f / fmod.Height);
		batch.Image(fmod, new Vec2(width * .5f, height * .82f), new Vec2(fmod.Width / 2f, fmod.Height / 2f), Vec2.One * fmodScale, 0, color);
		batch.PopSampler();
		batch.Render(target);
		batch.Clear();
	}

	public override void Disposed() => batch.Dispose();
}
