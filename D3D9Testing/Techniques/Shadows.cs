﻿using Igneel;
using Igneel.Assets;
using Igneel.Scenering;
using Igneel.Graphics;
using Igneel.Importers;
using Igneel.Rendering;
using Igneel.Scenering.Effects;
using Igneel.Windows.Forms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Igneel.Scenering.Assets;

namespace D3D9Testing.Techniques
{
    [Test]
    public class Shadows
    {       
        private RasterizerState rastState;
        Camera targetCamera;
        Mesh box;
        Matrix translation;
        private ShadowMapTechnique technique;
        GraphicBuffer vb;

        public Shadows()
        {
            box = Mesh.CreateBox(2, 2, 1);
            translation = Matrix.Translate(0, 0, 0.5f);                       
        }

        ContentPackage ImportContent()
        {
            ContentPackage content = null;
            using (OpenFileDialog d = new OpenFileDialog())
            {
                if (d.ShowDialog() == DialogResult.OK)
                {
                    SceneTests.InitializeScene();
                    content = ContentImporter.Import(SceneManager.Scene, d.FileName);
                }
            }
            return content;
        }

        [TestMethod]
        public void Import()
        {         
                    SceneTests.InitializeScene();
            
                    //RenderManager.PushTechnique<DefferedLigthing<DefferedLightingEffect>>();
                    //EngineState.Shadow.Enable = true;
                    //EngineState.Shadow.ShadowMapping.Enable = true;
                    //Deffered.DrawGBuffers<DefferedShadowRender>();

                   // var content = ContentImporter.Import(SceneManager.Scene, @"C:\Users\ansel\Documents\3dsmax\export\shadowScene.DAE");
                    var content = ImportContent();
                    if(content == null)
                        return;

                    if (SceneManager.Scene.Physics != null)
                        SceneManager.Scene.Physics.Enable = true;

                    if (SceneManager.Scene.Lights.Count == 0)
                    {
                        var light = new Light()
                        {
                            Diffuse = new Vector3(1, 1, 1),
                            Specular = new Vector3(1, 1, 1),
                            Type = LightType.Directional,
                            Enable = true
                        };

                        SceneManager.Scene.Create("DirectionalLight0", new LightInstance(light),
                            localRotationEuler:new Euler(0, Numerics.ToRadians(70), 0));                           

                        LightInstance.CreateShadowMapForAllLights(SceneManager.Scene);
                    }
                    technique = SceneManager.Scene.Lights.Where(x => x.Node.Technique is ShadowMapTechnique).Select(x => (ShadowMapTechnique)x.Node.Technique).FirstOrDefault();
                    if (technique == null)
                    {
                        LightInstance.CreateShadowMapForAllLights(SceneManager.Scene);
                        technique = SceneManager.Scene.Lights.Where(x => x.Node.Technique is ShadowMapTechnique).Select(x => (ShadowMapTechnique)x.Node.Technique).FirstOrDefault();
                    }
                    targetCamera = technique.Camera;
                    technique.KernelSize = 3;      
                    //targetCamera.ZNear = 1;
                    //targetCamera.ZFar = 1000;
                    //targetCamera.AspectRatio = 1;
                    //targetCamera.FieldOfView = Numerics.PIover6;
                    //targetCamera.Type = ProjectionType.Perspective;
                    //targetCamera.CommitChanges();
                    technique.Bias = 0.9e-2f;                  

                    SceneManager.Scene.AmbientLight.GroundColor = new Vector3(0, 0, 0);
                    SceneManager.Scene.AmbientLight.SkyColor = new Vector3(0.2f, 0.2f, 0.2f);
                    //EngineState.Lighting.Reflection.UseDefaultTechnique = true;
                  
                    Engine.Presenter.Rendering += Presenter_Rendering;
                    SceneManager.Scene.Dynamics.Add(new Dynamic(x =>
                    {
                        if (Engine.KeyBoard.IsKeyPressed(Igneel.Input.Keys.D1))
                            technique.KernelSize = 3;
                        else if (Engine.KeyBoard.IsKeyPressed(Igneel.Input.Keys.D2))
                            technique.KernelSize = 5;
                        else if (Engine.KeyBoard.IsKeyPressed(Igneel.Input.Keys.D3))
                            technique.KernelSize = 7;
                        else if (Engine.KeyBoard.IsKeyPressed(Igneel.Input.Keys.D4))
                            technique.KernelSize = 1;
                    }));
            //    }
            //}
        }

