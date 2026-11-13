using Android.App;
using Android.Content.PM;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace Celeste64;

[Activity(Label = "Celeste 64 VR - Quest", MainLauncher = true, Exported = true,
    LaunchMode = LaunchMode.SingleTask,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden)]
[IntentFilter(["android.intent.action.MAIN"], Categories = ["android.intent.category.LAUNCHER", "org.khronos.openxr.intent.category.IMMERSIVE_HMD", "com.oculus.intent.category.VR"])]
public class MainActivity : Org.Libsdl.App.SDLActivity
{
    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_SetMainReady();

    protected override string[] GetLibraries() => ["SDL3"];

    protected override void Main()
    {
        try
        {
            string root = FilesDir!.AbsolutePath;
            string content = Path.Combine(root, "Content");
            // Refresh packaged assets when an update adds or changes content.
            string contentMarker = Path.Combine(root, "content-extracted-v4");
            if (!File.Exists(contentMarker))
            {
                using var archive = Assets!.Open("content.zip");
                using var zip = new ZipArchive(archive, ZipArchiveMode.Read);
                zip.ExtractToDirectory(root, true);
                File.WriteAllText(contentMarker, "complete");
            }
            Environment.SetEnvironmentVariable("CELESTE64_CONTENT_PATH", content);
            Environment.SetEnvironmentVariable("CELESTE64_VR", "1");
#if QUEST_FMOD
            Java.Lang.JavaSystem.LoadLibrary("fmod");
            Org.Fmod.FMOD.Init(this);
#endif
            SDL_SetMainReady();
            Program.Main([]);
        }
        catch (Exception e)
        {
            Android.Util.Log.Error("CelesteVR", e.ToString());
        }
    }
}
