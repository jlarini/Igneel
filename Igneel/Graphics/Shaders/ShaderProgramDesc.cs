﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Igneel.Graphics
{
    public class ShaderProgramDesc
    {
        private List<Shader> shaders = new List<Shader>();
        private ReadOnlyCollection<Shader> shaderCollection;      
        private GraphicDevice device;

        static class InputCache<T>
        {
            public static Dictionary<ShaderCode, InputLayout> inputCache = new Dictionary<ShaderCode,InputLayout>();
        }

        public ShaderProgramDesc(GraphicDevice device)
        {
            this.device = device;
            shaderCollection = shaders.AsReadOnly();
        }

        public GraphicDevice Device { get { return device; } }

        public InputLayout Input { get; set; }                
       
        public ReadOnlyCollection<Shader> Shaders { get{return shaderCollection;} }

        private void LinkShader<TInput>(ShaderCompilationUnit<VertexShader> cunit)
           where TInput : struct
        {
            LinkShader(cunit.Shader);
            InputLayout input;
            if (!InputCache<TInput>.inputCache.TryGetValue(cunit.Code, out input))
            {
                input = Engine.Graphics.CreateInputLayout<TInput>(cunit.Code);
            }
            Input = input;
        }

        public void LinkShader<T>(T shader) where T : Shader
        {           
            shaders.RemoveAll(x => x.GetType() == typeof(T));         
            shaders.Add(shader);           
        }       

        public void LinkShader(string filename)
        {
            if(filename.Length < 2)throw new ArgumentException();          

            string type = filename.Substring(filename.Length - 2, 2).ToLower();
            switch (type)
            {
                case "vs":
                    LinkShader(device.VS.CreateShader(filename).Shader);
                    break;
                case "ps":
                    LinkShader(device.PS.CreateShader(filename).Shader);
                    break;
                case "gs":
                    LinkShader(device.GS.CreateShader(filename).Shader);
                    break;
                case "hs":
                    LinkShader(device.HS.CreateShader(filename).Shader);
                    break;
                case "ds":
                    LinkShader(device.DS.CreateShader(filename).Shader);
                    break;
                case "cs":
                    LinkShader(device.CS.CreateShader(filename).Shader);
                    break;
                default:
                    throw new ShaderCompilationException("Invalid Shader Filename");                    
            }
        }

        public void LinkVertexShader<TInput>(string filename) where TInput :struct
        {
            if (filename.Length < 2) throw new ArgumentException();
           
            string type = filename.Substring(filename.Length - 2, 2).ToLower();
            switch (type)
            {
                case "vs":
                    LinkShader<TInput>(device.VS.CreateShader(filename));
                    break;
                case "ps":
                    LinkShader(device.PS.CreateShader(filename).Shader);
                    break;
                case "gs":
                    LinkShader(device.GS.CreateShader(filename).Shader);
                    break;
                case "hs":
                    LinkShader(device.HS.CreateShader(filename).Shader);
                    break;
                case "ds":
                    LinkShader(device.DS.CreateShader(filename).Shader);
                    break;
                case "cs":
                    LinkShader(device.CS.CreateShader(filename).Shader);
                    break;
                default:
                    throw new ShaderCompilationException("Invalid Shader Filename");
            }
        }

        public void LinkGeometryShader<TOutput>(string filename, bool rasterizedStream0 =false) where TOutput : struct
        {
            int[] strides;
            var outputDesc = StreamOutDeclaration.GetDeclaration(typeof(TOutput), out strides);
            var byteCode = device.GS.CompileFromFile(filename);
            LinkShader(device.GS.CreateShaderWithSO(byteCode, outputDesc, strides, rasterizedStream0));
        }
    }
}
