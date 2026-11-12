namespace Celeste64;

/// <summary>Small tracked wrist displays, redrawn only when the saved counts change.</summary>
internal sealed class VrWristStats : IDisposable
{
	private readonly Target strawberries = new(Game.Instance.GraphicsDevice, 256, 128);
	private readonly Target deaths = new(Game.Instance.GraphicsDevice, 256, 128);
	private readonly Batcher batch = new(Game.Instance.GraphicsDevice);
	private readonly List<Sprite> sprites = new(2);
	private int previousStrawberries = -1, previousDeaths = -1;
	private SpriteFont? previousFont;

	public void BeginFrame()
	{
		sprites.Clear();
		var record = Save.CurrentRecord;
		var font = Language.Current.SpriteFont;
		bool fontChanged = previousFont != font;
		if (fontChanged || previousStrawberries != record.Strawberries.Count)
		{
			previousStrawberries = record.Strawberries.Count;
			Redraw(strawberries, "icon_strawberry", previousStrawberries, font);
		}
		if (fontChanged || previousDeaths != record.Deaths)
		{
			previousDeaths = record.Deaths;
			Redraw(deaths, "icon_skull", previousDeaths, font);
		}
		previousFont = font;
	}

	private void Redraw(Target target, string icon, int value, SpriteFont font)
	{
		target.Clear(Color.Transparent);
		batch.ImageFit(Assets.Subtextures[icon], new Rect(4, 20, 88, 88), new Vec2(.5f), Color.White, false, false);
		string label = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
		float scale = MathF.Min(76 / font.LineHeight, 150 / MathF.Max(font.WidthOf(label), 1));
		batch.PushMatrix(Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(98, 64));
		UI.Text(batch, label, Vec2.Zero, new Vec2(0, .5f), Color.White);
		batch.PopMatrix();
		batch.Render(target);
		batch.Clear();
	}

	public void Add(Matrix hand, Vec3 headPosition, bool left)
	{
		// Position a small, flat readable graphic at the bracelet center. Both
		// eyes use the same head position so the plane has consistent depth.
		var center = Vec3.Transform(new Vec3(0, .42f, 0), hand);
		var towardHead = headPosition - center;
		if (towardHead.LengthSquared() < .0001f) return;
		towardHead = Vec3.Normalize(towardHead);
		var right = Vec3.Cross(Vec3.UnitZ, towardHead);
		if (right.LengthSquared() < .0001f) right = Vec3.Cross(Vec3.UnitY, towardHead);
		right = Vec3.Normalize(right) * .31f;
		var up = Vec3.Normalize(Vec3.Cross(towardHead, right)) * .155f;
		center -= towardHead * .04f;
		sprites.Add(new Sprite {
			Subtexture = new Subtexture((left ? strawberries : deaths).Attachments[0]),
			A = center - right + up, B = center + right + up,
			C = center + right - up, D = center - right - up,
			Color = Color.White, Post = true
		});
	}

	public void Render(ref RenderState state, SpriteRenderer renderer) => renderer.Render(ref state, sprites, true);
	public void Dispose() { strawberries.Dispose(); deaths.Dispose(); batch.Dispose(); }
}
