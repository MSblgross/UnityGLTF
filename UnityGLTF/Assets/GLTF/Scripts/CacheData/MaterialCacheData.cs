﻿namespace UnityGLTFSerialization.CacheData
{
    public class MaterialCacheData
    {
        public UnityEngine.Material UnityMaterial { get; set; }
        public UnityEngine.Material UnityMaterialWithVertexColor { get; set; }
        public GLTFSerialization.Material GLTFMaterial { get; set; }

        public UnityEngine.Material GetContents(bool useVertexColors)
        {
            return useVertexColors ? UnityMaterialWithVertexColor : UnityMaterial;
        }
    }
}