        [TestMethod]
        public void AutomaticShadowMapping()
        {
            SceneTests.InitializeScene();
            var content = ImportContent();
            content.OnAddToScene(SceneManager.Scene);

            //ContentImporter.Import(SceneManager.Scene, @"C:\Users\ansel\Documents\3dsmax\export\nissan2.DAE");
            //ContentImporter.Import(SceneManager.Scene, @"C:\Users\ansel\Documents\3dsmax\export\talia.DAE");            

            if (SceneManager.Scene.Physics != null)
                SceneManager.Scene.Physics.Enable = true;

            if (SceneManager.Scene.Lights.Count == 0)
            {
                var light = new Light()
                {
                    Diffuse = new Vector3(1, 1, 1),
                    Specular = new Vector3(1, 1, 1),
                    Type = LightType.Directional,
                    Enable = true
                };

                SceneManager.Scene.Create("DirectionalLight0", new LightInstance(light),
                    localRotationEuler: new Euler(0, Numerics.ToRadians(70), 0));                
            }
            LightInstance.CreateShadowMapForAllLights(SceneManager.Scene);
            technique = SceneManager.Scene.Lights.Where(x => x.Node.Technique is ShadowMapTechnique).Select(x => (ShadowMapTechnique)x.Node.Technique).FirstOrDefault();
            targetCamera = technique.Camera;

            technique.Bias = 0.9e-2f;

            SceneManager.Scene.AmbientLight.GroundColor = new Vector3(0, 0, 0);
            SceneManager.Scene.AmbientLight.SkyColor = new Vector3(0.2f, 0.2f, 0.2f);

            Engine.Presenter.Rendering += Presenter_Rendering;
            SceneManager.Scene.Dynamics.Add(new Dynamic(x =>
            {
                if (Engine.KeyBoard.IsKeyPressed(Igneel.Input.Keys.D1))
                    technique.KernelSize = 3;
                else if (Engine.KeyBoard.IsKeyPressed(Igneel.Input.Keys.D2))
                    technique.KernelSize = 5;
                else if (Engine.KeyBoard.IsKeyPressed(Igneel.Input.Keys.D3))
                    technique.KernelSize = 7;
                else if (Engine.KeyBoard.IsKeyPressed(Igneel.Input.Keys.D4))
                    technique.KernelSize = 1;
            }));
        }

