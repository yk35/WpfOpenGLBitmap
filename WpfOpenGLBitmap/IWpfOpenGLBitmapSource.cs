using System;

namespace WpfOpenGLBitmap
{
    using System.Drawing;
    using System.Windows.Media;

    /// <summary>
    /// BitmapSourceUpdater interface
    /// </summary>
    public interface IWpfOpenGLBitmapSource
    {
        /// <summary>
        /// start render
        /// </summary>
        /// <param name="renderAction">rendering code</param>
        /// <param name="backbuffer">write to image</param>
        /// <returns></returns>
        IAsyncResult BeginRender(Action renderAction, ref ImageSource backbuffer);

        /// <summary>
        /// finish render
        /// </summary>
        /// <param name="asyncResult"></param>
        /// <returns>image</returns>
        ImageSource EndRender(IAsyncResult asyncResult);

        /// <summary>
        /// set or get render size
        /// </summary>
        Size Size { get; set; }
    }
}
