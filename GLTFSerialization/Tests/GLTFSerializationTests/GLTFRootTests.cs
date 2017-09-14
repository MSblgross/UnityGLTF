﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using GLTFSerialization;
using System.Text;
using System.IO;

namespace GLTFJsonSerializerTests
{
	[TestClass]
	public class GLTFRootTest
	{

		[TestMethod]
		public void TestMinimumGLTF()
		{
			var testStr = @"
			{
				""asset"": {
					""version"": ""2.0""
				}
			}
		";

			MemoryStream stream = new MemoryStream();
			StreamWriter writer = new StreamWriter(stream);
			writer.Write(testStr);
			writer.Flush();
			stream.Position = 0;

			var testRoot = GLTFParser.ParseJson(stream);

			Assert.AreEqual(testRoot.Asset.Version, "2.0");
		}
	}
}