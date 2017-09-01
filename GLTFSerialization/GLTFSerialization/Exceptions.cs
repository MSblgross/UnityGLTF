using System;

namespace GLTFSerialization
{
	[Serializable()]
	public class GLTFHeaderInvalidException : Exception
	{
		public GLTFHeaderInvalidException() : base() { }
		public GLTFHeaderInvalidException(string message) : base(message) { }
		public GLTFHeaderInvalidException(string message, Exception inner) : base(message, inner) { }

		protected GLTFHeaderInvalidException(System.Runtime.Serialization.SerializationInfo info,
			System.Runtime.Serialization.StreamingContext context)
		{ }
	}

	[Serializable()]
	public class ShaderNotFoundException : Exception
	{
		public ShaderNotFoundException() : base() { }
		public ShaderNotFoundException(string message) : base(message) { }
		public ShaderNotFoundException(string message, Exception inner) : base(message, inner) { }

		protected ShaderNotFoundException(System.Runtime.Serialization.SerializationInfo info,
			System.Runtime.Serialization.StreamingContext context)
		{ }
	}
}
