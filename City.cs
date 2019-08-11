using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Tao.OpenGl;
using Tao.OpenAl;
using Tao.Platform.Windows;
using System.Reflection;
using System.Drawing.Text;
using System.Collections;
using System.Collections.Generic;

namespace NeHe {
    #region Class Documentation
    /// <summary>
    ///     Lesson 34:  Height Mapping.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Original Author:    Ben Humphrey ( DigiBen )
    ///         http://nehe.gamedev.net/data/lessons/lesson.asp?lesson=34
    ///     </para>
    ///     <para>
    ///         C# Implementation:  Morten Lerudjordet & Randy Ridge
    ///         http://www.taoframework.com
    ///     </para>
    /// </remarks>
    #endregion Class Documentation
    public class City : Form {
        // --- Fields ---
        #region Private Static Fields
        private static IntPtr hDC;                                              // Private GDI Device Context
        private static IntPtr hRC;                                              // Permanent Rendering Context
        private static Form form;                                               // Our Current Windows Form
        private static bool[] keys = new bool[256];                             // Array Used For The Keyboard Routine
        private static bool active = true;                                      // Window Active Flag, Set To True By Default
        private static bool fullscreen = true;                                  // Fullscreen Flag, Set To Fullscreen Mode By Default
        private static bool done = false;                                       // Bool Variable To Exit Main Loop
        private static bool typing = false;
        private static PrivateFontCollection fonts;

        private const int MAP_SIZE = 1024;                                      // Size Of Our .RAW Height Map (NEW)
        private const int STEP_SIZE = 16;                                       // Width And Height Of Each Quad (NEW)
        private const float HEIGHT_RATIO = 3;                                // Ratio That The Y Is Scaled According To The X And Z (NEW)
        private static bool bRender = true;                                     // Polygon Flag Set To TRUE By Default (NEW)
        //private static byte[] heightmap = new byte[MAP_SIZE * MAP_SIZE];        // Holds The Height Map Data (NEW)
        private static float scaleValue = 0.15f;                                // Scale Value For The Terrain (NEW)
        private static byte[,] heightmap;
        private static uint[] pickBuffer;
        private static uint[] textures;
        private static Model[] models;                                          // Pool of models
        private static int fontbase;                                            // Base Display List For The Font Set

        public struct Camera
        {
            public float ratio;
            public int minHeight, maxHeight;//always keep this distance from terrain
            public int zoom, zoomOutAt;
            public int cameraX, cameraY, cameraZ;
            public uint lookatX, lookatY, lookatZ;
        }

        private struct FPS
        {
            public int frame;//frames since last check
            public DateTime time;//elapsed time in ms
            public double elapsed;//ms since last check
            public float fps;//holds the string value of last fps
        }

        private struct Chatbox
        {
            public Color col;
            public int textSize;
            public int printAtX;
            public int printAtY;
            public int chatSize;
            public string line;
            public string[] text;

            public void drawText()
            {
                if (typing)
                {//print text being typed
                    glPrint("Say: " + line, printAtX, printAtY, col);
                }
                for (int i = 0; i < chatSize - 1; i++)
                {//print previous sent messages
                    if (text[i] != null)
                    {
                        glPrint(text[i], printAtX, (textSize - 3) * i + 10, col);
                    }
                }
            }

            public void writeLine()
            {//send text to chatbox/to all players
                //printAtY += size;
                if (line != null)
                {
                    int i = chatSize - 1;//change to last non null element
                    while ((i > 0))
                    {
                        text[i] = text[i - 1];
                        i--;
                    }
                    text[0] = "Player: " + line;
                }
            }
        }

        private struct Minimap
        {
            public float posX;
            public float posY;
            public float height;
            public float width;

            public void drawMap()
            {
                Gl.glColor3f(1.0f, 1.0f, 1.0f); //green tinge the map
                Gl.glMatrixMode(Gl.GL_PROJECTION);
                Gl.glPushMatrix();
                Gl.glMatrixMode(Gl.GL_MODELVIEW);                               // Select The Modelview Matrix
                
                //map section
                Gl.glLoadIdentity();
                Gl.glBindTexture(Gl.GL_TEXTURE_2D, textures[2]);
                Gl.glTranslatef(posX, posY, -1);
                Gl.glBegin(Gl.GL_QUADS);
                Gl.glTexCoord2f(0, 0); Gl.glVertex2f(0, 0);
                Gl.glTexCoord2f(0, 1); Gl.glVertex2f(0, height);
                Gl.glTexCoord2f(1, 1); Gl.glVertex2f(width, height);
                Gl.glTexCoord2f(1, 0); Gl.glVertex2f(width, 0);
                Gl.glEnd();

                //camera box
                float fovWidth = (float)15 / heightmap.GetLength(0); //size of camera box
                float cameraX = (camera.cameraX - 45) * (float)camera.cameraX / (heightmap.GetLength(0) * 230);
                float cameraY = (camera.cameraY) * (float)camera.cameraY / (heightmap.GetLength(1) * 1200);
                Gl.glColor3f(0.0f, 1.0f, 0.0f);
                Gl.glBegin(Gl.GL_LINES);
                Gl.glVertex2f(cameraX, cameraY);
                Gl.glVertex2f(cameraX, cameraY + fovWidth);
                Gl.glVertex2f(cameraX, cameraY + fovWidth);
                Gl.glVertex2f(cameraX + fovWidth, cameraY + fovWidth);
                Gl.glVertex2f(cameraX + fovWidth, cameraY + fovWidth);
                Gl.glVertex2f(cameraX + fovWidth, cameraY);
                Gl.glVertex2f(cameraX + fovWidth, cameraY);
                Gl.glVertex2f(cameraX, cameraY);
                Gl.glEnd();
                Gl.glPopMatrix();
                Gl.glPopMatrix();
            }
        }

        private struct Vertex
        {
            public float x;
            public float y;
            public float z;
        }

        private struct Triangle
        {//byte? 256 max triangles
            public ushort vertex1;
            public ushort vertex2;
            public ushort vertex3;
        }

        private struct Quad
        {
            public ushort vertex1;
            public ushort vertex2;
            public ushort vertex3;
            public ushort vertex4;
        }

        private struct Model
        {
            public List<TextureCoord> texCoords;
            public List<Normal> normals;
            public List<Vertex> vertexs;
            public List<Triangle> triangles;
            public List<Quad> quads;
        }

        private struct TextureCoord
        {
            public float u;
            public float v;
        }

        private struct Normal
        {
            public float x;
            public float y;
            public float z;
        }

        private struct HeightMap
        {
            public Vertex[] vertices;
            public TextureCoord[] texCoord;
        }

        private static Camera camera;
        private static FPS fps;
        private static Chatbox chatbox;
        private static Minimap minimap;
        #endregion Private Static Fields

        // --- Constructors & Destructors ---
        #region City()
        public City() {
            this.CreateParams.ClassStyle = this.CreateParams.ClassStyle |       // Redraw On Size, And Own DC For Window.
                User.CS_HREDRAW | User.CS_VREDRAW | User.CS_OWNDC;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);            // No Need To Erase Form Background
            this.SetStyle(ControlStyles.DoubleBuffer, true);                    // Buffer Control
            this.SetStyle(ControlStyles.Opaque, true);                          // No Need To Draw Form Background
            this.SetStyle(ControlStyles.ResizeRedraw, true);                    // Redraw On Resize
            this.SetStyle(ControlStyles.UserPaint, true);                       // We'll Handle Painting Ourselves

            this.Activated += new EventHandler(this.Form_Activated);            // On Activate Event Call Form_Activated
            this.Closing += new CancelEventHandler(this.Form_Closing);          // On Closing Event Call Form_Closing
            this.Deactivate += new EventHandler(this.Form_Deactivate);          // On Deactivate Event Call Form_Deactivate
            this.KeyPress += new  KeyPressEventHandler(this.Form_KeyPress);
            //this.KeyDown += new KeyEventHandler(this.Form_KeyDown);             // On KeyDown Event Call Form_KeyDown
            //this.KeyUp += new KeyEventHandler(this.Form_KeyUp);                 // On KeyUp Event Call Form_KeyUp
            //this.MouseDown += new MouseEventHandler(this.Form_MouseDown);       // On MouseDown Even Call Form_MouseDown
            this.MouseWheel += new MouseEventHandler(mouseFunc);
            this.MouseDown += new MouseEventHandler(mouseFunc);
            this.Resize += new EventHandler(this.Form_Resize);                  // On Resize Event Call Form_Resize
        }
        #endregion City()

