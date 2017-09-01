using Microsoft.VisualStudio.TestTools.UnitTesting;
using GLTFSerialization;
using System.Threading.Tasks;
using System.IO;

namespace GLTFSerializationTests
{
    [TestClass]
    public class GLTFLoaderTest
    {
        readonly string GLTF_PATH = Directory.GetCurrentDirectory() + "/../../../../External/glTF/BoomBox.gltf";
        readonly string GLB_PATH = Directory.GetCurrentDirectory() + "/../../../../External/glTF-Binary/BoomBox.glb";

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void LoadGLTFFromStream()
        {
            Assert.IsTrue(File.Exists(GLTF_PATH));
            FileStream gltfStream = File.OpenRead(GLTF_PATH);
            GLTFRoot gltfRoot = GLTFParser.ParseJson(gltfStream);
            GLTFLoadTestHelper.TestGLTF(gltfRoot);
        }

        [TestMethod]
        public void LoadGLBFromStream()
        {
            Assert.IsTrue(File.Exists(GLB_PATH));
            FileStream gltfStream = File.OpenRead(GLB_PATH);
            GLTFRoot gltfRoot = GLTFParser.ParseJson(gltfStream);
            GLTFLoadTestHelper.TestGLB(gltfRoot);
        }
    }
}
