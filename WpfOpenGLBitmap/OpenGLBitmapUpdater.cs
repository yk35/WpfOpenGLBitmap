﻿using System;
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

    public class OpenGLBitmapUpdater : IDisposable
    {
        #region fields
        private int depthbufferId;

        private int framebufferId;

        private int colorbufferId;

        private int[] pixelbufferId = new int[2];

        private GLControl glControl;

        private bool loaded;

        private MessagingTask messagingTask = new MessagingTask();

        private Size framebufferSize;

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
        public OpenGLBitmapUpdater()
            : this(new Size(100, 100))
        {
            
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public OpenGLBitmapUpdater(Size size)
            : this(size, new GraphicsMode(DisplayDevice.Default.BitsPerPixel, 16, 0, 4, 0, 2, false))
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public OpenGLBitmapUpdater(Size size, GraphicsMode graphicsMode)
        {
            this.loaded = false;
            this.Size = size;
            this.framebufferId = -1;

            messagingTask.StartMessageLoop(
                prepare: () =>
                {
                    this.glControl = new GLControl(graphicsMode);
                    this.glControl.MakeCurrent();
                },
                finalize: () =>
                {
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
        public IAsyncResult BeginRender(Action renderAction, ref WriteableBitmap backbuffer)
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
            var resultBuffer = backbuffer;
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
            (asyncResult as MessagingTask.AsyncResult).AsyncState = resultBuffer;
            return asyncResult;
        }

        /// <summary>
        /// finish render request
        /// </summary>
        /// <param name="asyncResult">IAsyncResult that returns BeginRender()</param>
        /// <returns>bitmap that pass at BeginRender()</returns>
        public WriteableBitmap EndRender(IAsyncResult asyncResult)
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
            var halfHeight = size.Height >> 1;
            GL.BindBuffer(BufferTarget.PixelPackBuffer, pixelbufferId[0]);
            GL.ReadPixels(
                0,
                0,
                size.Width,
                halfHeight,
                PixelFormat.Bgra,
                PixelType.UnsignedByte,
                IntPtr.Zero);
            GL.BindBuffer(BufferTarget.PixelPackBuffer, pixelbufferId[1]);
            GL.ReadPixels(
                0,
                halfHeight,
                size.Width,
                size.Height - halfHeight,
                PixelFormat.Bgra,
                PixelType.UnsignedByte,
                IntPtr.Zero);
            IntPtr glPixel = IntPtr.Zero;
            var bufferSize = size.Width * halfHeight * 4;
            var lastBufferSize = size.Width * (size.Height - halfHeight) * 4;

            GL.BindBuffer(BufferTarget.PixelPackBuffer, pixelbufferId[0]);
            glPixel = GL.MapBuffer(BufferTarget.PixelPackBuffer, BufferAccess.ReadOnly);
            GL.UnmapBuffer(BufferTarget.PixelPackBuffer);
            CopyMemory(backbufferPtr, glPixel, (uint)bufferSize);

            GL.BindBuffer(BufferTarget.PixelPackBuffer, pixelbufferId[1]);
            glPixel = GL.MapBuffer(BufferTarget.PixelPackBuffer, BufferAccess.ReadOnly);
            CopyMemory(backbufferPtr+ bufferSize, glPixel, (uint)lastBufferSize);
            GL.UnmapBuffer(BufferTarget.PixelPackBuffer);
            GL.BindBuffer(BufferTarget.PixelPackBuffer, 0);
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
            if (this.pixelbufferId[0] > 0)
            {
                GL.DeleteBuffers(2, pixelbufferId);
            }

            var halfHeight = framebufferSize.Height >> 1;
            GL.GenBuffers(2, pixelbufferId);
            GL.BindBuffer(BufferTarget.PixelPackBuffer, pixelbufferId[0]);
            GL.BufferData(BufferTarget.PixelPackBuffer, (IntPtr)(framebufferSize.Width * halfHeight * 4), IntPtr.Zero, BufferUsageHint.StreamRead);
            GL.BindBuffer(BufferTarget.PixelPackBuffer, pixelbufferId[1]);
            GL.BufferData(BufferTarget.PixelPackBuffer, (IntPtr)(framebufferSize.Width * (framebufferSize.Height - halfHeight) * 4), IntPtr.Zero, BufferUsageHint.StreamRead);
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
        }
        #endregion
    }
}
