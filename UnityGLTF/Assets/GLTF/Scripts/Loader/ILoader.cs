using System.IO;
using GLTFSerialization;

namespace UnityGLTFSerialization.Loader
{
	public interface ILoader
	{
		Stream LoadJSON(string gltfFilePath);
		Stream LoadBuffer(Buffer buffer);
		UnityEngine.Texture2D LoadImage(Image image);
	}
}
