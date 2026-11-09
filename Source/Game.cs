using System.Diagnostics;
using System.Text.Json;

namespace Celeste64;

public struct Transition
{
	public enum Modes
	{
		Replace,
		Push,
		Pop
	}

	public Modes Mode;
	public Func<Scene>? Scene;
	public ScreenWipe? ToBlack;
	public ScreenWipe? FromBlack;
	public bool ToPause;
	public bool FromPause;
	public bool Saving;
	public bool StopMusic;
	public bool PerformAssetReload;
	public float HoldOnBlackFor;
}

public class Game : App
{
	private enum TransitionStep
	{
		None,
		FadeOut,
		Hold,
		Perform,
		FadeIn
	}

	public const string GamePath = "Celeste64";
	public const string GameTitle = "Celeste 64: Fragments of the Mountain";
	public const int Width = 640;
	public const int Height = 360;
	public static readonly Version Version = typeof(Game).Assembly.GetName().Version!;
	public static readonly string VersionString = $"v.{Version.Major}.{Version.Minor}.{Version.Build}";

	/// <summary>
	/// Used by various rendering elements to proportionally scale if you change the default game resolution
	/// </summary>
	public const float RelativeScale = Height / 360.0f;

	private static Game? instance;
	public static Game Instance => instance ?? throw new Exception("Game isn't running");

	private readonly Stack<Scene> scenes = new();
	private readonly Target target;
	private Target? stereoRightTarget;
	private XrBridge? xr;
	public System.Numerics.Vector2? VrPointerPosition { get; private set; }
	private bool vrPointerPressed, vrPointerWasDown;
	public bool ConsumeVrPointerPress()
	{
		bool pressed = vrPointerPressed;
		vrPointerPressed = false;
		return pressed;
	}
	public bool IsVr => xr != null;
	public Vec3? FirstPersonViewForward { get; private set; }
	internal XrBridge.Hands FirstPersonHands { get; private set; }
	internal Camera? FirstPersonHandAnchor { get; private set; }
	internal Vec3 FirstPersonHeadWorldPosition { get; private set; }
	internal ulong FirstPersonHandFrame { get; private set; }
	internal Vec3? FirstPersonHeadReference => xrReferencePosition;
	public bool QuestControlsEnabled { get; private set; } = true;
	public void ToggleQuestControls()
	{
		QuestControlsEnabled = !QuestControlsEnabled;
		xr?.SetInputEnabled(QuestControlsEnabled);
	}
	private Target[]? xrTargets;
	private System.Numerics.Vector3? xrReferencePosition;
	private readonly bool stereoPreview = Environment.GetEnvironmentVariable("CELESTE64_STEREO_PREVIEW") == "1";
	private readonly Batcher batcher;
	private Transition transition;
	private TransitionStep transitionStep = TransitionStep.None;
	private readonly FMOD.Studio.EVENT_CALLBACK audioEventCallback;
	private int audioBeatCounter;
	private bool audioBeatCounterEvent;
	private bool mouseCameraEnabled = false;

	public AudioHandle Ambience;
	public AudioHandle Music;
	public Controls Controls;

	public Game(AppConfig config) : base(config)
	{
		target = new(GraphicsDevice, Width, Height, [TextureFormat.Color, TextureFormat.Depth16]);
		batcher = new(GraphicsDevice);

		GraphicsDevice.VSync = Environment.GetEnvironmentVariable("CELESTE64_VR") != "1";
		Controls = new(Input, new ControlsConfig());

		// If this isn't stored, the delegate will get GC'd and everything will crash :)
		audioEventCallback = MusicTimelineCallback;
	}

	protected override void Startup()
	{
		instance = this;
		if (Environment.GetEnvironmentVariable("CELESTE64_VR") == "1")
			xr = new XrBridge(GraphicsDevice);
		Audio.Init();
		scenes.Push(new Startup());
	}

