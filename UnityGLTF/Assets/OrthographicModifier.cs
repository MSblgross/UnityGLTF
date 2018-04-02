using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OrthographicModifier : MonoBehaviour {
	Matrix4x4 previousMat;


	// Update is called once per frame
	void Update () {
		Camera camera = GetComponent<Camera>();
		Matrix4x4 mat = camera.projectionMatrix;
		if (mat != previousMat)
		{
			print(camera.projectionMatrix);
			previousMat = mat;
		}
	}
}
