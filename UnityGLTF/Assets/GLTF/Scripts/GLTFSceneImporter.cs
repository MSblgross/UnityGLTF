using GLTFJsonSerialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityGLTFSerialization.CacheData;
using UnityGLTFSerialization.Extensions;

namespace UnityGLTFSerialization
{
    public enum LoadType
    {
        Uri,
        Stream
    }

    public class GLTFSceneImporter
    {
        public enum MaterialType
        {
            PbrMetallicRoughness,
            PbrSpecularGlossiness,
            CommonConstant,
            CommonPhong,
            CommonBlinn,
            CommonLambert
        }

        protected GameObject _lastLoadedScene;
        protected readonly Transform _sceneParent;
        protected readonly Dictionary<MaterialType, Shader> _shaderCache = new Dictionary<MaterialType, Shader>();
        public int MaximumLod = 300;
        protected readonly GLTFJsonSerialization.Material DefaultMaterial = new GLTFJsonSerialization.Material();
        protected string _gltfUrl;
        protected string _gltfDirectoryPath;
        protected Stream _gltfStream;
        protected GLTFRoot _root;
        protected AssetCache _assetCache;
        protected AsyncAction _asyncAction;
        protected byte[] _gltfData;
        protected LoadType _loadType;
        protected Func<GLTFJsonSerialization.Material, UnityEngine.Material> _fMaterialLoadCallback;

        /// <summary>
        /// Creates a GLTFSceneBuilder object which will be able to construct a scene based off a url
        /// </summary>
        /// <param name="gltfUrl">URL to load</param>
        /// <param name="parent"></param>
        public GLTFSceneImporter(string gltfUrl, Transform parent = null, Func<GLTFJsonSerialization.Material, UnityEngine.Material> materialLoadCallback = null)
        {
            _gltfUrl = gltfUrl;
            _gltfDirectoryPath = AbsoluteUriPath(gltfUrl);
            _sceneParent = parent;
            _asyncAction = new AsyncAction();
            _loadType = LoadType.Uri;
            if (materialLoadCallback == null)
            {
                _fMaterialLoadCallback = CreateMaterial;
            }
        }

        public GLTFSceneImporter(string rootPath, Stream stream, Transform parent = null, Func<GLTFJsonSerialization.Material, UnityEngine.Material> materialLoadCallback = null)
        {
            _gltfUrl = rootPath;
            _gltfDirectoryPath = AbsoluteFilePath(rootPath);
            _gltfStream = stream;
            _sceneParent = parent;
            _asyncAction = new AsyncAction();
            _loadType = LoadType.Stream;
            if (materialLoadCallback == null)
            {
                _fMaterialLoadCallback = CreateMaterial;
            }
        }

        public GLTFSceneImporter(string rootPath, GLTFRoot rootNode, Stream glbStream = null, Func<GLTFJsonSerialization.Material, UnityEngine.Material> materialLoadCallback = null)
        {
            _gltfUrl = rootPath;
            _gltfDirectoryPath = AbsoluteFilePath(rootPath);
            _root = rootNode;
            _loadType = LoadType.Stream;
            _gltfStream = glbStream;
            if (materialLoadCallback == null)
            {
                _fMaterialLoadCallback = CreateMaterial;
            }
        }

        public GameObject LastLoadedScene
        {
            get
            {
                return _lastLoadedScene;
            }
        }

        /// <summary>
        /// Configures shaders in the shader cache for a given material type
        /// </summary>
        /// <param name="type">Material type to setup shader for</param>
        /// <param name="shader">Shader object to apply</param>
        public virtual void SetShaderForMaterialType(MaterialType type, Shader shader)
        {
            _shaderCache.Add(type, shader);
        }
        
        /// <summary>
        /// Loads via a web call the gltf file
        /// </summary>
        /// <returns></returns>
        public IEnumerator LoadJson()
        {
            if (_loadType == LoadType.Uri)
            {
                var www = UnityWebRequest.Get(_gltfUrl);

                yield return www.Send();
                if (www.responseCode >= 400)
                {
                    Debug.LogErrorFormat("{0} - {1}", www.responseCode, www.url);
                    yield break;
                }

                _gltfData = www.downloadHandler.data;
                _gltfStream = new MemoryStream(_gltfData, 0, _gltfData.Length, false, true);
            }
            else if(_loadType != LoadType.Stream)
            {
                throw new Exception("Invalid load type specified: " + _loadType);
            }

            _root = GLTFParser.ParseJson(_gltfStream);
        }
        