	protected override void Shutdown()
	{
		xr?.Dispose();
		xr = null;
		if (xrTargets != null)
			foreach (var eyeTarget in xrTargets) eyeTarget.Dispose();
		xrTargets = null;
		stereoRightTarget?.Dispose();
		stereoRightTarget = null;
		if (scenes.TryPeek(out var topScene))
			topScene.Exited();

		while (scenes.Count > 0)
		{
			var it = scenes.Pop();
			it.Disposed();
		}
		
		scenes.Clear();
		instance = null;
		Audio.Shutdown();
	}

	public bool IsMidTransition => transitionStep != TransitionStep.None;

	public void Goto(Transition next)
	{
		Debug.Assert(
			transitionStep == TransitionStep.None ||
			transitionStep == TransitionStep.FadeIn);
		transition = next;
		transitionStep = scenes.Count > 0 ? TransitionStep.FadeOut : TransitionStep.Perform;
		transition.ToBlack?.Restart(transitionStep != TransitionStep.FadeOut);

		if (transition.StopMusic)
			Music.Stop();
	}

	protected override void Update()
	{
		vrPointerPressed = false;
		VrPointerPosition = null;
		if (xr != null)
		{
			var pointer = xr.GetPointer();
			bool down = pointer.Trigger != 0;
			if (pointer.Valid != 0 && VrPointerMath.Intersect(pointer.Aim.Position, pointer.Aim.Rotation,
				pointer.Surface.Position, pointer.Surface.Rotation, out var point))
			{
				VrPointerPosition = point * new System.Numerics.Vector2(Width, Height);
				vrPointerPressed = down && !vrPointerWasDown;
			}
			vrPointerWasDown = down;
		}
		Audio.Update();

		// toggle mouse camera mode
		if (Input.Keyboard.Pressed(Keys.Escape))
			mouseCameraEnabled = false;
		if (Window.Focused && Input.Mouse.LeftPressed)
			mouseCameraEnabled = true;
		Window.SetMouseRelativeMode(mouseCameraEnabled);

		// update input filters depending on state
		{
			Input.BindingFilters.Clear();
			Input.BindingFilters.Add(Controls.IsUsingNintendo
				? ControlsConfig.FILTER_NINTENDO
				: ControlsConfig.FILTER_XBOX);
			Input.BindingFilters.Add(mouseCameraEnabled 
				? ControlsConfig.FILTER_MOUSE_CAMERA
				: ControlsConfig.FILTER_KEYBOARD_CAMERA);
		}

		// update top scene
		if (scenes.TryPeek(out var scene))
		{
			var pausing = 
				transitionStep == TransitionStep.FadeIn && transition.FromPause ||
				transitionStep == TransitionStep.FadeOut && transition.ToPause;

			if (!pausing)
				scene.Update();
		}

		// handle transitions
		if (transitionStep == TransitionStep.FadeOut)
		{
			if (transition.ToBlack == null || transition.ToBlack.IsFinished)
			{
				transitionStep = TransitionStep.Hold;
			}
			else
			{
				transition.ToBlack.Update(Time);
			}
		}
		else if (transitionStep == TransitionStep.Hold)
        {
            transition.HoldOnBlackFor -= Time.Delta;
			if (transition.HoldOnBlackFor <= 0)
            {
                if (transition.FromBlack != null)
                    transition.ToBlack = transition.FromBlack;
                transition.ToBlack?.Restart(true);
				transitionStep = TransitionStep.Perform;
            }
        }
		else if (transitionStep == TransitionStep.Perform)
		{
			Audio.StopBus(Sfx.bus_gameplay_world, false);

			// exit last scene
			if (scenes.TryPeek(out var lastScene))
			{
				lastScene?.Exited();
				if (transition.Mode != Transition.Modes.Push)
					lastScene?.Disposed();
			}

			// reload assets if requested
			if (transition.PerformAssetReload)
			{
				Assets.Load(GraphicsDevice);
			}

			// perform game save between transitions
			if (transition.Saving)
				Save.Instance.SaveToFile(UserPath);

			// perform transition
			switch (transition.Mode)
			{
			case Transition.Modes.Replace:
			Debug.Assert(transition.Scene != null);
			if (scenes.Count > 0)
				scenes.Pop();
			scenes.Push(transition.Scene());
			break;
			case Transition.Modes.Push:
			Debug.Assert(transition.Scene != null);
			scenes.Push(transition.Scene());
			audioBeatCounter = 0;
			break;
			case Transition.Modes.Pop:
			scenes.Pop();
			break;
			}

			// don't let the game sit in a sceneless place
			if (scenes.Count <= 0)
				scenes.Push(new Overworld(false));

			// run a single update when transition happens so stuff gets established
			if (scenes.TryPeek(out var nextScene))
			{
				nextScene.Entered();
				nextScene.Update();
			}

			// switch music
			{
				var last = Music.IsPlaying && lastScene != null ? lastScene.Music : string.Empty;
				var next = nextScene?.Music ?? string.Empty;
				if (next != last)
				{
					Music.Stop();
					Music = Audio.Play(next);
					if (Music)
						Music.SetCallback(audioEventCallback);
				}
			}

			// switch ambience
			{
				var last = Ambience.IsPlaying && lastScene != null ? lastScene.Ambience : string.Empty;
				var next = nextScene?.Ambience ?? string.Empty;
				if (next != last)
				{
					Ambience.Stop();
					Ambience = Audio.Play(next);
				}
			}

			// in case new music was played
			Save.Instance.ApplySettings(this);
			transitionStep = TransitionStep.FadeIn;
		}
		else if (transitionStep == TransitionStep.FadeIn)
		{
			if (transition.ToBlack == null || transition.ToBlack.IsFinished)
			{
				transitionStep = TransitionStep.None;
				transition = new();
			}
			else
			{
				transition.ToBlack.Update(Time);
			}
		}
		else if (transitionStep == TransitionStep.None)
		{
			// handle audio beat events on main thread
			if (audioBeatCounterEvent)
			{
				audioBeatCounterEvent = false;
				audioBeatCounter++;

				if (scene is World world)
				{
					foreach (var listener in world.All<IListenToAudioCallback>())
						(listener as IListenToAudioCallback)?.AudioCallbackEvent(audioBeatCounter);
				}
			}
		}

		
		if (scene is not Celeste64.Startup)
		{
			// toggle fullsrceen
			if ((Input.Keyboard.Alt && Input.Keyboard.Pressed(Keys.Enter)) || Input.Keyboard.Pressed(Keys.F4))
				Save.Instance.ToggleFullscreen(this);

			// reload state
			if (Input.Keyboard.Ctrl && Input.Keyboard.Pressed(Keys.R) && !IsMidTransition)
			{
				if (scene is World world)
				{
					Goto(new Transition()
					{
						Mode = Transition.Modes.Replace,
						Scene = () => new World(world.Entry),
						ToPause = true,
						ToBlack = new AngledWipe(),
						PerformAssetReload = true
					});
				}
				else
				{
					Goto(new Transition()
					{
						Mode = Transition.Modes.Replace,
						Scene = () => new Titlescreen(),
						ToPause = true,
						ToBlack = new AngledWipe(),
						PerformAssetReload = true
					});
				}
			}
		}
	}

