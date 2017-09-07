using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Collections;
using System;
using System.Linq;

public static class Utility {

	public static int[] calcMostNearestLineOfTriangle(Vector2 target, Vector2[] triangle){
		int[] result = new int[2]{0,0};

		float min_dist = float.MaxValue;

		for(int i = 0; i < 3; i++){
			float D = Mathf.Abs(Vector3.Cross(target - triangle[i], triangle[i + 1 != 3 ? i + 1 : 0] - triangle[0]).z);
			float L = Vector2.Distance(triangle[i], triangle[i + 1 != 3 ? i + 1 : 0]);
			float dist = D / L;

			if(dist < min_dist){
				result[0] = i;
				result[1] = i + 1;
			}
		}

		return result;
	}

	//Return a
	public static float calcMappedRing(MapsMesh mmesh, Vector3 pi, List<int> ring, bool on_boundary,ref List<float> thetas, ref List<float> temp_thetas, ref Dictionary<int, Vector2> mapped_ring){

		List<Vector3> ring_vs = new List<Vector3>();
		thetas = new List<float>();
		temp_thetas = new List<float>();
		mapped_ring = new Dictionary<int, Vector2>();
		foreach(int ind in ring) ring_vs.Add(mmesh.P[ind]);

		for(int l = 0; l < ring.Count(); l++) thetas.Add(Mathf.PI / 180.0f * Vector3.Angle(ring_vs[l - 1 != -1 ? l -1 : ring.Count - 1] - pi, ring_vs[l] - pi));
		float sum_theta = thetas.Sum();
		float temp_sum_theta = 0f;
		float a = Mathf.PI * (!on_boundary ? 2.0f : 1.0f) / sum_theta;

		if(!on_boundary){
			for(int l = 0; l < ring.Count(); l++){
				temp_sum_theta += thetas[l];
				temp_thetas.Add(temp_sum_theta);
				float r = Mathf.Pow((pi - ring_vs[l]).magnitude, a);
				float phai = temp_sum_theta * a;
				mapped_ring.Add(ring[l], new Vector2(r * 100.0f * Mathf.Cos(phai), r * 100.0f * Mathf.Sin(phai)));
			}
		}else{
			for(int l = 0; l < ring.Count(); l++){
				temp_thetas.Add(temp_sum_theta);
				float r = Mathf.Pow((pi - ring_vs[l]).magnitude, a);
				float phai = temp_sum_theta * a;
				mapped_ring.Add(ring[l], new Vector2(r * 100.0f * Mathf.Cos(phai), r * 100.0f * Mathf.Sin(phai)));
				temp_sum_theta += thetas[l];
			}
		}

		return a;
	}

	public static void calcPrioritiesAndStars(MapsMesh mmesh, List<int> candidate, out Dictionary<int ,List<Triangle>> stars, out Dictionary<int, float> priorities){
		priorities = new Dictionary<int, float>();
		stars = new Dictionary<int ,List<Triangle>>();

		List<double> areas = new List<double>();
		List<double> curvatures = new List<double>();
		
		double lambda = 0.9;
		double maxArea = 0;
		double maxCurvature = 0;

		//Calculate areas and apploximate gauss curvatures
		for(int i = 0; i < candidate.Count; i++){
			
			double area = 0;
			double curvature = 360f;
			List<Triangle> star = new List<Triangle>();
			
			for(int j = 0; j < mmesh.K.triangles.Count; j++){
				
				int pos = mmesh.K.triangles[j].contains(candidate[i]);

				if(pos != 0){
					int[] triangle = mmesh.K.triangles[j].getT(pos);

					star.Add(new Triangle(triangle[0], triangle[1], triangle[2]));

					Vector3 p0 = mmesh.P[triangle[0]];
					Vector3 p1 = mmesh.P[triangle[1]];
					Vector3 p2 = mmesh.P[triangle[2]];
					Vector3 a = p1 - p0;
					Vector3 b = p2 - p0;

					area += calcArea(a, b);
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
			priorities.Add(candidate[i] , (float)(lambda * areas[i] / maxArea + (1.0 - lambda) * curvatures[i] / maxCurvature));
		}
	} 
	
	public static double calcArea(Vector2 a, Vector2 b){
		return Mathf.Sqrt(a.sqrMagnitude * b.sqrMagnitude - Mathf.Pow(Vector3.Dot(a, b), 2.0f)) * 0.5f;
	}

	public static List<Triangle> retriangulationFromRing(List<int> ring){

		List<Triangle> added_triangles = new List<Triangle>();
		int fow = 1;
		int back = 1;
		int p1 = ring[0];
		int p2 = ring[fow];
		int p3 = ring[ring.Count - back];
		while(fow < ring.Count - back){
			added_triangles.Add(new Triangle(p1, p3, p2));
			
			if(fow == back){
				fow++;
				p1 = p2;
				p2 = ring[fow];
				p3 = ring[ring.Count - back];
			}else{
				back++;
				p1 = p3;
				p2 = ring[fow];
				p3 = ring[ring.Count - back];
			}
		}
		return added_triangles;
	}

	public static List<int> FindRingFromStar(List<Triangle> star, out bool on_boundary){

		on_boundary = false;
		bool flag = true;
		List<bool> used = new List<bool>();
		for(int r = 0; r < star.Count; r++) used.Add(false);

		var firstT = star[0];
		List<int> ring = new List<int>();
		used[0] = true;
		ring.Add(firstT.ind2);
			
		while(flag){
			if(!used.Contains(false)) break;
				
			for(int n = 0; n < star.Count; n++){

				if(!used[n] && star[n].ind2 == ring[ring.Count - 1]){
					ring.Add(star[n].ind3);
					if(star[n].ind3 == firstT.ind3)  flag = false;
					used[n] = true;
					break;
				}

				if(!used[n] && star[n].ind3 == ring[ring.Count - 1]){
					ring.Add(star[n].ind2);
					if(star[n].ind2 == firstT.ind3) flag = false;
					used[n] = true;
					break;
				}
					
				if(n == star.Count - 1) {
					flag = false;
					on_boundary = true;
				}
			}
		}	
		return ring;	
	}

	public static List<int> makeRemovedIndices(Dictionary<int, float> priorities, Dictionary<int ,List<Triangle>> stars){
		var upscebding_priorities = priorities.OrderBy((x) => x.Value).ToDictionary(pair => pair.Key, pair => pair.Value);
		List<int> remove_indices = new List<int>();
		while(upscebding_priorities.Count() > 0){
			int first = upscebding_priorities.ElementAt(0).Key;
			upscebding_priorities.Remove(first);
			remove_indices.Add(first);
			var star = stars[first];

			for(int i = 0; i < star.Count; i++){
				upscebding_priorities.Remove(star[i].ind2);			
				upscebding_priorities.Remove(star[i].ind3);
			}
		}
		return remove_indices;
	}
}
