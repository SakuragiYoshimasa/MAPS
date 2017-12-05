using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class TriangleTest : MonoBehaviour {



	// Use this for initialization
	void Start () {
		GameObject go = new GameObject();
		go.name = "test triangle";
		MeshFilter mf = go.AddComponent<MeshFilter>	();
		MeshCollider mc = go.AddComponent<MeshCollider>();
		MeshRenderer mr = go.AddComponent<MeshRenderer>();
		Mesh mesh = mf.mesh;

		List<Vector2> points = new List<Vector2>();
		List<List<Vector2>> holes = new List<List<Vector2>>();
		List<int> outIndices = null;
		List<Vector2> outVertices = null;
		
		//Create point list
		points.Add(new Vector2(0f, 0f));
		points.Add(new Vector2(10f, 10f));
		points.Add(new Vector2(0f, 5f));
		points.Add(new Vector2(-10f, 10f));
		points.Add(new Vector2(-10f, 0f));
		points.Add(new Vector2(-5f, -5f));
		points.Add(new Vector2(-3f, -10f));
		points.Add(new Vector2(0f, -5f));
		points.Add(new Vector2(4f, -3f));
		
		
		TrianglationNet.triangulate(points, holes, out outIndices, out outVertices);

		mesh.Clear();
		mesh.vertices = new List<Vector3>(outVertices.Select(p => new Vector3(p.x, p.y, 0))).ToArray();
		mesh.triangles = outIndices.ToArray();
		mesh.RecalculateNormals();
		mesh.RecalculateTangents();
		mesh.RecalculateBounds();

		go.GetComponent<MeshCollider>().sharedMesh = mesh;

		Vector2[] uvs = new Vector2[mesh.vertices.Count()];
		for(int i = 0; i < uvs.Length; i++){
			uvs[i] = new Vector2(mesh.vertices[i].x, mesh.vertices[i].y);
		}
		mesh.uv = uvs;
	}
	
	// Update is called once per frame
	void Update () {
		
	}
}
