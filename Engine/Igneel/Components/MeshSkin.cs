﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Igneel.Assets;
using Igneel.Graphics;
using Igneel.SceneManagement;
using Igneel.Utilities;

namespace Igneel.Components
{

    [Asset("OBJECT")]
    public class MeshSkin:Resource
    {
        Mesh _mesh;
        Frame[] _bones;
        Frame _boneRoot;           

        /// <summary>
        /// transforms of the bones when the mesh was bind to the skelleton 
        /// </summary>
        private Matrix[] _boneBindingMatrices;

        /// <summary>
        /// transform of the mesh when it was bind to the skelleton
        /// boneMatrix = bindShape * boneBindingMatrix * boneCombinedMatrix
        /// </summary>
        private Matrix _bindShapeMatrix = Matrix.Identity;

        internal int[][] layerBonesLookup;
        private int _maxVertexInfluences;

        public MeshSkin()
        {

        }

        public MeshSkin(Mesh mesh)
        {
            this._mesh = mesh;
            Name = _mesh.Name;
        }

        public Frame BoneRoot
        {
            get { return _boneRoot; }
        }

        [AssetMember]
        public int[][] LayerBonesLookup { get { return layerBonesLookup; } set { layerBonesLookup = value; } }

        [AssetMember(storeAs: StoreType.Reference)]
        public Mesh Mesh
        {
            get { return _mesh; }
            set
            {
                _mesh = value;
                Name = _mesh.Name;
            }
        }

        [AssetMember]
        public int MaxVertexInfluences { get { return _maxVertexInfluences; } set { _maxVertexInfluences = value; } }
        
        public Frame[] Bones
        {
            get { return _bones; }
            set
            {
                _bones = value;
                if (_bones != null)
                {
                    FindBoneRoot();
                }
            }
        }

      
        [AssetMember]
        public Matrix[] BoneBindingMatrices { get { return _boneBindingMatrices; } set { _boneBindingMatrices = value; } }

        [AssetMember]
        public Matrix BindShapePose { get { return _bindShapeMatrix; } set { _bindShapeMatrix = value; } }

        public bool HasBonesPerLayer { get { return layerBonesLookup != null; } }

        private void FindBoneRoot()
        {
            if (_bones.Length == 1)
                _boneRoot = _bones[0];

            else if (_bones.Length > 0)
            {
                _boneRoot = BoneInfo.GetRootBone(_bones[0]);
            }
        }


        public void ComputeBindingPoses()
        {
            if (_bones == null) throw new NullReferenceException("Bones not Set");

            for (int i = 0; i < _bones.Length; i++)
            {
                _boneBindingMatrices[i] = Matrix.Invert(_bones[i].GlobalPose);
            }
        }

        public int[] GetBones(MeshPart layer)
        {
            if (layerBonesLookup != null)
                return layerBonesLookup[layer.LayerId];
            return null;
        }

        public int[] GetBones(int meshLayerIdx)
        {
            if (layerBonesLookup != null)
                return layerBonesLookup[meshLayerIdx];
            return null;
        }

        public void SetBones(int layerIndex, int[] bonesIDs)
        {
            if (layerBonesLookup == null)
                layerBonesLookup = new int[_mesh.Layers.Length][];

            layerBonesLookup[layerIndex] = new int[bonesIDs.Length];

            Array.Copy(bonesIDs, layerBonesLookup[layerIndex], bonesIDs.Length);

            //for (int i = 0; i < bonesIDs.Length; i++)
            //{
            //    layerBonesLookup[layerIndex][i] = bonesIDs[i];
            //}
        }

        public IEnumerable<T> GetVertexAttrib<T>(int boneIndex, IASemantic usage, int index)
           where T : struct
        {
            var attrib = _mesh.GetVertexBufferView<T>(usage, index);
            var boneIndices = _mesh.GetVertexBufferView<float>(IASemantic.BlendIndices, 0);            
            for (int i = 0; i < attrib.Count; i++)
            {                
                for (int j = 0; j < 4; j++)
                {
                    if (boneIndices[i + j] == boneIndex)
                    {
                        yield return attrib[i];
                    }
                }
            }            
        }
        
        public void SkinMesh(int maxPalleteEntries)
        {
            ReSkinner skinner = new ReSkinner();
            skinner.ReSkin(this, maxPalleteEntries);
        }                   

        unsafe
        struct ReSkinner
        {
            private Mesh _mesh;
            private IntPtr _ibDataStream;        
            private bool _sixteenBits;
            private int _primitiveStride;
            private int _vertexStride;
            private int _indicesOffset;
            private int _weightsOffset;
            private byte[] _vbNewData;
            private List<int[]> _layerBones;
            private int[] _boneLookup;
            private bool[] _vertices;          
            private List<MeshPart> _newLayers;
            private unsafe byte* _pIbData;
            private unsafe byte* _pVbDAta;
            int _layerBonesCount;
            int _maxPalleteEntries;
            HashSet<int> _tempBonesSet;