        // --- Entry Point ---
        #region Run()
        /// <summary>
        ///     The application's entry point.
        /// </summary>
        /// <param name="commandLineArguments">
        ///     Any supplied command line arguments.
        /// </param>
        [STAThread]
        public static void Main()
        {
                fullscreen = false;                                             // Windowed Mode

            // Create Our OpenGL Window
            if(!CreateGLWindow("RTS - Cityscape", 640, 480, 16, fullscreen)) {
                return;                                                         // Quit If Window Was Not Created
            }

            while(!done) {                                                      // Loop That Runs While done = false
                Application.DoEvents();                                         // Process Events

                //KeyPress();
                // Draw The Scene.  Watch For ESC Key And Quit Messages From DrawGLScene()
                if ((active && (form != null) && !DrawGLScene()) )// || keys[(int)Keys.Escape])
                {
                    //  Active?  Was There A Quit Received?
                    done = true;                                                // ESC Or DrawGLScene Signalled A Quit
                }
                else
                {                                                          // Not Time To Quit, Update Screen
                    Gdi.SwapBuffers(hDC);                                       // Swap Buffers (Double Buffering)

                    if (keys[(int)Keys.F1])
                    {                                   // Is F1 Being Pressed?
                        keys[(int)Keys.F1] = false;                            // If So Make Key false
                        KillGLWindow();                                         // Kill Our Current Window
                        fullscreen = !fullscreen;                               // Toggle Fullscreen / Windowed Mode
                        // Recreate Our OpenGL Window
                        if (!CreateGLWindow("NeHe & Ben Humphrey's Height Map Tutorial", 640, 480, 16, fullscreen))
                        {
                            return;                                             // Quit If Window Was Not Created
                        }
                        done = false;                                           // We're Not Done Yet
                    }

                    if (keys[(int)Keys.Up])
                    {                                   // Is the UP ARROW key Being Pressed?
                        scaleValue += 0.001f;                                   // Increase the scale value to zoom in
                    }

                    if (keys[(int)Keys.Down])
                    {                                 // Is the DOWN ARROW key Being Pressed?
                        scaleValue -= 0.001f;                                   // Decrease the scale value to zoom out
                    }
                }
            }

            // Shutdown
            KillGLWindow();                                                     // Kill The Window
            return;                                                             // Exit The Program
        }
        #endregion Run()

        // --- Private Static Methods ---
        #region bool CreateGLWindow(string title, int width, int height, int bits, bool fullscreenflag)
        /// <summary>
        ///     Creates our OpenGL Window.
        /// </summary>
        /// <param name="title">
        ///     The title to appear at the top of the window.
        /// </param>
        /// <param name="width">
        ///     The width of the GL window or fullscreen mode.
        /// </param>
        /// <param name="height">
        ///     The height of the GL window or fullscreen mode.
        /// </param>
        /// <param name="bits">
        ///     The number of bits to use for color (8/16/24/32).
        /// </param>
        /// <param name="fullscreenflag">
        ///     Use fullscreen mode (<c>true</c>) or windowed mode (<c>false</c>).
        /// </param>
        /// <returns>
        ///     <c>true</c> on successful window creation, otherwise <c>false</c>.
        /// </returns>
        private static bool CreateGLWindow(string title, int width, int height, int bits, bool fullscreenflag) {
            int pixelFormat;                                                    // Holds The Results After Searching For A Match
            fullscreen = fullscreenflag;                                        // Set The Global Fullscreen Flag
            form = null;                                                        // Null The Form

            GC.Collect();                                                       // Request A Collection
            // This Forces A Swap
            Kernel.SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);