        /// <summary>
        /// Loads a GLTF scene into unity
        /// </summary>
        /// <param name="sceneIndex">Index into scene to load. -1 means load default</param>
        /// <param name="isMultithreaded">Whether to do loading operation on a thread</param>
        public IEnumerator LoadScene(int sceneIndex = -1, bool isMultithreaded = false)
        {
            if(_root == null)
            {
                yield return LoadJson();
            }
            yield return ImportScene(sceneIndex, isMultithreaded);
        }

        public GameObject LoadNode(int nodeIndex)
        {
            if (_root == null)
            {
                throw new InvalidOperationException("GLTF root must first be loaded and parsed");
            }

            if (_assetCache == null)
            {
                InitializeAssetCache();
            }

            return _LoadNode(nodeIndex);
        }

        private GameObject _LoadNode(int nodeIndex)
        {
            if(nodeIndex >= _root.Nodes.Count)
            {
                throw new ArgumentException("nodeIndex is out of range");
            }

            return CreateNode(_root.Nodes[nodeIndex]);
        }

        protected void InitializeAssetCache()
        {
            _assetCache = new AssetCache(
                _root.Images != null ? _root.Images.Count : 0,
                _root.Textures != null ? _root.Textures.Count : 0,
                _root.Materials != null ? _root.Materials.Count : 0,
                _root.Buffers != null ? _root.Buffers.Count : 0,
                _root.Meshes != null ? _root.Meshes.Count : 0
                );
        }
        
        /// <summary>
        /// Creates a scene based off loaded JSON. Includes loading in binary and image data to construct the meshes required.
        /// </summary>
        /// <param name="sceneIndex">The index of scene in gltf file to load</param>
        /// <param name="isMultithreaded">Whether to use a thread to do loading</param>
        /// <returns></returns>
        protected IEnumerator ImportScene(int sceneIndex = -1, bool isMultithreaded = false)
        {
            Scene scene;
            if (sceneIndex >= 0 && sceneIndex < _root.Scenes.Count)
            {
                scene = _root.Scenes[sceneIndex];
            }
            else
            {
                scene = _root.GetDefaultScene();
            }

            if (scene == null)
            {
                throw new Exception("No default scene in gltf file.");
            }

            if (_lastLoadedScene == null)
            {
                InitializeAssetCache();
                
                if (_root.Buffers != null)
                {
                    // todo add fuzzing to verify that buffers are before uri
                    for(int i = 0; i < _root.Buffers.Count; ++i)
                    {
                        var buffer = _root.Buffers[i];
                        if (_loadType == LoadType.Stream || buffer.Uri == null)
                        {
                            LoadBufferFromStream(i);
                        }
                        else if(_loadType == LoadType.Uri)
                        {
                            yield return LoadBufferFromURI(_gltfDirectoryPath, buffer, i);
                        }
                    }
                }
                
                if (_root.Images != null)
                {
                    for(int i = 0; i < _root.Images.Count; ++i)
                    {
                        Image image = _root.Images[i];
                        if (_loadType == LoadType.Stream || image.Uri == null)
                        {
                            LoadImageFromStream(_gltfDirectoryPath, image, i);
                        }
                        else if (_loadType == LoadType.Uri)
                        {
                            yield return LoadImageFromURI(_gltfDirectoryPath, image, i);
                        }
                    }
                }
#if !WINDOWS_UWP
                // generate these in advance instead of as-needed
                if (isMultithreaded)
                {
                    yield return _asyncAction.RunOnWorkerThread(() => BuildAttributesForMeshes());
                }
#endif
            }

            var sceneObj = CreateScene(scene);

            if (_sceneParent != null)
            {
                sceneObj.transform.SetParent(_sceneParent, false);
            }

            _lastLoadedScene = sceneObj;
        }

