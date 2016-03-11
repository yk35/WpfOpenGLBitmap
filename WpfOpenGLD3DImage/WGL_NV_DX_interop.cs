using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfOpenGLD3DImage
{
    using System.Reflection;
    using System.Runtime.InteropServices;

    public class WGL_NV_DX_interop
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate System.Boolean wglDXSetResourceShareHandleNV(IntPtr dxObject, IntPtr shareHandle);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate IntPtr wglDXOpenDeviceNV(IntPtr dxDevice);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate System.Boolean wglDXCloseDeviceNV(IntPtr hDevice);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate IntPtr wglDXRegisterObjectNV(IntPtr hDevice, IntPtr dxObject, uint name, uint typeEnum, uint accessEnum);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate System.Boolean wglDXUnregisterObjectNV(IntPtr hDevice, IntPtr hObject);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate System.Boolean wglDXObjectAccessNV(IntPtr hObject, uint accessEnum);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate System.Boolean wglDXLockObjectsNV(IntPtr hDevice, int count, IntPtr[] hObjectsPtr);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate System.Boolean wglDXUnlockObjectsNV(IntPtr hDevice, int count, IntPtr[] hObjectsPtr);

        public const uint WGL_ACCESS_READ_ONLY_NV = 0x0000;
        public const uint WGL_ACCESS_READ_WRITE_NV = 0x0001;
        public const uint WGL_ACCESS_WRITE_DISCARD_NV = 0x0002;

        public wglDXCloseDeviceNV WglDXCloseDeviceNV;
        public wglDXLockObjectsNV WglDXLockObjectsNV;
        public wglDXObjectAccessNV WglDXObjectAccessNV;
        public wglDXOpenDeviceNV WglDXOpenDeviceNV;
        public wglDXRegisterObjectNV WglDXRegisterObjectNV;
        public wglDXSetResourceShareHandleNV WglDXSetResourceShareHandleNV;
        public wglDXUnlockObjectsNV WglDXUnlockObjectsNV;
        public wglDXUnregisterObjectNV WglDXUnregisterObjectNV;

        private Func<IntPtr, bool> wglIsValid;
        private Func<string, IntPtr> wglGetAddress;

        private T fetch<T>() where T : class
        {
            var name = typeof(T).Name;
            var addr = wglGetAddress(name);
            
            if (wglIsValid(addr))
                return Marshal.GetDelegateForFunctionPointer<T>(addr);
            else
            {
                Console.WriteLine("WGL_NV_DX_interop: Missing " + name);
                return null;
            }
        }

        public WGL_NV_DX_interop()
        {
            var wgl = typeof(OpenTK.Platform.Windows.All).Assembly.GetType("OpenTK.Platform.Windows.Wgl", true);
            var wglInstance = wgl.GetConstructor(new Type[] { }).Invoke(new object[] { });

            // STATIC (but not visible) METHODS
            //  Public (even though the class isn't?!)
            var wglSupportsExtension = (Func<string, bool>)wgl.GetMethods().Where(m => m.Name == "SupportsExtension" && m.GetParameters().Length == 1).Single().CreateDelegate(typeof(Func<string, bool>));
            var wglSupportsFunction = (Func<string, bool>)wgl.GetMethods().Where(m => m.Name == "SupportsFunction" && m.GetParameters().Length == 1).Single().CreateDelegate(typeof(Func<string, bool>));
            //  Private
            wglIsValid = (Func<IntPtr, bool>)wgl.GetMethod("IsValid", BindingFlags.NonPublic | BindingFlags.Static).CreateDelegate(typeof(Func<IntPtr, bool>));
            // INSTANCE (still invisible!) METHODS
            //  Protected
            wglGetAddress = (Func<string, IntPtr>)wgl.GetMethod("GetAddress", BindingFlags.NonPublic | BindingFlags.Instance).CreateDelegate(typeof(Func<string, IntPtr>), wglInstance);

            if (!wglSupportsExtension("WGL_NV_DX_interop"))
                throw new NotSupportedException("OpenGL (WGL) doesn't support WGL_NV_DX_interop. Can't use WPF with OpenGL.");

            this.WglDXCloseDeviceNV = fetch<wglDXCloseDeviceNV>();
            this.WglDXLockObjectsNV = fetch<wglDXLockObjectsNV>();
            this.WglDXObjectAccessNV = fetch<wglDXObjectAccessNV>();
            this.WglDXOpenDeviceNV = fetch<wglDXOpenDeviceNV>();
            this.WglDXRegisterObjectNV = fetch<wglDXRegisterObjectNV>();
            this.WglDXSetResourceShareHandleNV = fetch<wglDXSetResourceShareHandleNV>();
            this.WglDXUnlockObjectsNV = fetch<wglDXUnlockObjectsNV>();
            this.WglDXUnregisterObjectNV = fetch<wglDXUnregisterObjectNV>();

            // calling getaddress'd thing:
            // [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            // private delegate int MultiplyByTen(int num);
            //
            // IntPtr pAddr=..getprocadddr..
            // MultiplyByTen multiplyByTen = (MultiplyByTen) Marshal.GetDelegateForFunctionPointer(pAddr, typeof(MultiplyByTen));

        }
    }
}
