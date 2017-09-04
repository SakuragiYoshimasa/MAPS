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

	bool levelDown(){
		//extract feature points from candidate
		List<int> candidate = new List<int>();

		for(int i = 0; i < mmesh.K.vertices.Count; i++){
			if(!mmesh.featurePoints.Contains(mmesh.K.vertices[i].ind)){
				candidate.Add(mmesh.K.vertices[i].ind);
			}
		}

		if(candidate.Count == 0){
			return false;
		}

		//calculate the priorities of candidate.
		List<float> areas = new List<float>();
		List<float> curvatures = new List<float>();
		Dictionary<int ,float> priorities = new Dictionary<int, float>();
		Dictionary<int ,List<Triangle>> stars = new Dictionary<int ,List<Triangle>>();
		float lambda = 0.8f;
		float maxArea = 0f;
		float maxCurvature = 0f;

		//Calculate areas and apploximate gauss curvatures
		for(int i = 0; i < candidate.Count; i++){
			
			float area = 0;
			float curvature = 360f;
			List<Triangle> star = new List<Triangle>();
			
			for(int j = 0; j < mmesh.K.triangles.Count; j++){
				
				int pos = mmesh.K.triangles[j].contains(candidate[i]);

				if(pos != 0){
					int[] triangle = mmesh.K.triangles[j].getT(pos);

					star.Add(new Triangle(triangle[0], triangle[1], triangle[2]));

					Vector3 p0 = mmesh.P[triangle[0]];
					Vector3 p1 = mmesh.P[triangle[1]];
					Vector3 p2 = mmesh.P[triangle[2]];
					//area = 1/4 sqrt(a^2 b^2 - (a * b)^2)
					Vector3 a = p1 - p0;
					Vector3 b = p2 - p0;

					area += Mathf.Sqrt(a.sqrMagnitude * b.sqrMagnitude - Mathf.Pow(Vector3.Dot(a, b), 2.0f)) * 0.5f;
					curvature -= Vector3.Angle(a, b);
				}
			}

			//Approcimate gauss curvature
			curvature /= area / 3.0f / 180.0f * Mathf.PI;
			areas.Add(area);
			curvatures.Add(curvature);
			stars.Add(candidate[i] ,star);

			maxArea = maxArea < area ? area : maxArea;
			maxCurvature = maxCurvature < curvature ? curvature : maxCurvature;
		}

		//Calculate priorities
		for(int i = 0; i < candidate.Count; i++){
			priorities.Add(candidate[i] , lambda * areas[i] / maxArea + (1.0f - lambda) * curvatures[i] / maxCurvature);
		}

		//by the priorities, select the maximum independent set
		var descebding_priorities = priorities.OrderBy((x) => x.Value).ToDictionary(pair => pair.Key, pair => pair.Value);
		List<int> remove_indices = new List<int>();
		while(descebding_priorities.Count() > 0){
			int first = descebding_priorities.ElementAt(0).Key;
			descebding_priorities.Remove(first);

			
			remove_indices.Add(first);
			var star = stars[first];
			//Debug.Log(star.Count());
			for(int i = 0; i < star.Count(); i++){
				if(descebding_priorities.ContainsKey(star[i].ind2)){
					descebding_priorities.Remove(star[i].ind2);
				}
				if(descebding_priorities.ContainsKey(star[i].ind3)){
					descebding_priorities.Remove(star[i].ind3);
				}
			}
		}
		
		//flatten and retriangulation
		//remove the maximum independent
		bool removed = false;

		for(int i = 0; i < mmesh.P.Count; i++){
			if(!remove_indices.Contains(i)){
				mmesh.bijection[i] = mmesh.bijection[i];
			}
		}

		for(int i = 0; i < remove_indices.Count(); i++){

			var star = stars[remove_indices[i]];
			bool on_boundary = false;
			bool flag = true;
			bool[] used = new bool[star.Count];

			for(int r = 0; r < star.Count; r++) used[r] = false;
			if(star.Count < 3) continue;
			
			//find a ring
			var firstT = star[0];
			var ring = new List<int>();
			used[0] = true;
			ring.Add(firstT.ind2);
			
			while(flag){
				if(!used.Contains(false)) break;
				
				for(int n = 0; n < star.Count(); n++){

					if(!used[n] && star[n].ind2 == ring.Last()){
						ring.Add(star[n].ind3);
						if(star[n].ind3 == firstT.ind3)  flag = false;
						used[n] = true;
						break;
					}

					if(!used[n] && star[n].ind3 == ring.Last()){
						ring.Add(star[n].ind2);
						if(star[n].ind2 == firstT.ind3) flag = false;
						used[n] = true;
						break;
					}
					
					if(n == star.Count() - 1) {
						flag = false;
						on_boundary = true;
					}
				}
			}		
			if(on_boundary || ring.Last() != firstT.ind3) continue;
			

			//Add new triangles
			//Fistly, using conformal map z^a, 1 ring will be flattened
			Vector3 pi = mmesh.P[remove_indices[i]];
			List<Vector3> ring_vs = new List<Vector3>();
			List<float> thetas = new List<float>();
			Dictionary<int, Vector2> mapped_ring = new Dictionary<int, Vector2>();
			foreach(int ind in ring) ring_vs.Add(mmesh.P[ind]);
			for(int l = 0; l < ring.Count(); l++) thetas.Add(Mathf.PI / 180.0f * Vector3.Angle(ring_vs[l] - pi, ring_vs[l + 1 != ring.Count() ? l + 1 : 0]));
			float sum_theta = thetas.Sum();
			float temp_sum_theta = 0f;

			for(int l = 0; l < ring.Count(); l++){
				temp_sum_theta += thetas[l];
				float r = (pi - ring_vs[0]).magnitude;
				float phai = temp_sum_theta * (on_boundary ? Mathf.PI : Mathf.PI * 2.0f) / sum_theta;
				mapped_ring.Add(ring[l], new Vector2(r * Mathf.Cos(phai), r * Mathf.Sin(phai)));
			}
			
			//Secondly, retriangulation by a constrained Delauney triangulation
			//In Test, implementation is easy one.
			List<Triangle> added_triangles = new List<Triangle>();
			if(ring.Count >= 3){
				int fow = 1;
				int back = 1;
				int p1 = ring[0];
				int p2 = ring[fow];
				int p3 = ring[ring.Count() - back];
				while(fow < ring.Count() - back){
					mmesh.K.triangles.Add(new Triangle(p1, p2, p3));
					added_triangles.Add(new Triangle(p1, p2, p3));
					if(fow == back){
						fow++;
						p1 = p2;
						p2 = ring[fow];
						p3 = ring[ring.Count() - back];
					}else{
						back++;
						p1 = p3;
						p2 = ring[fow];
						p3 = ring[ring.Count() - back];
					}
				}
			}else{
				continue;
			}

			//Finally, remove triangle and remove vertex from mmesh
			star = stars[remove_indices[i]];
			foreach(Triangle T in star){
				for(int j = 0; j < mmesh.K.triangles.Count(); j++){
					if(mmesh.K.triangles[j].isEqual(T)){
						mmesh.K.triangles.RemoveAt(j);
						j--;
					}
				}
			}

			//Pattern 2
			//When the previous bijection of removed vertex is null,
			//Updated bijection of it will be constructed by triangles whrere it on in conformed mapped star.
			//Detection by cross
			bool found = false;
			if(mmesh.bijection[remove_indices[i]].Count == 0){
				foreach(Triangle T in added_triangles){
					Vector2[] points = new Vector2[3]{mapped_ring[T.ind1], mapped_ring[T.ind2], mapped_ring[T.ind3]};
					if(checkContain(points, Vector2.zero)){
						if(found) Debug.Log("Bad Alrogi pattern2");
						found = true;

						//calc barycentric coordinates alpha, beta, gamma.
						//http://www.osaka-c.ed.jp/shijonawate/pdf/yuumeimondai/vector_4.pdf
						//because of conformal mapped pi = (0,0)
						//Area Mathf.Sqrt(a.sqrMagnitude * b.sqrMagnitude - Mathf.Pow(Vector3.Dot(a, b), 2.0f)) * 0.5f;
						float[] param = new float[3]{calcArea(points[1], points[2]), calcArea(points[2], points[0]), calcArea(points[0], points[1])};
						mmesh.bijection[remove_indices[i]].Add(T.ind1, param[0] / param.Sum());
						mmesh.bijection[remove_indices[i]].Add(T.ind2, param[1] / param.Sum());
						mmesh.bijection[remove_indices[i]].Add(T.ind3, param[2] / param.Sum());
					}
				}
			}
			if(found){
				Debug.Log("Found!!");
			}else{
				Debug.Log("Not Found!");
			}
			

			//Pattern3;
			
			foreach(KeyValuePair<int, Dictionary<int, float>> kv in mmesh.bijection){
				//if removed vertex is used for some construction, recalc the construction
				//https://www.chart.co.jp/subject/sugaku/suken_tsushin/74/74-1.pdf
				if(kv.Value.ContainsKey(remove_indices[i])){

					found = false;
					Vector3 recalced_p = mmesh.P[kv.Key];

					//Recalc mapped_ring centerd recalced_p 
					List<float> thetas_re = new List<float>();
					Dictionary<int, Vector2> mapped_ring_re = new Dictionary<int, Vector2>();
					for(int l = 0; l < ring.Count(); l++) thetas_re.Add(Mathf.PI / 180.0f * Vector3.Angle(ring_vs[l] - recalced_p, ring_vs[l + 1 != ring.Count() ? l + 1 : 0]));
					float sum_theta_re = thetas_re.Sum();
					float temp_sum_theta_re = 0f;

					for(int l = 0; l < ring.Count(); l++){
						temp_sum_theta_re += thetas_re[l];
						float r = (recalced_p - ring_vs[0]).magnitude;
						float phai = temp_sum_theta_re * Mathf.PI * 2.0f / sum_theta_re;
						mapped_ring_re.Add(ring[l], new Vector2(r * Mathf.Cos(phai), r * Mathf.Sin(phai)));
					}

					//find triangle which contain recalc_p
					foreach(Triangle T in added_triangles){
						Vector2[] points = new Vector2[3]{mapped_ring_re[T.ind1], mapped_ring_re[T.ind2], mapped_ring_re[T.ind3]};
						if(checkContain(points, Vector2.zero)){
							if(found) Debug.Log("Bad Alrogi pattern3");
							found = true;

							//calc barycentric coordinates alpha, beta, gamma.
							//http://www.osaka-c.ed.jp/shijonawate/pdf/yuumeimondai/vector_4.pdf
							//because of conformal mapped pi = (0,0)
							//Area Mathf.Sqrt(a.sqrMagnitude * b.sqrMagnitude - Mathf.Pow(Vector3.Dot(a, b), 2.0f)) * 0.5f;
							Vector3 param = new Vector3(calcArea(points[1], points[2]), calcArea(points[2], points[0]), calcArea(points[0], points[1]));
							Vector3 normalized_parmas = param.normalized;
				
							mmesh.bijection[kv.Key].Clear();
							mmesh.bijection[kv.Key].Add(T.ind1, normalized_parmas[0]);
							mmesh.bijection[kv.Key].Add(T.ind2, normalized_parmas[1]);
							mmesh.bijection[kv.Key].Add(T.ind3, normalized_parmas[2]);
						}
					}

					if(found){
						Debug.Log("Found!!");
					}else{
						Debug.Log("Not Found!");
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

	float calcArea(Vector2 a, Vector2 b){
		return Mathf.Sqrt(a.sqrMagnitude * b.sqrMagnitude - Mathf.Pow(Vector3.Dot(a, b), 2.0f)) * 0.5f;
	}
	

	// Use this for initialization
	void Start () {
		mmesh = TransformMesh2MapsMesh(mesh, numOfFeaturePoints);
		
		Mesh m = rebuiltMesh();
		var mf = GetComponent<MeshFilter>();
		mf.mesh = m;
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


	Mesh remeshByBijection(){

		Mesh m = new Mesh();
		//m_vertices = mmesh.P.ToArray();



		//remesh
		//(1:4) subdivide the base domain and use the inverse map to obtain a regular connectivity remeshing
		List<Vector3> mvertices = new List<Vector3>(mesh.vertices);
		List<int> mtris = new List<int>();

		//Fitstly, construct quadedge data structure from original meshes
		//When in this situation I need to condider the mesh is subdevided.
		//But by selecting one of the three points which construct new points, 
		//I have not to consider that problem
		List<QuadEdge> quadedges = new List<QuadEdge>();
		
		int[] oriTri = mesh.triangles;
		int triNum = oriTri.Length/ 3;
		
		for (int i = 0; i < triNum; i++){
			Edge orgdest = new Edge(oriTri[i * 3], oriTri[i * 3 + 1]);
			Edge onext = new Edge(oriTri[i * 3], oriTri[i * 3 + 2]);
			Edge dprev = new Edge(oriTri[i * 3 + 2], oriTri[i * 3 + 1]);
			quadedges.Add(new QuadEdge(orgdest, onext, dprev));
		}

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

		foreach(Triangle T in mmesh.K.triangles){
			Vector3 q_on_base_domain = mmesh.P[T.ind1] / 3.0f + mmesh.P[T.ind2] / 3.0f + mmesh.P[T.ind3] / 3.0f;
			//Solve the point location problem and find original vertex contain PI^(-1)(q)
			//And find alpha, beta, gamma
			//q = alpha * PI(pi) + beta * PI(pj) + gamma * PI(pk)
			//add the PI^(-1)(q) = alpha * pi + beta * pj + gamma * pk
			//Add 4 trianle (q, T.ind1, T.ind2), (q, T.ind2, T.ind3), (q, ind3, ind1)

			//Find a bijection which use T.ind1, T.ind2, T.ind3 and find location
			List<int> candidate = mmesh.FindBiject(T.ind1, T.ind2, T.ind3);
			candidate.Sort();

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
			float temp_sum_theta = 0f;

			for(int l = 0; l < projected_points.Count(); l++){
				temp_sum_theta += thetas[l];
				float r = (q_on_base_domain - projected_points[0]).magnitude;
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
			

			/* 
			//By bijection, project the triangle to base domain.
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




			//First: find a quadedge contains T.ind1;
			//int e_orig = T.ind1;
			//int startEdgeIndex = findContainedQuadEdge(e_orig, quadedges);

			//Find pi,pj,pk
			//QuadEdge solvedLocation = SolvePointLocation(q_on_base_domain, startEdgeIndex, quadedges);

			//calc alpha,beta,gamma

			//add 4 Triangle centerd on PI^(-1)(q)
		}

		m.vertices = mvertices.ToArray();
		m.triangles = mtris.ToArray();

		m.RecalculateBounds();
		m.RecalculateNormals();
		m.RecalculateTangents();
		return m;
	}

	int findContainedQuadEdge(int ind, List<QuadEdge> quadedges){
		for(int i = 0; i < quadedges.Count; i++){
			QuadEdge qe = quadedges[i];
			if(qe.contain(ind)) return i;
		}
		return 0;
	}

	QuadEdge SolvePointLocation(Vector3 target, int startIndex, List<QuadEdge> quadedges){
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

		if (RightOf(target, e.e)) e = e.sym;
		while(true){
			int whichop = 0;
			if(!RightOf(target, e.Onext)) whichop +=1;
			if(!RightOf(target, e.Dprev)) whichop +=2;

			switch(whichop){
				case 0:
					return e;

				case 1:
					break;
				case 2:
					break;
				case 3:
					break;
				default:
					break;
			}
		}

	}

	bool RightOf(Vector3 X, Edge e){
		Vector3 orig = mesh.vertices[e.ind1];
		Vector3 dest = mesh.vertices[e.ind2];

		Vector3 v0 = dest - orig;
		Vector3 v1 = X - orig;
		return false;

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
	}
}
