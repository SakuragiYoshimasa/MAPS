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

		//Debug.Log("About 190");
		//testObj.testStarMesh(stars[188], mmesh);
		//foreach(Triangle T in stars[190]){
		//	Debug.LogFormat("{0},{1},{2}", T.ind1, T.ind2, T.ind3);
		//}
		
		//flatten and retriangulation
		for(int i = 0; i < remove_indices.Count(); i++){

			var star = stars[remove_indices[i]];
			bool on_boundary = false;

			if(star.Count < 3 || star.Count > 12) {
				unremoval_indices.Add(remove_indices[i]);
				continue;
			}
		
			//find a ring
			List<int> ring = Utility.FindRingFromStar(star, ref on_boundary);
			//if(on_boundary || ring.Last() != star[0].ind3 || ring.GroupBy(x => x).SelectMany(g => g.Skip(1)).Any()){// continue;
			//if(ring.Count < 3){
			//	unremoval_indices.Add(remove_indices[i]);
			//	continue;
			//}

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
			Debug.Log(ring.Count);
			Debug.Log(on_boundary);
			List<Triangle> added_triangles = Utility.retriangulationFromRing(ring, on_boundary);//CDT.retriangulationFromRingByCDT(ring, mapped_ring.Values.ToList(), on_boundary);//
			//CDT.retriangulationFromRingByCDT(ring, mapped_ring.Values.ToList(), on_boundary);
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
			/* 
			foreach(KeyValuePair<int, Dictionary<int, float>> kv in mmesh.bijection){
				//if removed vertex is used for some construction, recalc the construction
				//https://www.chart.co.jp/subject/sugaku/suken_tsushin/74/74-1.pdf
				if(!kv.Value.ContainsKey(remove_indices[i])) continue;
				
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
							break;
						}
					}

					if(ind_max == ind_min) {
						//testObj.testMappedRing(mapped_ring.Values.ToList(), myu_pi);
						
						Debug.LogFormat("ring_count:{0}, star_count:{1}", ring.Count, star.Count);
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
							if(t.isEqual(new Triangle(kv.Value.ElementAt(0).Key, kv.Value.ElementAt(1).Key, kv.Value.ElementAt(2).Key))){
								Debug.Log("this is a same triangleewfowepfjkweopfjewpofjwepfojwefpowe");
							}
						}
						Debug.Log("removed tris");
						foreach(KeyValuePair<int, List<Triangle>> s in stars){
							foreach(Triangle t in s.Value){
							if(t.isEqual(new Triangle(kv.Value.ElementAt(0).Key, kv.Value.ElementAt(1).Key, kv.Value.ElementAt(2).Key))){
								Debug.LogFormat("Please don't extract this trianfleffe:{0},{1},{2}", t.ind1,t.ind2,t.ind3);
								Debug.LogFormat("Now:{0}, This:{1}", remove_indices[i], s.Key);
							}
							}
						}
					} 
						
					//Because when this situation, theta_of_myu_max = 2PI.
					if(ring[0] == ind_max) theta_of_myu_min = 0;
					double deg_min = Vector3.Angle(mmesh.P[ind_min], projected_recalced_p);
					double deg_max = Vector3.Angle(mmesh.P[ind_max], projected_recalced_p);
					double theta_of_myu = (theta_of_myu_max - theta_of_myu_min) * deg_min / (deg_min + deg_max) + theta_of_myu_min;
					theta_of_myu *= a;
					myu_pi = new Vector2(100f * r_of_myu * Mathf.Cos((float)theta_of_myu), 100f * r_of_myu * Mathf.Sin((float)theta_of_myu));

					Vector2[] checkLocation = new Vector2[3]{new Vector2(0,0), mapped_ring[ind_max], mapped_ring[ind_min]};
						
					int loop = 0;
					while(!MathUtility.checkContain(checkLocation, myu_pi)){
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
						
					if(!MathUtility.checkContain(checkLocation, myu_pi)){
						testObj.testMappedRing(mapped_ring.Values.ToList(), checkLocation, myu_pi);
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

					//If only have one triangle, it may on the boundary and fail here.
					//So, force to translate to in the triangle.
					int loop = 0;
					while(added_triangles.Count == 1 && !MathUtility.checkContain(points, myu_pi)){
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
					if(!MathUtility.checkContain(points, myu_pi)) continue;
					if(found) {
						Debug.Log("Bad Alrogi pattern3");
						foreach(Triangle t in added_triangles) Debug.LogFormat("{0}, {1}, {2}", t.ind1, t.ind2, t.ind3);
					}
					found = true;
					points = points.Select(p => p - myu_pi).ToArray();
			
					double[] param = new double[4]{MathUtility.calcArea(points[1], points[2]), MathUtility.calcArea(points[2], points[0]), MathUtility.calcArea(points[0], points[1]), 0};

					for(int x = 0; x < 3; x++) param[x] = double.IsNaN(param[x]) ? 0 : param[x];

					double threshold = 0.0001;
					double params_sum = param.Sum();
					param = param.Select(p => p / params_sum).ToArray();
					double param_sub = param.Where(p => p < threshold).Sum();

					mmesh.bijection[update_bij_ind].Clear();
					if(param[0] > threshold) mmesh.bijection[update_bij_ind].Add(T.ind1, (float)(param[0] + param_sub / 2.0));
					if(param[1] > threshold) mmesh.bijection[update_bij_ind].Add(T.ind2, (float)(param[1] + param_sub / 2.0));
					if(param[2] > threshold) mmesh.bijection[update_bij_ind].Add(T.ind3, (float)(param[2] + param_sub / 2.0));
					if(update_bij_ind == 188){
						Debug.Log("updated 188");
						foreach(KeyValuePair<int, float> _kv in mmesh.bijection[update_bij_ind]){
							Debug.LogFormat("188 is constructed by {0}, {1}", _kv.Key, _kv.Value);
						}
					}
				}
				if(!found){
					Debug.LogFormat("Not Found! {0}, {1} removing{2}", kv.Key, kv.Value.Count, remove_indices[i]);
					Debug.Log("removed tris");
					foreach(Triangle t in star)Debug.LogFormat("{0},{1},{2}", t.ind1,t.ind2,t.ind3);
					Debug.Log("Ring");
					foreach(int r in ring) Debug.Log(r);
				}
			}*/
			removed = true;
		}
		return removed;
	}
	
	// Use this for initialization
	void Start () {
		mesh = TestUtility.generateTestMesh();
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
			//Mesh m = remeshByBijection();

			//var mf = GetComponent<MeshFilter>();
			//mf.mesh = m;
		}

		if(Input.GetKeyUp(KeyCode.B)){
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
}
