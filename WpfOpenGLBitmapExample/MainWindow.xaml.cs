using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace OpenGLBitmapSourceExample
{
    using System.Windows.Threading;

    using OpenTK.Graphics.OpenGL;

    using WpfOpenGLBitmap;

    using Size = System.Drawing.Size;

    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Fields

        private ImageSource backbuffer;

        private IWpfOpenGLBitmapSource bmpSource;

        private int frames;

        private DateTime lastMeasureTime;

        private Renderer renderer;

        private IAsyncResult asyncResult;

        #endregion
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            //bmpSource = new OpenGLWriteableBitmapUpdater();
            bmpSource = new OpenGLD3DImageUpdater();
            asyncResult = null;
            int idx = 0;
            renderer = new Renderer();
            CompositionTarget.Rendering += (o, args) =>
            {
                if (DateTime.Now.Subtract(this.lastMeasureTime) > TimeSpan.FromSeconds(1))
                {
                    this.Title = this.frames + "fps";
                    this.frames = 0;
                    this.lastMeasureTime = DateTime.Now;
                }
                if (this.asyncResult == null || this.asyncResult.IsCompleted)
                {
                    this.frames++;
                    image.Source = bmpSource.EndRender(asyncResult);
                    var actualWidth = this.ActualWidth;
                    var actualHeight = this.ActualHeight;
                    bmpSource.Size = new Size((int)actualWidth, (int)actualHeight);
                    this.asyncResult = bmpSource.BeginRender(
                        () =>
                        {
                            GL.MatrixMode(MatrixMode.Projection);
                            GL.LoadIdentity();
                            float halfWidth = (float)(actualWidth / 2);
                            float halfHeight = (float)(actualHeight / 2);
                            GL.Ortho(-halfWidth, halfWidth, halfHeight, -halfHeight, 1000, -1000);
                            GL.Viewport(0, 0, (int)actualWidth, (int)actualHeight);

                            this.renderer.Render();
                        },
                        ref backbuffer);
                }
            };
        }
    }
}
