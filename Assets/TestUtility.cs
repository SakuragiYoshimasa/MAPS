using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class TestUtility {

	public static Mesh generateTestMesh(){
		Mesh m = new Mesh();

		int w = 12;
		int h = 12;

		List<Vector3> vertices = new List<Vector3>();
		List<int> indices = new List<int>();

		for(int i = 0; i < w; i++){
			for(int j = 0; j < h; j++){
				vertices.Add(new Vector3(5.0f * (float)i,5.0f * (float)j, 0));
				if(i != 0 && j != 0){
					indices.Add(j - 1 + i * h);
					indices.Add(j + i * h);
					indices.Add(j + (i - 1) * h);

					indices.Add(j - 1 + i * h);
					indices.Add(j + (i - 1) * h);
					indices.Add(j - 1 + (i - 1) * h);
				}
			}
		}

		m.vertices = vertices.ToArray();
		m.SetIndices(indices.ToArray(), MeshTopology.Triangles, 0);

		return m;
	}
}
