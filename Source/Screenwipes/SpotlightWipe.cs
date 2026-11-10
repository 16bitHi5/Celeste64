
namespace Celeste64;

public class SpotlightWipe : ScreenWipe
{
	public Vec2? FocusPoint;
	public float Modifier = 0;
	public bool Linear = false;
	private const float SmallCircleRadius = 96 * Game.RelativeScale;
	private const float EaseDuration = 1.2f;
	private const float EaseOpenPercent = 0.3f; // how long (in percent) it eases the small circle open
	private const float EaseClosePercent = 0.3f; // how long (in percent) it eases the entire screen
	// ex. if 0.2 and 0.3, it would open for 0.2, wait until 0.7, then open for the remaining 0.3

	public SpotlightWipe() : base(EaseDuration) {}

    public override void Start()
    {
		if (IsFromBlack)
			Audio.Play(Sfx.ui_spotlight_in);
		else
			Audio.Play(Sfx.ui_spotlight_out);
    }

    public override void Step(float percent)
	{

	}

	public override void Render(Batcher batch, Rect bounds)
	{
		if ((Percent <= 0 && IsFromBlack) || (Percent >= 1 && !IsFromBlack))
		{
			batch.Rect(bounds, Color.Black);
			return;
		}

		var ease = IsFromBlack ? Percent : 1 - Percent;
		var point = FocusPoint ?? bounds.Center;

		// get the radius
		var radius = 0f;
		var openRadius = SmallCircleRadius + Modifier;

		if (!Linear)
		{
			if (ease < EaseOpenPercent)
				radius = Ease.Cube.InOut(ease / EaseOpenPercent) * openRadius;
			else if (ease < 1f - EaseClosePercent)
				radius = openRadius;
			else
				radius = openRadius + ((ease - (1 - EaseClosePercent)) / EaseClosePercent) * (bounds.Width - openRadius);
		}
		else
			radius = Ease.Cube.InOut(ease) * bounds.Width;

		DrawSpotlight(batch, point, radius);
	}

	// Project one head-locked circle in front of the viewer into each eye.
	public void RenderVr(Batcher batch, Rect bounds, Camera eye, Vec3 center, Vec3 right, Vec3 up)
	{
		float openness = IsFromBlack ? Percent : 1 - Percent;
		if (openness <= 0)
		{
			batch.Rect(bounds, Color.Black);
			return;
		}
		if (openness >= 1) return;
		const float small = 2.4f;
		const float outer = 100f;
		float radius = Linear ? Ease.Cube.InOut(openness) * outer :
			openness < EaseOpenPercent ? Ease.Cube.InOut(openness / EaseOpenPercent) * small :
			openness < 1 - EaseClosePercent ? small :
			small + ((openness - (1 - EaseClosePercent)) / EaseClosePercent) * (outer - small);
		Vec2 Project(Vec3 point)
		{
			var clip = Vec4.Transform(new Vec4(point, 1), eye.ViewProjection);
			return new Vec2(bounds.X + (clip.X / clip.W + 1) * bounds.Width * .5f,
				bounds.Y + (1 - clip.Y / clip.W) * bounds.Height * .5f);
		}
		const int segments = 64;
		for (int i = 0; i < segments; i++)
		{
			var a = Calc.AngleToVector(i * MathF.Tau / segments, 1);
			var b = Calc.AngleToVector((i + 1) * MathF.Tau / segments, 1);
			Vec2 Point(Vec2 angle, float distance) => Project(center + (right * angle.X + up * angle.Y) * distance);
			var outerA = Point(a, outer);
			var innerA = Point(a, radius);
			var innerB = Point(b, radius);
			var outerB = Point(b, outer);
			batch.Triangle(outerA, innerA, innerB, Color.Black);
			batch.Triangle(outerA, outerB, innerB, Color.Black);
		}
	}

	public static void DrawSpotlight(Batcher batch, Vec2 position, float radius)
	{
		var lastAngle = new Vec2(1, 0);
		var steps = 256;

		for (int i = 0; i < steps; i += 12)
		{
			var nextAngle = Calc.AngleToVector(((i + 12f) / steps) * MathF.Tau, 1f);

			// main circle
			{
				batch.Triangle(
					position + lastAngle * 5000,
					position + lastAngle * radius,
					position + nextAngle * radius,
					Color.Black);

				batch.Triangle(
					position + lastAngle * 5000,
					position + nextAngle * 5000,
					position + nextAngle * radius,
					Color.Black);
			}

			lastAngle = nextAngle;
		}
	}
}