	protected override void Render()
	{
		Window.Clear(Color.Black);
		if (xr != null && RenderXrFrame())
			return;

		// Developer preview for checking stereo rendering without an XR headset.
		// Menus and transitions continue to use the normal single-view renderer.
		if (stereoPreview && transitionStep == TransitionStep.None && scenes.TryPeek(out var current) && current is World world)
		{
			RenderStereoPreview(world);
			return;
		}

		if (transitionStep != TransitionStep.Perform && transitionStep != TransitionStep.Hold)
		{
			// draw the world to the target
			if (scenes.TryPeek(out var scene))
				scene.Render(target);

			// draw screen wipe over top
			if (transitionStep != TransitionStep.None && transition.ToBlack != null)
			{
				transition.ToBlack.Render(batcher, new Rect(0, 0, target.Width, target.Height));
				batcher.Render(target);
				batcher.Clear();
			}

			// draw the target to the window
			{
				var scale = Math.Min(Window.WidthInPixels / (float)target.Width, Window.HeightInPixels / (float)target.Height);
				batcher.PushSampler(new(TextureFilter.Nearest, TextureWrap.Clamp, TextureWrap.Clamp));
				batcher.Image(target, Window.SizeInPixels / 2, target.Bounds.Size / 2, Vec2.One * scale, 0, Color.White);
				batcher.PopSampler();
				batcher.Render(Window);
				batcher.Clear();
			}
		}
	}

