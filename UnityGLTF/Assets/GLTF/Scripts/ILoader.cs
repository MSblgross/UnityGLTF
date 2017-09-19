// Copyright (c) Microsoft Corporation. All rights reserved.

using System.IO;
using System.Collections;
using GLTFSerialization;

namespace UnityGLTFSerialization
{
    public interface ILoader
    {
		IEnumerator LoadBuffer(Buffer buffer, System.Action<Stream> streamLoaded);
		IEnumerator LoadImage(Image image, System.Action<UnityEngine.Texture2D> imageLoaded);
    }
}
