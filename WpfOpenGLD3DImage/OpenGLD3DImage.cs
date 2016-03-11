using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfOpenGLD3DImage
{
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Windows;
    using System.Windows.Interop;

    using OpenTK;
    using OpenTK.Graphics;
    using OpenTK.Graphics.OpenGL;

    using SharpDX.Direct3D9;

    // http://halogenica.net/sharing-resources-between-directx-and-opengl/
    // ^^ DING DING DING DING DING!!! Both AMD and Intel have implemented wgl_nv_dx_interop. (..interop2 for dx10+, but I really don't think that matters to us...)
    // A (GPU) copy is necessary, and a bit of synchronization, but I think we can pull it off.
    //
    // https://www.opengl.org/registry/specs/NV/DX_interop.txt includes sample code, for gods' sakes... how can we NOT do this.
    // 
    // don't forget the gpu stalling rules; halogenica says they apply here. ideally lock (frame n)'s render target when we've finished (frame n+1).
    // To actually render OpenGL offscreen we create an always-invisible native window.
    // (You're really not supposed to mix D3D and OpenGL in the same window, from what I can tell. DWM will spaz out.)
    // Re. multithreading: http://blog.gvnott.com/some-usefull-facts-about-multipul-opengl-contexts/
    // > You can however achieve significant performance improvements by using a second thread for data streaming (see the Performance section below).
    // ...actually, it's probably better to have one OpenGL thread and just do async uploads/downloads.
    // https://msdn.microsoft.com/en-us/library/cc656785(v=vs.110).aspx
    // ^ has LOTS of considerations, performance and otherwise, that we ignore here!!
    public class OpenGLD3DImage
    {
        private GLControl glControl;

        private DeviceEx device;

        private Surface colorBuffer;

        private Surface sharedSurface;

        private WGL_NV_DX_interop wgl;

        private IntPtr wglHandleDevice;

        private int glSharedSurface;

        private IntPtr wglHandleSharedSurface;

        private IntPtr[] singleWglHandleSharedSurfaceArray;

        private int fbo;

        [DllImport("user32.dll", SetLastError = false)]
        private static extern IntPtr GetDesktopWindow();

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenGLD3DImage"/> class.
        /// </summary>
        public OpenGLD3DImage(D3DImage image)
        {
            this.glControl = new GLControl(new GraphicsMode(DisplayDevice.Default.BitsPerPixel, 16, 0, 4, 0, 2, false));
            this.glControl.MakeCurrent();
            this.createD3D9ExContext();
            image.Lock();
            image.SetBackBuffer(D3DResourceType.IDirect3DSurface9, colorBuffer.NativePointer);
            image.AddDirtyRect(new Int32Rect(0, 0, 400, 300)); 
            image.Unlock();


        }

        /// <summary>
        /// The render by OpenGL.
        /// </summary>
        /// <param name="rendering">
        /// The rendering.
        /// </param>
        public void RenderGLToD3dImage(D3DImage image, int w, int h, Action rendering)
        {
            if (w == 0 || h == 0)
            {
                return;
            }
            this.glControl.MakeCurrent();

            // resize D3D/OpenGL Surface if need
            //ResizeIfNeed(w, h);

            // OnRender may be called twice in the same frame. Only render the first time.
            if (image.IsFrontBufferAvailable)
            {
                try
                {
                    image.Lock();
                    image.SetBackBuffer(D3DResourceType.IDirect3DSurface9, colorBuffer.NativePointer);

                    //device.Clear(ClearFlags.Target, new SharpDX.ColorBGRA(1f, 1f, 0f, 1f), 0f, 0);

                    // resize

                    wgl.WglDXLockObjectsNV(wglHandleDevice, 1, singleWglHandleSharedSurfaceArray);
                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
                    rendering();
                    //GL.Flush();

                    wgl.WglDXUnlockObjectsNV(wglHandleDevice, 1, singleWglHandleSharedSurfaceArray);

                    device.StretchRectangle(sharedSurface, colorBuffer, TextureFilter.Linear);

                    device.Present();

                    // !!!!!!
                    image.AddDirtyRect(new Int32Rect(0, 0, 400, 300));
                }
                finally
                {
                    image.Unlock();
                }
            }
        }

        private void ResizeIfNeed(int w, int h)
        {
            if (w == 0 || h == 0)
            {
                return;
            }
            if (w != this.sharedSurface.Description.Width || h != this.sharedSurface.Description.Height)
            {
                wgl.WglDXUnregisterObjectNV(this.wglHandleDevice, wglHandleSharedSurface);
                colorBuffer.Dispose();
                colorBuffer = null;
                sharedSurface.Dispose();
                sharedSurface = null;
                // resizing
                var tex = new Texture(device, w, h, 1, Usage.RenderTarget, Format.A8R8G8B8, Pool.Default);
                colorBuffer = tex.GetSurfaceLevel(0);

                IntPtr sharedSurfaceShareHandle = IntPtr.Zero;

                //IntPtr colorBufferShareHandle = IntPtr.Zero;
                // create offscreen plane on D3D9
                sharedSurface = Surface.CreateOffscreenPlainEx(
                    device,
                    w,
                    h,
                    Format.A8R8G8B8,
                    Pool.Default,
                    Usage.None,
                    ref sharedSurfaceShareHandle);
                //Console.WriteLine("ColorBufferShareHandle: " + colorBufferShareHandle.ToInt64());
                device.SetRenderTarget(0, colorBuffer);

                //GL.BindTexture(TextureTarget.Texture2D, this.glSharedSurface);
                //GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, w, h, 0, PixelFormat.AbgrExt, PixelType.Byte, IntPtr.Zero);

                wglHandleSharedSurface = wgl.WglDXRegisterObjectNV(wglHandleDevice, sharedSurface.NativePointer, (uint)glSharedSurface, (uint)TextureTarget.Texture2D, WGL_NV_DX_interop.WGL_ACCESS_WRITE_DISCARD_NV);
                singleWglHandleSharedSurfaceArray = new IntPtr[] { wglHandleSharedSurface };
            }
        }

        private void createD3D9ExContext()
        {
            var d3d = new Direct3DEx();

            int initW = 400;
            int initH = 300;

            PresentParameters presentparams = new PresentParameters();
            presentparams.Windowed = true;
            presentparams.SwapEffect = SwapEffect.Discard;
            presentparams.DeviceWindowHandle = GetDesktopWindow();
            presentparams.PresentationInterval = PresentInterval.Default;
            // FpuPreserve for WPF
            // Multithreaded so that resources are actually sharable between DX and GL
            device = new DeviceEx(
                d3d,
                0,
                DeviceType.Hardware,
                IntPtr.Zero,
                CreateFlags.HardwareVertexProcessing | CreateFlags.Multithreaded | CreateFlags.FpuPreserve,
                presentparams);

            var tex = new Texture(device, initW, initH, 1, Usage.RenderTarget, Format.A8R8G8B8, Pool.Default);
            colorBuffer = tex.GetSurfaceLevel(0);

            IntPtr sharedSurfaceShareHandle = IntPtr.Zero;

            //IntPtr colorBufferShareHandle = IntPtr.Zero;
            // create offscreen plane on D3D9

            sharedSurface = Surface.CreateOffscreenPlainEx(
                device,
                initW,
                initH,
                Format.A8R8G8B8,
                Pool.Default,
                Usage.None,
                ref sharedSurfaceShareHandle);
            //Console.WriteLine("ColorBufferShareHandle: " + colorBufferShareHandle.ToInt64());
            //Surface.CreateOffscreenPlainEx()
            //wgl.WglDXSetResourceShareHandleNV()

            #region OpenGL Interop
            wgl = new WGL_NV_DX_interop();
            if (!wgl.WglDXSetResourceShareHandleNV(sharedSurface.NativePointer, sharedSurfaceShareHandle))
            {
                throw new Exception("failed wglDXSetResourceShareHandleNV");
            }
            wglHandleDevice = wgl.WglDXOpenDeviceNV(device.NativePointer);

            glSharedSurface = GL.GenTexture();
            //glColorBuffer = GL.GenFramebuffer();
            //GL.BindTexture(TextureTarget.Texture2D, this.glSharedSurface);
            //GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, initW, initH, 0, PixelFormat.AbgrExt, PixelType.Byte, IntPtr.Zero);

            fbo = GL.GenFramebuffer();

            wglHandleSharedSurface = wgl.WglDXRegisterObjectNV(wglHandleDevice, sharedSurface.NativePointer, (uint)glSharedSurface, (uint)TextureTarget.Texture2D, WGL_NV_DX_interop.WGL_ACCESS_WRITE_DISCARD_NV);
            singleWglHandleSharedSurfaceArray = new IntPtr[] { wglHandleSharedSurface };

            //wgl.WglDXLockObjectsNV(wglHandleDevice, 1, singleWglHandleColorBufferArray);

            Console.WriteLine(GL.GetError());
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            Console.WriteLine(GL.GetError());
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, glSharedSurface, 0);
            Console.WriteLine(GL.GetError());

            GL.DrawBuffer((DrawBufferMode)FramebufferAttachment.ColorAttachment0);
            #endregion

            device.SetRenderTarget(0, colorBuffer);

            //wgl.WglDXUnlockObjectsNV(wglHandleDevice, 1, singleWglHandleColorBufferArray);


            // depth, stencil attachment? cba

        }
    }
}
