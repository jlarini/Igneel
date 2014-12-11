﻿using Igneel;
using Igneel.Graphics;
using Igneel.Importers;
using Igneel.Rendering;
using Igneel.Rendering.Effects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace D3D9Testing.Techniques
{
    [Test]
    class Reflections
    {
         private RasterizerState rastState;   
        BoxBuilder boxBuilder;
        private GraphicBuffer vb;        

        public Reflections()
        {
            boxBuilder = new BoxBuilder(2, 2, 1);
            Vector3 translation = new Vector3(0, 0, 0.5f);
            for (int i = 0; i < boxBuilder.Vertices.Length; i++)
            {
                boxBuilder.Vertices[i].Position += translation;
            }
            vb = Engine.Graphics.CreateVertexBuffer<MeshVertex>(data: boxBuilder.Vertices);
        }

        [TestMethod]
        public void TestEnvironmentMap()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                if (d.ShowDialog() == DialogResult.OK)
                {
                    SceneTests.InitializeScene();
                    var content = ContentImporter.Import(Engine.Scene, d.FileName);                                     
                    var technique = Engine.Scene.EnumerateNodesInPreOrden().Where(x => x.Technique is EnvironmentMapTechnique).Select(x => (EnvironmentMapTechnique)x.Technique).FirstOrDefault();
                    Engine.Presenter.Rendering += () =>
                    {
                        if (rastState == null)
                        {
                            rastState = Engine.Graphics.CreateRasterizerState(new RasterizerDesc(true)
                            {
                                Fill = FillMode.Wireframe,
                                Cull = CullMode.None
                            });
                        }
                        var device = Engine.Graphics;
                        var effect = Service.Require<RenderMeshColorEffect>();

                        effect.Constants.ViewProj = Engine.Scene.ActiveCamera.ViewProj;
                        effect.Constants.Color = new Vector4(1);

                        device.PushGraphicState<RasterizerState>(rastState);
                        device.IAPrimitiveTopology = IAPrimitive.TriangleList;
                        device.IASetVertexBuffer(0, vb, 0);
                        var oldtech = effect.Technique;
                        foreach (var pass in effect.Passes(1))
                        {
                            effect.Apply(pass);
                            foreach (var camera in technique.Cameras)
                            {
                                effect.Constants.World = camera.InvViewProjection;
                                device.Draw(boxBuilder.Vertices.Length, 0);
                                //device.DrawIndexedUser(0, boxBuilder.Vertices.Length, boxBuilder.Indices.Length / 3, boxBuilder.Indices, boxBuilder.Vertices);
                            }
                        }
                        effect.EndPasses();

                        effect.Technique = oldtech;
                        device.PopGraphicState<RasterizerState>();
                    };
                }
            }
        }    


        [TestMethod]
        public void TestPlanarReflection()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                if (d.ShowDialog() == DialogResult.OK)
                {
                    SceneTests.InitializeScene();
                    var content = ContentImporter.Import(Engine.Scene, d.FileName);
                    ReflectiveNodeTechnique technique = Engine.Scene.EnumerateNodesInPreOrden().Where(x => x.Technique is ReflectiveNodeTechnique).Select(x => (ReflectiveNodeTechnique)x.Technique).FirstOrDefault();
                    Engine.Presenter.Rendering += ()=>
                    {                        
                         var untranformed = Service.Require<RenderQuadEffect>();
                        var sprite = Service.Require<Sprite>();
                        Engine.Graphics.PSStage.SetResource(0, technique.ReflectionTexture);

                        untranformed.Constants.alpha = 1;
                        untranformed.Technique = 1;

                        sprite.Begin();
                        sprite.SetTrasform(untranformed, new Rectangle(0, 0, 256, 256), Matrix.Identity);
                        sprite.DrawQuad(untranformed);
                        sprite.End();           
                    };
                }
            }
        }
    }
}