using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace Алясева_tomogram_visualizer
{
    public partial class Form1 : Form
    {
        bool loaded = false;
        Bin bin = new Bin();
        View view = new View();
        static int currentLayer = 0;
        int FrameCount;
        DateTime NextFPSUpdate = DateTime.Now.AddSeconds(1);
        bool checkTexture, checkQuads;
        static int currentMin;
        static int currentWidthTF;

        void displayFPS()
        {
            if(DateTime.Now >= NextFPSUpdate)
            {
                this.Text = String.Format("CT Visualizer (fps={0})", FrameCount);
                NextFPSUpdate = DateTime.Now.AddSeconds(1);
                FrameCount = 0;
            }
            FrameCount++;
        }
        public Form1()
        {
            InitializeComponent();
        }

        class Bin
        {
            public static int X, Y, Z;
            public static short[] array;
            public Bin() { }
            public void readBIN(string path)
            {
                if (File.Exists(path))
                {
                    BinaryReader reader =
                        new BinaryReader(File.Open(path, FileMode.Open));
                    X = reader.ReadInt32();
                    Y = reader.ReadInt32();
                    Z = reader.ReadInt32();

                    int arraySize = X * Y * Z;
                    array = new short[arraySize];
                    for (int i = 0; i < arraySize; i++)
                    {
                        array[i] = reader.ReadInt16();
                    }
                }
            }
        }
        class View
        {
            int VBOtexture;
            Bitmap textureImage;
            
            public void generateTextureImage(int layerNumber)
            {
                textureImage = new Bitmap(Bin.X, Bin.Y);
                for(int i = 0; i < Bin.X; i++)
                {
                    for(int j = 0; j < Bin.Y; j++)
                    {
                        int pixelNumber = i + j * Bin.X + layerNumber * Bin.X * Bin.Y;
                        textureImage.SetPixel(i, j, TransferFunction(Bin.array[pixelNumber], currentMin, currentWidthTF));
                    }
                }
            }

            public void DrawTexture()
            {
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                GL.Enable(EnableCap.Texture2D);
                GL.BindTexture(TextureTarget.Texture2D, VBOtexture);

                GL.Begin(BeginMode.Quads);
                GL.Color3(Color.White);
                GL.TexCoord2(0f, 0f);
                GL.Vertex2(0, 0);
                GL.TexCoord2(0f, 1f);
                GL.Vertex2(0, Bin.Y);
                GL.TexCoord2(1f, 1f);
                GL.Vertex2(Bin.X, Bin.Y);
                GL.TexCoord2(1f, 0f);
                GL.Vertex2(Bin.X, 0);
                GL.End();

                GL.Disable(EnableCap.Texture2D);
            }
            public void Load2DTexture()
            {
                GL.BindTexture(TextureTarget.Texture2D, VBOtexture);
                BitmapData data = textureImage.LockBits(
                    new System.Drawing.Rectangle(0, 0, textureImage.Width, textureImage.Height),
                    ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                    data.Width, data.Height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra,
                    PixelType.UnsignedByte, data.Scan0);

                textureImage.UnlockBits(data);

                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                    (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                    (int)TextureMagFilter.Linear);

                ErrorCode Er = GL.GetError();
                string str = Er.ToString();

            }
            public void SetupView(int width, int height)
            {
                GL.ShadeModel(ShadingModel.Smooth);
                GL.MatrixMode(MatrixMode.Projection);
                GL.LoadIdentity();
                GL.Ortho(0, Bin.X, 0, Bin.Y, -1, 1);
                GL.Viewport(0, 0, width, height);
            }

            public int clamp(int value, int min, int max)
            {
                if (value < min)
                    return min;
                if (value > max) 
                    return max;
                return value;
            }
            Color TransferFunction(short value, int currentMin, int currentWidthTF)
            {
                int min = currentMin;
                int max = currentMin + currentWidthTF;
                if (max <= min)
                {
                    max = min + 1;
                }
                int newVal = clamp((value - min) * 255 / (max - min), 0, 255);
                return Color.FromArgb(255, newVal, newVal, newVal);
            }
            public void DrawQuads(int layerNumber, int curMin, int width)
            {
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                GL.Begin(BeginMode.Quads);
                for(int x_coord = 0;x_coord < Bin.X - 1; x_coord++)
                    for (int y_coord = 0;y_coord < Bin.Y - 1; y_coord++)
                    {
                        short value;
                        // 1 вершина
                        value = Bin.array[x_coord + y_coord * Bin.X 
                                            + layerNumber * Bin.X * Bin.Y];
                        GL.Color3(TransferFunction(value, curMin, width));
                        GL.Vertex2(x_coord, y_coord);

                        //2 вершина
                        value = Bin.array[x_coord + (y_coord + 1) * Bin.X
                                           + layerNumber * Bin.X * Bin.Y];
                        GL.Color3(TransferFunction(value, curMin, width));
                        GL.Vertex2(x_coord, y_coord + 1);

                        //3 вершина
                        value = Bin.array[x_coord + 1 + (y_coord + 1) * Bin.X
                                           + layerNumber * Bin.X * Bin.Y];
                        GL.Color3(TransferFunction(value, curMin, width));
                        GL.Vertex2(x_coord + 1, y_coord + 1);

                        //4 вершина
                        value = Bin.array[x_coord + 1 + y_coord * Bin.X
                                           + layerNumber * Bin.X * Bin.Y];
                        GL.Color3(TransferFunction(value, curMin, width));
                        GL.Vertex2(x_coord + 1, y_coord);

                    }
                GL.End();
            }
        
        }

        private void открытьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                string str = dialog.FileName;
                bin.readBIN(str);
                trackBar1.Maximum = Bin.Z - 1;
                view.SetupView(glControl1.Width, glControl1.Height);
                loaded = true;
                glControl1.Invalidate();
            }
        }
        private void glControl1_Load(object sender, EventArgs e)
        {
            
        }

        bool needReload = false;
        private void glControl1_Paint(object sender, PaintEventArgs e)
        {
            if (loaded)
            {
                if (checkQuads)
                {
                    view.DrawQuads(currentLayer, currentMin, currentWidthTF);
                }
                if (checkTexture)
                {
                    if (needReload)
                    {
                        view.generateTextureImage(currentLayer);
                        view.Load2DTexture();
                        needReload = false;
                    }
                    view.DrawTexture();
                }
                glControl1.SwapBuffers();
            }
        }
        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            currentLayer = trackBar1.Value;
            needReload = true;
        }

        void Application_Idle(object sender, EventArgs e)
        {
            while (glControl1.IsIdle)
            {
                displayFPS();
                glControl1.Invalidate();
            }
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            Application.Idle += Application_Idle;
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            checkQuads = radioButton1.Checked;
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            checkTexture = radioButton2.Checked;
        }

        private void trackBar2_Scroll(object sender, EventArgs e)
        {
            currentMin = trackBar2.Value;
            needReload = true;
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void trackBar3_Scroll(object sender, EventArgs e)
        {
            currentWidthTF = trackBar3.Value;
            needReload = true;
        }
    }
}
