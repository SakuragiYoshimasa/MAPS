using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

//definitions of structures
public struct MapsMesh {
	public List<Vector3> P;
	public Topologies K;
	public List<int> featurePoints;

	public MapsMesh (List<Vector3> ps, Topologies topo, List<int> fps){
		P = ps;
		K = topo;
		featurePoints = fps;
	}
}

public struct Topologies {
	public List<Vertex> vertices;
	public List<Edge> edges;
	public List<Triangle> triangles;

	public Topologies (List<Vertex> vs, List<Edge> es, List<Triangle> ts){
		vertices = vs;
		edges = es;
		triangles = ts;
	}
}

public struct Vertex {
	public int ind;

	public Vertex (int i){
		ind = i;
	}
}

public struct Edge {
	public int ind1;
	public int ind2;

	public Edge (int i1, int i2){
		ind1 = i1;
		ind2 = i2;
	}
}

public struct Triangle {
	public int ind1;
	public int ind2;
	public int ind3;

	public Triangle (int i1, int i2, int i3){
		ind1 = i1;
		ind2 = i2;
		ind3 = i3;
	}

	//this return pos
	public int contains(int ind){
		if(ind == ind1) return 1;
		if(ind == ind2) return 2;
		if(ind == ind3) return 3;
		return 0;
	}

	public int[] getT(int pos){
		if(pos == 1) return new int[3]{ind1, ind2, ind3};
		if(pos == 2) return new int[3]{ind2, ind3, ind1};
		if(pos == 3) return new int[3]{ind3, ind1, ind2};
		return new int[0]{};
	}

	public bool isEqual(Triangle T){
		if(ind1 != T.ind1 && ind1 != T.ind2 && ind1 != T.ind3) return false;
		if(ind2 != T.ind1 && ind2 != T.ind2 && ind2 != T.ind3) return false;
		if(ind3 != T.ind1 && ind3 != T.ind2 && ind3 != T.ind3) return false;
		return true;
	}
}


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
		float lambda = 0.5f;
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

					area += Mathf.Sqrt(a.sqrMagnitude * b.sqrMagnitude - Mathf.Pow(Vector3.Dot(a, b), 2.0f)) * 0.25f;
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

		for(int i = 0; i < remove_indices.Count(); i++){

			var star = stars[remove_indices[i]];
			bool on_boundary = false;
			bool flag = true;
			bool[] used = new bool[star.Count];

			for(int r = 0; r < star.Count; r++){
				used[r] = false;
			}

			if(star.Count < 3){
				continue;
			}

			//find a ring
			var firstT = star[0];
			used[0] = true;

			var ring = new List<int>();
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
			
			//OK
			//Add new triangles
			//Fistly, using conformal map z^a, 1 ring will be flattened
			Vector3 pi = mmesh.P[remove_indices[i]];
			List<Vector3> ring_vs = new List<Vector3>();

			foreach(int ind in ring) ring_vs.Add(mmesh.P[ind]);
			
			List<float> thetas = new List<float>();

			for(int l = 0; l < ring.Count(); l++){
				thetas.Add(Mathf.PI / 180.0f * Vector3.Angle(ring_vs[l] - pi, ring_vs[l + 1 != ring.Count() ? l + 1 : 0]));
			}

			float sum_theta = thetas.Sum();
			
			Dictionary<int, Vector2> mapped_ring = new Dictionary<int, Vector2>();

			float temp_sum_theta = 0f;
			for(int l = 0; l < ring.Count(); l++){
				temp_sum_theta += thetas[l];
				float r = (pi - ring_vs[0]).magnitude;
				float phai = temp_sum_theta * (on_boundary ? Mathf.PI : Mathf.PI * 2.0f) / sum_theta;
				mapped_ring.Add(l, new Vector2(r * Mathf.Cos(phai), r * Mathf.Sin(phai)));
			}
			
			//Secondly, retriangulation by a constrained Delauney triangulation
			//In Test, implementation is easy one.
			
			if(ring.Count >= 3){
				int fow = 1;
				int back = 1;
				int p1 = ring[0];
				int p2 = ring[fow];
				int p3 = ring[ring.Count() - back];
				while(fow < ring.Count() - back){
					mmesh.K.triangles.Add(new Triangle(p1, p2, p3));
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

			//Remove triangle and remove vertex from mmesh
			star = stars[remove_indices[i]];
			foreach(Triangle T in star){
				for(int j = 0; j < mmesh.K.triangles.Count(); j++){
					if(mmesh.K.triangles[j].isEqual(T)){
						mmesh.K.triangles.RemoveAt(j);
						j--;
					}
				}
			}
			removed = true;
		}
		return removed;
}

	// Use this for initialization
	void Start () {
		mmesh = TransformMesh2MapsMesh(mesh, numOfFeaturePoints);
		while(levelDown()){
			Debug.Log("Level Down");
		}
		Mesh m = rebuiltMesh();

		GameObject go = new GameObject();
		var mf = go.AddComponent<MeshFilter>();
		var mr = go.AddComponent<MeshRenderer>();
		mr.material = material;
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
	
	// Update is called once per frame
	void Update () {
		
	}
}
