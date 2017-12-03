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
		bool tested = false;
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
			if(ring.Count < 3){
				unremoval_indices.Add(remove_indices[i]);
				continue;
			}

			List<Triangle> added_triangles = Utility.retriangulationFromRing(ring, on_boundary);//CDT.retriangulationFromRingByCDT(ring, mapped_ring.Values.ToList(), on_boundary);
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
			foreach(KeyValuePair<int, Dictionary<int, float>> kv in mmesh.bijection){
				//if removed vertex is used for some construction, recalc the construction
				//https://www.chart.co.jp/subject/sugaku/suken_tsushin/74/74-1.pdf
				if(!kv.Value.ContainsKey(remove_indices[i])) continue;
				//------------------------------------------------------------------------------
				//Find update bijection index;
				//------------------------------------------------------------------------------
				Vector2 myu_pi = Vector2.one * 1000.0f;
				int update_bij_ind = kv.Key; //新しい場所を探すkey
				float al = 0, bet = 0;
				if(kv.Value.Count == 1){
					//Initialy bijection[vertexIndex]={vertexIndex:1.0}
					myu_pi = Vector2.zero;
				}else if(kv.Value.Count == 3){
					//３つから構成されている場合
					for(int l = 0; l < ring.Count(); l++){
						if(kv.Value.ContainsKey(ring[l]) && kv.Value.ContainsKey(ring[l + 1 != ring.Count ? l + 1 : 0])){
							int ind_min = ring[l];
							int ind_max = ring[l + 1 != ring.Count ? l + 1 : 0];
							al = kv.Value[ind_min];
							bet = kv.Value[ind_max];
							myu_pi = mapped_ring[ind_min] * kv.Value[ind_min] + mapped_ring[ind_max] * kv.Value[ind_max];
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
						/*if(found && !tested) {
							Debug.LogFormat("Bad Alrogi pattern3 {0}", on_boundary);
							testObj.testMappedRing(mapped_ring.Values.ToList(), myu_pi);

							foreach(int key in mapped_ring.Keys.ToArray()){
								Debug.LogFormat("Key: {0}", key);
							}

							foreach(Triangle T0 in added_triangles){
								Debug.LogFormat("T: {0}, {1}, {2}", T0.ind1, T0.ind2, T0.ind3);
							}
							tested = true;
						}*/
						found = true;
						points = points.Select(p => p - myu_pi).ToArray();
						double[] param = new double[4]{MathUtility.calcArea(points[1], points[2]), MathUtility.calcArea(points[2], points[0]), MathUtility.calcArea(points[0], points[1]), 0};
						for(int x = 0; x < 3; x++) param[x] = double.IsNaN(param[x]) ? 0 : param[x];
						double params_sum = param.Sum();
						param = param.Select(p => p / params_sum).ToArray();
						mmesh.bijection[update_bij_ind].Clear();
						mmesh.bijection[update_bij_ind].Add(T.ind1, (float)(param[0]));
						mmesh.bijection[update_bij_ind].Add(T.ind2, (float)(param[1]));
						mmesh.bijection[update_bij_ind].Add(T.ind3, (float)(param[2]));
					}
					myu_pi += 0.001f * (center - myu_pi);
					nloop++;
				}
				if(!found){
					Debug.LogFormat("Not Found! {0}, {1} removing{2}", kv.Key, kv.Value.Count, remove_indices[i]);
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
		
		Mesh m = RemeshUtility.rebuiltMesh(ref mmesh);
		var mf = GetComponent<MeshFilter>();
		mf.mesh = m;

		all_removed_indices = new List<int>();
	}

	// Update is called once per frame
	void Update () {
		if(Input.GetKeyUp(KeyCode.Space)){
			levelDown();
			Debug.Log("Level Down");
			Mesh m = RemeshUtility.rebuiltMesh(ref mmesh);
			var mf = GetComponent<MeshFilter>();
			mf.mesh = m;
		}
		if(Input.GetKeyUp(KeyCode.A)){
			Mesh m = RemeshUtility.generateBaseMeshByBijection(ref mmesh, ref mesh);
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

	void OnDrawGizmos(){
		#if UNITY_EDITOR
		if(mmesh == null){ return; }
		foreach(Vertex v in mmesh.K.vertices){
			UnityEditor.Handles.Label(mmesh.P[v.ind], v.ind.ToString());
		}
		#endif
	}
}
