namespace Celeste64;

/// <summary>Interactive control guide using the artist's transparent controller drawings.</summary>
public sealed class VrControlsLayout : Menu.ILayout
{
	public static bool ShowGamepad { get; set; }
	private static readonly Rect[] ItemBounds =
	[
		new(20, 31, 190, 43), new(225, 31, 190, 43), new(430, 31, 190, 43),
		new(75, 300, 230, 31), new(335, 300, 230, 31)
	];
	private static readonly Color Ink = new(0x262039);
	private static readonly Color Muted = new(0x625c70);
	private static readonly Color Accent = new(0x7955a8);
	private static readonly Color Selected = new(0xede2f9);

	public int HitTest(Vec2 point, Vec2 center)
	{
		var p = point - (center - new Vec2(320, 180));
		if (p.X >= 540 && p.X <= 615 && p.Y >= 6 && p.Y <= 29) return -2;
		for (int i = 0; i < ItemBounds.Length; i++)
		{
			var rect = ItemBounds[i];
			if (p.X >= rect.X && p.X <= rect.Right && p.Y >= rect.Y && p.Y <= rect.Bottom) return i;
		}
		return -1;
	}

	public int PointerSide(int index, Vec2 point, Vec2 center)
	{
		if (index < 0 || index >= ItemBounds.Length) return 1;
		float localX = point.X - (center.X - 320);
		return localX < ItemBounds[index].Center.X ? -1 : 1;
	}

	public void Render(Menu menu, Batcher batch, Vec2 center)
	{
		batch.PushMatrix(center - new Vec2(320, 180));
		batch.RectRounded(5, 5, 630, 350, 9, Color.White);
		Text(batch, "VR CONTROLS", new(320, 9), .82f, Ink, .5f);
		batch.RectRounded(540, 7, 72, 21, 5, new Color(0xf4f2f6));
		Text(batch, "B: BACK", new(576, 11), .60f, Muted, .5f);

		for (int i = 0; i < ItemBounds.Length; i++)
		{
			var rect = ItemBounds[i];
			bool selected = menu.Index == i && menu.Focused;
			batch.RectRounded(rect, 6, selected ? Selected : new Color(0xf4f2f6));
			if (selected) batch.RectRoundedLine(rect, 6, 1.5f, Accent);
			string label = menu.Items[i].Label;
			if (i < 3)
			{
				int separator = label.IndexOf(':');
				string name = separator < 0 ? label : label[..separator].Trim();
				string value = separator < 0 ? "" : label[(separator + 1)..].Trim();
				Text(batch, name, new(rect.Center.X, rect.Y + 4), .58f, Muted, .5f);
				Text(batch, $"<  {value}  >", new(rect.Center.X, rect.Y + 19), .72f, Ink, .5f);
			}
			else Text(batch, label, new(rect.Center.X, rect.Y + 9), .72f, Ink, .5f);
		}

		string textureName = ShowGamepad ? "vr_controls/Gamepad" : "vr_controls/Quest";
		if (Assets.Textures.TryGetValue(textureName, out var texture))
		{
			// The drawings contain empty space on the left. Crop only that empty
			// margin at draw time; the source PNGs remain untouched.
			var source = new Rect(400, 110, 830, 660);
			batch.PushSampler(new TextureSampler(TextureFilter.Linear, TextureWrap.Clamp, TextureWrap.Clamp));
			batch.Image(texture, source, new Vec2(175, 74), Vec2.Zero, new Vec2(.35f), 0, Color.White);
			batch.PopSampler();
		}

		if (ShowGamepad)
		{
			Legend(batch, 22, 94, false, ("LB / LT", "CLIMB"), ("LEFT STICK", "MOVE"), ("D-PAD", "MENUS"), ("VIEW", "PAUSE"));
			Legend(batch, 600, 94, true, ("RB / RT", "CLIMB"), ("RIGHT STICK", "CAMERA"), ("A / Y", "JUMP"), ("B / X", "DASH"), ("MENU", "PAUSE"));
		}
		else
		{
			string climb = Save.Instance.VrFirstPerson && !Save.Instance.VrFirstPersonStickClimb ? "HOLD + PULL" : "HOLD TO CLIMB";
			string movement = !Save.Instance.VrFirstPerson || Save.Instance.VrFirstPersonStickClimb ? "MOVE / CLIMB" : "MOVE / MENUS";
			Legend(batch, 22, 94, false, ("LEFT TRIGGER", climb), ("LEFT STICK", movement), ("X", "PAUSE"), ("Y", "SELECT"), ("LEFT GRIP", Save.Instance.VrGripDash ? "DASH" : "DASH OFF"));
			Legend(batch, 600, 94, true, ("RIGHT TRIGGER", "GRAB / SELECT"), ("RIGHT STICK", "TURN / CLICK PAUSE"), ("A", "JUMP / CONFIRM"), ("B", "DASH / BACK"), ("RIGHT GRIP", Save.Instance.VrGripDash ? "DASH" : "DASH OFF"));
		}
		Text(batch, Game.Instance.QuestControlsEnabled ?
			"LEFT STICK + A OR POINT + TRIGGER  /  BOTH CONTROLLERS WORK" :
			"QUEST DISABLED  /  USE GAMEPAD TO TURN IT BACK ON", new(320, 336), .51f, Muted, .5f);
		batch.PopMatrix();
	}

	private static void Legend(Batcher batch, float x, float y, bool right, params (string button, string action)[] entries)
	{
		foreach (var (button, action) in entries)
		{
			Text(batch, button, new(x, y), .65f, Accent, right ? 1 : 0);
			Text(batch, action, new(x, y + 13), .62f, Ink, right ? 1 : 0);
			y += 37;
		}
	}

	private static void Text(Batcher batch, string text, Vec2 position, float scale, Color color, float align)
	{
		batch.PushMatrix(Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(position));
		batch.Text(Language.Current.SpriteFont, text, Vec2.Zero, new Vec2(align, 0), color);
		batch.PopMatrix();
	}
}