            public void ReSkin(MeshSkin skin, int maxPalleteEntries)
            {
                this._maxPalleteEntries = maxPalleteEntries;
                this._mesh = skin.Mesh;              
                _sixteenBits = _mesh.Is16BitIndices;
                _primitiveStride = 3 * (_sixteenBits ? 2 : 4);
                _vertexStride = _mesh.VertexDescriptor.Size;
                _indicesOffset = _mesh.VertexDescriptor.OffsetOf(IASemantic.BlendIndices, 0);
                _weightsOffset = _mesh.VertexDescriptor.OffsetOf(IASemantic.BlendWeight, 0);
                _tempBonesSet = new HashSet<int>();

                _ibDataStream = _mesh.IndexBuffer.Map();
                _vbNewData = _mesh.VertexBuffer.ToArray<byte>();                
                //vbDataStream.Read(vbNewData, 0, mesh.VertexBuffer.SizeInBytes);                             

                //holds an array of bones indices into the skin bones array, an array of bones for each layer
                _layerBones = new List<int[]>();

                //table containing a mapping betwing a skin bone index and a layer bone index
                _boneLookup = new int[skin._bones.Length];

                //a set of vertices indices into the vertex buffer
                _vertices = new bool[_mesh.VertexCount];

                //list of new layers
                _newLayers = new List<MeshPart>(_mesh.Layers.Length);

                //reset the bones mapping
                for (int i = 0; i < _boneLookup.Length; i++)
                    _boneLookup[i] = -1;

                //reset the vertex set                      
                Array.Clear(_vertices, 0, _vertices.Length);

                //current processing layer
                MeshPart newLayer = null;

                GCHandle pinH = GCHandle.Alloc(_vbNewData, GCHandleType.Pinned);
                _pVbDAta = (byte*)Marshal.UnsafeAddrOfPinnedArrayElement(_vbNewData, 0);

                //get  a  pointer to the indexbuffer stream
                _pIbData = (byte*)_ibDataStream.ToPointer();
                for (int matIdx = 0; matIdx < _mesh.MaterialSlots; matIdx++)
                {
                    //for each material get the corresponding layers
                    var layers = _mesh.GetLayersByMaterial(matIdx);

                    //create the first layer for matIdx
                    newLayer = new MeshPart();
                    newLayer.materialIndex = matIdx;
                    newLayer.startIndex = layers[0].startIndex;

                    int primitiveCount = 0;                   

                    foreach (var layer in layers)
                    {
                        //loop through add to the layer as much bones as maxPalleteEntries
                        //newLayer.startIndex = Math.Min(newLayer.startIndex, layer.startIndex);
                        for (int itriangle = 0; itriangle < layer.primitiveCount; itriangle++)
                        {
                            primitiveCount++;
                            //check if the triangle can be added to the newLayer
                            if (!AddTriangleBones(layer, itriangle))
                            {
                                //the layer is full, close it and open a new layer
                                newLayer.primitiveCount = primitiveCount - 1;
                                CloseLayer(newLayer);

                                //start a new Layer       
                                newLayer = new MeshPart();
                                newLayer.materialIndex = matIdx;
                                newLayer.startIndex = layer.startIndex + 3 * itriangle;

                                //add the triangle to the newlayer
                                AddTriangleBones(layer, itriangle);
                                primitiveCount = 1;
                            }
                        }                       
                    }

                    newLayer.primitiveCount = primitiveCount;
                    CloseLayer(newLayer);                 
                }
               
                _mesh.Layers = _newLayers.ToArray();
                for (int i = 0; i < _layerBones.Count; i++)
                {
                    skin.SetBones(i, _layerBones[i]);
                }

                //for (int i = 0; i < mesh.VertexCount; i++)
                //{
                //    byte* bones = GetVertexBones(i);
                //    float* weights = GetVertexWeights(i);
                //    for (int j = 0; j < 4; j++)
                //    {
                //        if (bones[j] == 0xFF)
                //        {
                //            bones[j] = 0;
                //            weights[j] = 0;
                //        }
                //    }
                //}

                pinH.Free();

                _mesh.VertexBuffer.Unmap();
                _mesh.IndexBuffer.Unmap();

                _mesh.VertexBuffer.Write(_vbNewData);                
            }

            unsafe private bool AddTriangleBones(MeshPart layer, int iTriangle)
            {
                int* indices = stackalloc int[3];

                indices[0] = GetVertexIndex(layer.startIndex + 3 * iTriangle);
                indices[1] = GetVertexIndex(layer.startIndex + 3 * iTriangle + 1);
                indices[2] = GetVertexIndex(layer.startIndex + 3 * iTriangle + 2); //sixteenBits ? (int)((ushort*)pIbData)[layer.startIndex + 3 * iTriangle + 2] : ((int*)pIbData)[layer.startIndex + 3 * iTriangle + 2];              

                if (AddTriangleBones(indices))
                {
                    foreach (var boneIdx in _tempBonesSet)
                    {
                        //add the bone to the layer
                          _boneLookup[boneIdx] = _layerBonesCount++;                          
                    }
                  
                    //assign new bones indices tp trianglesVertices
                    for (int k = 0; k < 3; k++)
                    {                       
                        if (!_vertices[indices[k]])
                        {
                            //mark the vertex as processed
                            _vertices[indices[k]] = true;

                            float* pvBones = GetVertexBones(indices[k]);
                            float* pvWeights = GetVertexWeights(indices[k]);

                            for (int j = 0; j < 4; j++)
                            {
                                if (pvWeights[j] > 0)
                                    pvBones[j] = _boneLookup[(int)pvBones[j]];
                            }   
                        }
                    }

                    return true;
                }

                return false;
            }

