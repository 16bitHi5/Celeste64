using System.Numerics;
using System.Runtime.InteropServices;

namespace Celeste64;

// The native bridge owns the OpenXR session and swapchains; SDL owns the instance.
internal sealed class XrBridge : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Eye
    {
        public float Px, Py, Pz, Qx, Qy, Qz, Qw, Left, Right, Down, Up;
        public int Width, Height;
        public nint Texture;
        public readonly Vector3 Position => new(Px, Py, Pz);
        public readonly Quaternion Orientation => new(Qx, Qy, Qz, Qw);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Pose
    {
        public float Qx, Qy, Qz, Qw, Px, Py, Pz;
        public readonly Vector3 Position => new(Px, Py, Pz);
        public readonly Quaternion Rotation => new(Qx, Qy, Qz, Qw);
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Pointer { public Pose Aim, Surface; public int Valid, Trigger; }

    [DllImport("CelesteXR", CallingConvention = CallingConvention.Cdecl)] private static extern nint CelesteXR_New(nint device, nint instance, ulong system);
    [DllImport("CelesteXR", CallingConvention = CallingConvention.Cdecl)] private static extern int CelesteXR_Init(nint bridge);
    [DllImport("CelesteXR", CallingConvention = CallingConvention.Cdecl)] private static extern nint CelesteXR_Error(nint bridge);
    [DllImport("CelesteXR", CallingConvention = CallingConvention.Cdecl)] private static extern int CelesteXR_BeginFrame(nint bridge, [Out] Eye[] eyes, out int render);
    [DllImport("CelesteXR", CallingConvention = CallingConvention.Cdecl)] private static extern int CelesteXR_EndFrame(nint bridge, int flat);
    [DllImport("CelesteXR", CallingConvention = CallingConvention.Cdecl)] private static extern void CelesteXR_Destroy(nint bridge);
    [DllImport("CelesteXR", CallingConvention = CallingConvention.Cdecl)] private static extern void CelesteXR_SetInputEnabled(nint bridge, int enabled);
    [DllImport("CelesteXR", CallingConvention = CallingConvention.Cdecl)] private static extern void CelesteXR_SetInputOptions(nint bridge, int menuActive, int gripDash);
    [DllImport("CelesteXR", CallingConvention = CallingConvention.Cdecl)] private static extern void CelesteXR_GetPointer(nint bridge, out Pointer pointer);

    private nint handle;
    public readonly Eye[] Eyes = new Eye[3];

    public XrBridge(GraphicsDevice graphics)
    {
        var (device, instance, system) = graphics.XrHandles;
        if (device == 0 || instance == 0 || system == 0)
            throw new InvalidOperationException("The graphics device was not created with OpenXR enabled.");
        handle = CelesteXR_New(device, instance, system);
        if (handle == 0) throw new InvalidOperationException("Could not allocate the OpenXR bridge.");
        if (CelesteXR_Init(handle) == 0)
        {
            var error = Error();
            Dispose();
            throw new InvalidOperationException(error);
        }
    }

    public bool Begin(out bool render)
    {
        int result = CelesteXR_BeginFrame(handle, Eyes, out int shouldRender);
        if (result < 0) throw new InvalidOperationException(Error());
        render = shouldRender != 0;
        return result != 0;
    }

    public void End(int mode)
    {
        if (CelesteXR_EndFrame(handle, mode) == 0) throw new InvalidOperationException(Error());
    }

    private string Error() => Marshal.PtrToStringUTF8(CelesteXR_Error(handle)) ?? "Unknown OpenXR error";

    public void SetInputEnabled(bool enabled) => CelesteXR_SetInputEnabled(handle, enabled ? 1 : 0);
    public void SetInputOptions(bool menuActive, bool gripDash) => CelesteXR_SetInputOptions(handle, menuActive ? 1 : 0, gripDash ? 1 : 0);
    public Pointer GetPointer() { CelesteXR_GetPointer(handle, out var pointer); return pointer; }

    public void Dispose()
    {
        if (handle != 0) CelesteXR_Destroy(handle);
        handle = 0;
    }
}
