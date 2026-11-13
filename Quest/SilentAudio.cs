// Temporary silent backend for the first Android experiment. FMOD's Android
// binaries are not included in the desktop project.
namespace Celeste64;

public static class Audio
{
    public static void Init() { }
    public static void Shutdown() { }
    public static void Update() { }
    public static void Load(string directory) { }
    public static void Unload() { }
    public static void SetListener(in Camera camera) { }
    public static AudioHandle Create(in FMOD.GUID id) => new();
    public static AudioHandle Create(in FMOD.GUID id, in Vec3 position) => new();
    public static AudioHandle Create(string path) => new();
    public static AudioHandle Play(in FMOD.GUID id, Vec3? position = null, float volume = 1) => new();
    public static AudioHandle Play(string path, Vec3? position = null, float volume = 1) => new();
    public static void StopAll(bool immediate) { }
    public static void StopBus(string name, bool immediate) { }
    public static void SetBusPaused(string name, bool paused) { }
    public static void SetBusVolume(FMOD.GUID id, float volume) { }
    public static void SetBusVolume(string name, float volume) { }
    public static void SetVCAVolume(string name, float volume) { }
    public static void SetParameter(string id, float value) { }
}

public struct AudioHandle
{
    public readonly FMOD.GUID ID => default;
    public readonly string Path => string.Empty;
    public readonly bool IsLooping => false;
    public readonly bool IsOneshot => true;
    public readonly bool IsPlaying => false;
    public readonly bool IsStopping => false;
    public Vec3 Position { readonly get => default; set { } }
    public float Volume { readonly get => 0; set { } }
    public readonly void SetCallback(FMOD.Studio.EVENT_CALLBACK callback) { }
    public readonly void Set(FMOD.Studio.PARAMETER_ID id, float value) { }
    public readonly void Set(ulong id, float value) { }
    public readonly void Set((ulong ID, float Value) parameter) { }
    public readonly void Set(string id, float value) { }
    public readonly bool Has(string id) => false;
    public readonly void Play() { }
    public readonly void PlayNoRelease() { }
    public readonly void Stop() { }
    public readonly void StopNoRelease() { }
    public readonly void Release() { }
    public static implicit operator bool(AudioHandle handle) => false;
}