	private bool RenderXrFrame()
	{
		if (xr == null) return false;
		bool menuActive = scenes.TryPeek(out var top) && (top is not World || ((World)top).Paused);
		xr.SetInputOptions(menuActive, Save.Instance.VrGripDash, Save.Instance.VrFirstPerson);
		if (!xr.Begin(out bool shouldRender)) { FirstPersonHands = default; FirstPersonHandAnchor = null; return false; }
		int layerMode = 0;
		try
		{
			if (!shouldRender) { FirstPersonHands = default; FirstPersonHandAnchor = null; return false; }
			var eyes = xr.Eyes;
			var (renderWidth, renderHeight) = VrGraphics.RenderSize(Save.Instance.VrClearGraphics,
				Math.Max(eyes[0].Width, eyes[1].Width), Math.Max(eyes[0].Height, eyes[1].Height));
			if (xrTargets == null || xrTargets[0].Width != renderWidth || xrTargets[0].Height != renderHeight)
			{
				if (xrTargets != null)
					foreach (var oldTarget in xrTargets) oldTarget.Dispose();
				xrTargets = [new Target(GraphicsDevice, renderWidth, renderHeight, [TextureFormat.Color, TextureFormat.Depth16]),
					new Target(GraphicsDevice, renderWidth, renderHeight, [TextureFormat.Color, TextureFormat.Depth16])];
			}
			xrReferencePosition ??= (eyes[0].Position + eyes[1].Position) / 2;
			if (scenes.TryPeek(out var current) && current is World world)
			{
				layerMode = world.Paused ? 3 : 2;
				var originalCamera = world.Camera;
				var player = world.Get<Player>();
				bool firstPerson = Save.Instance.VrFirstPerson && player != null;
				var renderAnchor = firstPerson ? VrCamera.FirstPersonAnchor(originalCamera, player!.Position, player.Forward) : originalCamera;
				world.VrReferenceCamera = renderAnchor;
				world.VrFirstPersonRendering = firstPerson;
				if (firstPerson)
				{
					FirstPersonHands = xr.GetHands();
					FirstPersonHandAnchor = renderAnchor;
					FirstPersonHeadWorldPosition = Vec3.Transform(Vec3.Zero, VrCamera.TrackedPose(renderAnchor,
						(eyes[0].Position + eyes[1].Position) / 2, Quaternion.Identity, xrReferencePosition.Value, 10));
					FirstPersonHandFrame++;
					world.VrTrackedHands = FirstPersonHands;
					world.VrHeadReference = xrReferencePosition.Value;
					world.VrHeadWorldPosition = FirstPersonHeadWorldPosition;
				}
				else { FirstPersonViewForward = null; FirstPersonHands = default; FirstPersonHandAnchor = null; }
				try
				{
					for (int i = 0; i < 2; i++)
					{
						var eye = eyes[i];
						world.Camera = VrCamera.ForEye(renderAnchor, eye.Position, eye.Orientation, xrReferencePosition.Value,
							10, eye.Left, eye.Right, eye.Down, eye.Up, 1, 800);
						if (firstPerson && i == 0) FirstPersonViewForward = world.Camera.Forward;
						world.Render(xrTargets[i], false);
						GraphicsDevice.BlitToXrImage(xrTargets[i].Attachments[0], eye.Texture, eye.Width, eye.Height);
					}
				}
				finally { world.Camera = originalCamera; world.VrReferenceCamera = null; world.VrFirstPersonRendering = false; }
				world.RenderVrUi(target);
				if (world.Paused) DrawVrPointer(target);
				GraphicsDevice.BlitToXrImage(target.Attachments[0], eyes[2].Texture, eyes[2].Width, eyes[2].Height);
			}
			else
			{
				// Menus and transitions use a screen positioned in the XR reference space.
				layerMode = 1;
				target.Clear(Color.Black, 1, 0, ClearMask.All);
				if (transitionStep != TransitionStep.Perform && transitionStep != TransitionStep.Hold && scenes.TryPeek(out var scene))
					scene.Render(target);
				if (transitionStep != TransitionStep.None && transition.ToBlack != null)
				{
					transition.ToBlack.Render(batcher, new Rect(0, 0, target.Width, target.Height));
					batcher.Render(target);
					batcher.Clear();
				}
				DrawVrPointer(target);
				GraphicsDevice.BlitToXrImage(target.Attachments[0], eyes[2].Texture, eyes[2].Width, eyes[2].Height);
			}
			GraphicsDevice.FlushXrImages();
			var mirror = scenes.TryPeek(out var shown) && shown is World
				? xrTargets[0] : target;
			var mirrorScale = Math.Min(Window.WidthInPixels / (float)mirror.Width, Window.HeightInPixels / (float)mirror.Height);
			batcher.Image(mirror, Window.SizeInPixels / 2, mirror.Bounds.Size / 2, Vec2.One * mirrorScale, 0, Color.White);
			if (shown is World)
			{
				// The HUD/dialogue is a separate XR layer; composite it into the desktop mirror too.
				var uiScale = Math.Min(mirror.Width * mirrorScale / target.Width, mirror.Height * mirrorScale / target.Height);
				batcher.Image(target, Window.SizeInPixels / 2, target.Bounds.Size / 2, Vec2.One * uiScale, 0, Color.White);
			}
			batcher.Render(Window);
			batcher.Clear();
			return true;
		}
		finally { xr.End(layerMode); }
	}

