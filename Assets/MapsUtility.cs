using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class MapsUtility {

   public static MapsMesh TransformMesh2MapsMesh(ref Mesh mesh, int U){

		int[] tris = mesh.triangles;
		List<Vert> vs = new List<Vert>();

		for(int i = 0; i < mesh.vertexCount; i++){
			vs.Add(new Vert(i));
		}

		List<Triangle> mmaptris = new List<Triangle>();
		List<Edge> edges = new List<Edge>();

		for(int i = 0; i < tris.GetLength(0) / 3; i++){
			int ind1 = tris[i * 3];
			int ind2 = tris[i * 3 + 1];
			int ind3 = tris[i * 3 + 2];

			mmaptris.Add(new Triangle(ind1, ind2, ind3));
		}
		Topologies topo = new Topologies(vs, edges, mmaptris);
		List<int> fp = makeFeaturePoints(ref mesh, U);
		return new MapsMesh(new List<Vector3>(mesh.vertices), topo, fp);
	}

    public static List<int> makeFeaturePoints(ref Mesh mesh, int U){

		List<int> fp = new List<int>();
		Vector3[] vs = mesh.vertices;
		for(int i = 0; i < U; i++){
			
			float theta = Random.Range(0, Mathf.PI * 2.0f);
			float phai = Random.Range(0, Mathf.PI * 2.0f);
			Vector3 direction = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phai), Mathf.Sin(theta) * Mathf.Sin(phai), Mathf.Cos(theta));
			
			float minDeg = 1000f;
			int minInd = 0;

			for(int n = 0; n < mesh.vertexCount; n++){
				float dig = Vector3.Angle(direction, vs[n]);

				if(Mathf.Abs(dig) < minDeg){
					minDeg = Mathf.Abs(dig);
					minInd = n;
				}
			}

			if(fp.Contains(minInd)){
				i--;
				continue;
			}

			fp.Add(minInd);
		}
		return fp;
	}
}