            if(fullscreen) {                                                    // Attempt Fullscreen Mode?
                Gdi.DEVMODE dmScreenSettings = new Gdi.DEVMODE();               // Device Mode
                // Size Of The Devmode Structure
                dmScreenSettings.dmSize = (short) Marshal.SizeOf(dmScreenSettings);
                dmScreenSettings.dmPelsWidth = width;                           // Selected Screen Width
                dmScreenSettings.dmPelsHeight = height;                         // Selected Screen Height
                dmScreenSettings.dmBitsPerPel = bits;                           // Selected Bits Per Pixel
                dmScreenSettings.dmFields = Gdi.DM_BITSPERPEL | Gdi.DM_PELSWIDTH | Gdi.DM_PELSHEIGHT;

                // Try To Set Selected Mode And Get Results.  NOTE: CDS_FULLSCREEN Gets Rid Of Start Bar.
                if(User.ChangeDisplaySettings(ref dmScreenSettings, User.CDS_FULLSCREEN) != User.DISP_CHANGE_SUCCESSFUL) {
                    // If The Mode Fails, Offer Two Options.  Quit Or Use Windowed Mode.
                    if(MessageBox.Show("The Requested Fullscreen Mode Is Not Supported By\nYour Video Card.  Use Windowed Mode Instead?", "NeHe GL",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes) {
                        fullscreen = false;                                     // Windowed Mode Selected.  Fullscreen = false
                    }
                    else {
                        // Pop up A Message Box Lessing User Know The Program Is Closing.
                        MessageBox.Show("Program Will Now Close.", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                        return false;                                           // Return false
                    }
                }
            }

            form = new City();                                              // Create The Window

            if(fullscreen) {                                                    // Are We Still In Fullscreen Mode?
                form.FormBorderStyle = FormBorderStyle.None;                    // No Border
                Cursor.Hide();                                                  // Hide Mouse Pointer
            }
            else {                                                              // If Windowed
                form.FormBorderStyle = FormBorderStyle.Sizable;                 // Sizable
                Cursor.Show();                                                  // Show Mouse Pointer
            }

            form.Width = width;                                                 // Set Window Width
            form.Height = height;                                               // Set Window Height
            form.Text = title;                                                  // Set Window Title

            Gdi.PIXELFORMATDESCRIPTOR pfd = new Gdi.PIXELFORMATDESCRIPTOR();    // pfd Tells Windows How We Want Things To Be
            pfd.nSize = (short) Marshal.SizeOf(pfd);                            // Size Of This Pixel Format Descriptor
            pfd.nVersion = 1;                                                   // Version Number
            pfd.dwFlags = Gdi.PFD_DRAW_TO_WINDOW |                              // Format Must Support Window
                Gdi.PFD_SUPPORT_OPENGL |                                        // Format Must Support OpenGL
                Gdi.PFD_DOUBLEBUFFER;                                           // Format Must Support Double Buffering
            pfd.iPixelType = (byte) Gdi.PFD_TYPE_RGBA;                          // Request An RGBA Format
            pfd.cColorBits = (byte) bits;                                       // Select Our Color Depth
            pfd.cRedBits = 0;                                                   // Color Bits Ignored
            pfd.cRedShift = 0;
            pfd.cGreenBits = 0;
            pfd.cGreenShift = 0;
            pfd.cBlueBits = 0;
            pfd.cBlueShift = 0;
            pfd.cAlphaBits = 0;                                                 // No Alpha Buffer
            pfd.cAlphaShift = 0;                                                // Shift Bit Ignored
            pfd.cAccumBits = 0;                                                 // No Accumulation Buffer
            pfd.cAccumRedBits = 0;                                              // Accumulation Bits Ignored
            pfd.cAccumGreenBits = 0;
            pfd.cAccumBlueBits = 0;
            pfd.cAccumAlphaBits = 0;
            pfd.cDepthBits = 16;                                                // 16Bit Z-Buffer (Depth Buffer)
            pfd.cStencilBits = 0;                                               // No Stencil Buffer
            pfd.cAuxBuffers = 0;                                                // No Auxiliary Buffer
            pfd.iLayerType = (byte) Gdi.PFD_MAIN_PLANE;                         // Main Drawing Layer
            pfd.bReserved = 0;                                                  // Reserved
            pfd.dwLayerMask = 0;                                                // Layer Masks Ignored
            pfd.dwVisibleMask = 0;
            pfd.dwDamageMask = 0;

            hDC = User.GetDC(form.Handle);                                      // Attempt To Get A Device Context
            if(hDC == IntPtr.Zero) {                                            // Did We Get A Device Context?
                KillGLWindow();                                                 // Reset The Display
                MessageBox.Show("Can't Create A GL Device Context.", "ERROR",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            pixelFormat = Gdi.ChoosePixelFormat(hDC, ref pfd);                  // Attempt To Find An Appropriate Pixel Format
            if(pixelFormat == 0) {                                              // Did Windows Find A Matching Pixel Format?
                KillGLWindow();                                                 // Reset The Display
                MessageBox.Show("Can't Find A Suitable PixelFormat.", "ERROR",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if(!Gdi.SetPixelFormat(hDC, pixelFormat, ref pfd)) {                // Are We Able To Set The Pixel Format?
                KillGLWindow();                                                 // Reset The Display
                MessageBox.Show("Can't Set The PixelFormat.", "ERROR",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            hRC = Wgl.wglCreateContext(hDC);                                    // Attempt To Get The Rendering Context
            if(hRC == IntPtr.Zero) {                                            // Are We Able To Get A Rendering Context?
                KillGLWindow();                                                 // Reset The Display
                MessageBox.Show("Can't Create A GL Rendering Context.", "ERROR",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if(!Wgl.wglMakeCurrent(hDC, hRC)) {                                 // Try To Activate The Rendering Context
                KillGLWindow();                                                 // Reset The Display
                MessageBox.Show("Can't Activate The GL Rendering Context.", "ERROR",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            form.Show();                                                        // Show The Window
            //form.TopMost = true;                                                // Topmost Window
            form.Focus();                                                       // Focus The Window

            if(fullscreen) {                                                    // This Shouldn't Be Necessary, But Is
                Cursor.Hide();
            }
            ReSizeGLScene(width, height);                                       // Set Up Our Perspective GL Screen

            if(!InitGL()) {                                                     // Initialize Our Newly Created GL Window
                KillGLWindow();                                                 // Reset The Display
                MessageBox.Show("Initialization Failed.", "ERROR",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            return true;                                                        // Success
        }
        #endregion bool CreateGLWindow(string title, int width, int height, int bits, bool fullscreenflag)

        #region bool DrawGLScene()
        /// <summary>
        ///     Draws everything.
        /// </summary>
        /// <returns>
        ///     Returns <c>true</c> on success, otherwise <c>false</c>.
        /// </returns>
        private static bool DrawGLScene()
        {
            Gl.glColor3d(1, 1, 1);
            Gl.glClear(Gl.GL_COLOR_BUFFER_BIT | Gl.GL_DEPTH_BUFFER_BIT);        // Clear The Screen And The Depth Buffer

            setCamera();
            drawSkybox();
            //drawHeightmap();
            drawModels();
            drawStreet();
            calcFps();
            chatbox.drawText();
            minimap.drawMap();
            //Winmm.PlaySound(@".\Sounds\Traffic.wav", new System.IntPtr(), Winmm.SND_ASYNC | Winmm.SND_NOSTOP);
            //int format;
            //int size;
            //float frequency;
            //Tao.OpenAl.Alut.alutLoadMemoryFromFile(@".\Sounds.Traffic.wav", out format, out size, out frequency);
            //Tao.OpenAl.Alut.alutCreateBufferFromFile(@".\Sounds.Traffic.wav");

            //Tao.FreeGlut.Glut.glutFullScreen();

            return true;                                                        // Everything Went OK
        }
        #endregion bool DrawGLScene()

        #region int GetHeight(byte[] heightmap, int x, int y)
        /// <summary>
        ///     Returns the height from a height map index.
        /// </summary>
        /// <param name="heightmap">
        ///     Height map data.
        /// </param>
        /// <param name="x">
        ///     X coordinate value.
        /// </param>
        /// <param name="y">
        ///     Y coordinate value.
        /// </param>
        /// <returns>
        ///     Returns int with height data.
        /// </returns>
        private static int GetHeight(byte[] heightmap, int x, int y) {          // This Returns The Height From A Height Map Index
            x = x % MAP_SIZE;                                                   // Error Check Our x Value
            y = y % MAP_SIZE;                                                   // Error Check Our y Value

            return heightmap[x + (y * MAP_SIZE)];                               // Index Into Our Height Array And Return The Height
        }
        #endregion int GetHeight(byte[] heightmap, int x, int y)

        #region bool InitGL()
        /// <summary>
        ///     All setup for OpenGL goes here.
        /// </summary>
        /// <returns>
        ///     Returns <c>true</c> on success, otherwise <c>false</c>.
        /// </returns>
        private static bool InitGL() {                                          // All Setup For OpenGL Goes Here
            Gl.glEnable(Gl.GL_TEXTURE_2D);                                      // Enable Texture Mapping ( NEW )
            Gl.glShadeModel(Gl.GL_SMOOTH);                                      // Enable Smooth Shading
            Gl.glClearColor(0, 0, 0, 0.5f);                                     // Black Background
            Gl.glClearDepth(1);                                                 // Depth Buffer Setup
            Gl.glEnable(Gl.GL_DEPTH_TEST);                                      // Enables Depth Testing
            Gl.glDepthFunc(Gl.GL_LEQUAL);                                       // The Type Of Depth Testing To Do
            //Gl.glHint(Gl.GL_PERSPECTIVE_CORRECTION_HINT, Gl.GL_NICEST);         // Really Nice Perspective Calculations
            
            //test each of these
            Gl.glEnable(Gl.GL_POLYGON_SMOOTH);
            Gl.glEnable(Gl.GL_CULL_FACE);                                         // Removes outline artifacts
            Gl.glCullFace(Gl.GL_FRONT);
            Gl.glBlendFunc(Gl.GL_SRC_ALPHA, Gl.GL_ONE);						// Select The Type Of Blending (transperancy)

            camera = new Camera();
            fps = new FPS();
            chatbox = new Chatbox();
            minimap = new Minimap();
            pickBuffer = new uint[256];//--max selectable units per map?
            textures = new uint[256];//--max textures per map
            fonts = new PrivateFontCollection();
            Gl.glSelectBuffer(256, pickBuffer);
            
            loadObjects();
            loadFont();
            loadCamera();
            loadChatbox();
            loadTextures();
            loadHeightmap();
            loadMinimap();

            #region VBO
            /*
            float[] verticies = new float[] {
                                            -1.0f, -1.0f, 1.0f,
                                            1.0f, -1.0f, 1.0f,
                                            -1.0f, 1.0f, 1.0f,
                                            1.0f, 1.0f, 1.0f,

                                            1.0f, -1.0f, 1.0f,
                                            1.0f, -1.0f, -1.0f, 
                                            1.0f, 1.0f, 1.0f, 
                                            1.0f, 1.0f, -1.0f,

                                            1.0f, -1.0f, -1.0f, 
                                            -1.0f, -1.0f, -1.0f, 
                                            1.0f, 1.0f, -1.0f, 
                                            -1.0f, 1.0f, -1.0f,

                                            -1.0f, -1.0f, -1.0f, 
                                            -1.0f, -1.0f, 1.0f, 
                                            -1.0f, 1.0f, -1.0f, 
                                            -1.0f, 1.0f, 1.0f,

                                            -1.0f, -1.0f, -1.0f, 
                                            1.0f, -1.0f, -1.0f, 
                                            -1.0f, -1.0f, 1.0f, 
                                            1.0f, -1.0f, 1.0f,

                                            -1.0f, 1.0f, 1.0f, 
                                            1.0f, 1.0f, 1.0f, 
                                            -1.0f, 1.0f, -1.0f, 
                                            1.0f, 1.0f, -1.0f, 
                                            };
            int[] vertexBuffer = new int[1];

            Gl.glGenBuffersARB(1, vertexBuffer);    //make a buffer for the VBO
            Gl.glBindBufferARB(Gl.GL_ARRAY_BUFFER, vertexBuffer[0]); //bind to an openGL buffer
            Gl.glBufferDataARB(Gl.GL_ARRAY_BUFFER, (IntPtr)(verticies.Length * sizeof(float)),
                verticies, Gl.GL_STATIC_DRAW);
            //change the GL_static_draw_arb if want dynamic/destructable terrain
            */
            #endregion

            //if(!LoadRawFile("NeHe.City.Terrain.raw", MAP_SIZE * MAP_SIZE, ref heightmap)) {
            //    return false;                                                   // (NEW) Try To Open Terrain Data
            //}

            return true;                                                        // Initialization Went OK
        }
        #endregion bool InitGL()

        #region KillGLWindow()
        /// <summary>
        ///     Properly kill the window.
        /// </summary>
        private static void KillGLWindow() {
            if(fullscreen) {                                                    // Are We In Fullscreen Mode?
                User.ChangeDisplaySettings(IntPtr.Zero, 0);                     // If So, Switch Back To The Desktop
                Cursor.Show();                                                  // Show Mouse Pointer
            }

            if(hRC != IntPtr.Zero) {                                            // Do We Have A Rendering Context?
                if(!Wgl.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero)) {             // Are We Able To Release The DC and RC Contexts?
                    MessageBox.Show("Release Of DC And RC Failed.", "SHUTDOWN ERROR",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                if(!Wgl.wglDeleteContext(hRC)) {                                // Are We Able To Delete The RC?
                    MessageBox.Show("Release Rendering Context Failed.", "SHUTDOWN ERROR",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                hRC = IntPtr.Zero;                                              // Set RC To Null
            }

            if(hDC != IntPtr.Zero) {                                            // Do We Have A Device Context?
                if(form != null && !form.IsDisposed) {                          // Do We Have A Window?
                    if(form.Handle != IntPtr.Zero) {                            // Do We Have A Window Handle?
                        if(!User.ReleaseDC(form.Handle, hDC)) {                 // Are We Able To Release The DC?
                            MessageBox.Show("Release Device Context Failed.", "SHUTDOWN ERROR",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }

                hDC = IntPtr.Zero;                                              // Set DC To Null
            }

            if(form != null) {                                                  // Do We Have A Windows Form?
                form.Hide();                                                    // Hide The Window
                form.Close();                                                   // Close The Form
                form = null;                                                    // Set form To Null
            }
        }
        #endregion KillGLWindow()

        public static void errorHandler(string className, string functionName, string resource)
        {
            MessageBox.Show("Exception in " + className + " Class." + Environment.NewLine +
                "Inside " + functionName + "() Function." + Environment.NewLine +
                "Could not load " + resource + " Resources.", "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            done = true;
        }

        private static void loadFont()
        {
            try
            {
                fonts.AddFontFile(@".\Fonts\28 Days Later.ttf");
                //gameFont = new Font(fontsCollection.Families[0], 24);
                //as with textures, use a directory search, for mods
                //Font gameFont = new Font(fontsCollection.Families[0], 12);

                IntPtr font;
                IntPtr oldfont;
                fontbase = Gl.glGenLists(96);                                       // Storage For 96 Characters
                font = Gdi.CreateFont(                                              // Create The Font
                chatbox.textSize,                                                            // Height Of Font
                0,                                                              // Width Of Font
                0,                                                              // Angle Of Escapement
                0,                                                              // Orientation Angle
                Gdi.FW_BOLD,                                                    // Font Weight
                false,                                                          // Italic
                false,                                                          // Underline
                false,                                                          // Strikeout
                Gdi.ANSI_CHARSET,                                               // Character Set Identifier
                Gdi.OUT_TT_PRECIS,                                              // Output Precision
                Gdi.CLIP_DEFAULT_PRECIS,                                        // Clipping Precision
                Gdi.ANTIALIASED_QUALITY,                                        // Output Quality
                Gdi.FF_DONTCARE | Gdi.DEFAULT_PITCH,                            // Family And Pitch
                "28 Days Later");                                                 // Font Name

                oldfont = Gdi.SelectObject(hDC, font);                              // Selects The Font We Want
                Wgl.wglUseFontBitmapsA(hDC, 32, 96, fontbase);                       // Builds 96 Characters Starting At Character 32
                Gdi.SelectObject(hDC, oldfont);                                     // Selects The Font We Want
                Gdi.DeleteObject(font);                                             // Delete The Font
            }
            catch
            {
                errorHandler(form.ToString(), MethodBase.GetCurrentMethod().Name, "Font");
            }
        }

        private static void loadCamera()
        {
            //load from file
            //camera.ratio = this.Width / this.Height;
            camera.zoom = 50; camera.zoomOutAt = 150;//165 150when to jump out and follow terrain
            camera.minHeight = 40; camera.maxHeight = 50; //20 75
            camera.cameraX = 25;//twist
            camera.cameraY = 0;//-32;//zoom out
            camera.cameraZ = camera.minHeight;
            camera.lookatX = 25;//-22;//twist
            camera.lookatY = 45;//angle (up/down)
            camera.lookatZ = 0;
        }

        private static void loadChatbox()
        {
            chatbox.textSize = 10;
            chatbox.col = Color.Yellow;//speakers player colour
            chatbox.printAtX = 0;
            chatbox.printAtY = 0;
            chatbox.chatSize = 8;
            chatbox.line = null;
            chatbox.text = new string[chatbox.chatSize];
            chatbox.text[0] = "abc ABC 123 ,.?!()*"; 
        }

        private static void loadMinimap()
        {
            minimap.posX = -0.55f;
            minimap.posY = 0.1f;
            minimap.height = 0.2f;
            minimap.width = 0.2f;
        }

        private static void loadObjects()
        {//take in object mesh so that objects can be preloaded from pool
            Vertex vertex = new Vertex();
            TextureCoord texCoord = new TextureCoord();
            Normal normal = new Normal();
            Triangle triangle = new Triangle();
            Quad quad = new Quad();
            List<TextureCoord> texCoords;
            List<Normal> normals;
            List<Vertex> vertexs;
            List<Triangle> triangles;
            List<Quad> quads;
            string[] data;
            string line;
            StreamReader objectStream;//resuse reader for locating resources and reading files
            models = new Model[2];//loop thru mapfile and units folder and load cache
            string[] filename = new string[2];
            filename[0] = "cube.obj"; filename[1] = "cube1.obj";

            try
            {
                for (int i = 0; i < 2; i++)
                {
                    texCoords = new List<TextureCoord>();
                    normals = new List<Normal>();
                    vertexs = new List<Vertex>();
                    triangles = new List<Triangle>();
                    quads = new List<Quad>();
                    objectStream = new StreamReader(@".\Models\Units\" + filename[i]);
                    while (!objectStream.EndOfStream)
                    {
                        line = objectStream.ReadLine();
                        line = line.Trim();
                        line = line.Replace("  ", " ");
                        //!! object cannot have negative values
                        data = line.Split(new char[] { ' ', '-', '/' },
                        StringSplitOptions.RemoveEmptyEntries);
                        Console.WriteLine(line);

                        if (data.Length > 0)
                        {
                            switch (data[0])
                            {
                                case ("v"):
                                    //vertex (x, y, z)
                                    vertex.x = float.Parse(data[1]);
                                    vertex.y = float.Parse(data[2]);
                                    vertex.z = float.Parse(data[3]);
                                    vertexs.Add(vertex);
                                    break;
                                case ("vt"):
                                    //texture coord (u, v)
                                    texCoord.u = float.Parse(data[1]);
                                    texCoord.v = float.Parse(data[2]);
                                    //ignore the 3rd coord (wtf with taht?)
                                    texCoords.Add(texCoord);
                                    break;
                                case ("vn"):
                                    //vertex normal (x, y, z)
                                    normal.x = float.Parse(data[1]);
                                    normal.y = float.Parse(data[2]);
                                    normal.z = float.Parse(data[3]);
                                    normals.Add(normal);
                                    break;
                                case ("f"):
                                    //face (list of vertexs)
                                    switch (data.Length)
                                    {
                                        case 4:
                                            //triangle
                                            break;
                                        case 5:
                                            //quad
                                            quad.vertex1 = ushort.Parse(data[1]);
                                            quad.vertex2 = ushort.Parse(data[2]);
                                            quad.vertex3 = ushort.Parse(data[3]);
                                            quad.vertex4 = ushort.Parse(data[4]);
                                            quads.Add(quad);
                                            break;
                                        case 7:
                                            //triangle 3 with textures 3 pluss f 1
                                            triangle.vertex1 = ushort.Parse(data[1]);
                                            triangle.vertex2 = ushort.Parse(data[3]);
                                            triangle.vertex3 = ushort.Parse(data[5]);
                                            triangles.Add(triangle);
                                            break;
                                        case 9:
                                            //quad 4 with textures 4 pluss f
                                            break;
                                    }
                                    break;
                                case ("g"):
                                    //group (list of faces)
                                    break;
                                default:
                                    //either comment or newline etc - ignore
                                    break;
                            }
                        }
                    }
                    //save space by nulling unused attributes
                    if (texCoords.Count > 0)
                        models[i].texCoords = texCoords;
                    if (normals.Count > 0)
                        models[i].normals = normals;
                    if (vertexs.Count > 0)
                        models[i].vertexs = vertexs;
                    if (triangles.Count > 0)
                        models[i].triangles = triangles;
                    if (quads.Count > 0)
                        models[i].quads = quads;
                    
                    objectStream.Close();
                }
            }
            catch
            {
                errorHandler(form.ToString(), MethodBase.GetCurrentMethod().Name, "Object Model");
            }
        }

        private static void loadTextures()
        {
            int imageResources = textures.Length;//count how many images in maps folder
            string fileName = @".\Textures\Road.bmp";
            Bitmap[] image = new Bitmap[imageResources];

            try
            {
                image[0] = new Bitmap(fileName);
                image[1] = new Bitmap(@".\Textures\Pavement.bmp");
                image[2] = new Bitmap(@".\Maps\Forrest01\Heightmap.bmp");
                image[3] = new Bitmap(@".\Maps\Forrest01\Skybox1.bmp");
                image[4] = new Bitmap(@".\Maps\Forrest01\Skybox2.bmp");
                Gl.glGenTextures(imageResources, textures);

                for (int i = 0; i < imageResources; i++)
                {
                    if (image[i] != null)
                    {
                        image[i].RotateFlip(RotateFlipType.RotateNoneFlipY);
                        System.Drawing.Imaging.BitmapData bitmapdata;
                        Rectangle rect = new Rectangle(0, 0, image[i].Width, image[i].Height);
                        bitmapdata = image[i].LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly,
                        System.Drawing.Imaging.PixelFormat.Format24bppRgb);

                        Gl.glBindTexture(Gl.GL_TEXTURE_2D, textures[i]);
                        Gl.glTexParameteri(Gl.GL_TEXTURE_2D, Gl.GL_TEXTURE_MIN_FILTER, (int)Gl.GL_LINEAR);		// Linear Filtering
                        Gl.glTexParameteri(Gl.GL_TEXTURE_2D, Gl.GL_TEXTURE_MAG_FILTER, (int)Gl.GL_LINEAR);//why is this repeated?
                        Gl.glTexParameterf(Gl.GL_TEXTURE_2D, Gl.GL_TEXTURE_WRAP_S, Gl.GL_REPEAT);
                        Gl.glTexParameterf(Gl.GL_TEXTURE_2D, Gl.GL_TEXTURE_WRAP_T, Gl.GL_REPEAT);
                        //repeated to repeat texture instead of clamping texture to edge of shape
                        Gl.glTexImage2D(Gl.GL_TEXTURE_2D, 0, (int)Gl.GL_RGB8, image[i].Width, image[i].Height,
                            0, Gl.GL_BGR_EXT, Gl.GL_UNSIGNED_BYTE, bitmapdata.Scan0);// Linear Filtering
                        image[i].UnlockBits(bitmapdata);
                        image[i].Dispose();
                    }
                }
            }
            catch
            {
                errorHandler(form.ToString(), MethodBase.GetCurrentMethod().Name, "Image");
            }
        }

        private static void loadHeightmap()
        {
            string fileName = @".\Maps\Forrest01\Heightmap.bmp";

            try
            {
                Bitmap rawImage = new Bitmap(fileName);
                heightmap = new byte[rawImage.Width, rawImage.Height];
                byte heightVal;

                for (int y = 0; y < rawImage.Height; y++)
                {
                    for (int x = 0; x < rawImage.Width; x++)
                    {
                        heightVal = (byte)((rawImage.GetPixel(x, y).R + rawImage.GetPixel(x, y).G + rawImage.GetPixel(x, y).B) / 3);
                        heightmap[x, y] = heightVal;
                    }
                }
                rawImage.Dispose();
            }
            catch
            {
                errorHandler(form.ToString(), MethodBase.GetCurrentMethod().Name, "Heightmap");
            }
        }

        private static void setCamera()
        {
            Gl.glMatrixMode(Gl.GL_MODELVIEW);
            Gl.glLoadIdentity();//Reset Camera again
            Glu.gluLookAt(camera.cameraX, camera.cameraY, camera.cameraZ + camera.zoom,
            camera.lookatX, camera.lookatY, camera.lookatZ, 0.0f, 1.0f, 0.0f);
        }

        #region cityscape
        private static void drawFace(int height, int width, int repeatX, int repeatY)
        {
            Gl.glBegin(Gl.GL_QUADS);
            Gl.glTexCoord2f(0, 0); Gl.glVertex2f(0, 0);
            Gl.glTexCoord2f(0, repeatY); Gl.glVertex2f(0, width);
            Gl.glTexCoord2f(repeatX, repeatY); Gl.glVertex2f(height, width);
            Gl.glTexCoord2f(repeatX, 0); Gl.glVertex2f(height, 0);
            Gl.glEnd();
        }

        private static void drawBox(int startX, int startY, int startZ, int height, int width, int depth, int repeatX, int repeatY)
        {
            Gl.glPushMatrix();
            //height - 10, width - 20, depth - 30
            Gl.glTranslatef(startX, startY, startZ);//positioning
            Gl.glTranslatef(0, 0, height);
            drawFace(width, depth, repeatX, repeatY);//top
            Gl.glTranslatef(width, 0, 0);
            Gl.glRotatef(90, 0, 1, 0);
            drawFace(height, depth, repeatX, repeatY);//right
            Gl.glTranslatef(height, 0, 0);
            Gl.glRotatef(90, 0, 1, 0);
            drawFace(width, depth, repeatX, repeatY);//bottom
            Gl.glTranslatef(width, 0, 0);
            Gl.glRotatef(90, 0, 1, 0);
            drawFace(height, depth, repeatX, repeatY);//left
            Gl.glTranslatef(0, 0, -width);
            Gl.glRotatef(90, 1, 0, 0);
            drawFace(height, width, repeatX, repeatY);//front
            Gl.glTranslatef(0, width, -depth);
            Gl.glRotatef(180, 1, 0, 0);
            drawFace(height, width, repeatX, repeatY);//back
            Gl.glPopMatrix();
        }

        private static void drawRoad(int startX, int startY, int length, int angle)
        {//width?
            Gl.glPushMatrix();
            Gl.glBindTexture(Gl.GL_TEXTURE_2D, textures[0]);
            Gl.glTranslatef(startX, startY, 0);//replace 0 with heightMap[x, y]
            Gl.glRotatef(angle, 0, 0, 1);
            drawFace(10, length, 1, length/10);
            Gl.glPopMatrix();
        }

        private static void drawIntersection(int startX, int startY)
        {
            Gl.glPushMatrix();
            Gl.glBindTexture(Gl.GL_TEXTURE_2D, textures[0]);
            Gl.glTranslatef(startX, startY, 0);
            drawFace(10, 10, 1, 0);
            Gl.glPopMatrix();
        }

        private static void drawPath(int startX, int startY, int length)
        {
            Gl.glPushMatrix();
            Gl.glBindTexture(Gl.GL_TEXTURE_2D, textures[1]);
            drawBox(startX, startY, 0, 1, 30, length, 3, 10);//replace 0 with heightmap[x, y]
            Gl.glPopMatrix();
        }
                
        public static void drawStreet()
        {
            for (int y = 0; y < heightmap.GetLength(1) - 1; y++)
            {
                for (int x = 0; x < heightmap.GetLength(0) - 1; x++)
                {
                    heightmap[x, y] = 0;
                }
            }

            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    drawRoad(30 + (x*40), 0 + (y*60), 50, 0);
                    drawPath(0 + (x * 40), 0 + (y * 60), 50);
                    drawIntersection(30 + (x * 40), 50 + (y * 60));
                    drawRoad(30 + (x * 40), 50 + (y * 60), 30, 90);
                }
            }
        }
        #endregion

        private static void drawSkybox()
        {
            Gl.glPushMatrix();
            Gl.glTranslatef(camera.cameraX, camera.cameraY, camera.cameraZ+camera.zoom);
            Gl.glDisable(Gl.GL_LIGHTING);
            Gl.glDisable(Gl.GL_BLEND);
            Gl.glDepthMask(0);
            Gl.glColor3f(1.0f, 1.0f, 1.0f);

            // Render the top quad
            Gl.glBindTexture(Gl.GL_TEXTURE_2D, textures[3]);
            Gl.glBegin(Gl.GL_QUADS);
            Gl.glTexCoord2f(0, 1); Gl.glVertex3f(-0.5f, 0.5f, -0.5f);
            Gl.glTexCoord2f(0, 0); Gl.glVertex3f(-0.5f, 0.5f, 0.5f);
            Gl.glTexCoord2f(1, 0); Gl.glVertex3f(0.5f, 0.5f, 0.5f);
            Gl.glTexCoord2f(1, 1); Gl.glVertex3f(0.5f, 0.5f, -0.5f);
            Gl.glEnd();

            // Render the bottom quad
            Gl.glBindTexture(Gl.GL_TEXTURE_2D, textures[4]);
            Gl.glBegin(Gl.GL_QUADS);
            Gl.glTexCoord2f(0, 0); Gl.glVertex3f(0.5f, -0.5f, -0.5f);
            Gl.glTexCoord2f(1, 0); Gl.glVertex3f(-0.5f, -0.5f, -0.5f);
            Gl.glTexCoord2f(1, 1); Gl.glVertex3f(-0.5f, 0.5f, -0.5f);
            Gl.glTexCoord2f(0, 1); Gl.glVertex3f(0.5f, 0.5f, -0.5f);
            Gl.glEnd();

            Gl.glDepthMask(1);
            Gl.glPopMatrix();
        }

        private static void drawHeightmap()
        {
            Gl.glBindTexture(Gl.GL_TEXTURE_2D, textures[0]);
            Gl.glPushMatrix();
            Gl.glScalef(0.6f, 0.6f, 0.6f / HEIGHT_RATIO);
            for (int y = 0; y < heightmap.GetLength(1) - 1; y++)
            {
                for (int x = 0; x < heightmap.GetLength(0) - 1; x++)
                {
                    if (bRender)
                        Gl.glBegin(Gl.GL_TRIANGLE_STRIP);
                    else
                        Gl.glBegin(Gl.GL_LINES);
                    Gl.glColor3f(heightmap[x, y] / 256.0f, heightmap[x, y] / 256.0f, heightmap[x, y] / 256.0f);
                    Gl.glTexCoord2f((float)x / heightmap.GetLength(0), (float)y / heightmap.GetLength(1));              Gl.glVertex3f(x, y, heightmap[x, y]);
                    Gl.glTexCoord2f((float)x / heightmap.GetLength(0), (float)(y + 1) / heightmap.GetLength(1));        Gl.glVertex3f(x, y + 1, heightmap[x, y + 1]);
                    Gl.glTexCoord2f((float)(x + 1) / heightmap.GetLength(0), (float)y / heightmap.GetLength(1));        Gl.glVertex3f(x + 1, y, heightmap[x + 1, y]);
                    Gl.glTexCoord2f((float)(x + 1) / heightmap.GetLength(0), (float)(y + 1) / heightmap.GetLength(1));  Gl.glVertex3f(x + 1, y + 1, heightmap[x + 1, y + 1]);
                    Gl.glEnd();
                }
            }
            Gl.glPopMatrix();
        }

        private static void drawModels()
        {//!! replace models with activeModels (ones in viewscape)
            float[] texCoord1 = new float[2];
            float[] texCoord2 = new float[2];
            float[] texCoord3 = new float[2];
            Vertex v1;
            Vertex v2;
            Vertex v3;
            Vertex v4;

            Gl.glPushMatrix();
            Gl.glDisable(Gl.GL_CULL_FACE);
            Gl.glTranslatef(25.0f, 25.0f, 25.0f);
            Gl.glScalef(10.0f, 10.0f, 10.0f);

            for (int j = 0; j < models.Length; j++)
            {
                //#define TANK 1
                Gl.glLoadName( j);
                if (models[j].triangles != null)
                {
                    //Gl.glBegin(Gl.GL_TRIANGLE_STRIP);
                    Gl.glBegin(Gl.GL_TRIANGLES);
                    for (int i = 0; i < models[j].triangles.Count - 1; i++)
                    {
                        v1 = models[j].vertexs[models[j].triangles[i].vertex1 - 1];
                        v2 = models[j].vertexs[models[j].triangles[i].vertex2 - 1];
                        v3 = models[j].vertexs[models[j].triangles[i].vertex3 - 1];
                        if (models[j].texCoords != null)
                        {
                            texCoord1[0] = models[j].texCoords[(i)].u; texCoord1[1] = models[j].texCoords[i].v;
                            texCoord2[0] = models[j].texCoords[(i + 1) + 1].u; texCoord2[1] = models[j].texCoords[i + 1].v;
                            texCoord3[0] = models[j].texCoords[(i + 2)].u; texCoord3[1] = models[j].texCoords[i + 2].v;
                        }
                        //texCoord1[0] = models[j].texCoords[(i * 2)].u; texCoord1[1] = models[j].texCoords[(i * 2) + 1].v;
                        //texCoord2[0] = models[j].texCoords[(i * 2) + 1].u; texCoord1[1] = models[j].texCoords[(i * 2) + 1].v;
                        //texCoord3[0] = models[j].texCoords[(i * 3) + 2].u; texCoord1[1] = models[j].texCoords[(i * 3) + 2].v;
                        
                        //triangles gives 3 vertex offsets in vertex array for face
                        //  __4__5__
                        // |\      |\
                        //7| \_____|_\
                        //6|_|_____| |10
                        // \ |      \|11
                        //  \|_______\
                        //      3 2
                        Gl.glTexCoord2f(texCoord1[0], texCoord1[1]); Gl.glVertex3f(v1.x, v1.y, v1.z);
                        Gl.glTexCoord2f(texCoord2[0], texCoord2[1]); Gl.glVertex3f(v2.x, v2.y, v2.z);
                        Gl.glTexCoord2f(texCoord3[0], texCoord3[1]); Gl.glVertex3f(v3.x, v3.y, v3.z);
                    }
                }
                if (models[j].quads != null)
                {
                    Gl.glBegin(Gl.GL_QUADS);
                    for (int i = 0; i < models[j].quads.Count - 1; i++)
                    {
                        v1 = models[j].vertexs[models[j].quads[i].vertex1 - 1];
                        v2 = models[j].vertexs[models[j].quads[i].vertex2 - 1];
                        v3 = models[j].vertexs[models[j].quads[i].vertex3 - 1];
                        v4 = models[j].vertexs[models[j].quads[i].vertex4 - 1];
                        Gl.glVertex3f(v1.x, v1.y, v1.z);
                        Gl.glVertex3f(v2.x, v2.y, v2.z);
                        Gl.glVertex3f(v3.x, v3.y, v3.z);
                        Gl.glVertex3f(v4.x, v4.y, v4.z);
                    }
                }
                Gl.glEnd();
                Gl.glEnable(Gl.GL_CULL_FACE);
                Gl.glPopMatrix();
            }
        }

        private static void glPrint(string text, int posX, int posY, Color col)
        {
            Gl.glDisable(Gl.GL_DEPTH_TEST);
            Gl.glColor3f(col.R, col.G, col.B);
            Gl.glMatrixMode(Gl.GL_PROJECTION);
            Gl.glPushMatrix();
            Gl.glLoadIdentity();
            Gl.glOrtho(0, 640, 0, 480, -1, 1);
            Gl.glMatrixMode(Gl.GL_MODELVIEW);                               // Select The Modelview Matrix
            Gl.glPushMatrix();                                              // Store The Modelview Matrix
            Gl.glLoadIdentity();                                        // Reset The Modelview Matrix
            Gl.glTranslated(posX, posY, 0);
            Gl.glRasterPos2f(posX, posY);

            if (text != null || text.Length != 0)
            {                              // If There's No Text
                Gl.glPushAttrib(Gl.GL_LIST_BIT);                                    // Pushes The Display List Bits
                Gl.glListBase(fontbase - 32);                                   // Sets The Base Character to 32
                // .NET -- we can't just pass text, we need to convert
                byte[] textbytes = new byte[text.Length];
                for (int i = 0; i < text.Length; i++) textbytes[i] = (byte)text[i];
                Gl.glCallLists(text.Length, Gl.GL_UNSIGNED_BYTE, textbytes);        // Draws The Display List Text
                Gl.glMatrixMode(Gl.GL_PROJECTION);
                Gl.glPopAttrib();                                                   // Pops The Display List Bits

                Gl.glPopMatrix();                                               // Restore The Old Projection Matrix
                Gl.glMatrixMode(Gl.GL_MODELVIEW);                               // Select The Modelview Matrix
                Gl.glPopMatrix();                                                   // Restore The Old Projection Matrix
                Gl.glEnable(Gl.GL_DEPTH_TEST);
            }
        }

        private static void calcFps()
        {
            fps.frame++;
            fps.elapsed = (DateTime.Now - fps.time).TotalMilliseconds;
            if (fps.elapsed > 1000)
            {//if more than 1 second
                fps.fps = (float)Math.Round(fps.frame / (fps.elapsed / 1000), 3);
                fps.time = DateTime.Now;
                fps.frame = 0;
            }
            glPrint("FPS:" + fps.fps, 0, 215, Color.Yellow);
        }

        /*
        #region bool LoadRawFile(string name, int size, ref byte[] heightmap)
        /// <summary>
        ///     Read data from file.
        /// </summary>
        /// <param name="name">
        ///     Name of file where data resides.
        /// </param>
        /// <param name="size">
        ///     Size of file to be read.
        /// </param>
        /// <param name="heightmap">
        ///     Where data is put when read.
        /// </param>
        /// <returns>
        ///     Returns <c>true</c> if success, <c>false</c> failure.
        /// </returns>
        private static bool LoadRawFile(string name, int size, ref byte[] heightmap) {
            if(name == null || name == string.Empty) {                          // Make Sure A Filename Was Given
                return false;                                                   // If Not Return false
            }

            string fileName1 = string.Format("Data{0}{1}",                      // Look For Data\Filename
                Path.DirectorySeparatorChar, name);
            string fileName2 = string.Format("{0}{1}{0}{1}Data{1}{2}",          // Look For ..\..\Data\Filename
                "..", Path.DirectorySeparatorChar, name);

            // Make Sure The File Exists In One Of The Usual Directories
            if(!File.Exists(name) && !File.Exists(fileName1) && !File.Exists(fileName2)) {
                MessageBox.Show("Can't Find The Height Map!", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return false;                                                   // File Could Not Be Found
            }

            if(File.Exists(fileName1)) {                                        // Does The File Exist Here?
                name = fileName1;                                               // Set To Correct File Path
            }
            else if(File.Exists(fileName2)) {                                   // Does The File Exist Here?
                name = fileName2;                                               // Set To Correct File Path
            }

            // Open The File In Read / Binary Mode
            using(FileStream fs = new FileStream(name, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                BinaryReader r = new BinaryReader(fs);
                heightmap = r.ReadBytes(size);
            }
            return true;                                                        // Found And Loaded Data In File
        }
        #endregion bool LoadRawFile(string name, int size, ref byte[] heightmap)

        #region bool RenderHeightMap(byte[] heightmap)
        /// <summary>
        ///     This renders the height map as quads.
        /// </summary>
        /// <param name="heightmap">
        ///     Height map data.
        /// </param>
        private static bool RenderHeightMap(byte[] heightmap) {                 // This Renders The Height Map As Quads
            int X, Y;                                                           // Create Some Variables To Walk The Array With.
            int x, y, z;                                                        // Create Some Variables For Readability

            if(heightmap == null) {                                             // Make Sure Our Height Data Is Valid
                return false;
            }

            if(bRender) {                                                       // What We Want To Render
                Gl.glBegin(Gl.GL_QUADS);                                        // Render Polygons
            }
            else {
                Gl.glBegin(Gl.GL_LINES);                                        // Render Lines Instead
            }

            for(X = 0; X < (MAP_SIZE - STEP_SIZE); X += STEP_SIZE) {
                for (Y = 0; Y < (MAP_SIZE-STEP_SIZE); Y += STEP_SIZE) {
                    // Get The (X, Y, Z) Value For The Bottom Left Vertex
                    x = X;
                    y = GetHeight(heightmap, X, Y);
                    z = Y;

                    SetVertexColor(heightmap, x, z);                            // Set The Color Value Of The Current Vertex
                    Gl.glVertex3i(x, y, z);                                     // Send This Vertex To OpenGL To Be Rendered (Integer Points Are Faster)

                    // Get The (X, Y, Z) Value For The Top Left Vertex
                    x = X;
                    y = GetHeight(heightmap, X, Y + STEP_SIZE);
                    z = Y + STEP_SIZE;

                    SetVertexColor(heightmap, x, z);                            // Set The Color Value Of The Current Vertex
                    Gl.glVertex3i(x, y, z);                                     // Send This Vertex To OpenGL To Be Rendered

                    // Get The (X, Y, Z) Value For The Top Right Vertex
                    x = X + STEP_SIZE;
                    y = GetHeight(heightmap, X + STEP_SIZE, Y + STEP_SIZE);
                    z = Y + STEP_SIZE;

                    SetVertexColor(heightmap, x, z);                            // Set The Color Value Of The Current Vertex
                    Gl.glVertex3i(x, y, z);                                     // Send This Vertex To OpenGL To Be Rendered

                    // Get The (X, Y, Z) Value For The Bottom Right Vertex
                    x = X + STEP_SIZE;
                    y = GetHeight(heightmap, X + STEP_SIZE, Y);
                    z = Y;

                    SetVertexColor(heightmap, x, z);                            // Set The Color Value Of The Current Vertex
                    Gl.glVertex3i(x, y, z);                                     // Send This Vertex To OpenGL To Be Rendered
                }
            }
            Gl.glEnd();
            Gl.glColor4f(1, 1, 1, 1);                                           // Reset The Color
            return true;                                                        // All Good
        }
        #endregion bool RenderHeightMap(byte[] heightmap)

        #region SetVertexColor(byte[] heightmap, int x, int y)
        /// <summary>
        ///     Sets the color value for a particular index, depending on the height index.
        /// </summary>
        /// <param name="heightmap">
        ///     Height map data.
        /// </param>
        /// <param name="x">
        ///     X coordinate value.
        /// </param>
        /// <param name="y">
        ///     Y coordinate value.
        /// </param>
        private static void SetVertexColor(byte[] heightmap, int x, int y) {
            float fColor = -0.15f + (GetHeight(heightmap, x, y ) / 256.0f);
            Gl.glColor3f(0, 0, fColor);                                         // Assign This Blue Shade To The Current Vertex
        }
        #endregion SetVertexColor(byte[] heightmap, int x, int y)
        */
 
        #region ReSizeGLScene(int width, int height)
        /// <summary>
        ///     Resizes and initializes the GL window.
        /// </summary>
        /// <param name="width">
        ///     The new window width.
        /// </param>
        /// <param name="height">
        ///     The new window height.
        /// </param>
        private static void ReSizeGLScene(int width, int height) {
            if(height == 0) {                                                   // Prevent A Divide By Zero...
                height = 1;                                                     // By Making Height Equal To One
            }

            Gl.glViewport(0, 0, width, height);                                 // Reset The Current Viewport
            Gl.glMatrixMode(Gl.GL_PROJECTION);                                  // Select The Projection Matrix
            Gl.glLoadIdentity();                                                // Reset The Projection Matrix
            // Calculate The Aspect Ratio Of The Window.  Farthest Distance Changed To 500.0f (NEW)
            Glu.gluPerspective(45, width / (double) height, 0.1, 500);          
            Gl.glMatrixMode(Gl.GL_MODELVIEW);                                   // Select The Modelview Matrix
            Gl.glLoadIdentity();                                                // Reset The Modelview Matrix
        }
        #endregion ReSizeGLScene(int width, int height)

        private void mouseFunc(object sender, MouseEventArgs e)
        {
            if (e.Delta > 0)
            {//push forward on wheel
                moveCamera(4);
            }
            else if (e.Delta < 0)
            {
                moveCamera(5);
            }
            if (e.Button.Equals(MouseButtons.Left))
            {
                
            }
        }

        private void Form_KeyPress(object sender, KeyPressEventArgs e)
        {
            char c = char.ToUpper(e.KeyChar);
            if (c == (char)Keys.Escape)
            {
                done = true;
            }
            if (typing)
            {
                if (c != (char)Keys.Enter)
                {
                    switch (c)
                    {//profanity filter?
                        case '\b':
                            if (chatbox.line.Length > 0)
                            {
                                chatbox.line = chatbox.line.Remove(chatbox.line.Length - 1, 1);
                            }
                            break;
                        case (char)Keys.None:
                            break;
                        default:
                            chatbox.line += char.ToLower(c);
                            break;
                    }
                    chatbox.drawText();
                }
                else
                {
                    chatbox.writeLine();
                    chatbox.line = null;
                    typing = false;
                }
            }
            else
            {
                switch (c)
                {
                    case (char)Keys.A:
                        moveCamera(0);
                        break;
                    case (char)Keys.D:
                        moveCamera(1);
                        break;
                    case (char)Keys.S:
                        moveCamera(2);
                        break;
                    case (char)Keys.W:
                        moveCamera(3);
                        break;
                    case (char)Keys.I:
                        bRender = false;
                        break;
                    case (char)Keys.K:
                        bRender = true;
                        break;
                    case (char)Keys.Enter:
                        typing = true;
                        chatbox.drawText();
                        break;
                }
            }
        }

        private void moveCamera(byte camType)
        {
            int curHeight = heightmap[camera.cameraX, camera.cameraY];
            switch (camType)
            {
                case 0://X scroll Left
                    if (camera.cameraX > 45)
                    {
                        camera.cameraX--;
                        camera.lookatX--;
                        if (curHeight < camera.zoomOutAt)
                        {
                            camera.cameraZ = camera.minHeight;
                        }
                        else if (curHeight > camera.zoomOutAt)
                        {
                            camera.cameraZ = (int)(curHeight);
                        }
                    }
                    break;
                case 1://X scroll Right
                    if (camera.cameraX + 50 < heightmap.GetLength(0) - 1)
                    {
                        camera.cameraX++;
                        camera.lookatX++;
                        if (curHeight < camera.zoomOutAt)
                        {
                            camera.cameraZ = camera.minHeight;
                        }
                        else if (curHeight > camera.zoomOutAt)
                        {
                            camera.cameraZ = (int)(curHeight);
                        }
                    }
                    break;
                case 2://Y scroll Down
                    if (camera.cameraY > 0)
                    {
                        camera.cameraY--;
                        camera.lookatY--;
                        if (curHeight < camera.zoomOutAt)
                        {
                            camera.cameraZ = camera.minHeight;
                        }
                        else if (curHeight > camera.zoomOutAt)
                        {
                            camera.cameraZ = (int)(curHeight);
                        }
                    }
                    break;
                case 3://Y scroll Up
                    if (camera.cameraY + 15 < heightmap.GetLength(1) - 1)
                    {
                        camera.cameraY++;
                        camera.lookatY++;
                        if (curHeight < camera.zoomOutAt)
                        {
                            camera.cameraZ = camera.minHeight;
                        }
                        else if (curHeight > camera.zoomOutAt)
                        {
                            camera.cameraZ = (int)(curHeight);
                        }
                    }
                    break;
                case 4://Zoom in
                    if (camera.zoom > 15)
                    {
                        camera.zoom -= 10;
                        camera.lookatY += 4;
                        if (curHeight < camera.zoomOutAt)
                        {
                            camera.cameraZ = camera.minHeight;
                        }
                    }
                    break;
                case 5://Zoom out
                    if (camera.zoom < camera.maxHeight)
                    {
                        camera.zoom += 10;
                        camera.lookatY -= 4;
                        if (curHeight < camera.zoomOutAt)
                        {
                            camera.cameraZ = camera.minHeight;
                        }
                    }
                    break;
            }
        }

        /*
        private void Form_KeyPress(object sender, KeyPressEventArgs e)
        {//keys[(int)Keys.Escape]
            keys[e.KeyChar] = true;
            int upper = char.ToUpper(e.KeyChar);
            char lower = char.ToLower(e.KeyChar);
            
            if( keys[(int)Keys.Escape])
                    done = true;
            else if (keys[(int)Keys.A])
            {
                camera.cameraX--;
            }
            else if (keys[(int)Keys.D])
            {
                camera.cameraX++;
            }
            else if (keys[(int)Keys.W])
            {
                camera.cameraY++;
            }
            else if (keys[(int)Keys.S])
            {
                camera.cameraY--;
            }
            keys[e.KeyChar] = false;
        }
        */
        // --- Private Instance Event Handlers ---
        #region Form_Activated
        /// <summary>
        ///     Handles the form's activated event.
        /// </summary>
        /// <param name="sender">
        ///     The event sender.
        /// </param>
        /// <param name="e">
        ///     The event arguments.
        /// </param>
        private void Form_Activated(object sender, EventArgs e) {
            active = true;                                                      // Program Is Active
        }
        #endregion Form_Activated

        #region Form_Closing(object sender, CancelEventArgs e)
        /// <summary>
        ///     Handles the form's closing event.
        /// </summary>
        /// <param name="sender">
        ///     The event sender.
        /// </param>
        /// <param name="e">
        ///     The event arguments.
        /// </param>
        private void Form_Closing(object sender, CancelEventArgs e) {
            done = true;                                                        // Send A Quit Message
        }
        #endregion Form_Closing(object sender, CancelEventArgs e)

        #region Form_Deactivate(object sender, EventArgs e)
        /// <summary>
        ///     Handles the form's deactivate event.
        /// </summary>
        /// <param name="sender">
        ///     The event sender.
        /// </param>
        /// <param name="e">
        ///     The event arguments.
        /// </param>
        private void Form_Deactivate(object sender, EventArgs e) {
            active = false;                                                     // Program Is No Longer Active
        }
        #endregion Form_Deactivate(object sender, EventArgs e)

        #region Form_KeyDown(object sender, KeyEventArgs e)
        /// <summary>
        ///     Handles the form's key down event.
        /// </summary>
        /// <param name="sender">
        ///     The event sender.
        /// </param>
        /// <param name="e">
        ///     The event arguments.
        /// </param>
        private void Form_KeyDown(object sender, KeyEventArgs e) {
            keys[e.KeyValue] = true;                                            // Key Has Been Pressed, Mark It As true
        }
        #endregion Form_KeyDown(object sender, KeyEventArgs e)

        #region Form_KeyUp(object sender, KeyEventArgs e)
        /// <summary>
        ///     Handles the form's key down event.
        /// </summary>
        /// <param name="sender">
        ///     The event sender.
        /// </param>
        /// <param name="e">
        ///     The event arguments.
        /// </param>
        private void Form_KeyUp(object sender, KeyEventArgs e) {
            keys[e.KeyValue] = false;                                           // Key Has Been Released, Mark It As false
        }
        #endregion Form_KeyUp(object sender, KeyEventArgs e)

        #region Form_MouseDown(object sender, MouseEventArgs e)
        /// <summary>
        ///     Handles the form's key down event.
        /// </summary>
        /// <param name="sender">
        ///     The event sender.
        /// </param>
        /// <param name="e">
        ///     The event arguments.
        /// </param>
        public void Form_MouseDown(object sender, MouseEventArgs e) {
            if(e.Button == MouseButtons.Left) {
                bRender = !bRender;                                             // Change The Rendering State Between Fill And Wireframe
            }
        }
        #endregion Form_MouseDown(object sender, MouseEventArgs e)

        #region Form_Resize(object sender, EventArgs e)
        /// <summary>
        ///     Handles the form's resize event.
        /// </summary>
        /// <param name="sender">
        ///     The event sender.
        /// </param>
        /// <param name="e">
        ///     The event arguments.
        /// </param>
        private void Form_Resize(object sender, EventArgs e) {
            ReSizeGLScene(form.Width, form.Height);                             // Resize The OpenGL Window
        }
        #endregion Form_Resize(object sender, EventArgs e)
    }
}