        protected virtual void BuildAttributesForMeshes()
        {
            for (int i = 0; i < _root.Meshes.Count; ++i)
            {
                GLTFJsonSerialization.Mesh mesh = _root.Meshes[i];
                if(_assetCache.MeshCache[i] == null)
                {
                    _assetCache.MeshCache[i] = new MeshCacheData();
                }
                foreach (var primitive in mesh.Primitives)
                {
                    BuildMeshAttributes(primitive, i);
                }
            }
        }

        protected virtual void BuildMeshAttributes(MeshPrimitive primitive, int meshID)
        {
            if (_assetCache.MeshCache[meshID].MeshAttributes == null || _assetCache.MeshCache[meshID].MeshAttributes.Count == 0)
            {
                Dictionary<string, AttributeAccessor> attributeAccessors = new Dictionary<string, AttributeAccessor>(primitive.Attributes.Count + 1);
                foreach (var attributePair in primitive.Attributes)
                {
                    int bufferId = attributePair.Value.Value.BufferView.Value.Buffer.Id;

                    // on cache miss, load the buffer
                    if(_assetCache.BufferCache[bufferId] == null || _assetCache.BufferCache[bufferId].Stream == null)
                    {
                        if (_loadType == LoadType.Stream)
                        {
                            LoadBufferFromStream(bufferId);
                        }
                        else
                        {
                            throw new InvalidOperationException("Cannot load buffer \"just in time\" for type: " + _loadType);
                        }
                    }

                    AttributeAccessor AttributeAccessor = new AttributeAccessor()
                    {
                        AccessorId = attributePair.Value,
                        Stream = _assetCache.BufferCache[bufferId].Stream,
                        Offset = _assetCache.BufferCache[bufferId].ChunkOffset
                    };

                    attributeAccessors[attributePair.Key] = AttributeAccessor;
                }

                if (primitive.Indices != null)
                {
                    int bufferId = primitive.Indices.Value.BufferView.Value.Buffer.Id;
                    AttributeAccessor indexBuilder = new AttributeAccessor()
                    {
                        AccessorId = primitive.Indices,
                        Stream = _assetCache.BufferCache[bufferId].Stream,
                        Offset = _assetCache.BufferCache[bufferId].ChunkOffset
                    };

                    attributeAccessors[SemanticProperties.INDICES] = indexBuilder;
                }

                GLTFHelpers.BuildMeshAttributes(ref attributeAccessors);
                _assetCache.MeshCache[meshID].MeshAttributes = attributeAccessors;
            }
        }

        protected virtual GameObject CreateScene(Scene scene)
        {
            var sceneObj = new GameObject(scene.Name ?? "GLTFScene");

            foreach (var node in scene.Nodes)
            {
                var nodeObj = CreateNode(node.Value);
                nodeObj.transform.SetParent(sceneObj.transform, false);
            }

            return sceneObj;
        }

        protected virtual GameObject CreateNode(Node node)
        {
            var nodeObj = new GameObject(node.Name ?? "GLTFNode");

            Vector3 position;
            Quaternion rotation;
            Vector3 scale;
            node.GetUnityTRSProperties(out position, out rotation, out scale);
            nodeObj.transform.localPosition = position;
            nodeObj.transform.localRotation = rotation;
            nodeObj.transform.localScale = scale;

            // TODO: Add support for skin/morph targets
            if (node.Mesh != null)
            {
                CreateMeshObject(node.Mesh.Value, nodeObj.transform, node.Mesh.Id);
            }

            /* TODO: implement camera (probably a flag to disable for VR as well)
            if (camera != null)
            {
                GameObject cameraObj = camera.Value.Create();
                cameraObj.transform.parent = nodeObj.transform;
            }
            */

            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    // todo blgross: replace with an iterartive solution
                    var childObj = CreateNode(child.Value);
                    childObj.transform.SetParent(nodeObj.transform, false);
                }
            }

