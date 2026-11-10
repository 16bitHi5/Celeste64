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
	internal XrBridge.Pointer VrMenuPointer { get; private set; }
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
	internal Vec3? FirstPersonHeadReference => vrRoomReference;
	public bool QuestControlsEnabled { get; private set; } = true;
	public void ToggleQuestControls()
	{
		QuestControlsEnabled = !QuestControlsEnabled;
		xr?.SetInputEnabled(QuestControlsEnabled);
	}
	private Target[]? xrTargets;
	private System.Numerics.Vector3? xrReferencePosition;
	private Vec3? vrRoomReference, vrRoomHead;
	private long vrTurnFrameTime;
	private bool vrUncappedRendering;
	private float vrCollectionBlend;
	private Camera vrCollectionCamera;
	private World? vrCollectionWorld;
	private bool vrCollectionWasTape;
	private bool vrCollectionActive;
	private readonly bool stereoPreview = Environment.GetEnvironmentVariable("CELESTE64_STEREO_PREVIEW") == "1";
	private readonly Batcher batcher;
	private Transition transition;
	private TransitionStep transitionStep = TransitionStep.None;
	private readonly FMOD.Studio.EVENT_CALLBACK audioEventCallback;
	private int audioBeatCounter;
	private bool audioBeatCounterEvent;
	private bool mouseCameraEnabled = false;
	private float vrMirrorZoom = 1f;

	internal void ApplyVrRoomScale(Player player)
	{
		if (!IsVr || !Save.Instance.VrFirstPerson || !player.VrRoomScaleCanMove || player.World.Paused ||
			vrCollectionBlend > 0 || FirstPersonHandAnchor is not { } anchor ||
			vrRoomReference is not { } reference || vrRoomHead is not { } head) return;
		// Move the body by the physical horizontal motion, through normal collision.
		// Recenter even if a wall blocks it, keeping the head on the near side.
		if (VrRoomScale.TryMovement(anchor, head, reference, 10, out var movement))
			player.SweepTestMove(movement, false);
		vrRoomReference = VrRoomScale.Recenter(reference, head);
	}

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
			if (Window.Focused && Input.Mouse.Wheel.Y != 0)
				vrMirrorZoom = VrMirrorLayout.Zoom(vrMirrorZoom, Input.Mouse.Wheel.Y);
			var pointer = VrMenuPointer = xr.GetPointer();
			bool down = pointer.Valid != 0 && pointer.Trigger != 0;
			if (pointer.Valid != 0 && VrPointerMath.Intersect(pointer.Aim.Position, pointer.Aim.Rotation,
				pointer.Surface.Position, pointer.Surface.Rotation, out var point))
			{
				VrPointerPosition = point * new System.Numerics.Vector2(Width, Height);
				vrPointerPressed = down && !vrPointerWasDown;
			}
			if (pointer.Valid != 0 && scenes.TryPeek(out var pointerScene) && pointerScene is Overworld && !IsMidTransition)
				vrPointerPressed = down && !vrPointerWasDown;
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
		if (!xr.Begin(out bool shouldRender))
		{
			SetVrRenderTiming(false);
			FirstPersonHands = default; FirstPersonHandAnchor = null; return false;
		}
		// OpenXR's frame wait paces rendering to the headset. Keep simulation at
		// its original 60 Hz, but don't make every render wait for a simulation tick.
		SetVrRenderTiming(true);
		int layerMode = 0;
		bool imagesReady = false;
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
			if (scenes.TryPeek(out var current) && current is Titlescreen title && transitionStep != TransitionStep.Hold && transitionStep != TransitionStep.Perform)
			{
				vrRoomReference = vrRoomHead = null;
				FirstPersonHandAnchor = null;
				FirstPersonHands = default;
				FirstPersonViewForward = null;
				vrCollectionWorld = null;
				layerMode = 2;
				var titleAnchor = title.VrCamera(xrTargets[0]);
				for (int i = 0; i < 2; i++)
				{
					var eye = eyes[i];
					var camera = VrCamera.ForEye(titleAnchor, eye.Position, eye.Orientation, xrReferencePosition.Value,
						10, eye.Left, eye.Right, eye.Down, eye.Up, 1, 800);
					title.RenderVrEye(xrTargets[i], camera);
					GraphicsDevice.BlitToXrImage(xrTargets[i].Attachments[0], eye.Texture, eye.Width, eye.Height);
				}
				title.RenderVrUi(target);
				DrawVrPointer(target);
				GraphicsDevice.BlitToXrImage(target.Attachments[0], eyes[2].Texture, eyes[2].Width, eyes[2].Height);
			}
			else if (current is Overworld overworld && transitionStep != TransitionStep.Hold && transitionStep != TransitionStep.Perform)
			{
				vrRoomReference = vrRoomHead = null;
				FirstPersonHandAnchor = null;
				FirstPersonHands = default;
				FirstPersonViewForward = null;
				vrCollectionWorld = null;
				layerMode = 2;
				var cardAnchor = overworld.VrCamera(xrTargets[0]);
				overworld.PrepareVrFrame(cardAnchor, xrReferencePosition.Value, xr.GetPointer());
				for (int i = 0; i < 2; i++)
				{
					var eye = eyes[i];
					var camera = VrCamera.ForEye(cardAnchor, eye.Position, eye.Orientation, xrReferencePosition.Value,
						10, eye.Left, eye.Right, eye.Down, eye.Up, 1, 1000);
					overworld.RenderVrEye(xrTargets[i], camera);
					GraphicsDevice.BlitToXrImage(xrTargets[i].Attachments[0], eye.Texture, eye.Width, eye.Height);
				}
				overworld.RenderVrUi(target);
				if (overworld.VrUsesFlatPointer) DrawVrPointer(target);
				GraphicsDevice.BlitToXrImage(target.Attachments[0], eyes[2].Texture, eyes[2].Width, eyes[2].Height);
			}
			else if (current is World world)
			{
				layerMode = world.Paused ? 3 : 2;
				var originalCamera = world.Camera;
				var player = world.Get<Player>();
				bool firstPerson = Save.Instance.VrFirstPerson && player != null;
				bool vrIrisTransition = firstPerson && transitionStep != TransitionStep.None && transition.ToBlack is SpotlightWipe;
				bool vrTransitionBlack = firstPerson && (transitionStep == TransitionStep.Hold || transitionStep == TransitionStep.Perform);
				var headCenter = (eyes[0].Position + eyes[1].Position) / 2;
				bool revealingTapeArea = firstPerson && player!.VrCassetteAreaReveal;
				if (!firstPerson || vrCollectionWorld != world)
				{
					vrRoomReference = firstPerson ? headCenter : null;
					vrRoomHead = firstPerson ? headCenter : null;
					// A tape area starts with its scripted view, even if it was loaded
					// through a transition that did not render the preceding world.
					bool tapeArrival = revealingTapeArea || (firstPerson && vrCollectionWorld != null && vrCollectionWasTape);
					vrCollectionBlend = tapeArrival ? 1 : 0;
					if (tapeArrival) vrCollectionCamera = originalCamera;
					vrCollectionWorld = firstPerson ? world : null;
					vrCollectionActive = false;
				}
				vrCollectionWasTape = firstPerson && player!.VrTapeCinematic;
				long now = Stopwatch.GetTimestamp();
				float elapsed = vrTurnFrameTime == 0 ? 0 : Math.Clamp((float)Stopwatch.GetElapsedTime(vrTurnFrameTime, now).TotalSeconds, 0, .05f);
				vrTurnFrameTime = firstPerson ? now : 0;
				bool collecting = firstPerson && (player!.VrCollectionCinematic || revealingTapeArea);
				if (firstPerson)
				{
					vrRoomHead = headCenter;
					vrRoomReference ??= headCenter;
					if (world.Paused || collecting || vrCollectionBlend > 0 || !player!.VrRoomScaleCanMove)
						vrRoomReference = VrRoomScale.Recenter(vrRoomReference.Value, headCenter);
				}
				var poseReference = firstPerson ? vrRoomReference!.Value : xrReferencePosition.Value;
				if (firstPerson && !world.Paused && !collecting && vrCollectionBlend <= 0) player!.UpdateVrTurn(elapsed);
				var headingCamera = originalCamera;
				if (firstPerson) headingCamera.LookAt = headingCamera.Position + player!.VrTurnForward;
				var handAnchor = firstPerson ? VrCamera.FirstPersonAnchor(headingCamera, player!.Position, player.Forward) : originalCamera;
				if (revealingTapeArea)
				{
					// Follow the original reveal camera until gameplay resumes.
					vrCollectionCamera = originalCamera;
				}
				else if (collecting && !vrCollectionActive)
				{
					var eye = eyes[0];
					var trackedView = VrCamera.ForEye(handAnchor, eye.Position, eye.Orientation, poseReference,
						10, eye.Left, eye.Right, eye.Down, eye.Up, 1, 800);
					vrCollectionCamera = VrCamera.CollectionView(handAnchor, player!.Position, trackedView.Forward);
				}
				vrCollectionActive = collecting;
				if (!world.Paused) vrCollectionBlend = Calc.Approach(vrCollectionBlend, collecting ? 1 : 0, elapsed / .5f);
				var renderAnchor = firstPerson && vrCollectionBlend > 0 ? VrCamera.CollectionAnchor(handAnchor, vrCollectionCamera, vrCollectionBlend) : handAnchor;
				var headCorrection = Vec3.Zero;
				world.VrReferenceCamera = renderAnchor;
				world.VrFirstPersonRendering = firstPerson && vrCollectionBlend < .35f;
				world.VrCollectionRendering = firstPerson && vrCollectionBlend >= .35f;
				if (firstPerson)
				{
					FirstPersonHands = xr.GetHands();
					FirstPersonHandAnchor = handAnchor;
					var trackedHead = Vec3.Transform(Vec3.Zero, VrCamera.TrackedPose(handAnchor,
						headCenter, Quaternion.Identity, poseReference, 10));
					FirstPersonHeadWorldPosition = world.ConstrainVrHead(handAnchor.Position, trackedHead);
					headCorrection = (FirstPersonHeadWorldPosition - trackedHead) * (1 - vrCollectionBlend);
					FirstPersonHandFrame++;
					world.VrTrackedHands = FirstPersonHands;
					world.VrHeadReference = poseReference;
					world.VrHeadWorldPosition = FirstPersonHeadWorldPosition;
				}
				else { FirstPersonViewForward = null; FirstPersonHands = default; FirstPersonHandAnchor = null; }
				// Both eyes project the same circle, two metres in front of the head.
				var irisCenter = Vec3.Zero;
				var irisAxis = Vec3.Zero;
				var irisUp = Vec3.Zero;
				if (vrIrisTransition && !vrTransitionBlack)
				{
				var irisLeft = VrCamera.ForEye(renderAnchor, eyes[0].Position, eyes[0].Orientation, poseReference,
					10, eyes[0].Left, eyes[0].Right, eyes[0].Down, eyes[0].Up, 1, 800);
				var irisRight = VrCamera.ForEye(renderAnchor, eyes[1].Position, eyes[1].Orientation, poseReference,
					10, eyes[1].Left, eyes[1].Right, eyes[1].Down, eyes[1].Up, 1, 800);
				var irisForward = Vec3.Normalize(irisLeft.Forward + irisRight.Forward);
				irisAxis = Vec3.Normalize(Vec3.Cross(irisForward, irisLeft.ViewUp + irisRight.ViewUp));
				irisUp = Vec3.Normalize(Vec3.Cross(irisAxis, irisForward));
				irisCenter = (irisLeft.Position + irisRight.Position) * .5f + headCorrection + irisForward * 20;
				}
				try
				{
					for (int i = 0; i < 2; i++)
					{
						var eye = eyes[i];
						world.Camera = VrCamera.ForEye(renderAnchor, eye.Position, eye.Orientation, poseReference,
							10, eye.Left, eye.Right, eye.Down, eye.Up, 1, 800);
						if (firstPerson)
						{
							world.Camera.Position += headCorrection;
							world.Camera.LookAt += headCorrection;
						}
						if (firstPerson && i == 0) FirstPersonViewForward = world.Camera.Forward;
						world.Render(xrTargets[i], false);
						if (vrTransitionBlack || vrIrisTransition)
						{
							var bounds = new Rect(0, 0, renderWidth, renderHeight);
							if (vrTransitionBlack) batcher.Rect(bounds, Color.Black);
							else ((SpotlightWipe)transition.ToBlack!).RenderVr(batcher, bounds, world.Camera, irisCenter, irisAxis, irisUp);
							batcher.Render(xrTargets[i]);
							batcher.Clear();
						}
						if (firstPerson && player!.VrDeathFade > 0)
						{
							batcher.Rect(new Rect(0, 0, renderWidth, renderHeight), Color.Black * player.VrDeathFade);
							batcher.Render(xrTargets[i]);
							batcher.Clear();
						}
						GraphicsDevice.BlitToXrImage(xrTargets[i].Attachments[0], eye.Texture, eye.Width, eye.Height);
					}
				}
				finally { world.Camera = originalCamera; world.VrReferenceCamera = null; world.VrFirstPersonRendering = false; world.VrCollectionRendering = false; }
				world.RenderVrUi(target);
				if (firstPerson && (player!.VrDeathFade > 0 || vrIrisTransition || vrTransitionBlack)) target.Clear(Color.Transparent);
				if (world.Paused) DrawVrPointer(target);
				GraphicsDevice.BlitToXrImage(target.Attachments[0], eyes[2].Texture, eyes[2].Width, eyes[2].Height);
			}
			else
			{
				vrRoomReference = vrRoomHead = null;
				FirstPersonHandAnchor = null;
				FirstPersonHands = default;
				FirstPersonViewForward = null;
				vrTurnFrameTime = 0;
				vrCollectionBlend = 0;
				vrCollectionWorld = null;
				vrCollectionWasTape = false;
				vrCollectionActive = false;
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
			imagesReady = GraphicsDevice.FlushXrImages();
			var mirror = scenes.TryPeek(out var shown) && (shown is World || ((shown is Titlescreen || shown is Overworld) && layerMode == 2))
				? xrTargets[0] : target;
			bool stereoMirror = shown is World || ((shown is Titlescreen || shown is Overworld) && layerMode == 2);
			var viewport = VrMirrorLayout.Viewport(Window.WidthInPixels, Window.HeightInPixels);
			var center = new Vec2(viewport.X + viewport.Width / 2f, viewport.Y + viewport.Height / 2f);
			var mirrorScale = VrMirrorLayout.FillScale(mirror.Width, mirror.Height, viewport, stereoMirror ? vrMirrorZoom : 1f);
			batcher.PushSampler(new(TextureFilter.Linear, TextureWrap.Clamp, TextureWrap.Clamp));
			batcher.Image(mirror, center, mirror.Bounds.Size / 2, Vec2.One * mirrorScale, 0, Color.White);
			if (stereoMirror)
			{
				// Keep dialogue and menus readable in the stream even when the world is zoomed.
				var uiScale = Math.Min(viewport.Width / (float)target.Width, viewport.Height / (float)target.Height);
				batcher.Image(target, center, target.Bounds.Size / 2, Vec2.One * uiScale, 0, Color.White);
			}
			batcher.PopSampler();
			// Mask the edges after drawing. A window scissor can exceed SDL's
			// current backbuffer during resize or startup and halt rendering.
			batcher.Rect(0, 0, Window.WidthInPixels, viewport.Y, Color.Black);
			batcher.Rect(0, viewport.Y + viewport.Height, Window.WidthInPixels,
				Window.HeightInPixels - viewport.Y - viewport.Height, Color.Black);
			batcher.Rect(0, viewport.Y, viewport.X, viewport.Height, Color.Black);
			batcher.Rect(viewport.X + viewport.Width, viewport.Y,
				Window.WidthInPixels - viewport.X - viewport.Width, viewport.Height, Color.Black);
			batcher.Render(Window);
			batcher.Clear();
			return true;
		}
		finally { xr.End(layerMode, imagesReady); }
	}

	private void SetVrRenderTiming(bool xrRunning)
	{
		if (vrUncappedRendering == xrRunning) return;
		vrUncappedRendering = xrRunning;
		UpdateMode = UpdateMode.FixedStep(60, !xrRunning);
		if (!xrRunning) vrTurnFrameTime = 0;
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
