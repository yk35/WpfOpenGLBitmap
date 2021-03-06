﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfOpenGLBitmap
{
    using System.Windows;
    using System.Windows.Interop;
    using System.Windows.Media;

    using OpenTK;
    using OpenTK.Graphics;
    using OpenTK.Graphics.OpenGL;

    using SharpDX.Direct3D9;

    using WpfOpenGLBitmap.Helpers;

    using Size = System.Drawing.Size;

    /// <summary>
    /// Render to Offscreen using OpenTK(OpenGL) and write to D3DImage using NV_DX_interop.  
    /// </summary>
    public class OpenGLD3DImageUpdater : IDisposable, IWpfOpenGLBitmapSource
    {
        #region fields
        private GLControl glControl;

        private DeviceEx device;

        private Surface sharedSurface;

        private WGL_NV_DX_interop wgl;

        private IntPtr wglHandleDevice;

        private int glSharedSurface;

        private IntPtr wglHandleSharedSurface;

        private IntPtr[] singleWglHandleSharedSurfaceArray;

        private int fbo;

        private PresentParameters presentparams;

        #endregion

        public void Dispose()
        {
            if (this.sharedSurface != null)
            {
                this.sharedSurface.Dispose();
                this.sharedSurface = null;
                this.device.Dispose();
                this.device = null;
            }
            if (this.glControl != null)
            {
                this.glControl.Dispose();
                this.glControl = null;
            }
        }

        public IAsyncResult BeginRender(Action renderAction, ref ImageSource backbuffer)
        {
            if (backbuffer == null)
            {
                backbuffer = new D3DImage(96, 96);
            }
            var asyncResult = new AsyncResult();
            asyncResult.AsyncState = backbuffer;

            // rendering
            RenderGLToD3dImage((D3DImage)backbuffer, Size.Width, Size.Height, renderAction);

            // finished rendering
            asyncResult._handle.Set();
            return asyncResult;
        }

        public ImageSource EndRender(IAsyncResult asyncResult)
        {
            if (asyncResult == null)
            {
                return null;
            }
            var result = (ImageSource)asyncResult.AsyncState;
            ((AsyncResult)asyncResult).Dispose();
            return result;
        }

        public Size Size { get; set; }

        /// <summary>
        /// constructor
        /// </summary>
        public OpenGLD3DImageUpdater()
            : this(new Size(100, 100))
        {
            
        }

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="size">initialize surface size</param>
        public OpenGLD3DImageUpdater(Size size)
            : this(size, new GraphicsMode(DisplayDevice.Default.BitsPerPixel, 24, 0, 4, 0, 2, false))
        {
        }

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="size">initialize surface size</param>
        /// <param name="graphicsMode">OpenGL graphics mode</param>
        public OpenGLD3DImageUpdater(
            Size size,
            GraphicsMode graphicsMode)
        {
            this.glControl = new GLControl(graphicsMode);
            this.glControl.MakeCurrent();
            Size = size;
            this.CreateD3D9ExContext(this.glControl.Handle, graphicsMode.Depth > 0, graphicsMode.Stencil > 0);
        }

        #region private methods
        /// <summary>
        /// Create D3D9Ex context and OpenGL framebuffer
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="hasDepthBuffer"></param>
        /// <param name="hasStencilBuffer"></param>
        private void CreateD3D9ExContext(IntPtr handle, bool hasDepthBuffer, bool hasStencilBuffer)
        {
            var d3d = new Direct3DEx();

            int initW = Size.Width;
            int initH = Size.Height;

            this.presentparams = new PresentParameters();
            this.presentparams.Windowed = true;
            this.presentparams.SwapEffect = SwapEffect.Discard;
            this.presentparams.DeviceWindowHandle = handle;
            this.presentparams.PresentationInterval = PresentInterval.Default;
            // FpuPreserve for WPF
            // Multithreaded so that resources are actually sharable between DX and GL
            this.device = new DeviceEx(
                d3d,
                0,
                DeviceType.Hardware,
                IntPtr.Zero,
                CreateFlags.HardwareVertexProcessing | CreateFlags.Multithreaded | CreateFlags.FpuPreserve,
                this.presentparams);

            // create shared surface
            this.sharedSurface = Surface.CreateRenderTarget(
                this.device,
                initW,
                initH,
                Format.A8R8G8B8,
                MultisampleType.None,
                0,
                false);

            #region OpenGL Interop

            this.wgl = new WGL_NV_DX_interop();
            this.wglHandleDevice = wgl.WglDXOpenDeviceNV(device.NativePointer);
            this.glSharedSurface = GL.GenTexture();
            this.fbo = GL.GenFramebuffer();

            // refer D3D9 surface and OpenGL Texture2D
            this.wglHandleSharedSurface = wgl.WglDXRegisterObjectNV(
                this.wglHandleDevice,
                this.sharedSurface.NativePointer,
                (uint)this.glSharedSurface,
                (uint)TextureTarget.Texture2D,
                WGL_NV_DX_interop.WGL_ACCESS_READ_WRITE_NV);
            this.singleWglHandleSharedSurfaceArray = new IntPtr[] { this.wglHandleSharedSurface };

            // setup framebuffer
            Console.WriteLine(GL.GetError());
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, this.fbo);
            Console.WriteLine(GL.GetError());
            GL.FramebufferTexture2D(
                FramebufferTarget.Framebuffer,
                FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D,
                this.glSharedSurface,
                0);
            Console.WriteLine(GL.GetError());
            if (hasDepthBuffer)
            {
                var depth = GL.GenRenderbuffer();
                GL.FramebufferRenderbuffer(
                    FramebufferTarget.Framebuffer,
                    FramebufferAttachment.DepthAttachment,
                    RenderbufferTarget.Renderbuffer,
                    depth);
                GL.DeleteRenderbuffer(depth);
            }
            if (hasStencilBuffer)
            {
                var stencil = GL.GenRenderbuffer();
                GL.FramebufferRenderbuffer(
                    FramebufferTarget.Framebuffer,
                    FramebufferAttachment.StencilAttachment,
                    RenderbufferTarget.Renderbuffer,
                    stencil);
                GL.DeleteRenderbuffer(stencil);
            }

            GL.DrawBuffer((DrawBufferMode)FramebufferAttachment.ColorAttachment0);

            #endregion

        }

        /// <summary>
        /// Resize shared surface if need
        /// </summary>
        /// <param name="w"></param>
        /// <param name="h"></param>
        private void ResizeIfNeed(int w, int h)
        {
            if (w == 0 || h == 0)
            {
                return;
            }
            if (w != this.sharedSurface.Description.Width || h != this.sharedSurface.Description.Height)
            {
                this.wgl.WglDXUnregisterObjectNV(this.wglHandleDevice, this.wglHandleSharedSurface);
                this.sharedSurface.Dispose();
                this.sharedSurface = null;
                this.sharedSurface = Surface.CreateRenderTarget(
                    this.device,
                    w,
                    h,
                    Format.A8R8G8B8,
                    MultisampleType.None,
                    0,
                    false);
                this.wglHandleSharedSurface = this.wgl.WglDXRegisterObjectNV(
                    this.wglHandleDevice,
                    this.sharedSurface.NativePointer,
                    (uint)this.glSharedSurface,
                    (uint)TextureTarget.Texture2D,
                    WGL_NV_DX_interop.WGL_ACCESS_READ_WRITE_NV);
                this.singleWglHandleSharedSurfaceArray = new IntPtr[] { this.wglHandleSharedSurface };
            }
        }

        /// <summary>
        /// The render by OpenGL.
        /// </summary>
        private void RenderGLToD3dImage(D3DImage image, int w, int h, Action rendering)
        {
            if (w == 0 || h == 0)
            {
                return;
            }
            this.glControl.MakeCurrent();

            // resize D3D/OpenGL Surface if need
            this.ResizeIfNeed(w, h);

            // OnRender may be called twice in the same frame. Only render the first time.
            if (image.IsFrontBufferAvailable)
            {
                // render to sharedSurface using OpenGL
                this.wgl.WglDXLockObjectsNV(wglHandleDevice, 1, singleWglHandleSharedSurfaceArray);
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
                GL.DrawBuffer((DrawBufferMode)FramebufferAttachment.ColorAttachment0);
                rendering();
                GL.Finish();
                this.wgl.WglDXUnlockObjectsNV(wglHandleDevice, 1, singleWglHandleSharedSurfaceArray);

                try
                {
                    image.Lock();
                    image.SetBackBuffer(D3DResourceType.IDirect3DSurface9, sharedSurface.NativePointer);
                    image.AddDirtyRect(new Int32Rect(0, 0, w, h));
                }
                catch (Exception ex)
                {
                    // ???
                    Console.WriteLine(ex.ToString());
                    this.device.ResetEx(ref this.presentparams);
                }
                finally
                {
                    image.Unlock();
                }
            }
        }
        #endregion
    }
}
