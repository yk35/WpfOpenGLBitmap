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

namespace WpfOpenGLD3DImageExample
{
    using System.Windows.Interop;

    using OpenTK.Graphics.OpenGL;

    using WpfOpenGLD3DImage;

    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private OpenGLD3DImage glImage;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            
        }

        private void Image_OnLoaded(object sender, RoutedEventArgs e)
        {
            this.glImage = new OpenGLD3DImage(this.d3dImage);
            CompositionTarget.Rendering += OnCompositionRendering;
        }

        private void OnCompositionRendering(object sender, EventArgs e)
        {
            this.glImage.RenderGLToD3dImage(this.d3dImage, (int)this.container.ActualWidth, (int)this.container.ActualHeight,
                () =>
                {
                    GL.Viewport(0, 0, (int)this.image.ActualWidth, (int)this.image.ActualHeight);
                    GL.ClearColor(1, 1, 0, 1);
                    GL.Clear(ClearBufferMask.ColorBufferBit);
                });
        }

        private void D3dImage_OnIsFrontBufferAvailableChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (this.d3dImage.IsFrontBufferAvailable)
            {
                CompositionTarget.Rendering += OnCompositionRendering;
            }
            else
            {
                CompositionTarget.Rendering -= OnCompositionRendering;
            }
        }

    }
}
