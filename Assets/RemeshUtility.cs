using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public static class RemeshUtility {

  	public static Mesh rebuiltMesh(ref MapsMesh mmesh){

		Mesh rebuilted_mesh = new Mesh();

		rebuilted_mesh.vertices = mmesh.P.ToArray();

		List<int> tris = new List<int>();

		for(int i = 0; i < mmesh.K.triangles.Count; i++){
			tris.Add(mmesh.K.triangles[i].ind1);
			tris.Add(mmesh.K.triangles[i].ind2);
			tris.Add(mmesh.K.triangles[i].ind3);
		}

		rebuilted_mesh.SetTriangles(tris, 0);
		rebuilted_mesh.RecalculateBounds();
		rebuilted_mesh.RecalculateNormals();
		rebuilted_mesh.RecalculateTangents();
		return rebuilted_mesh;
	}

	public static Mesh generateBaseMeshByBijection(ref MapsMesh mmesh, ref Mesh mesh){
		int verts = mesh.vertices.Count();
		List<int> indices = new List<int>();
		for(int i = 0; i < verts; i++) indices.Add(i);	
		List<Vector3> projected_verts = mmesh.getProjectedPoints(indices);
		Mesh m = new Mesh();
		m.vertices = projected_verts.ToArray();
		m.triangles = mesh.triangles;
		return m;
	}

	static Dictionary<string, int[]> makeOriTriDict(ref Mesh mesh){
		int[] oriTri = mesh.triangles;
		int triNum = oriTri.Length/ 3;

		Dictionary<string, int[]> orig_tri_dict = new Dictionary<string, int[]>();
		for (int i = 0; i < triNum; i++){
			int ind1 = oriTri[i * 3];
			int ind2 = oriTri[i * 3 + 1];
			int ind3 = oriTri[i * 3 + 2];
			List<int> tri = new List<int>(){ind1, ind2, ind3};
			tri.Sort();
			string key = tri[0].ToString() + "," + tri[1].ToString() + "," + tri[2].ToString();
			orig_tri_dict[key] = tri.ToArray();
		}

		return orig_tri_dict;
	}

	public static Mesh remeshByBijection(ref MapsMesh mmesh, ref Mesh mesh){

		Mesh m = new Mesh();
		
		//remesh
		//(1:4) subdivide the base domain and use the inverse map to obtain a regular connectivity remeshing
		List<Vector3> mvertices = new List<Vector3>(mesh.vertices);
		List<int> mtris = new List<int>();

		//Fitstly, construct quadedge data structure from original meshes
		//When in this situation I need to condider the mesh is subdevided.
		//But by selecting one of the three points which construct new points, 
		//I have not to consider that problem
		List<QuadEdge> quadedges = MathUtility.makeQuadedgeStructure(ref mesh);
		Dictionary<string, int[]> orig_tri_dict = makeOriTriDict(ref mesh);
		Debug.Log(mmesh.K.triangles.Count);
		foreach(Triangle T in mmesh.K.triangles){
			Vector3 q_on_base_domain = mmesh.P[T.ind1] / 3.0f + mmesh.P[T.ind2] / 3.0f + mmesh.P[T.ind3] / 3.0f;
			//Solve the point location problem and find original vertex contain PI^(-1)(q)
			//And find alpha, beta, gamma
			//q = alpha * PI(pi) + beta * PI(pj) + gamma * PI(pk)
			//add the PI^(-1)(q) = alpha * pi + beta * pj + gamma * pk
			//Add 4 trianle (q, T.ind1, T.ind2), (q, T.ind2, T.ind3), (q, ind3, ind1)

						
			//By bijection, project the triangle to base domain.
			/* 
			List<List<Vector3>> projected_orig_tris = new List<List<Vector3>>();

			foreach(int[] tri in cand_orig_tri){

				List<Vector3> projected_orig_tri = new List<Vector3>();
				foreach(int ind in tri){
					Dictionary<int, float> elements_of_linearfunc = mmesh.bijection[tri[ind]];
					Vector3 p0 = Vector3.zero;

					foreach(KeyValuePair<int, float> param in elements_of_linearfunc){
						p0 += mmesh.P[param.Key] * param.Value;
					}
					projected_orig_tri.Add(p0);
				}

				projected_orig_tris.Add(projected_orig_tri);
			}*/

			//Firstly, check whether q on the plane defined by triangle
			//This stage is no need?
			//Check the target in the triangle in the projected space
			#region pointlocation
				
				Vector3 n = Vector3.Cross(mmesh.P[T.ind2] - mmesh.P[T.ind1], mmesh.P[T.ind3] - mmesh.P[T.ind1]);
				//Find pi,pj,pk
				QuadEdge? solvedLocation = null;
				int[] e_origs = new int[3]{T.ind1,T.ind2,T.ind3};
				foreach(int e_orig in e_origs){
					int startEdgeIndex = MathUtility.findContainedQuadEdge(e_orig, quadedges);
					solvedLocation = PointLocationSolver.SolvePointLocation(ref mmesh, q_on_base_domain, startEdgeIndex, quadedges, n);
					if(solvedLocation == null) solvedLocation = PointLocationSolver.SolvePointLocation(ref mmesh, q_on_base_domain, startEdgeIndex, quadedges, -n);
					if(solvedLocation != null){ 
						break;
					}
				}

				if(solvedLocation != null){
					Debug.Log("Solved");
					int new_ind = mvertices.Count;
					Vector3 q = mmesh.P[solvedLocation.Value.Org.ind] * 0.33f + mmesh.P[solvedLocation.Value.Dest.ind] * 0.33f + mmesh.P[solvedLocation.Value.Onext.ind2] * 0.33f;
					mvertices.Add(q);

					mtris.Add(new_ind);
					mtris.Add(solvedLocation.Value.Org.ind);
					mtris.Add(solvedLocation.Value.Dest.ind);

					mtris.Add(new_ind);
					mtris.Add(solvedLocation.Value.Onext.ind2);
					mtris.Add(solvedLocation.Value.Org.ind);
					
					mtris.Add(new_ind);
					mtris.Add(solvedLocation.Value.Dest.ind);
					mtris.Add(solvedLocation.Value.Onext.ind2);

				}else{
					Debug.Log("NONONONONONO");
					mtris.Add(T.ind1);
					mtris.Add(T.ind2);
					mtris.Add(T.ind3);
				}
			#endregion

			#region greedy

				/* 
				Vector3 n = Vector3.Cross(mmesh.P[T.ind2] - mmesh.P[T.ind1], mmesh.P[T.ind3] - mmesh.P[T.ind1]);
				QuadEdge? solvedLocation = null;
				foreach(QuadEdge quadedge in quadedges){
					List<int> tri = new List<int>{quadedge.Org.ind, quadedge.Dest.ind, quadedge.Onext.ind1};
					List<Vector3> projected_tri = mmesh.getProjectedPoints(tri);
					if(checkContain3D(projected_tri.ToArray(), q_on_base_domain, n)){
						Debug.Log("Solved Greedy");
						solvedLocation = quadedge;
						break;
					}
				}

				if(solvedLocation != null){
					Debug.Log("Solved");
					int new_ind = mvertices.Count;
					Vector3 q = mmesh.P[solvedLocation.Value.Org.ind] * 0.33f + mmesh.P[solvedLocation.Value.Dest.ind] * 0.33f + mmesh.P[solvedLocation.Value.Onext.ind2] * 0.33f;
					mvertices.Add(q);
					Debug.Log("new:" + new_ind.ToString() + " i1:" + solvedLocation.Value.Org.ind.ToString() + " i2:" + solvedLocation.Value.Dest.ind.ToString() + " i3:" + solvedLocation.Value.Onext.ind2.ToString());
					mtris.Add(new_ind);
					mtris.Add(solvedLocation.Value.Org.ind);
					mtris.Add(solvedLocation.Value.Dest.ind);

					mtris.Add(new_ind);
					mtris.Add(solvedLocation.Value.Onext.ind2);
					mtris.Add(solvedLocation.Value.Org.ind);
					
					mtris.Add(new_ind);
					mtris.Add(solvedLocation.Value.Dest.ind);
					mtris.Add(solvedLocation.Value.Onext.ind2);

				}else{
					Debug.Log("NONONONONONO");
				}
				*/
			

			#endregion
				
			//return m;
			//calc alpha,beta,gamma

			//add 4 Triangle centerd on PI^(-1)(q)
			#region Comment

				//Find a bijection which use T.ind1, T.ind2, T.ind3 and find location
				/* 
				List<int> candidate = mmesh.FindBiject(T.ind1, T.ind2, T.ind3);
				candidate.Sort();
				Debug.Log(candidate.Count);
				if(candidate.Count == 0) continue;
				
				//Find a original triangle which contain all indices in candidate.
				List<int[]> cand_orig_tri = new List<int[]>();

				for(int i = 0; i < candidate.Count - 2; i++){
					int ind1 = candidate[i];
					for(int j = i + 1; j < candidate.Count - 1; j++){
						int ind2 = candidate[j];
						for(int k = j + 1; k < candidate.Count; k++){
							int ind3 = candidate[k];
							string key = ind1.ToString() + "," + ind2.ToString() + "," + ind3.ToString();
							if(orig_tri_dict.ContainsKey(key)) cand_orig_tri.Add(orig_tri_dict[key]);
						}
					}
				}

				//By bijection, the gravitation of all tris determined by T.
				//So, I only have to calculate the distance between target and candidate gravitation
				float min_dist = float.MaxValue;
				int[] min_dist_tri = new int[3]{0,0,0};

				foreach(int[] tri in cand_orig_tri){
					float[] abgmma= new float[3]{0,0,0};

					//calc gravitation of tri represented by parameters
					foreach(int ind in tri){
						Dictionary<int, float> elements_of_linearfunc = mmesh.bijection[ind];
						abgmma[0] += elements_of_linearfunc[T.ind1] / 3.0f;
						abgmma[1] += elements_of_linearfunc[T.ind2] / 3.0f;
						abgmma[2] += elements_of_linearfunc[T.ind3] / 3.0f;
					}
					//calc dist by parameters
					float dist = Mathf.Pow((abgmma[0] - 1.0f / 3.0f), 2.0f) + Mathf.Pow((abgmma[1] - 1.0f / 3.0f), 2.0f) + Mathf.Pow((abgmma[2] - 1.0f / 3.0f), 2.0f);
					if(dist < min_dist){
						min_dist = dist;
						min_dist_tri = tri;
					}
				}
				
				//found target ori_tri
				List<int> target_ori_tri = new List<int>();
				target_ori_tri.AddRange(min_dist_tri);
				List<Vector3> projected_points = mmesh.getProjectedPoints(target_ori_tri);

				List<float> thetas = new List<float>();
				List<Vector2> mapped_ring = new List<Vector2>();

				for(int l = 0; l < projected_points.Count; l++) {
					thetas.Add(Mathf.PI / 180.0f * Vector3.Angle(projected_points[l] - q_on_base_domain, projected_points[l + 1 != projected_points.Count() ? l + 1 : 0]));
				}
				float sum_theta = thetas.Sum();
				Debug.Log("sum:" + sum_theta.ToString());
				float temp_sum_theta = 0f;

				for(int l = 0; l < projected_points.Count(); l++){
					temp_sum_theta += thetas[l];
					float r = (q_on_base_domain - projected_points[l]).magnitude;
					float phai = temp_sum_theta * Mathf.PI * 2.0f / sum_theta;
					mapped_ring.Add(new Vector2(r * Mathf.Cos(phai), r * Mathf.Sin(phai)));
				}

				float alpha = calcArea(mapped_ring[1], mapped_ring[2]);
				float beta = calcArea(mapped_ring[0], mapped_ring[2]);
				float gamma = calcArea(mapped_ring[0], mapped_ring[1]);
				float sum = alpha + beta + gamma;
				alpha /= sum;
				beta /= sum;
				gamma /= sum;

				Vector3 q_on_L = alpha * mmesh.P[target_ori_tri[0]] + beta * mmesh.P[target_ori_tri[1]] + gamma * mmesh.P[target_ori_tri[2]];

				//Add 4 trianle (q, T.ind1, T.ind2), (q, T.ind2, T.ind3), (q, ind3, ind1)
				int new_ind = mvertices.Count;
				Debug.Log(q_on_L);
				mvertices.Add(q_on_L);
				mtris.Add(new_ind);
				mtris.Add(T.ind1);
				mtris.Add(T.ind2);

				mtris.Add(new_ind);
				mtris.Add(T.ind2);
				mtris.Add(T.ind3);

				mtris.Add(new_ind);
				mtris.Add(T.ind3);
				mtris.Add(T.ind1);
				*/
			#endregion
		}

		m.vertices = mvertices.ToArray();
		m.triangles = mtris.ToArray();

		m.RecalculateBounds();
		m.RecalculateNormals();
		m.RecalculateTangents();
		return m;
	}
}
