using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfOpenGLBitmap
{
    using System.Drawing;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;

    /// <summary>
    /// BitmapSourceUpdater
    /// </summary>
    public interface IWpfOpenGLBitmapSource
    {
        IAsyncResult BeginRender(Action renderAction, ref ImageSource backbuffer);

        ImageSource EndRender(IAsyncResult asyncResult);

        Size Size { get; set; }
    }
}
