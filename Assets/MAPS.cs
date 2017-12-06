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
	private List<int> all_removed_indices = new List<int>();
	private List<int> unremoval_indices = new List<int>();
	public MAPStest testObj;
	private Mesh m;

	List<int> makeCandidate(){
		List<int> candidate = new List<int>();

		for(int i = 0; i < mmesh.K.vertices.Count; i++){
			int ind = mmesh.K.vertices[i].ind;
			if(!mmesh.featurePoints.Contains(ind) && !all_removed_indices.Contains(ind) && !unremoval_indices.Contains(ind)){
				candidate.Add(ind);
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
			bool invalid = false;

			if(star.Count < 3 || star.Count > 12) {
				unremoval_indices.Add(remove_indices[i]);
				continue;
			}
			List<int> ring = Utility.FindRingFromStar(star, ref on_boundary, ref invalid);
			if(invalid){
				unremoval_indices.Add(remove_indices[i]);
				continue;
			}
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
			if(ring.Count < 3 || ring.Count >= 12){
				unremoval_indices.Add(remove_indices[i]);
				continue;
			}

			List<Triangle> added_triangles = null;
			bool found_triangles = TrianglationNet.triangulateFromRing(ring, mapped_ring, out added_triangles);
			if(!found_triangles){
				unremoval_indices.Add(remove_indices[i]);
				continue;
			}
			mmesh.K.triangles.AddRange(added_triangles);
			
			//Finally, remove triangle and remove vertex from mmesh
			all_removed_indices.Add(remove_indices[i]);
			mmesh.removeStars(star);
			mmesh.removeVertex(remove_indices[i]);
			
			bool found = false;
			//About Bijection
			//When the previous bijection of removed vertex is identity,
			//Updated bijection of it will be constructed by triangles whrere it on in conformed mapped star.
			//Detection by cross
			//KeyValuePair<vertexIndex, bijection<>> initialy bijection[vertexIndex]={vertexIndex:1.0}
			
			foreach(Dictionary<int, float> kv in mmesh.bijection){
				//if removed vertex is used for some construction, recalc the construction
				//https://www.chart.co.jp/subject/sugaku/suken_tsushin/74/74-1.pdf
				if(!kv.ContainsKey(remove_indices[i])) continue;
				//------------------------------------------------------------------------------
				//Find update bijection index;
				//------------------------------------------------------------------------------
				Vector2 myu_pi = Vector2.one * 1000.0f;
				float al = 0, bet = 0;
				if(kv.Count == 1){
					//Initialy bijection[vertexIndex]={vertexIndex:1.0}
					myu_pi = Vector2.zero;
				}else if(kv.Count == 2){
					foreach(KeyValuePair<int, float> bb in kv){
						if(bb.Key == remove_indices[i]) continue;
						myu_pi = mapped_ring[bb.Key] * bb.Value;
					}
				}else if(kv.Count == 3){
					//３つから構成されている場合
					for(int l = 0; l < ring.Count(); l++){
						if(kv.ContainsKey(ring[l]) && kv.ContainsKey(ring[l + 1 != ring.Count ? l + 1 : 0])){
							int ind_min = ring[l];
							int ind_max = ring[l + 1 != ring.Count ? l + 1 : 0];
							al = kv[ind_min];
							bet = kv[ind_max];
							myu_pi = mapped_ring[ind_min] * kv[ind_min] + mapped_ring[ind_max] * kv[ind_max];
							break;
						}
					}
				}
				
				//find triangle which contain myu_pi
				//For example case 1,
				//myu_pi = (0,0) and find added triangle contain (0,0)
				//------------------------------------------------------------------------------
				//Find update bijection index;
				//------------------------------------------------------------------------------
				//Calc center of mapped ring to refine myu_pi.
				Vector2 center = new Vector2(0,0);
				foreach(KeyValuePair<int, Vector2> mp in mapped_ring){
					center += mp.Value / (float)(mapped_ring.Count);
				}

				found = false;
				int nloop = 0;
				while(!found && nloop < 10){
					foreach(Triangle T in added_triangles){
						Vector2[] points = new Vector2[3]{mapped_ring[T.ind1], mapped_ring[T.ind2], mapped_ring[T.ind3]};
						if(!MathUtility.checkContain(points, myu_pi)) continue;
						points = points.Select(p => p - myu_pi).ToArray();
						float[] param = new float[3]{MathUtility.calcArea(points[1], points[2]), MathUtility.calcArea(points[2], points[0]), MathUtility.calcArea(points[0], points[1])};
						for(int x = 0; x < 3; x++) param[x] = double.IsNaN(param[x]) ? 0 : param[x];
						float params_sum = param.Sum();  
						if(params_sum == 0) continue;
						param = param.Select(p => p / params_sum).ToArray();
						kv.Clear();
						if(param[0] != 0) kv.Add(T.ind1, param[0]);
						if(param[1] != 0) kv.Add(T.ind2, param[1]);
						if(param[2] != 0) kv.Add(T.ind3, param[2]);
						found = true;
						break;
					}
					myu_pi += 0.1f * (center - myu_pi);
					nloop++;
				}
				if(!found){
					Debug.LogFormat("Not Found! {0}, {1} removing{2}", kv.Keys, kv.Values, remove_indices[i]);
					Debug.Log(myu_pi);
					Debug.LogFormat("al:{0}, bet:{1}", al, bet);
					Debug.Log(on_boundary);
					foreach(Vector2 v in mapped_ring.Values){
						Debug.Log(v);
					}
				}
			}
			removed = true;
		}
		return removed;
	}
	
	// Use this for initialization
	void Start () {
		//mesh = TestUtility.generateTestMesh();
		mmesh = MapsUtility.TransformMesh2MapsMesh(ref mesh, numOfFeaturePoints);
		
		//Mesh
		m = RemeshUtility.rebuiltMesh(ref mmesh);
		var mf = GetComponent<MeshFilter>();
		mf.mesh = m;

		all_removed_indices = new List<int>();
	}

	// Update is called once per frame
	void Update () {
		if(Input.GetKeyUp(KeyCode.Space)){
			levelDown();
			Debug.Log("Level Down");
			//Mesh 
			m = RemeshUtility.rebuiltMesh(ref mmesh);
			var mf = GetComponent<MeshFilter>();
			mf.mesh = m;
		}
		if(Input.GetKeyUp(KeyCode.A)){
			//Mesh 
			m = RemeshUtility.generateBaseMeshByBijection(ref mmesh, ref mesh);
			var mf = GetComponent<MeshFilter>();
			mf.mesh = m;
		}
	}
	/* 
	void OnDrawGizmos(){
		#if UNITY_EDITOR
		if(mmesh == null){ return; }
		foreach(Vert v in mmesh.K.vertices){
			//if(v.ind == 10){
			//	Debug.LogFormat("heihei{0}", mmesh.P[v.ind]);
			//}
			//UnityEditor.Handles.Label(mmesh.P[v.ind], v.ind.ToString());
		}

		for(int i = 0; i < mmesh.P.Count; i++){
			//UnityEditor.Handles.Label(mmesh.P[i], i.ToString());
		}

		for(int i = 0; i < mesh.vertices.Count(); i++){
			//UnityEditor.Handles.Label(mesh.vertices[i], i.ToString());
		}

		for(int i = 0; i < m.vertices.Count(); i++){
			UnityEditor.Handles.Label(m.vertices[i], i.ToString());
		}
		#endif
	}*/
}