        [TestMethod]
        public void EdgeFiltering()
        {
            SceneTests.InitializeScene();
            
            //var content = ImportContent();
            var content = ContentImporter.Import(SceneManager.Scene, @"C:\Users\ansel\Documents\3dsmax\export\shadowScene.DAE");

            EngineState.Shadow.ShadowMapping.PCFBlurSize = 5;
            content.OnAddToScene(SceneManager.Scene);

            //ContentImporter.Import(SceneManager.Scene, @"C:\Users\ansel\Documents\3dsmax\export\nissan2.DAE");
            //ContentImporter.Import(SceneManager.Scene, @"C:\Users\ansel\Documents\3dsmax\export\talia.DAE");            

            if (SceneManager.Scene.Physics != null)
                SceneManager.Scene.Physics.Enable = true;

            //if (SceneManager.Scene.Lights.Count == 0)
            //{
            //    var light = new Light()
            //    {
            //        Diffuse = new Vector3(1, 1, 1),
            //        Specular = new Vector3(1, 1, 1),
            //        Type = LightType.Directional,
            //        Enable = true
            //    };

            //    SceneManager.Scene.Create("DirectionalLight0", new LightInstance(light),
            //        localRotationEuler: new Euler(0, Numerics.ToRadians(70), 0));
            //}
            LightInstance.CreateShadowMapForAllLights(SceneManager.Scene);
            technique = SceneManager.Scene.Lights.Where(x => x.Node.Technique is ShadowMapTechnique).Select(x => (ShadowMapTechnique)x.Node.Technique).FirstOrDefault();
            targetCamera = technique.Camera;

            technique.Bias = 0.9e-2f;

            SceneManager.Scene.AmbientLight.GroundColor = new Vector3(0, 0, 0);
            SceneManager.Scene.AmbientLight.SkyColor = new Vector3(0.2f, 0.2f, 0.2f);

            EngineState.Shadow.ShadowMapping.PCFBlurSize = 3;
            var edgeTechnique = new EdgeShadowFilteringTechnique();
            RenderManager.PushTechnique(edgeTechnique);
            bool debug = true;

            if (debug)
            {
                Form form = new Form();
                form.BackColor = Color.Blue;
                form.StartPosition = FormStartPosition.CenterScreen;
                form.Size = new System.Drawing.Size(edgeTechnique.ShadowFactorTex.Width, edgeTechnique.ShadowFactorTex.Height);

                form.SuspendLayout();

                Canvas3D canvas = new Canvas3D()
                {
                    Width = form.Width,
                    Height = form.Height
                };
                canvas.Dock = DockStyle.Fill;
                var presenter = canvas.CreateSwapChainPresenter();
                form.Controls.Add(canvas);
                form.ResumeLayout();

                Engine.RenderFrame += () =>
                {
                    presenter.Begin(new Color4(Color.Aqua.ToArgb()));

                    var device = GraphicDeviceFactory.Device;
                    device.PS.SamplerStacks[0].Push(SamplerState.Linear);                    
                    device.Blend = SceneTechnique.NoBlend;

                    var texture = edgeTechnique.EdgeSrcTexture;
                    RenderTexture(device, texture, width: texture.Width, height: texture.Height);

                    device.PS.SamplerStacks[0].Pop();

                    presenter.End();
                };

                form.Show();
            }            
         
        }
       

        void screen_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.D1:
                    technique.KernelSize = 3;
                    break;
                case Keys.D2:
                    technique.KernelSize = 5;
                    break;
                case Keys.D3:
                    technique.KernelSize = 7;
                    break;
            }                   
        }        

        void Presenter_Rendering()
        {
            if (rastState == null)
            {
                rastState = GraphicDeviceFactory.Device.CreateRasterizerState(new RasterizerDesc(true)
                {
                    Fill = FillMode.Wireframe,
                    Cull = CullMode.None
                });
            }
            var device = GraphicDeviceFactory.Device;
            var effect = Service.Require<RenderMeshIdEffect>();            

            effect.U.World = translation * targetCamera.InvViewProjection;
            effect.U.ViewProj = SceneManager.Scene.ActiveCamera.ViewProj;
            effect.U.gId = new Vector4(1);

            device.RasterizerStack.Push(rastState);
            box.Draw(device, effect);            
            device.RasterizerStack.Pop();

            RenderTexture(device, technique.DepthTexture);
        }
      
        private void RenderTexture(GraphicDevice device, Texture texture , int x = 0, int y=0, int width =256, int height = 256)
        {
            var untranformed = Service.Require<RenderQuadEffect>();
            var sprite = Service.Require<Sprite>();
            device.PS.SetResource(0, texture );
            device.PS.SetSampler(0, SamplerState.Linear);

            sprite.Begin();
            sprite.SetTrasform(untranformed, new Igneel.Rectangle(x, y, width, height), Matrix.Identity);
            sprite.DrawQuad(untranformed);
            sprite.End();

            device.PS.SetResource(0, null);
        }
    }
}
