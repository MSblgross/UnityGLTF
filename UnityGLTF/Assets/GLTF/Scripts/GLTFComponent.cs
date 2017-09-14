using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;

namespace UnityGLTFSerialization {

	/// <summary>
	/// Component to load a GLTF scene with
	/// </summary>
	public class GLTFComponent : MonoBehaviour
	{
		public string Url;
		public bool Multithreaded = true;
		public bool UseStream = false;

		public int MaximumLod = 300;

		public Shader GLTFStandard = null;
		public Shader GLTFConstant = null;

		IEnumerator Start()
		{
			GLTFSceneImporter loader = null;
			FileStream gltfStream = null;
			if (UseStream)
			{
				var fullPath = Application.streamingAssetsPath + Path.DirectorySeparatorChar + Url;
				gltfStream = File.OpenRead(fullPath);
				var gltfRoot = GLTFSerialization.GLTFParser.ParseJson(gltfStream);
				loader = new GLTFSceneImporter(
					fullPath,
					gltfRoot,
					gltfStream
					);
			}
			else
			{
				loader = new GLTFSceneImporter(
					Url,
					gameObject.transform
					);
			}

			loader.SetShaderForMaterialType(GLTFSceneImporter.MaterialType.PbrMetallicRoughness, GLTFStandard);
			loader.SetShaderForMaterialType(GLTFSceneImporter.MaterialType.CommonConstant, GLTFConstant);
			loader.MaximumLod = MaximumLod;
			if(gltfStream != null)
			{
				GameObject node = loader.LoadNode(0);
				node.transform.SetParent(gameObject.transform, false);

#if !WINDOWS_UWP
				gltfStream.Close();
#else
				gltfStream.Dispose();
#endif
			}
			else
			{
				yield return loader.LoadScene(-1, Multithreaded);
			}
		}
	}
}
