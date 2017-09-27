using System;
using System.IO;
using GLTFSerialization;
using UnityEngine;
using System.Text.RegularExpressions;
using System.Net;

namespace UnityGLTFSerialization.Loader
{
	public class WebRequestLoader : ILoader
	{
		private const string Base64StringInitializer = "^data:[a-z-]+/[a-z-]+;base64,";
		private string _rootURI;
		
		public WebRequestLoader(string rootURI)
		{
			_rootURI = rootURI;
		}

		public Stream LoadJSON(string gltfFilePath)
		{
			if(gltfFilePath == null)
			{
				throw new ArgumentNullException("gltfFilePath");
			}

			return CreateHTTPRequest(_rootURI, gltfFilePath);
		}

		public Stream LoadBuffer(GLTFSerialization.Buffer buffer)
		{
			if(buffer == null)
			{
				throw new ArgumentNullException("buffer");
			}

			if(buffer.Uri != null)
			{
				throw new ArgumentException("Cannot load buffer with null URI. Should be loaded via GLB method instead", "buffer");
			}

			Stream bufferDataStream = null;
			var uri = buffer.Uri;

			Regex regex = new Regex(Base64StringInitializer);
			Match match = regex.Match(uri);
			if (match.Success)
			{
				var base64Data = uri.Substring(match.Length);
				bufferDataStream = new MemoryStream(Convert.FromBase64String(base64Data));
			}
			else
			{
				bufferDataStream = CreateHTTPRequest(_rootURI, uri);
			}

			return bufferDataStream;
		}

		public Texture2D LoadImage(Image image)
		{
			if(image == null)
			{
				throw new ArgumentNullException("image");
			}

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
				Stream requestStream = CreateHTTPRequest(_rootURI, uri);
				
				if (requestStream != null)
				{
					texture = new Texture2D(0, 0);
					if(requestStream.Length > int.MaxValue)
					{
						throw new Exception("Stream is larger than can be copied into byte array");
					}

					byte[] streamAsBytes = new byte[requestStream.Length];
					requestStream.Read(streamAsBytes, 0, (int)requestStream.Length);
					texture.LoadRawTextureData(streamAsBytes);
					texture.Apply(true);
				}
			}

			return texture;
		}

		private Stream CreateHTTPRequest(string rootUri, string httpRequestPath)
		{
			HttpWebRequest www = (HttpWebRequest)WebRequest.Create(Path.Combine(_rootURI, httpRequestPath));
			HttpWebResponse webResponse = (HttpWebResponse)www.GetResponse();

			if ((int)webResponse.StatusCode >= 400)
			{
				Debug.LogErrorFormat("{0} - {1}", webResponse.StatusCode, webResponse.ResponseUri);
				return null;
			}

			return webResponse.GetResponseStream();
		}
	}
}
