using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfOpenGLBitmap
{
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;

    using WpfOpenGLBitmap.Helpers;

    using OpenTK;
    using OpenTK.Graphics;
    using OpenTK.Graphics.OpenGL;

    using PixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;
    using Size = System.Drawing.Size;

    /// <summary>
    /// Render to Offscreen using OpenTK(OpenGL) and write to WriteableBitmap.  
    /// </summary>
    public class OpenGLWriteableBitmapUpdater : IDisposable, IWpfOpenGLBitmapSource
    {
        #region fields
        private int depthbufferId;

        private int framebufferId;

        private int colorbufferId;

        private int pixelbufferId; 

        private GLControl glControl;

        private bool loaded;

        private MessagingTask messagingTask = new MessagingTask();

        private Size framebufferSize;

        private event Action<GLControl> OnPrepare;

        private event Action<GLControl> OnFinalize;

        #endregion

        #region perperties
        /// <summary>
        /// framebuffer size
        /// </summary>
        public Size Size { get; set; }

        #endregion

        /// <summary>
        /// constructor
        /// </summary>
        public OpenGLWriteableBitmapUpdater(Action<GLControl> onPrepare = null, Action<GLControl> onFinalize = null)
            : this(new Size(100, 100), onPrepare ,onFinalize)
        {
            
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public OpenGLWriteableBitmapUpdater(Size size, Action<GLControl> onPrepare = null, Action<GLControl> onFinalize = null)
            : this(size, new GraphicsMode(DisplayDevice.Default.BitsPerPixel, 16, 0, 4, 0, 2, false), onPrepare, onFinalize)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public OpenGLWriteableBitmapUpdater(Size size, GraphicsMode graphicsMode, Action<GLControl> onPrepare = null, Action<GLControl> onFinalize = null)
        {
            this.loaded = false;
            this.Size = size;
            this.framebufferId = -1;

            OnPrepare += onPrepare;
            OnFinalize += onFinalize;

            messagingTask.StartMessageLoop(
                prepare: () =>
                {
                    this.glControl = new GLControl(graphicsMode);
                    this.glControl.MakeCurrent();
                    if (OnPrepare != null)
                    {
                        OnPrepare(this.glControl);
                        OnPrepare -= onPrepare;
                    }
                },
                finalize: () =>
                {
                    if (OnFinalize != null)
                    {
                        OnFinalize(this.glControl);
                        OnFinalize -= onFinalize;
                    }
                    this.glControl.Context.MakeCurrent(null);
                    this.glControl.Dispose();
                });
        }

        /// <summary>
        /// stop rendering thread
        /// </summary>
        public void Dispose()
        {
            messagingTask.Dispose();
            messagingTask = null;
        }

        /// <summary>
        /// start render on render thread
        /// </summary>
        /// <param name="renderAction">request render per frame</param>
        /// <param name="backbuffer">writing frame bitmap</param>
        /// <returns>async result</returns>
        public IAsyncResult BeginRender(Action renderAction, ref ImageSource backbuffer)
        {
            if (backbuffer == null || backbuffer.Width != this.Size.Width || backbuffer.Height != this.Size.Height)
            {
                backbuffer = new WriteableBitmap(
                    this.Size.Width,
                    this.Size.Height,
                    96,
                    96,
                    PixelFormats.Bgra32,
                    BitmapPalettes.WebPalette);
            }
            var resultBuffer = (WriteableBitmap)backbuffer;
            resultBuffer.Lock();
            IntPtr bufferPtr = resultBuffer.BackBuffer;
            var curSize = Size;
            var asyncResult = messagingTask.BeginAsyncAction(
                () =>
                    {
                        Prepare(curSize);
                        renderAction();
                        GL.Finish();
                        Cleanup(curSize, bufferPtr);
                    });
            (asyncResult as AsyncResult).AsyncState = resultBuffer;
            return asyncResult;
        }

        /// <summary>
        /// finish render request
        /// </summary>
        /// <param name="asyncResult">IAsyncResult that returns BeginRender()</param>
        /// <returns>bitmap that pass at BeginRender()</returns>
        public ImageSource EndRender(IAsyncResult asyncResult)
        {
            if (messagingTask.EndAysncAction(asyncResult)) 
            {
                var bmp = (WriteableBitmap)asyncResult.AsyncState;
                bmp.AddDirtyRect(new Int32Rect(0, 0, (int)bmp.Width, (int)bmp.Height));
                bmp.Unlock();
                return bmp;
            }
            else
            {
                return null;
            }
            
        }

        #region private methods
        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        public static extern void CopyMemory(IntPtr dest, IntPtr src, uint count);

        private void Cleanup(Size size, IntPtr backbufferPtr)
        {
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            IntPtr glPixel = IntPtr.Zero;
            var bufferSize = size.Width * size.Height * 4;
            GL.ReadPixels(0, 0, size.Width, size.Height, PixelFormat.Bgra, PixelType.UnsignedByte, IntPtr.Zero);

            glPixel = GL.MapBuffer(BufferTarget.PixelPackBuffer, BufferAccess.ReadOnly);
            CopyMemory(backbufferPtr, glPixel, (uint)bufferSize);
            GL.UnmapBuffer(BufferTarget.PixelPackBuffer);
        }

        private void Prepare(Size framebuffersize)
        {
            if (GraphicsContext.CurrentContext != this.glControl.Context)
            {
                this.glControl.MakeCurrent();
            }

            if (framebuffersize != this.framebufferSize || this.loaded == false)
            {
                this.framebufferSize = framebuffersize;
                this.CreateFramebuffer();
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, this.framebufferId);
        }

        private void CreateFramebuffer()
        {
            this.glControl.MakeCurrent();

            if (this.framebufferId > 0)
            {
                GL.DeleteFramebuffer(this.framebufferId);
            }

            if (this.colorbufferId > 0)
            {
                GL.DeleteRenderbuffer(this.colorbufferId);
            }

            if (this.depthbufferId > 0)
            {
                GL.DeleteRenderbuffer(this.depthbufferId);
            }
            if (this.pixelbufferId > 0)
            {
                GL.DeleteBuffer(pixelbufferId);
            }

            pixelbufferId = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.PixelPackBuffer, pixelbufferId);
            GL.BufferData(BufferTarget.PixelPackBuffer, (IntPtr)(framebufferSize.Width * framebufferSize.Height * 4), IntPtr.Zero, BufferUsageHint.StreamRead);
            GL.BindBuffer(BufferTarget.PixelPackBuffer, 0);

            this.framebufferId = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, this.framebufferId);

            this.colorbufferId = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, this.colorbufferId);
            GL.RenderbufferStorage(
                RenderbufferTarget.Renderbuffer,
                RenderbufferStorage.Rgba8,
                this.framebufferSize.Width,
                this.framebufferSize.Height);
            GL.FramebufferRenderbuffer(
                FramebufferTarget.Framebuffer, 
                FramebufferAttachment.ColorAttachment0, 
                RenderbufferTarget.Renderbuffer, 
                this.colorbufferId);

            this.depthbufferId = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, this.depthbufferId);
            GL.RenderbufferStorage(
                RenderbufferTarget.Renderbuffer,
                RenderbufferStorage.DepthComponent24,
                this.framebufferSize.Width,
                this.framebufferSize.Height);
            GL.FramebufferRenderbuffer(
                FramebufferTarget.Framebuffer,
                FramebufferAttachment.DepthAttachment,
                RenderbufferTarget.Renderbuffer,
                this.depthbufferId);

            FramebufferErrorCode error = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (error != FramebufferErrorCode.FramebufferComplete)
            {
                throw new Exception();
            }

            this.loaded = true;
            GL.BindBuffer(BufferTarget.PixelPackBuffer, pixelbufferId);
        }
        #endregion
    }


}

