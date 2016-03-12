﻿namespace OpenGLBitmapSourceExample
{
    using System;
    using System.Drawing;

    using OpenTK;
    using OpenTK.Graphics.OpenGL;

    /// <summary>
    /// The renderer.
    /// </summary>
    public class Renderer
    {
        #region Fields

        private float angle;

        private int displayList;

        #endregion

        #region Constructors and Destructors

        public Renderer()
        {
        }

        #endregion

        #region Public Methods and Operators

        public void Render()
        {
            if (this.displayList <= 0)
            {
                this.displayList = GL.GenLists(1);
                GL.NewList(this.displayList, ListMode.Compile);

                GL.Color3(Color.Red);

                GL.Begin(PrimitiveType.Points);

                Random rnd = new Random();
                for (int i = 0; i < 1000000; i++)
                {
                    float factor = 0.2f;
                    Vector3 position = new Vector3(
                        rnd.Next(-1000, 1000) * factor,
                        rnd.Next(-1000, 1000) * factor,
                        rnd.Next(-1000, 1000) * factor);
                    GL.Vertex3(position);

                    position.Normalize();
                    GL.Normal3(position);
                }

                GL.End();

                GL.EndList();
            }

            GL.Enable(EnableCap.Lighting);
            GL.Enable(EnableCap.Light0);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
            GL.Enable(EnableCap.DepthTest);

            //GL.ClearColor(Color.LightBlue);
            GL.ClearColor(0.0f, 0.3f, 0.0f, 0.3f);
            GL.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);

            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();

            this.angle += 1f;
            GL.Rotate(this.angle, Vector3.UnitZ);
            GL.Rotate(this.angle, Vector3.UnitY);
            GL.Rotate(this.angle, Vector3.UnitX);
            GL.Translate(0.5f, 0, 0);

            GL.CallList(this.displayList);
        }

        #endregion
    }
}