	private void DrawVrPointer(Target destination)
	{
		if (VrPointerPosition is not { } point) return;
		batcher.Rect(point.X - 3, point.Y - 3, 6, 6, Color.White);
		batcher.Render(destination);
		batcher.Clear();
	}

	private void RenderStereoPreview(World world)
	{
		stereoRightTarget ??= new Target(GraphicsDevice, Width, Height, [TextureFormat.Color, TextureFormat.Depth16]);
		var originalCamera = world.Camera;
		try
		{
			// Temporary 64 mm IPD and a 10 game-units-per-metre scale. Real VR
			// will replace these values and the FOV with the runtime's eye poses.
			Camera Eye(float x) => VrCamera.ForEye(originalCamera, new(x, 0, 0), System.Numerics.Quaternion.Identity,
				System.Numerics.Vector3.Zero, 10, -MathF.PI / 4, MathF.PI / 4, -MathF.PI / 4, MathF.PI / 4, 1, 800);
			world.Camera = Eye(-0.032f);
			world.Render(target);
			world.Camera = Eye(0.032f);
			world.Render(stereoRightTarget);
		}
		finally
		{
			world.Camera = originalCamera;
		}

		float scale = Math.Min(Window.WidthInPixels / (float)(Width * 2), Window.HeightInPixels / (float)Height);
		var center = Window.SizeInPixels / 2;
		var halfOffset = new Vec2(Width * scale / 2, 0);
		batcher.PushSampler(new(TextureFilter.Nearest, TextureWrap.Clamp, TextureWrap.Clamp));
		batcher.Image(target, center - halfOffset, target.Bounds.Size / 2, Vec2.One * scale, 0, Color.White);
		batcher.Image(stereoRightTarget, center + halfOffset, stereoRightTarget.Bounds.Size / 2, Vec2.One * scale, 0, Color.White);
		batcher.PopSampler();
		batcher.Render(Window);
		batcher.Clear();
	}

	private FMOD.RESULT MusicTimelineCallback(FMOD.Studio.EVENT_CALLBACK_TYPE type, IntPtr _event, IntPtr parameters)
	{
		// notify that an audio event happened (but handle it on the main thread)
		if (transitionStep == TransitionStep.None)
			audioBeatCounterEvent = true;
		return FMOD.RESULT.OK;
	}
}
