using GLTF;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGLTF.Cache
{
	public class MeshCacheData
	{
		public Mesh LoadedMesh { get; set; }
		public List<Dictionary<string, AttributeAccessor>> PrimitivesMeshAttributes { get; set; }
        public GameObject PrimitiveGO { get; set; }

		public MeshCacheData()
		{
			PrimitivesMeshAttributes = new List<Dictionary<string, AttributeAccessor>>();
		}

		/// <summary>
		/// Unloads the meshes in this cache.
		/// </summary>
		public void Unload()
		{
			Object.Destroy(LoadedMesh);
		}
	}
}
