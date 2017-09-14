﻿using UnityEngine;
using GLTFSerialization;

namespace UnityGLTFSerialization.Extensions
{
	public static class SchemaExtensions
	{
		public static void GetUnityTRSProperties(this Node node, out Vector3 position, out Quaternion rotation, out Vector3 scale)
		{
			Vector3 localPosition, localScale;
			Quaternion localRotation;

			if (!node.UseTRS)
			{
				GetTRSProperties(node.Matrix, out localPosition, out localRotation, out localScale);
			}
			else
			{
				localPosition = node.Translation.ToUnityVector3();
				localRotation = node.Rotation.ToUnityQuaternion();
				localScale = node.Scale.ToUnityVector3();
			}

			position = new Vector3(localPosition.x, localPosition.y, -localPosition.z);
			rotation = new Quaternion(-localRotation.x, -localRotation.y, localRotation.z, localRotation.w);
			scale = new Vector3(localScale.x, localScale.y, localScale.z);
			// normally you would flip scale.z here too, but that's done in Accessor
		}
		
		public static void SetUnityTransform(this Node node, Transform transform)
		{
			node.Translation = new GLTFSerialization.Math.Vector3(transform.localPosition.x, transform.localPosition.y, -transform.localPosition.z);
			node.Rotation = new GLTFSerialization.Math.Quaternion(-transform.localRotation.x, -transform.localRotation.y, transform.localRotation.z, transform.localRotation.w);
			node.Scale = new GLTFSerialization.Math.Vector3(transform.localScale.x, transform.localScale.y, transform.localScale.z);
		}

		// todo: move to utility class
		public static void GetTRSProperties(GLTFSerialization.Math.Matrix4x4 mat, out Vector3 position, out Quaternion rotation, out Vector3 scale)
		{
			position = mat.GetColumn(3);

			scale = new Vector3(
				mat.GetColumn(0).magnitude,
				mat.GetColumn(1).magnitude,
				mat.GetColumn(2).magnitude
			);

			rotation = Quaternion.LookRotation(mat.GetColumn(2), mat.GetColumn(1));
		}
		
		public static Vector3 GetColumn(this GLTFSerialization.Math.Matrix4x4 mat, uint columnNum)
		{
			switch(columnNum)
			{
				case 0:
				{
					return new Vector3(mat.M11, mat.M21, mat.M31);
				}
				case 1:
				{
					return new Vector3(mat.M12, mat.M22, mat.M32);
				}
				case 2:
				{
					return new Vector3(mat.M13, mat.M23, mat.M33);
				}
				case 3:
				{
					return new Vector3(mat.M14, mat.M24, mat.M34);
				}
				default:
					throw new System.Exception("column num is out of bounds");
			}
		}

		public static Vector2 ToUnityVector2(this GLTFSerialization.Math.Vector2 vec3)
		{
			return new Vector2(vec3.X, vec3.Y);
		}

		public static Vector2[] ToUnityVector2(this GLTFSerialization.Math.Vector2[] inVecArr)
		{
			Vector2[] outVecArr = new Vector2[inVecArr.Length];
			for (int i = 0; i < inVecArr.Length; ++i)
			{
				outVecArr[i] = inVecArr[i].ToUnityVector2();
			}
			return outVecArr;
		}

		public static Vector3 ToUnityVector3(this GLTFSerialization.Math.Vector3 vec3)
		{
			return new Vector3(vec3.X, vec3.Y, vec3.Z);
		}

		public static Vector3[] ToUnityVector3(this GLTFSerialization.Math.Vector3[] inVecArr)
		{
			Vector3[] outVecArr = new Vector3[inVecArr.Length];
			for(int i = 0; i < inVecArr.Length; ++i)
			{
				outVecArr[i] = inVecArr[i].ToUnityVector3();
			}
			return outVecArr;
		}

		public static Vector4 ToUnityVector4(this GLTFSerialization.Math.Vector4 vec4)
		{
			return new Vector4(vec4.X, vec4.Y, vec4.Z, vec4.W);
		}

		public static Vector4[] ToUnityVector4(this GLTFSerialization.Math.Vector4[] inVecArr)
		{
			Vector4[] outVecArr = new Vector4[inVecArr.Length];
			for (int i = 0; i < inVecArr.Length; ++i)
			{
				outVecArr[i] = inVecArr[i].ToUnityVector4();
			}
			return outVecArr;
		}

		public static UnityEngine.Color ToUnityColor(this GLTFSerialization.Math.Color color)
		{
			return new UnityEngine.Color(color.R, color.G, color.B, color.A);
		}

		public static GLTFSerialization.Math.Color ToNumericsColor(this UnityEngine.Color color)
		{
			return new GLTFSerialization.Math.Color(color.r, color.g, color.b, color.a);
		}

		public static UnityEngine.Color[] ToUnityColor(this GLTFSerialization.Math.Color[] inColorArr)
		{
			UnityEngine.Color[] outColorArr = new UnityEngine.Color[inColorArr.Length];
			for (int i = 0; i < inColorArr.Length; ++i)
			{
				outColorArr[i] = inColorArr[i].ToUnityColor();
			}
			return outColorArr;
		}

		public static int[] ToIntArray(this uint[] uintArr)
		{
			int[] intArr = new int[uintArr.Length];
			for (int i = 0; i < uintArr.Length; ++i)
			{
				uint uintVal = uintArr[i];
				Debug.Assert(uintVal <= int.MaxValue);
				intArr[i] = (int)uintVal;
			}

			return intArr;
		}

		public static Quaternion ToUnityQuaternion(this GLTFSerialization.Math.Quaternion quaternion)
		{
			return new Quaternion(quaternion.X, quaternion.Y, quaternion.Z, quaternion.W);
		}
	}
}