            return nodeObj;
        }

        protected virtual void CreateMeshObject(GLTFJsonSerialization.Mesh mesh, Transform parent, int meshId)
        {
            foreach (var primitive in mesh.Primitives)
            {
                GameObject primitiveObj;
                primitiveObj = CreateMeshPrimitive(primitive, meshId);
                primitiveObj.transform.SetParent(parent, false);
                primitiveObj.SetActive(true);
            }
        }

        protected virtual GameObject CreateMeshPrimitive(MeshPrimitive primitive, int meshID)
        {
            var primitiveObj = new GameObject("Primitive");
            var meshFilter = primitiveObj.AddComponent<MeshFilter>();
            
            if (_assetCache.MeshCache[meshID] == null)
            {
                _assetCache.MeshCache[meshID] = new MeshCacheData();
            }
            if (_assetCache.MeshCache[meshID].LoadedMesh == null)
            {
                if (_assetCache.MeshCache[meshID].MeshAttributes.Count == 0)
                {
                    BuildMeshAttributes(primitive, meshID);
                }
                var meshAttributes = _assetCache.MeshCache[meshID].MeshAttributes;
                var vertexCount = primitive.Attributes[SemanticProperties.POSITION].Value.Count;

                // todo optimize: There are multiple copies being performed to turn the buffer data into mesh data. Look into reducing them
                UnityEngine.Mesh mesh = new UnityEngine.Mesh
                {
                    vertices = primitive.Attributes.ContainsKey(SemanticProperties.POSITION)
                        ? meshAttributes[SemanticProperties.POSITION].AccessorContent.AsVertices.ToUnityVector3()
                        : null,
                    normals = primitive.Attributes.ContainsKey(SemanticProperties.NORMAL)
                        ? meshAttributes[SemanticProperties.NORMAL].AccessorContent.AsNormals.ToUnityVector3()
                        : null,

                    uv = primitive.Attributes.ContainsKey(SemanticProperties.TexCoord(0))
                        ? meshAttributes[SemanticProperties.TexCoord(0)].AccessorContent.AsTexcoords.ToUnityVector2()
                        : null,

                    uv2 = primitive.Attributes.ContainsKey(SemanticProperties.TexCoord(1))
                        ? meshAttributes[SemanticProperties.TexCoord(1)].AccessorContent.AsTexcoords.ToUnityVector2()
                        : null,

                    uv3 = primitive.Attributes.ContainsKey(SemanticProperties.TexCoord(2))
                        ? meshAttributes[SemanticProperties.TexCoord(2)].AccessorContent.AsTexcoords.ToUnityVector2()
                        : null,

                    uv4 = primitive.Attributes.ContainsKey(SemanticProperties.TexCoord(3))
                        ? meshAttributes[SemanticProperties.TexCoord(3)].AccessorContent.AsTexcoords.ToUnityVector2()
                        : null,

                    colors = primitive.Attributes.ContainsKey(SemanticProperties.Color(0))
                        ? meshAttributes[SemanticProperties.Color(0)].AccessorContent.AsColors.ToUnityColor()
                        : null,

                    triangles = primitive.Indices != null
                        ? (int[])(object)meshAttributes[SemanticProperties.INDICES].AccessorContent.AsTriangles // todo: unity wants indices as int instead of uint. We should maybe check to see if any of the indices are greater than uint max balue
                        : MeshPrimitive.GenerateTriangles(vertexCount),

                    tangents = primitive.Attributes.ContainsKey(SemanticProperties.TANGENT)
                        ? meshAttributes[SemanticProperties.TANGENT].AccessorContent.AsTangents.ToUnityVector4()
                        : null
                };

                _assetCache.MeshCache[meshID].LoadedMesh = mesh;
            }

            meshFilter.sharedMesh = _assetCache.MeshCache[meshID].LoadedMesh;
            var meshRenderer = primitiveObj.AddComponent<MeshRenderer>();

            UnityEngine.Material materialToSet = null;
            bool shouldUseDefaultMaterial = primitive.Material == null;
            GLTFJsonSerialization.Material materialToLoad = shouldUseDefaultMaterial ? DefaultMaterial : primitive.Material.Value;
            int materialIndex = primitive.Material != null ? primitive.Material.Id : -1;
            var material = _fMaterialLoadCallback(materialToLoad);
            MaterialCacheData materialWrapper = new MaterialCacheData
            {
                UnityMaterial = material,
                UnityMaterialWithVertexColor = new UnityEngine.Material(material),
                GLTFMaterial = materialToLoad
            };
            materialWrapper.UnityMaterialWithVertexColor.EnableKeyword("VERTEX_COLOR_ON");
            materialToSet = materialWrapper.GetContents(primitive.Attributes.ContainsKey(SemanticProperties.Color(0)));

            if (!shouldUseDefaultMaterial)
            {
                _assetCache.MaterialCache[materialIndex] = materialWrapper;
            }
            meshRenderer.material = materialToSet;

            return primitiveObj;
        }

        protected virtual UnityEngine.Material CreateMaterial(GLTFJsonSerialization.Material def)
        {
            Shader shader;

            // get the shader to use for this material
            try
            {
                if (def.PbrMetallicRoughness != null)
                    shader = _shaderCache[MaterialType.PbrMetallicRoughness];
                else if (_root.ExtensionsUsed != null && _root.ExtensionsUsed.Contains("KHR_materials_common")
                         && def.CommonConstant != null)
                    shader = _shaderCache[MaterialType.CommonConstant];
                else
                    shader = _shaderCache[MaterialType.PbrMetallicRoughness];
            }
            catch (KeyNotFoundException)
            {
                Debug.LogWarningFormat("No shader supplied for type of glTF material {0}, using Standard fallback", def.Name);
                shader = Shader.Find("Standard");
            }

            shader.maximumLOD = MaximumLod;

            var material = new UnityEngine.Material(shader);

            if (def.AlphaMode == AlphaMode.MASK)
            {
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
                material.EnableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                material.SetFloat("_Cutoff", (float)def.AlphaCutoff);
            }
            else if (def.AlphaMode == AlphaMode.BLEND)
            {
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else
            {
                material.SetOverrideTag("RenderType", "Opaque");
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = -1;
            }

            if (def.DoubleSided)
            {
                material.SetInt("_Cull", (int)CullMode.Off);
            }
            else
            {
                material.SetInt("_Cull", (int)CullMode.Back);
            }

            if (def.PbrMetallicRoughness != null)
            {
                var pbr = def.PbrMetallicRoughness;

                material.SetColor("_Color", pbr.BaseColorFactor.ToUnityColor());

                if (pbr.BaseColorTexture != null)
                {
                    var texture = pbr.BaseColorTexture.Index.Value;
                    material.SetTexture("_MainTex", CreateTexture(texture));
                }

                material.SetFloat("_Metallic", (float)pbr.MetallicFactor);

                if (pbr.MetallicRoughnessTexture != null)
                {
                    var texture = pbr.MetallicRoughnessTexture.Index.Value;
                    material.SetTexture("_MetallicRoughnessMap", CreateTexture(texture));
                }

                material.SetFloat("_Roughness", (float)pbr.RoughnessFactor);
            }

            if (def.CommonConstant != null)
            {
                material.SetColor("_AmbientFactor", def.CommonConstant.AmbientFactor.ToUnityColor());

                if (def.CommonConstant.LightmapTexture != null)
                {
                    material.EnableKeyword("LIGHTMAP_ON");

                    var texture = def.CommonConstant.LightmapTexture.Index.Value;
                    material.SetTexture("_LightMap", CreateTexture(texture));
                    material.SetInt("_LightUV", def.CommonConstant.LightmapTexture.TexCoord);
                }

                material.SetColor("_LightFactor", def.CommonConstant.LightmapFactor.ToUnityColor());
            }

            if (def.NormalTexture != null)
            {
                var texture = def.NormalTexture.Index.Value;
                material.SetTexture("_BumpMap", CreateTexture(texture));
                material.SetFloat("_BumpScale", (float)def.NormalTexture.Scale);
            }

            if (def.OcclusionTexture != null)
            {
                var texture = def.OcclusionTexture.Index;

                material.SetFloat("_OcclusionStrength", (float)def.OcclusionTexture.Strength);

                if (def.PbrMetallicRoughness != null
                    && def.PbrMetallicRoughness.MetallicRoughnessTexture != null
                    && def.PbrMetallicRoughness.MetallicRoughnessTexture.Index.Id == texture.Id)
                {
                    material.EnableKeyword("OCC_METAL_ROUGH_ON");
                }
                else
                {
                    material.SetTexture("_OcclusionMap", CreateTexture(texture.Value));
                }
            }

            if (def.EmissiveTexture != null)
            {
                var texture = def.EmissiveTexture.Index.Value;
                material.EnableKeyword("EMISSION_MAP_ON");
                material.SetTexture("_EmissionMap", CreateTexture(texture));
                material.SetInt("_EmissionUV", def.EmissiveTexture.TexCoord);
            }

            material.SetColor("_EmissionColor", def.EmissiveFactor.ToUnityColor());

            return material;
        }

        protected virtual UnityEngine.Texture CreateTexture(GLTFJsonSerialization.Texture texture)
        {
            if (_assetCache.TextureCache[texture.Source.Id] == null)
            {
                if(_assetCache.ImageCache[texture.Source.Id] == null)
                {
                    if(_loadType == LoadType.Stream)
                    {
                        LoadImageFromStream(_gltfDirectoryPath, _root.Images[texture.Source.Id], texture.Source.Id);
                    }
                    else
                    {
                        throw new InvalidOperationException("Cannot load buffer \"just in time\" for type: " + _loadType);
                    }
                }
                var source = _assetCache.ImageCache[texture.Source.Id];
                var desiredFilterMode = FilterMode.Bilinear;
                var desiredWrapMode = TextureWrapMode.Repeat;

                if (texture.Sampler != null)
                {
                    var sampler = texture.Sampler.Value;
                    switch (sampler.MinFilter)
                    {
                        case MinFilterMode.Nearest:
                            desiredFilterMode = FilterMode.Point;
                            break;
                        case MinFilterMode.Linear:
                        default:
                            desiredFilterMode = FilterMode.Bilinear;
                            break;
                    }

                    switch (sampler.WrapS)
                    {
                        case GLTFJsonSerialization.WrapMode.ClampToEdge:
                            desiredWrapMode = UnityEngine.TextureWrapMode.Clamp;
                            break;
                        case GLTFJsonSerialization.WrapMode.Repeat:
                        default:
                            desiredWrapMode = UnityEngine.TextureWrapMode.Repeat;
                            break;
                    }
                }

                if (source.filterMode == desiredFilterMode && source.wrapMode == desiredWrapMode)
                {
                    _assetCache.TextureCache[texture.Source.Id] = source;
                }
                else
                {
                    var unityTexture = UnityEngine.Object.Instantiate(source);
                    unityTexture.filterMode = desiredFilterMode;
                    unityTexture.wrapMode = desiredWrapMode;
                    _assetCache.TextureCache[texture.Source.Id] = unityTexture;
                }
            }

            return _assetCache.TextureCache[texture.Source.Id];
        }

        protected const string Base64StringInitializer = "^data:[a-z-]+/[a-z-]+;base64,";

        protected virtual IEnumerator LoadImageFromURI(string rootPath, Image image, int imageID)
        {
            if (_assetCache.ImageCache[imageID] == null)
            {
                if (image.Uri != null)
                {
                    Texture2D texture = null;
                    var uri = image.Uri;

                    Regex regex = new Regex(Base64StringInitializer);
                    Match match = regex.Match(uri);
                    if (match.Success)
                    {
                        var base64Data = uri.Substring(match.Length);
                        var textureData = Convert.FromBase64String(base64Data);
                        texture = new Texture2D(0, 0);
                        texture.LoadImage(textureData);
                    }
                    else
                    {
                        var www = UnityWebRequest.Get(Path.Combine(rootPath, uri));
                        www.downloadHandler = new DownloadHandlerTexture();

                        yield return www.Send();

                        // HACK to enable mipmaps :(
                        var tempTexture = DownloadHandlerTexture.GetContent(www);
                        if (tempTexture != null)
                        {
                            texture = new Texture2D(tempTexture.width, tempTexture.height, tempTexture.format, true);
                            texture.SetPixels(tempTexture.GetPixels());
                            texture.Apply(true);
                        }
                        else
                        {
                            Debug.LogFormat("{0} {1}", www.responseCode, www.url);
                            texture = new Texture2D(16, 16);
                        }
                    }

                    _assetCache.ImageCache[imageID] = texture;
                }
            }
        }

        protected virtual void LoadImageFromStream(string rootPath, Image image, int imageID)
        {
            if (_assetCache.ImageCache[imageID] == null)
            {
                Texture2D texture = null;
                if (image.Uri != null)
                {
                    var pathToLoad = Path.Combine(rootPath, image.Uri);
                    var file = File.OpenRead(pathToLoad);
                    byte[] bufferData = new byte[file.Length];
                    file.Read(bufferData, 0, (int)file.Length);
#if !WINDOWS_UWP
                    file.Close();
#else
                    file.Dispose();
#endif
                    texture = new Texture2D(0, 0);
                    texture.LoadImage(bufferData);
                }
                else
                {
                    texture = new Texture2D(0, 0);
                    var bufferView = image.BufferView.Value;
                    var buffer = bufferView.Buffer.Value;
                    var data = new byte[bufferView.ByteLength];

                    var bufferContents = _assetCache.BufferCache[bufferView.Buffer.Id];
                    bufferContents.Stream.Position = bufferView.ByteOffset + bufferContents.ChunkOffset;
                    bufferContents.Stream.Read(data, 0, data.Length);
                    texture.LoadImage(data);
                }

                _assetCache.ImageCache[imageID] = texture;
            }
        }

        /// <summary>
        /// Load the remote URI data into a byte array.
        /// </summary>
        protected virtual IEnumerator LoadBufferFromURI(string sourceUri, GLTFJsonSerialization.Buffer buffer, int bufferIndex)
        {
            if (_assetCache.BufferCache[bufferIndex] == null || _assetCache.BufferCache[bufferIndex].Stream == null)
            {
                _assetCache.BufferCache[bufferIndex] = new BufferCacheData();
                if (buffer.Uri != null)
                {
                    byte[] bufferData = null;
                    var uri = buffer.Uri;

                    Regex regex = new Regex(Base64StringInitializer);
                    Match match = regex.Match(uri);
                    if (match.Success)
                    {
                        var base64Data = uri.Substring(match.Length);
                        bufferData = Convert.FromBase64String(base64Data);
                        _assetCache.BufferCache[bufferIndex].Stream = new MemoryStream(bufferData, 0, bufferData.Length, false, true);
                    }
                    else if (_loadType == LoadType.Uri)
                    {
                        var www = UnityWebRequest.Get(Path.Combine(sourceUri, uri));

                        yield return www.Send();

                        bufferData = www.downloadHandler.data;
                        _assetCache.BufferCache[bufferIndex].Stream = new MemoryStream(bufferData, 0, bufferData.Length, false, true);
                    }
                }
            }
        }

        protected virtual void LoadBufferFromStream(int bufferIndex)
        {
            _assetCache.BufferCache[bufferIndex] = new BufferCacheData();
            GLTFJsonSerialization.Buffer buffer = _root.Buffers[bufferIndex];
            if (buffer.Uri != null)
            {
                var pathToLoad = Path.Combine(_gltfDirectoryPath, buffer.Uri);
                _assetCache.BufferCache[bufferIndex].Stream = File.OpenRead(pathToLoad);
            }
            else //null buffer uri indicates GLB buffer loading
            {
                GLTFParser.SeekToBinaryChunk(_gltfStream, bufferIndex);  // sets stream to correct start position
                _assetCache.BufferCache[bufferIndex] = new BufferCacheData
                {
                    Stream = _gltfStream,
                    ChunkOffset = _gltfStream.Position
                };
            }
        }

        /// <summary>
        ///  Get the absolute path to a gltf uri reference.
        /// </summary>
        /// <param name="gltfPath">The path to the gltf file</param>
        /// <returns>A path without the filename or extension</returns>
        protected static string AbsoluteUriPath(string gltfPath)
        {
            var uri = new Uri(gltfPath);
            var partialPath = uri.AbsoluteUri.Remove(uri.AbsoluteUri.Length - uri.Segments[uri.Segments.Length - 1].Length);
            return partialPath;
        }

        /// <summary>
        /// Get the absolute path a gltf file directory
        /// </summary>
        /// <param name="gltfPath">The path to the gltf file</param>
        /// <returns>A path without the filename or extension</returns>
        protected static string AbsoluteFilePath(string gltfPath)
        {
            var fileName = Path.GetFileName(gltfPath);
            var lastIndex = gltfPath.IndexOf(fileName);
            var partialPath = gltfPath.Substring(0, lastIndex);
            return partialPath;
        }
    }
}