            unsafe private bool AddTriangleBones(int* indices)
            {
                /*
                * First of all you need to check if all the triangle's bones can be added
                * if the test pass then add the 
                */
                int boneCount = 0;
                _tempBonesSet.Clear();

                //First of all you need to check if all the triangle's bones can be added
                for (int k = 0; k < 3; k++)
                {
                    float* bones = GetVertexBones(indices[k]);
                    float* weights = GetVertexWeights(indices[k]);

                    for (int ibone = 0; ibone < 4; ibone++)
                    {
                        //check all the bones 
                        int boneIdx = (int)bones[ibone];
                        if (weights[ibone] > 0 && _boneLookup[boneIdx] < 0 && !_tempBonesSet.Contains(boneIdx))
                        {
                            boneCount++;
                            //if the bones is valid and its not in the layer check if there is space for it
                            if (_layerBonesCount + boneCount > _maxPalleteEntries)
                                return false;

                            //add the bone to the layer                        
                            _tempBonesSet.Add(boneIdx);
                        }
                    }
                }
                return true;
            }

            //unsafe private void AssingBones(MeshPart layer)
            //{
            //    for (int i = 0; i < layer.primitiveCount; i++)
            //    {
            //        for (int k = 0; k < 3; k++)
            //        {
            //            byte* pvBones = GetVertexBones(GetVertexIndex(layer.startIndex + 3 * i + k));
            //            float* pvWeights = GetVertexWeights(GetVertexIndex(layer.startIndex + 3 * i + k));

            //            for (int j = 0; j < 4; j++)
            //            {
            //                if (pvBones[j] < 0xFF)
            //                    pvBones[j] = (byte)boneLookup[pvBones[j]];
            //            }   
            //        }                    
            //    }             
            //}

            unsafe float* GetVertexBones(int ivertex)
            {
                return (float*)(_pVbDAta + ivertex * _vertexStride + _indicesOffset);
            }

            unsafe float* GetVertexWeights(int ivertex)
            {
                return (float*)(_pVbDAta + ivertex * _vertexStride + _weightsOffset);
            }

            int GetVertexIndex(int i)
            {
               return _sixteenBits ? (int)((ushort*)_pIbData)[i] : ((int*)_pIbData)[i];
            }

            void CloseLayer(MeshPart layer)
            {
                layer.IndexCount = layer.PrimitiveCount * 3;

                int vertexCount = 0;
                int startVertex = int.MaxValue;                

                for (int i = 0; i < _vertices.Length; i++)
                {
                    if (_vertices[i])
                    {
                        vertexCount++;
                        startVertex = Math.Min(startVertex, i);
                    }
                    //reset processed vertex
                    _vertices[i] = false;
                }

                layer.vertexCount = vertexCount;
                layer.startVertex = startVertex;
                int[] invbones = new int[_layerBonesCount];                          
                for (int i = 0; i < _boneLookup.Length; i++)
                {
                    if (_boneLookup[i] >= 0)
                        invbones[_boneLookup[i]] = i;

                    //reset bones
                    _boneLookup[i] = -1;
                }

                _layerBones.Add(invbones);
                _newLayers.Add(layer);
                _layerBonesCount = 0;
            }

        }

        //class SkinBonesConverter : IStoreConverter
        //{
        //    public object GetStorage(IAssetProvider provider, object propValue, System.Reflection.PropertyInfo pi)
        //    {
        //        var skin = (SkinDeformer)provider;
        //        Frame[] bones = skin.Bones;

        //        AssetReference[] refe = new AssetReference[bones.Length];
        //        for (int i = 0; i < refe.Length; i++)
        //        {
        //            refe[i] = AssetManager.Instance.GetAssetReference(bones[i]);
        //        }

        //        return refe;
        //    }

        //    public void SetStorage(IAssetProvider provider, object storeValue, System.Reflection.PropertyInfo pi)
        //    {
        //        AssetReference[] refe = (AssetReference[])storeValue;
        //        var bones = new Frame[refe.Length];

        //        for (int i = 0; i < refe.Length; i++)
        //        {
        //            bones[i] = (Frame)AssetManager.Instance.GetAssetProvider(refe[i]);
        //        }

        //        var skin = (SkinDeformer)provider;
        //        skin.Bones = bones;
        //    }
        //}

        protected override void OnDispose(bool disposing)
        {
            if (disposing)
            {
                _mesh = null;
                _bones = null;
                _boneRoot = null;
            }
        }
    }
}
