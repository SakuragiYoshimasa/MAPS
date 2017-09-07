using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

//Referenced MAPS: Multiresolution adaptive parameterization of surfaces.
//Easy implementation
//I haven't impelemnted smoothing and feature edges yet.

public class MAPS : MonoBehaviour {

	public Mesh mesh;
	public int numOfFeaturePoints; 
	public Material material;
	private MapsMesh mmesh;
	private List<int> all_removed_indices;

	MapsMesh TransformMesh2MapsMesh(Mesh mesh, int U){

		int[] tris = mesh.triangles;
		List<Vertex> vs = new List<Vertex>();

		for(int i = 0; i < mesh.vertexCount; i++){
			vs.Add(new Vertex(i));
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
		List<int> fp = makeFeaturePoints(mesh, U);

		List<Vector3> vertices = new List<Vector3>(mesh.vertices);
		for(int i = 0; i < vertices.Count; i++){
			vertices[i] += new Vector3(Random.Range(-0.05f,0.05f), Random.Range(-0.05f,0.05f),Random.Range(-0.05f,0.05f));
		}

		return new MapsMesh(new List<Vector3>(mesh.vertices), topo, fp);
	}

	List<int> makeFeaturePoints(Mesh mesh, int U){

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

	List<int> makeCandidate(){
		List<int> candidate = new List<int>();

		for(int i = 0; i < mmesh.K.vertices.Count; i++){
			if(!mmesh.featurePoints.Contains(mmesh.K.vertices[i].ind) && !all_removed_indices.Contains(mmesh.K.vertices[i].ind)){
				candidate.Add(mmesh.K.vertices[i].ind);
			}
		}
		return candidate;
	}


	bool levelDown(){
		bool removed = false;
		//extract feature points from candidate
		List<int> candidate = makeCandidate();
		if(candidate.Count == 0) return false;
		
		//calculate the priorities of candidate.
		Dictionary<int ,float> priorities = new Dictionary<int, float>();
		Dictionary<int ,List<Triangle>> stars = new Dictionary<int ,List<Triangle>>();
		Utility.calcPrioritiesAndStars(mmesh, candidate, out stars, out priorities);
		
		//by the priorities, select the independent set
		List<int> remove_indices = Utility.makeRemovedIndices(priorities, stars);
		
		//flatten and retriangulation
		for(int i = 0; i < remove_indices.Count(); i++){

			var star = stars[remove_indices[i]];
			bool on_boundary = false;
			//if(star.Count < 3) continue;
			if(star.Count < 2) continue;
		
			//find a ring
			List<int> ring = Utility.FindRingFromStar(star, out on_boundary);
			//if(on_boundary || ring.Last() != star[0].ind3 || ring.GroupBy(x => x).SelectMany(g => g.Skip(1)).Any()) continue;
			
			//Add new triangles
			//Fistly, using conformal map z^a, 1 ring will be flattened
			Vector3 pi = mmesh.P[remove_indices[i]];
			List<Vector3> ring_vs = new List<Vector3>();
			List<float> thetas = new List<float>();
			List<float> temp_thetas = new List<float>();
			Dictionary<int, Vector2> mapped_ring = new Dictionary<int, Vector2>();
			float a = Utility.calcMappedRing(mmesh, pi, ring, on_boundary, ref thetas, ref temp_thetas, ref mapped_ring);

			//Secondly, retriangulation by a constrained Delauney triangulation
			//In Test, implementation is easy one.
			List<Triangle> added_triangles = new List<Triangle>();
			//if(ring.Count >= 3){
			added_triangles = Utility.retriangulationFromRing(ring);
			//}else{ continue; }
			mmesh.K.triangles.AddRange(added_triangles);
			
			//Finally, remove triangle and remove vertex from mmesh
			all_removed_indices.Add(remove_indices[i]);
			mmesh.removeStars(stars[remove_indices[i]]);
			mmesh.removeVertex(remove_indices[i]);
			
			bool found = false;
			//Pattern 2
			//When the previous bijection of removed vertex is identity,
			//Updated bijection of it will be constructed by triangles whrere it on in conformed mapped star.
			//Detection by cross
			//Pattern3;
			foreach(KeyValuePair<int, Dictionary<int, float>> kv in mmesh.bijection){
				//if removed vertex is used for some construction, recalc the construction
				//https://www.chart.co.jp/subject/sugaku/suken_tsushin/74/74-1.pdf
				if(kv.Value.ContainsKey(remove_indices[i])){
		
					found = false;
					int recalced_p_index = kv.Key;
					Vector3 projected_recalced_p = mmesh.getProjectedPoints(new List<int>{recalced_p_index})[0];
					Vector2 myu_pi = Vector2.one * 1000.0f;
					float r_of_myu = Mathf.Pow((projected_recalced_p - pi).magnitude, a);
					int update_bij_ind = 0;

					if(kv.Value.Count == 1){
						myu_pi = Vector2.zero;
						update_bij_ind = remove_indices[i];
					}else if(kv.Value.Count == 2){

						for(int l = 0; l < ring.Count(); l++){
							if(kv.Value.ContainsKey(ring[l])){
								myu_pi = mapped_ring[ring[l]] * kv.Value[ring[l]];
								update_bij_ind = kv.Key;
								break;
							}
						}
				
					}else if(kv.Value.Count == 3){
						float theta_of_myu_min = 0f;
						float theta_of_myu_max = 0f;
						int ind_min = 0, ind_max = 0;
						update_bij_ind = kv.Key;

						for(int l = 0; l < ring.Count(); l++){
							if(kv.Value.ContainsKey(ring[l]) && kv.Value.ContainsKey(ring[l + 1 != ring.Count ? l + 1 : 0])){
								theta_of_myu_min = temp_thetas[l];
								theta_of_myu_max = temp_thetas[l + 1 != ring.Count ? l + 1 : 0];
								ind_min = ring[l];
								ind_max = ring[l + 1 != ring.Count ? l + 1 : 0];
							}
						}

						if(ind_max == ind_min) {
							Debug.Log("Fucking search");
							Debug.LogFormat("ind_min:{0}, ind_max{1}", ind_min, ind_max);
							Debug.Log("Ring");
							foreach(int r in ring){
								Debug.Log(r);
							}

							Debug.Log("Star");
							foreach(Triangle t in star){
								Debug.LogFormat("{0}, {1}, {2}", t.ind1, t.ind2, t.ind3);
							}

							Debug.Log("hoshii in");
							foreach(KeyValuePair<int, float> che in kv.Value){
								if(che.Key != remove_indices[i]){
									Debug.Log(che.Key);
									if(all_removed_indices.Contains(che.Key)){
										Debug.Log("searching removed index");
									}
								}
							}
							Debug.Log("All Tris");
							foreach(Triangle t in mmesh.K.triangles){
								//Debug.LogFormat("{0},{1},{2}", t.ind1,t.ind2,t.ind3);
								if(t.isEqual(new Triangle(kv.Value.ElementAt(0).Key, kv.Value.ElementAt(1).Key, kv.Value.ElementAt(2).Key))){
									Debug.Log("this is a same triangleewfowepfjkweopfjewpofjwepfojwefpowe");
								}
							}

							Debug.Log("removed tris");
							foreach(KeyValuePair<int, List<Triangle>> s in stars){
								foreach(Triangle t in s.Value){
								//	Debug.LogFormat("{0},{1},{2}", t.ind1,t.ind2,t.ind3);
								if(t.isEqual(new Triangle(kv.Value.ElementAt(0).Key, kv.Value.ElementAt(1).Key, kv.Value.ElementAt(2).Key))){
									Debug.Log("Please don't extract this trianfleffe");
								}
								}
							}
						} 
						
						//Because when this situation, theta_of_myu_max = 2PI.
						if(ring[0] == ind_max){
							theta_of_myu_max = theta_of_myu_min;
							theta_of_myu_min = 0;
						}

						double deg_min = Vector3.Angle(mmesh.P[ind_min], projected_recalced_p);
						double deg_max = Vector3.Angle(mmesh.P[ind_max], projected_recalced_p);
						double theta_of_myu = (theta_of_myu_max - theta_of_myu_min) * deg_min / (deg_min + deg_max) + theta_of_myu_min;
						myu_pi = new Vector2(100f * r_of_myu * Mathf.Cos((float)theta_of_myu), 100f * r_of_myu * Mathf.Sin((float)theta_of_myu));

						Vector2[] checkLocation = new Vector2[3]{new Vector2(0,0), mapped_ring[ind_max], mapped_ring[ind_min]};
						
						int loop = 0;
						while(!checkContain(checkLocation, myu_pi)){
							//calc distance and nearest line is where on the myu_pi on
							int[] nearest_line_ind = Utility.calcMostNearestLineOfTriangle(myu_pi, checkLocation);
							float delta = 0.01f;
							//Force translate to make myu_pi on the triangle
							//add other points component and translate
							for(int rem = 0; rem < 3; rem++){
								if(!nearest_line_ind.Contains(rem)) myu_pi += delta * (checkLocation[rem] - myu_pi);
							}
							if(loop > 10) break;
							loop+=1;
						}
						if(checkContain(checkLocation, myu_pi)){
							//Debug.Log("Location OK");
						}else{
							Debug.LogFormat("Bad location{0}", kv.Value.Count);
							foreach(KeyValuePair<int, float> pair in kv.Value){
								Debug.LogFormat("ind:{0}, value:{1}", pair.Key,pair.Value);
							}
							Debug.LogFormat("points0:{0:f}, {1:f}", checkLocation[0].x, checkLocation[0].y);
							Debug.LogFormat("points1:{0:f}, {1:f}", checkLocation[1].x, checkLocation[1].y);
							Debug.LogFormat("points2:{0:f}, {1:f}", checkLocation[2].x, checkLocation[2].y);
							Debug.LogFormat("myu_pi:{0:f}, {1:f}", myu_pi.x, myu_pi.y);
							Debug.Log("inds:" + ind_min.ToString() + "," + ind_max.ToString() + " deg_min:" + deg_min.ToString() + " max:" + deg_max.ToString()+" r_of_myu:" + r_of_myu.ToString()+" Theta_of_nyu:" + theta_of_myu.ToString()+" Myu_pi:" + myu_pi[0].ToString() + ":" + myu_pi[1].ToString());
							//Debug.LogFormat("alpha{0:f}, beta{1:f}, gamma{2:f}", param[0], param[1], param[2]);
						}
					}

					if(update_bij_ind == 0) Debug.LogFormat("Not updated index {0}, remove {1}, size{2}", kv.Key, remove_indices[i], kv.Value.Count);

					//find triangle which contain myu_pi
					foreach(Triangle T in added_triangles){

						Vector2[] points = new Vector2[3]{mapped_ring[T.ind1], mapped_ring[T.ind2], mapped_ring[T.ind3]};
						/* 
						if(remove_indices[i]==33){
							 Debug.LogFormat("{0}, {1}, {2}", T.ind1, T.ind2, T.ind3);
							Debug.LogFormat("points0:{0:f}, {1:f}", points[0].x, points[0].y);
							Debug.LogFormat("points1:{0:f}, {1:f}", points[1].x, points[1].y);
							Debug.LogFormat("points2:{0:f}, {1:f}", points[2].x, points[2].y);
							Debug.LogFormat("myu_pi:{0:f}, {1:f}", myu_pi.x, myu_pi.y);
						}*/
						//If only have one triangle, it may on the boundary and fail here.
						//So, force to translate to in the triangle.
						int loop = 0;
						while(added_triangles.Count == 1 && !checkContain(points, myu_pi)){
							//calc distance and nearest line is where on the myu_pi on
							int[] nearest_line_ind = Utility.calcMostNearestLineOfTriangle(myu_pi, points);
							float delta = 0.01f;
							//Force translate to make myu_pi on the triangle
							//add other points component and translate
							for(int rem = 0; rem < 3; rem++){
								if(!nearest_line_ind.Contains(rem)) myu_pi += delta * (points[rem] - myu_pi);
							}
							if(loop > 10) break;
							loop+=1;
						}
						if(!checkContain(points, myu_pi)) continue;
						if(found) {
							Debug.Log("Bad Alrogi pattern3");
							foreach(Triangle t in added_triangles) Debug.LogFormat("{0}, {1}, {2}", t.ind1, t.ind2, t.ind3);
						}
						found = true;
						points = points.Select(p => p - myu_pi).ToArray();
			
						double[] param = new double[4]{calcArea(points[1], points[2]), calcArea(points[2], points[0]), calcArea(points[0], points[1]), 0};

						for(int x = 0; x < 3; x++) param[x] = double.IsNaN(param[x]) ? 0 : param[x];

						double threshold = 0.0001;
						double params_sum = param.Sum();
						param = param.Select(p => p / params_sum).ToArray();
						double param_sub = param.Where(p => p < threshold).Sum();

						mmesh.bijection[update_bij_ind].Clear();
						if(param[0] > threshold) mmesh.bijection[update_bij_ind].Add(T.ind1, (float)(param[0] + param_sub / 2.0));
						if(param[1] > threshold) mmesh.bijection[update_bij_ind].Add(T.ind2, (float)(param[1] + param_sub / 2.0));
						if(param[2] > threshold) mmesh.bijection[update_bij_ind].Add(T.ind3, (float)(param[2] + param_sub / 2.0));
					}
					if(!found){
						Debug.LogFormat("Not Found! {0}, {1} removing{2}", kv.Key, kv.Value.Count, remove_indices[i]);
						Debug.Log("removed tris");
						foreach(Triangle t in star)Debug.LogFormat("{0},{1},{2}", t.ind1,t.ind2,t.ind3);
						Debug.Log("Ring");
						foreach(int r in ring) Debug.Log(r);
					}
				}
			}
			removed = true;
		}
		return removed;
	}

	//http://ja.akionux.net/wiki/index.php/点の三角形内外判別法
	bool checkContain(Vector2[] tri, Vector2 p){
		bool sign1 = Vector3.Cross(tri[0] - tri[1], tri[0] - p).z > 0;
		bool sign2 = Vector3.Cross(tri[1] - tri[2], tri[1] - p).z > 0;
		bool sign3 = Vector3.Cross(tri[2] - tri[0], tri[2] - p).z > 0;
		return (sign1 && sign2 && sign3) || (!sign1 && !sign2 && !sign3);
	}

	double calcArea(Vector2 a, Vector2 b){
		return Mathf.Sqrt(Mathf.Pow(a.magnitude, 2.0f) * Mathf.Pow(b.magnitude, 2.0f) - Mathf.Pow(Vector3.Dot(a, b), 2.0f)) * 0.5f;
	}
	
	// Use this for initialization
	void Start () {
		//mesh = TestUtility.generateTestMesh();
		mmesh = TransformMesh2MapsMesh(mesh, numOfFeaturePoints);
		
		Mesh m = rebuiltMesh();
		var mf = GetComponent<MeshFilter>();
		mf.mesh = m;

		all_removed_indices = new List<int>();

		Debug.Log(mesh.subMeshCount);
	}

	Mesh rebuiltMesh(){

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

	Mesh generateBaseMeshByBijection(){
		int verts = mesh.vertices.Count();
		List<int> indices = new List<int>();
		for(int i = 0; i < verts; i++) indices.Add(i);	
		List<Vector3> projected_verts = mmesh.getProjectedPoints(indices);

		Mesh m = new Mesh();
		m.vertices = projected_verts.ToArray();
		m.triangles = mesh.triangles;
		return m;
	}

	List<QuadEdge> makeQuadedgeStructure(){
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

	Dictionary<string, int[]> makeOriTriDict(){
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

	Mesh remeshByBijection(){

		Mesh m = new Mesh();
		
		//remesh
		//(1:4) subdivide the base domain and use the inverse map to obtain a regular connectivity remeshing
		List<Vector3> mvertices = new List<Vector3>(mesh.vertices);
		List<int> mtris = new List<int>();

		//Fitstly, construct quadedge data structure from original meshes
		//When in this situation I need to condider the mesh is subdevided.
		//But by selecting one of the three points which construct new points, 
		//I have not to consider that problem
		List<QuadEdge> quadedges = makeQuadedgeStructure();
		Dictionary<string, int[]> orig_tri_dict = makeOriTriDict();
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
					int startEdgeIndex = findContainedQuadEdge(e_orig, quadedges);
					solvedLocation = SolvePointLocation(q_on_base_domain, startEdgeIndex, quadedges, n);
					if(solvedLocation == null) solvedLocation = SolvePointLocation(q_on_base_domain, startEdgeIndex, quadedges, -n);
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

	bool checkContain3D(Vector3[] tri, Vector3 p, Vector3 n){
		bool sign1 = Vector3.Dot(Vector3.Cross(tri[0] - tri[1], tri[0] - p), n) > 0;
		bool sign2 = Vector3.Dot(Vector3.Cross(tri[1] - tri[2], tri[1] - p), n) > 0;
		bool sign3 = Vector3.Dot(Vector3.Cross(tri[2] - tri[0], tri[2] - p), n) > 0;
		return (sign1 && sign2 && sign3) || (!sign1 && !sign2 && !sign3);
	}

	int findContainedQuadEdge(int ind, List<QuadEdge> quadedges){
		for(int i = 0; i < quadedges.Count; i++){
			QuadEdge qe = quadedges[i];
			if(qe.contain(ind)) return i;
		}
		return 0;
	}

	QuadEdge? SolvePointLocation(Vector3 target, int startIndex, List<QuadEdge> quadedges, Vector3 n){
		//Function: BF_Locate
		//In: X: Point whose loaction is to be found
		//	  T: Triangulation in which point is to be located 
		//Out: e: Edge on which X lies, or which has the triangle containing X on its left

		//begin
		//e = some edge of T
		//if RightOf(X, e) then e = e.Sym endif
		//while(true)
		//	if X = e.Org or e.Dest then return e
		//	else 
		//		whichop = 0
		//		if not RightOf(X,e.Onext) then whichop +=1 endif
		//		if not RightOf(X,e.Dprev) then whichop +=2 endif
		//		case whichop of
		//			when 0: return e
		//			when 1: e = e.Onext
		//			when 2: e = e.Dprev
		//			when 3:
		//				if dist(e.Onext.X) < dist(e.Dprev, X) then e = e.Onext
		//				else e = e.Dprev

		QuadEdge e = quadedges[startIndex];

		if (RightOf(target, e.e, n)) e = e.sym;
		while(true){
			int whichop = 0;
			if(!RightOf(target, e.Onext, n)) whichop +=1;
			if(!RightOf(target, e.Dprev, n)) whichop +=2;
			int? ind = null;
			switch(whichop){
				case 0:
					return e;
				case 1:
					Debug.Log("case1");
					ind = FindOnextIndex(quadedges, e.Onext);
					break;
				case 2:
					Debug.Log("case2");
					ind = FindDprevIndex(quadedges, e.Dprev);
					break;
				case 3:
					Debug.Log("case3");
					ind = selectByDist(quadedges, e, target);
					break;
				default:
					break;
			}
			if(ind == null) return null;
			e = quadedges[ind.Value];
		}
	}

	int? selectByDist(List<QuadEdge> quadedges, QuadEdge e, Vector3 target){
		Vector3 eo = mmesh.P[e.Onext.ind1] - mmesh.P[e.Onext.ind2];
		Vector3 xe = target - mmesh.P[e.Onext.ind2];
		float inner_product = Vector3.Dot(eo, xe);

		if(inner_product < 0){ 
			return FindDprevIndex(quadedges, e.Dprev);
		} else { 
			return FindOnextIndex(quadedges, e.Onext);
		}
	}

	int? FindOnextIndex(List<QuadEdge> quadedges, Edge onext){
		for(int i = 0; i < quadedges.Count; i++){
			if(quadedges[i].e.isEqual(onext)) return i;
		}
		return null;
	}

	int? FindDprevIndex(List<QuadEdge> quadedges, Edge dprev){
		for(int i = 0; i < quadedges.Count; i++){
			if(quadedges[i].e.isEqual(dprev)) return i;
		}
		return null;
	}

	bool RightOf(Vector3 X, Edge e, Vector3 n){
		Vector3 orig = mmesh.getProjectedPoints(new List<int>{e.ind1})[0];
		Vector3 dest = mmesh.getProjectedPoints(new List<int>{e.ind2})[0];
		Vector3 v0 = dest - orig;
		Vector3 v1 = X - orig;
		Vector3 cross = Vector3.Cross(v0, v1);
		return Vector3.Dot(cross, n) > 0;
	}
	
	// Update is called once per frame
	void Update () {
		if(Input.GetKeyUp(KeyCode.Space)){
			levelDown();
			Debug.Log("Level Down");
			Mesh m = rebuiltMesh();

			var mf = GetComponent<MeshFilter>();
			mf.mesh = m;
		}
		if(Input.GetKeyUp(KeyCode.A)){
			Mesh m = remeshByBijection();

			var mf = GetComponent<MeshFilter>();
			mf.mesh = m;
		}

		if(Input.GetKeyUp(KeyCode.B)){
			Mesh m = generateBaseMeshByBijection();
			var mf = GetComponent<MeshFilter>();
			mf.mesh = m;
		}

		if(Input.GetKeyUp(KeyCode.V)){
			var bijection = mmesh.bijection;
			Debug.Log(bijection.Count);

			foreach(KeyValuePair<int, Dictionary<int, float>> kv in bijection){
				Debug.Log("V:" + kv.Key.ToString());
				foreach(KeyValuePair<int, float> pa in kv.Value){
					Debug.Log("I:" + pa.Key.ToString() + " pa:" + pa.Value.ToString());
				}
			}
		}
	}
}
