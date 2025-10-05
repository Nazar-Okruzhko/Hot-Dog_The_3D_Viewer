using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Graphics;

namespace Viewer3D
{
    public class OBJLoader
    {
        public List<Vector3> Vertices = new List<Vector3>();
        public List<Vector2> TexCoords = new List<Vector2>();
        public List<Vector3> Normals = new List<Vector3>();
        public List<Face> Faces = new List<Face>();
        
        public int ColorMapId = -1;
        public int NormalMapId = -1;
        public int SpecularMapId = -1;
        public int MetallicMapId = -1;
        public int RoughnessMapId = -1;
        public int OpacityMapId = -1;
        
        public bool HasTexture = false;
        public int TextureId = -1;
        
        public Vector3 BoundsMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        public Vector3 BoundsMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        public int EdgeCount { get; private set; }
        
        public class Face
        {
            public int[] VertexIndices;
            public int[] TexCoordIndices;
            public int[] NormalIndices;
        }
        
        public void LoadFromFile(string filename)
        {
            Vertices.Clear();
            Faces.Clear();
            Normals.Clear();
            TexCoords.Clear();
            
            var lines = File.ReadAllLines(filename);
            
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;
                    
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                
                if (parts[0] == "v" && parts.Length >= 4)
                {
                    var vertex = new Vector3(
                        float.Parse(parts[1]),
                        float.Parse(parts[2]),
                        float.Parse(parts[3])
                    );
                    Vertices.Add(vertex);
                    
                    BoundsMin.X = Math.Min(BoundsMin.X, vertex.X);
                    BoundsMin.Y = Math.Min(BoundsMin.Y, vertex.Y);
                    BoundsMin.Z = Math.Min(BoundsMin.Z, vertex.Z);
                    BoundsMax.X = Math.Max(BoundsMax.X, vertex.X);
                    BoundsMax.Y = Math.Max(BoundsMax.Y, vertex.Y);
                    BoundsMax.Z = Math.Max(BoundsMax.Z, vertex.Z);
                }
                else if (parts[0] == "vt" && parts.Length >= 3)
                {
                    TexCoords.Add(new Vector2(float.Parse(parts[1]), float.Parse(parts[2])));
                }
                else if (parts[0] == "vn" && parts.Length >= 4)
                {
                    Normals.Add(new Vector3(float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3])));
                }
                else if (parts[0] == "f" && parts.Length >= 4)
                {
                    var face = new Face
                    {
                        VertexIndices = new int[parts.Length - 1],
                        TexCoordIndices = new int[parts.Length - 1],
                        NormalIndices = new int[parts.Length - 1]
                    };
                    
                    for (int i = 1; i < parts.Length; i++)
                    {
                        var indices = parts[i].Split('/');
                        face.VertexIndices[i - 1] = int.Parse(indices[0]) - 1;
                        face.TexCoordIndices[i - 1] = indices.Length > 1 && !string.IsNullOrEmpty(indices[1]) ? int.Parse(indices[1]) - 1 : 0;
                        face.NormalIndices[i - 1] = indices.Length > 2 && !string.IsNullOrEmpty(indices[2]) ? int.Parse(indices[2]) - 1 : 0;
                    }
                    
                    if (face.VertexIndices.Length > 3)
                    {
                        for (int i = 1; i < face.VertexIndices.Length - 1; i++)
                        {
                            Faces.Add(new Face
                            {
                                VertexIndices = new[] { face.VertexIndices[0], face.VertexIndices[i], face.VertexIndices[i + 1] },
                                TexCoordIndices = new[] { face.TexCoordIndices[0], face.TexCoordIndices[i], face.TexCoordIndices[i + 1] },
                                NormalIndices = new[] { face.NormalIndices[0], face.NormalIndices[i], face.NormalIndices[i + 1] }
                            });
                        }
                    }
                    else
                    {
                        Faces.Add(face);
                    }
                }
            }

            var edgeSet = new HashSet<string>();
            foreach (var face in Faces)
            {
                for (int i = 0; i < face.VertexIndices.Length; i++)
                {
                    int v1 = face.VertexIndices[i];
                    int v2 = face.VertexIndices[(i + 1) % face.VertexIndices.Length];
                    string edge = Math.Min(v1, v2) + "," + Math.Max(v1, v2);
                    edgeSet.Add(edge);
                }
            }
            EdgeCount = edgeSet.Count;
            
            LoadMaterial(filename);
        }
        
        private void LoadMaterial(string objFilename)
        {
            var mtlFile = Path.ChangeExtension(objFilename, ".mtl");
            if (File.Exists(mtlFile))
            {
                var lines = File.ReadAllLines(mtlFile);
                foreach (var line in lines)
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;
                    
                    string mapType = parts[0];
                    string texturePath = Path.Combine(Path.GetDirectoryName(objFilename), parts[1]);
                    
                    if (File.Exists(texturePath))
                    {
                        if (mapType == "map_Kd")
                            ColorMapId = LoadTextureFile(texturePath);
                        else if (mapType == "map_Bump" || mapType == "bump")
                            NormalMapId = LoadTextureFile(texturePath);
                        else if (mapType == "map_Ks")
                            SpecularMapId = LoadTextureFile(texturePath);
                        else if (mapType == "map_Pm")
                            MetallicMapId = LoadTextureFile(texturePath);
                        else if (mapType == "map_Pr" || mapType == "map_Ns")
                            RoughnessMapId = LoadTextureFile(texturePath);
                        else if (mapType == "map_d")
                            OpacityMapId = LoadTextureFile(texturePath);
                    }
                }
                
                if (ColorMapId >= 0)
                {
                    TextureId = ColorMapId;
                    HasTexture = true;
                    return;
                }
            }
            
            var baseDir = Path.GetDirectoryName(objFilename);
            var baseName = Path.GetFileNameWithoutExtension(objFilename);
            var extensions = new[] { ".png", ".jpg", ".jpeg", ".bmp" };
            
            foreach (var ext in extensions)
            {
                var colorPath = Path.Combine(baseDir, baseName + ext);
                if (File.Exists(colorPath))
                {
                    ColorMapId = LoadTextureFile(colorPath);
                    TextureId = ColorMapId;
                    HasTexture = true;
                    return;
                }
            }
        }
        
        private int LoadTextureFile(string filename)
        {
            try
            {
                var bitmap = new Bitmap(filename);
                bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);
                var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                
                int texId = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, texId);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb, bitmap.Width, bitmap.Height, 0,
                    PixelFormat.Bgr, PixelType.UnsignedByte, data.Scan0);
                
                bitmap.UnlockBits(data);
                bitmap.Dispose();
                
                return texId;
            }
            catch
            {
                return -1;
            }
        }
        
        public void LoadTexture(string filename)
        {
            int texId = LoadTextureFile(filename);
            if (texId >= 0)
            {
                ColorMapId = texId;
                TextureId = texId;
                HasTexture = true;
            }
        }
        
        public void SetActiveTexture(int mapType)
        {
            switch (mapType)
            {
                case 0: if (ColorMapId >= 0) { TextureId = ColorMapId; HasTexture = true; } break;
                case 1: if (NormalMapId >= 0) { TextureId = NormalMapId; HasTexture = true; } break;
                case 2: if (SpecularMapId >= 0) { TextureId = SpecularMapId; HasTexture = true; } break;
                case 3: if (RoughnessMapId >= 0) { TextureId = RoughnessMapId; HasTexture = true; } break;
                case 4: if (MetallicMapId >= 0) { TextureId = MetallicMapId; HasTexture = true; } break;
                case 5: if (OpacityMapId >= 0) { TextureId = OpacityMapId; HasTexture = true; } break;
            }
        }
        
        public float GetModelSize()
        {
            var size = BoundsMax - BoundsMin;
            return Math.Max(Math.Max(size.X, size.Y), size.Z);
        }
        
        public Vector3 GetModelCenter()
        {
            return (BoundsMin + BoundsMax) / 2;
        }
        
        public void Render(bool wireframe, bool solidWhite)
        {
            if (wireframe)
            {
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                GL.Disable(EnableCap.Lighting);
                GL.Color3(0.96f, 0.96f, 0.96f);
                
                GL.Begin(PrimitiveType.Triangles);
                foreach (var face in Faces)
                {
                    for (int i = 0; i < face.VertexIndices.Length; i++)
                    {
                        if (face.VertexIndices[i] < Vertices.Count)
                            GL.Vertex3(Vertices[face.VertexIndices[i]]);
                    }
                }
                GL.End();
                
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                GL.LineWidth(1.0f);
                GL.Color3(0f, 0f, 0f);
                
                GL.Begin(PrimitiveType.Triangles);
                foreach (var face in Faces)
                {
                    for (int i = 0; i < face.VertexIndices.Length; i++)
                    {
                        if (face.VertexIndices[i] < Vertices.Count)
                            GL.Vertex3(Vertices[face.VertexIndices[i]]);
                    }
                }
                GL.End();
                
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            }
            else
            {
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                GL.Enable(EnableCap.Lighting);
                
                bool useTexture = !solidWhite && HasTexture && TextureId >= 0;
                
                if (useTexture)
                {
                    GL.Enable(EnableCap.Texture2D);
                    GL.BindTexture(TextureTarget.Texture2D, TextureId);
                    GL.Color3(1f, 1f, 1f);
                }
                else
                {
                    GL.Color3(1f, 1f, 1f);
                }
                
                GL.Begin(PrimitiveType.Triangles);
                foreach (var face in Faces)
                {
                    for (int i = 0; i < face.VertexIndices.Length; i++)
                    {
                        if (useTexture && face.TexCoordIndices[i] < TexCoords.Count)
                        {
                            GL.TexCoord2(TexCoords[face.TexCoordIndices[i]]);
                        }
                        if (face.NormalIndices[i] < Normals.Count)
                        {
                            GL.Normal3(Normals[face.NormalIndices[i]]);
                        }
                        if (face.VertexIndices[i] < Vertices.Count)
                        {
                            GL.Vertex3(Vertices[face.VertexIndices[i]]);
                        }
                    }
                }
                GL.End();
                
                if (useTexture)
                {
                    GL.Disable(EnableCap.Texture2D);
                }
            }
        }
    }
    
    public class Viewer3DForm : Form
    {
        private GLControl glControl;
        private TabControl tabControl;
        private TabPage envLightTab, statsShadingTab;
        private Button loadButton;
        private Label verticesLabel, trianglesLabel, edgesLabel, loadedLabel;
        private Panel previewPanel, lightControlPanel;
        
        private Button solidButton, wireframeButton, textureButton;
        private Button colorMapButton, normalMapButton, specularMapButton, metallicMapButton, roughnessMapButton, opacityMapButton;
        private Button showUVButton;
        
        private float rotationX = 0f, rotationY = 0f;
        private float zoom = -5f;
        private float panX = 0f, panY = 0f;
        private Vector3 lookAtPoint = Vector3.Zero;
        private float minRotationX = -90f, maxRotationX = 90f;
        private float minZoom = -1f, maxZoom = -100f;
        
        private float lightAngle = 0f;
        private bool draggingLight = false;
        
        private bool draggingRotate = false, draggingPan = false;
        private Point lastMousePos;
        private DateTime lastClickTime = DateTime.MinValue;
        
        private OBJLoader currentModel = null;
        private bool showDefaultCube = true;
        
        private int shadingMode = 0;
        private bool showUVPreview = false;
        private int currentTextureMap = 0;
        
        // Windows default colors
        private Color panelBg = Color.FromArgb(240, 240, 240);
        private Color buttonIdle = Color.FromArgb(225, 225, 225);
        private Color buttonHover = Color.FromArgb(229, 241, 251);
        private Color buttonPress = Color.FromArgb(204, 228, 247);
        private Color buttonBorder = Color.FromArgb(173, 173, 173);
        private Color buttonBorderActive = Color.FromArgb(0, 120, 215);
        
        public Viewer3DForm()
        {
            InitializeComponent();
            this.Load += (s, e) => SetupGL();
            glControl.Resize += (s, e) => { SetupProjection(); glControl.Invalidate(); };
            UpdateLabels();
        }
        
        private void InitializeComponent()
        {
            this.Text = "HotDog - 3D Viewer";
            this.ClientSize = new Size(1200, 800);
            this.MinimumSize = new Size(800, 600);
            this.AllowDrop = true;
            this.DragEnter += Viewer3DForm_DragEnter;
            this.DragDrop += Viewer3DForm_DragDrop;
            
            glControl = new GLControl(new GraphicsMode(32, 24, 0, 4));
            glControl.Dock = DockStyle.Fill;
            glControl.Paint += GlControl_Paint;
            glControl.MouseDown += GlControl_MouseDown;
            glControl.MouseUp += GlControl_MouseUp;
            glControl.MouseMove += GlControl_MouseMove;
            glControl.MouseWheel += GlControl_MouseWheel;
            this.Controls.Add(glControl);
            
            loadButton = new Button
            {
                Text = "Load OBJ",
                Location = new Point(10, 10),
                Size = new Size(120, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = buttonIdle,
                FlatAppearance = { BorderColor = buttonBorder }
            };
            loadButton.Click += LoadButton_Click;
            loadButton.MouseEnter += (s, e) => { loadButton.BackColor = buttonHover; loadButton.FlatAppearance.BorderColor = buttonBorderActive; };
            loadButton.MouseLeave += (s, e) => { loadButton.BackColor = buttonIdle; loadButton.FlatAppearance.BorderColor = buttonBorder; };
            loadButton.MouseDown += (s, e) => loadButton.BackColor = buttonPress;
            loadButton.MouseUp += (s, e) => loadButton.BackColor = buttonHover;
            glControl.Controls.Add(loadButton);
            
            tabControl = new TabControl
            {
                Dock = DockStyle.Right,
                Width = 290,
                Appearance = TabAppearance.FlatButtons
            };
            this.Controls.Add(tabControl);
            
            envLightTab = new TabPage("Env & Light") { BackColor = panelBg };
            tabControl.TabPages.Add(envLightTab);
            
            lightControlPanel = new Panel
            {
                Location = new Point(55, 120),
                Size = new Size(180, 180),
                BackColor = Color.FromArgb(250, 250, 250),
                BorderStyle = BorderStyle.FixedSingle
            };
            lightControlPanel.Paint += LightControlPanel_Paint;
            lightControlPanel.MouseDown += LightControlPanel_MouseDown;
            lightControlPanel.MouseUp += LightControlPanel_MouseUp;
            lightControlPanel.MouseMove += LightControlPanel_MouseMove;
            envLightTab.Controls.Add(lightControlPanel);
            
            statsShadingTab = new TabPage("Stats & Shading") { BackColor = panelBg };
            tabControl.TabPages.Add(statsShadingTab);
            
            int y = 10;
            
            loadedLabel = new Label
            {
                Text = "Default Cube",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Location = new Point(10, y),
                Size = new Size(270, 30),
                BackColor = panelBg
            };
            statsShadingTab.Controls.Add(loadedLabel);
            y += 40;
            
            previewPanel = new Panel
            {
                Location = new Point(10, y),
                Size = new Size(270, 270),
                BackColor = Color.FromArgb(60, 60, 60),
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false
            };
            previewPanel.Paint += PreviewPanel_Paint;
            statsShadingTab.Controls.Add(previewPanel);
            y += 280;
            
            solidButton = AddStyledButton(statsShadingTab, "Solid Shading", ref y);
            solidButton.BackColor = buttonPress;
            solidButton.Click += (s, e) => { shadingMode = 0; UpdateShadingButtons(); glControl.Invalidate(); };
            
            wireframeButton = AddStyledButton(statsShadingTab, "Wireframe View", ref y);
            wireframeButton.Click += (s, e) => { shadingMode = 1; UpdateShadingButtons(); glControl.Invalidate(); };
            
            textureButton = AddStyledButton(statsShadingTab, "Texture View", ref y);
            textureButton.Click += (s, e) => { shadingMode = 2; UpdateShadingButtons(); UpdateTextureButtonStates(); glControl.Invalidate(); };
            y += 10;
            
            var texGroup = new GroupBox
            {
                Text = "Texture Maps",
                Location = new Point(10, y),
                Size = new Size(270, 190),
                BackColor = panelBg
            };
            statsShadingTab.Controls.Add(texGroup);
            
            int gy = 25;
            colorMapButton = AddTextureButton(texGroup, "Color", ref gy);
            colorMapButton.BackColor = buttonPress;
            colorMapButton.Click += (s, e) => { currentTextureMap = 0; if (currentModel != null) currentModel.SetActiveTexture(0); UpdateTextureButtons(); glControl.Invalidate(); };
            
            normalMapButton = AddTextureButton(texGroup, "Normal", ref gy);
            normalMapButton.Click += (s, e) => { currentTextureMap = 1; if (currentModel != null) currentModel.SetActiveTexture(1); UpdateTextureButtons(); glControl.Invalidate(); };
            
            specularMapButton = AddTextureButton(texGroup, "Specular", ref gy);
            specularMapButton.Click += (s, e) => { currentTextureMap = 2; if (currentModel != null) currentModel.SetActiveTexture(2); UpdateTextureButtons(); glControl.Invalidate(); };
            
            roughnessMapButton = AddTextureButton(texGroup, "Roughness", ref gy);
            roughnessMapButton.Click += (s, e) => { currentTextureMap = 3; if (currentModel != null) currentModel.SetActiveTexture(3); UpdateTextureButtons(); glControl.Invalidate(); };
            
            metallicMapButton = AddTextureButton(texGroup, "Metallic", ref gy);
            metallicMapButton.Click += (s, e) => { currentTextureMap = 4; if (currentModel != null) currentModel.SetActiveTexture(4); UpdateTextureButtons(); glControl.Invalidate(); };
            
            opacityMapButton = AddTextureButton(texGroup, "Opacity", ref gy);
            opacityMapButton.Click += (s, e) => { currentTextureMap = 5; if (currentModel != null) currentModel.SetActiveTexture(5); UpdateTextureButtons(); glControl.Invalidate(); };
            
            y += 200;
            AddStyledButton(statsShadingTab, "Show Grid", ref y);
            AddStyledButton(statsShadingTab, "Show Axes", ref y);
            
            showUVButton = AddStyledButton(statsShadingTab, "Show UV Preview", ref y);
            showUVButton.Click += (s, e) => 
            { 
                showUVPreview = !showUVPreview; 
                previewPanel.Visible = showUVPreview;
                showUVButton.BackColor = showUVPreview ? buttonPress : buttonIdle;
                if (showUVPreview) previewPanel.Invalidate();
            };
            
            AddStyledButton(statsShadingTab, "Performance Mode", ref y);
            y += 20;
            
            var statsLabel = new Label
            {
                Text = "Model Statistics",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Location = new Point(10, y),
                AutoSize = true,
                BackColor = panelBg
            };
            statsShadingTab.Controls.Add(statsLabel);
            y += 30;
            
            verticesLabel = CreateLabel("Vertices: 8", 10, ref y);
            trianglesLabel = CreateLabel("Triangles: 12", 10, ref y);
            edgesLabel = CreateLabel("Edges: 18", 10, ref y);
            
            statsShadingTab.Controls.Add(verticesLabel);
            statsShadingTab.Controls.Add(trianglesLabel);
            statsShadingTab.Controls.Add(edgesLabel);
            
            UpdateTextureButtonStates();
        }
        
        private Button AddStyledButton(Control parent, string text, ref int y)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(10, y),
                Size = new Size(270, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = buttonIdle,
                FlatAppearance = { BorderColor = buttonBorder }
            };
            btn.MouseEnter += (s, e) => { if (btn.Enabled) { btn.BackColor = buttonHover; btn.FlatAppearance.BorderColor = buttonBorderActive; } };
            btn.MouseLeave += (s, e) => { if (btn.Enabled) { btn.BackColor = btn.BackColor == buttonPress ? buttonPress : buttonIdle; btn.FlatAppearance.BorderColor = buttonBorder; } };
            btn.MouseDown += (s, e) => { if (btn.Enabled) btn.BackColor = buttonPress; };
            btn.MouseUp += (s, e) => { if (btn.Enabled) btn.BackColor = buttonHover; };
            parent.Controls.Add(btn);
            y += 32;
            return btn;
        }
        
        private Button AddTextureButton(Control parent, string text, ref int y)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(10, y),
                Size = new Size(250, 22),
                FlatStyle = FlatStyle.Flat,
                BackColor = buttonIdle,
                FlatAppearance = { BorderColor = buttonBorder },
                Enabled = false
            };
            btn.MouseEnter += (s, e) => { if (btn.Enabled) { btn.BackColor = buttonHover; btn.FlatAppearance.BorderColor = buttonBorderActive; } };
            btn.MouseLeave += (s, e) => { if (btn.Enabled) { btn.BackColor = btn.BackColor == buttonPress ? buttonPress : buttonIdle; btn.FlatAppearance.BorderColor = buttonBorder; } };
            btn.MouseDown += (s, e) => { if (btn.Enabled) btn.BackColor = buttonPress; };
            btn.MouseUp += (s, e) => { if (btn.Enabled) btn.BackColor = buttonHover; };
            parent.Controls.Add(btn);
            y += 26;
            return btn;
			        }
        
        private Label CreateLabel(string text, int x, ref int y)
        {
            var lbl = new Label { Text = text, Location = new Point(x, y), AutoSize = true, BackColor = panelBg };
            y += 22;
            return lbl;
        }
        
        private void UpdateTextureButtons()
        {
            colorMapButton.BackColor = currentTextureMap == 0 ? buttonPress : buttonIdle;
            normalMapButton.BackColor = currentTextureMap == 1 ? buttonPress : buttonIdle;
            specularMapButton.BackColor = currentTextureMap == 2 ? buttonPress : buttonIdle;
            roughnessMapButton.BackColor = currentTextureMap == 3 ? buttonPress : buttonIdle;
            metallicMapButton.BackColor = currentTextureMap == 4 ? buttonPress : buttonIdle;
            opacityMapButton.BackColor = currentTextureMap == 5 ? buttonPress : buttonIdle;
        }
        
        private void UpdateTextureButtonStates()
        {
            bool enableTexButtons = shadingMode == 2 && currentModel != null;
            colorMapButton.Enabled = enableTexButtons && currentModel.ColorMapId >= 0;
            normalMapButton.Enabled = enableTexButtons && currentModel.NormalMapId >= 0;
            specularMapButton.Enabled = enableTexButtons && currentModel.SpecularMapId >= 0;
            roughnessMapButton.Enabled = enableTexButtons && currentModel.RoughnessMapId >= 0;
            metallicMapButton.Enabled = enableTexButtons && currentModel.MetallicMapId >= 0;
            opacityMapButton.Enabled = enableTexButtons && currentModel.OpacityMapId >= 0;
            
            if (!colorMapButton.Enabled) colorMapButton.BackColor = Color.FromArgb(230, 230, 230);
            if (!normalMapButton.Enabled) normalMapButton.BackColor = Color.FromArgb(230, 230, 230);
            if (!specularMapButton.Enabled) specularMapButton.BackColor = Color.FromArgb(230, 230, 230);
            if (!roughnessMapButton.Enabled) roughnessMapButton.BackColor = Color.FromArgb(230, 230, 230);
            if (!metallicMapButton.Enabled) metallicMapButton.BackColor = Color.FromArgb(230, 230, 230);
            if (!opacityMapButton.Enabled) opacityMapButton.BackColor = Color.FromArgb(230, 230, 230);
        }
        
        private void UpdateShadingButtons()
        {
            solidButton.BackColor = shadingMode == 0 ? buttonPress : buttonIdle;
            wireframeButton.BackColor = shadingMode == 1 ? buttonPress : buttonIdle;
            textureButton.BackColor = shadingMode == 2 ? buttonPress : buttonIdle;
        }
        
        private void LightControlPanel_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            
            int centerX = lightControlPanel.Width / 2;
            int centerY = lightControlPanel.Height / 2;
            int radius = 70;
            
            g.DrawEllipse(Pens.Gray, centerX - radius, centerY - radius, radius * 2, radius * 2);
            
            int lightX = (int)(centerX + Math.Cos(lightAngle) * (radius - 10));
            int lightY = (int)(centerY - Math.Sin(lightAngle) * (radius - 10));
            
            g.DrawLine(new Pen(Color.FromArgb(0, 120, 215), 2), centerX, centerY, lightX, lightY);
            g.FillEllipse(Brushes.Yellow, lightX - 8, lightY - 8, 16, 16);
        }
        
        private void LightControlPanel_MouseDown(object sender, MouseEventArgs e)
        {
            draggingLight = true;
            UpdateLightAngle(e.Location);
        }
        
        private void LightControlPanel_MouseUp(object sender, MouseEventArgs e)
        {
            draggingLight = false;
        }
        
        private void LightControlPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (draggingLight)
                UpdateLightAngle(e.Location);
        }
        
        private void UpdateLightAngle(Point mousePos)
        {
            int centerX = lightControlPanel.Width / 2;
            int centerY = lightControlPanel.Height / 2;
            float dx = mousePos.X - centerX;
            float dy = centerY - mousePos.Y;
            lightAngle = (float)Math.Atan2(dy, dx);
            lightControlPanel.Invalidate();
            glControl.Invalidate();
        }
        
        private void PreviewPanel_Paint(object sender, PaintEventArgs e)
        {
            if (!showUVPreview || currentModel == null || currentModel.TexCoords.Count == 0)
                return;
                
            var g = e.Graphics;
            g.Clear(Color.FromArgb(60, 60, 60));
            
            using (Pen pen = new Pen(Color.Orange, 1.2f))
            {
                float scale = Math.Min(previewPanel.Width, previewPanel.Height) * 0.9f;
                float offsetX = previewPanel.Width / 2f;
                float offsetY = previewPanel.Height / 2f;
                
                // Draw only every Nth face for performance on complex models
                int step = currentModel.Faces.Count > 10000 ? 10 : currentModel.Faces.Count > 5000 ? 5 : 1;
                
                for (int faceIdx = 0; faceIdx < currentModel.Faces.Count; faceIdx += step)
                {
                    var face = currentModel.Faces[faceIdx];
                    for (int i = 0; i < face.TexCoordIndices.Length; i++)
                    {
                        if (face.TexCoordIndices[i] < currentModel.TexCoords.Count)
                        {
                            int nextIdx = (i + 1) % face.TexCoordIndices.Length;
                            if (face.TexCoordIndices[nextIdx] < currentModel.TexCoords.Count)
                            {
                                var uv1 = currentModel.TexCoords[face.TexCoordIndices[i]];
                                var uv2 = currentModel.TexCoords[face.TexCoordIndices[nextIdx]];
                                
                                float x1 = offsetX + (uv1.X - 0.5f) * scale;
                                float y1 = offsetY + (0.5f - uv1.Y) * scale;
                                float x2 = offsetX + (uv2.X - 0.5f) * scale;
                                float y2 = offsetY + (0.5f - uv2.Y) * scale;
                                
                                g.DrawLine(pen, x1, y1, x2, y2);
                            }
                        }
                    }
                }
            }
        }
        
        private void SetupGL()
        {
            glControl.MakeCurrent();
            
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Lighting);
            GL.Enable(EnableCap.Light0);
            GL.Enable(EnableCap.Light1);
            GL.Enable(EnableCap.ColorMaterial);
            GL.ColorMaterial(MaterialFace.FrontAndBack, ColorMaterialParameter.AmbientAndDiffuse);
            
            GL.Light(LightName.Light0, LightParameter.Ambient, new[] { 0.4f, 0.4f, 0.4f, 1f });
            GL.Light(LightName.Light0, LightParameter.Diffuse, new[] { 0.9f, 0.9f, 0.9f, 1f });
            GL.Light(LightName.Light0, LightParameter.Specular, new[] { 0.6f, 0.6f, 0.6f, 1f });
            
            GL.Light(LightName.Light1, LightParameter.Position, new[] { -10f, -5f, -10f, 1f });
            GL.Light(LightName.Light1, LightParameter.Ambient, new[] { 0.2f, 0.2f, 0.2f, 1f });
            GL.Light(LightName.Light1, LightParameter.Diffuse, new[] { 0.5f, 0.5f, 0.5f, 1f });
            
            GL.ShadeModel(ShadingModel.Smooth);
            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular, new[] { 0.5f, 0.5f, 0.5f, 1f });
            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Shininess, 32f);
            GL.ClearColor(0.96f, 0.96f, 0.96f, 1f);
            
            SetupProjection();
        }
        
        private void SetupProjection()
        {
            if (glControl.Width == 0 || glControl.Height == 0) return;
            
            GL.Viewport(0, 0, glControl.Width, glControl.Height);
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            var aspectRatio = (float)glControl.Width / glControl.Height;
            Matrix4 perspective = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45), aspectRatio, 0.1f, 500f);
            GL.LoadMatrix(ref perspective);
            GL.MatrixMode(MatrixMode.Modelview);
        }
        
        private void GlControl_Paint(object sender, PaintEventArgs e)
        {
            if (!glControl.Context.IsCurrent) glControl.MakeCurrent();
            
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.LoadIdentity();
            
            float lightX = (float)Math.Cos(lightAngle) * 10f;
            float lightZ = (float)Math.Sin(lightAngle) * 10f;
            GL.Light(LightName.Light0, LightParameter.Position, new[] { lightX, 10f, lightZ, 1f });
            GL.Light(LightName.Light1, LightParameter.Position, new[] { -10f, -5f, -10f, 1f });
            
            GL.Translate(panX, panY, zoom);
            GL.Rotate(rotationX, 1, 0, 0);
            GL.Rotate(rotationY, 0, 1, 0);
            
            if (showDefaultCube)
            {
                DrawCube();
            }
            else if (currentModel != null)
            {
                var center = currentModel.GetModelCenter();
                GL.Translate(-center.X, -center.Y, -center.Z);
                
                bool renderWhite = (shadingMode == 0) || (shadingMode == 2 && currentModel.TextureId < 0);
                currentModel.Render(shadingMode == 1, renderWhite);
            }
            
            glControl.SwapBuffers();
        }
        
        private void DrawCube()
        {
            GL.Color3(0.5f, 0.7f, 1f);
            GL.Begin(PrimitiveType.Quads);
            GL.Normal3(0, 0, 1); GL.Vertex3(-1, -1, 1); GL.Vertex3(1, -1, 1); GL.Vertex3(1, 1, 1); GL.Vertex3(-1, 1, 1);
            GL.Normal3(0, 0, -1); GL.Vertex3(-1, -1, -1); GL.Vertex3(-1, 1, -1); GL.Vertex3(1, 1, -1); GL.Vertex3(1, -1, -1);
            GL.Normal3(-1, 0, 0); GL.Vertex3(-1, -1, -1); GL.Vertex3(-1, -1, 1); GL.Vertex3(-1, 1, 1); GL.Vertex3(-1, 1, -1);
            GL.Normal3(1, 0, 0); GL.Vertex3(1, -1, -1); GL.Vertex3(1, 1, -1); GL.Vertex3(1, 1, 1); GL.Vertex3(1, -1, 1);
            GL.Normal3(0, 1, 0); GL.Vertex3(-1, 1, -1); GL.Vertex3(-1, 1, 1); GL.Vertex3(1, 1, 1); GL.Vertex3(1, 1, -1);
            GL.Normal3(0, -1, 0); GL.Vertex3(-1, -1, -1); GL.Vertex3(1, -1, -1); GL.Vertex3(1, -1, 1); GL.Vertex3(-1, -1, 1);
            GL.End();
        }
        
        private void LoadButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "OBJ files (*.obj)|*.obj|All files (*.*)|*.*";
                if (dialog.ShowDialog() == DialogResult.OK)
                    LoadOBJFile(dialog.FileName);
            }
        }
        
        private void LoadOBJFile(string filename)
        {
            currentModel = new OBJLoader();
            currentModel.LoadFromFile(filename);
            showDefaultCube = false;
            
            var modelSize = currentModel.GetModelSize();
            zoom = -modelSize * 2.5f;
            minZoom = -modelSize * 0.1f;
            maxZoom = -modelSize * 20f;
            panX = 0; panY = 0;
            lookAtPoint = currentModel.GetModelCenter();
            
            loadedLabel.Text = Path.GetFileName(filename);
            UpdateLabels();
            UpdateTextureButtonStates();
            glControl.Invalidate();
        }
        
        private void UpdateLabels()
        {
            if (currentModel != null)
            {
                verticesLabel.Text = $"Vertices: {currentModel.Vertices.Count}";
                trianglesLabel.Text = $"Triangles: {currentModel.Faces.Count}";
                edgesLabel.Text = $"Edges: {currentModel.EdgeCount}";
            }
            else
            {
                verticesLabel.Text = "Vertices: 8";
                trianglesLabel.Text = "Triangles: 12";
                edgesLabel.Text = "Edges: 18";
            }
        }
        
        private void GlControl_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) 
            { 
                TimeSpan timeSinceLastClick = DateTime.Now - lastClickTime;
                if (timeSinceLastClick.TotalMilliseconds < SystemInformation.DoubleClickTime)
                {
                    if (currentModel != null)
                    {
                        lookAtPoint = currentModel.GetModelCenter();
                        rotationX = 0;
                        rotationY = 0;
                        panX = 0;
                        panY = 0;
                        glControl.Invalidate();
                    }
                }
                lastClickTime = DateTime.Now;
                
                draggingRotate = true; 
                lastMousePos = e.Location; 
            }
            else if (e.Button == MouseButtons.Right) { draggingPan = true; lastMousePos = e.Location; }
        }
        
        private void GlControl_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) draggingRotate = false;
            else if (e.Button == MouseButtons.Right) draggingPan = false;
        }
        
        private void GlControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (draggingRotate)
            {
                rotationY += (e.X - lastMousePos.X) * 0.5f;
                rotationX += (e.Y - lastMousePos.Y) * 0.5f;
                rotationX = Math.Max(minRotationX, Math.Min(maxRotationX, rotationX));
                lastMousePos = e.Location;
                glControl.Invalidate();
            }
            else if (draggingPan)
            {
                float panScale = Math.Abs(zoom) * 0.001f;
                panX += (e.X - lastMousePos.X) * panScale;
                panY -= (e.Y - lastMousePos.Y) * panScale;
                lastMousePos = e.Location;
                glControl.Invalidate();
            }
        }
        
        private void GlControl_MouseWheel(object sender, MouseEventArgs e)
        {
            float scaleFactor = (e.Delta > 0) ? 0.9f : 1.1f;
            zoom *= scaleFactor;
            zoom = Math.Max(maxZoom, Math.Min(minZoom, zoom));
            glControl.Invalidate();
        }
        
        private void Viewer3DForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }
        
        private void Viewer3DForm_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0)
            {
                string file = files[0];
                if (file.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
                    LoadOBJFile(file);
                else if (currentModel != null && (file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)))
                {
                    currentModel.LoadTexture(file);
                    UpdateTextureButtonStates();
                    glControl.Invalidate();
                }
            }
        }
    }
    
    static class Program
    {
        [STAThread]
        static void Main() { Application.EnableVisualStyles(); Application.Run(new Viewer3DForm()); }
    }
}