using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class MathUtility {
	public static List<QuadEdge> makeQuadedgeStructure(ref Mesh mesh){
		List<QuadEdge> quadedges = new List<QuadEdge>();
		
		int[] oriTri = mesh.triangles;
		int triNum = oriTri.Length/ 3;
		
		for (int i = 0; i < triNum; i++){
			Edge orgdest = new Edge(oriTri[i * 3], oriTri[i * 3 + 1]);
			Edge onext = new Edge(oriTri[i * 3], oriTri[i * 3 + 2]);
			Edge dprev = new Edge(oriTri[i * 3 + 2], oriTri[i * 3 + 1]);
			quadedges.Add(new QuadEdge(orgdest, onext, dprev));
		}

		return quadedges;
	}
	
	public static bool checkContain3D(Vector3[] tri, Vector3 p, Vector3 n){
		bool sign1 = Vector3.Dot(Vector3.Cross(tri[0] - tri[1], tri[0] - p), n) > 0;
		bool sign2 = Vector3.Dot(Vector3.Cross(tri[1] - tri[2], tri[1] - p), n) > 0;
		bool sign3 = Vector3.Dot(Vector3.Cross(tri[2] - tri[0], tri[2] - p), n) > 0;
		return (sign1 && sign2 && sign3);
		//|| (!sign1 && !sign2 && !sign3);
	}

	public static int findContainedQuadEdge(int ind, List<QuadEdge> quadedges){
		for(int i = 0; i < quadedges.Count; i++){
			QuadEdge qe = quadedges[i];
			if(qe.contain(ind)) return i;
		}
		return 0;
	}

		//http://ja.akionux.net/wiki/index.php/点の三角形内外判別法
	public static bool checkContain(Vector2[] tri, Vector2 p){
		bool sign1 = Vector3.Cross(tri[0] - tri[1], tri[0] - p).z > 0;
		bool sign2 = Vector3.Cross(tri[1] - tri[2], tri[1] - p).z > 0;
		bool sign3 = Vector3.Cross(tri[2] - tri[0], tri[2] - p).z > 0;
		return (sign1 && sign2 && sign3);
		//|| (!sign1 && !sign2 && !sign3);
	}

	public static double calcArea(Vector2 a, Vector2 b){
		return Mathf.Sqrt(Mathf.Pow(a.magnitude, 2.0f) * Mathf.Pow(b.magnitude, 2.0f) - Mathf.Pow(Vector3.Dot(a, b), 2.0f)) * 0.5f;
	}
}
