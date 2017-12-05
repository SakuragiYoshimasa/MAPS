using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

//definitions of structures
//P is a set of original positions of vertices.
//K is a toopology. Now I implemented not using edge information.
//feature points are selected randomly. TODO: It should be seleced to share between two meshes.
//bijection phai(K^L) -> phai(K^l). If it is null, it mean identity.

public class MapsMesh {
	public List<Vector3> P;
	public Topologies K;
	public List<int> featurePoints;
	public List<Dictionary<int, float>> bijection;

	public MapsMesh (List<Vector3> ps, Topologies topo, List<int> fps){
		P = ps;
		K = topo;
		featurePoints = fps;
		bijection = new List<Dictionary<int, float> >(); 

		for(int i = 0; i < P.Count; i++){
			bijection.Add(new Dictionary<int, float>());
			bijection[i].Add(i, 1.0f);
		}
	}

	public List<Vector3> getProjectedPoints(List<int> indices){
		List<Vector3> projected_points = new List<Vector3>();

		foreach(int ind in indices){
			Vector3 v = Vector3.zero;
			Dictionary<int, float> ps = bijection[ind];
			
			foreach(KeyValuePair<int, float> kv in ps){
				v += P[kv.Key] * kv.Value;
			}
			projected_points.Add(v);
		}
		return projected_points;
	}

	public void removeVertex(int ind){
		for(int n = 0; n < K.vertices.Count; n++){
			if(K.vertices[n].ind == ind){
				K.vertices.RemoveAt(n);
				break;
			}
		}
	}

	public void removeStars(List<Triangle> star){
		
		foreach(Triangle T in star){
			for(int j = 0; j < K.triangles.Count; j++){
				if(K.triangles[j].isEqual(T)){
					K.triangles.RemoveAt(j);
					j--;
				}
			}
		}
	}
}

public struct Topologies {
	public List<Vert> vertices;
	public List<Edge> edges;
	public List<Triangle> triangles;

	public Topologies (List<Vert> vs, List<Edge> es, List<Triangle> ts){
		vertices = vs;
		edges = es;
		triangles = ts;
	}
}

public struct Vert {
	public int ind;

	public Vert (int i){
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

	public bool isEqual(Edge e){
		if(e.ind1 == ind1 && e.ind2 == ind2) return true;
		if(e.ind2 == ind1 && e.ind1 == ind2) return true;
		return false;
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

	public bool contains(int _ind1, int _ind2){
		List<int> indices = new List<int>(){ind1, ind2, ind3};
		return indices.Contains(_ind1) && indices.Contains(_ind2);
	}

	public int[] getT(int pos){
		if(pos == 1) return new int[3]{ind1, ind2, ind3};
		if(pos == 2) return new int[3]{ind2, ind3, ind1};
		if(pos == 3) return new int[3]{ind3, ind1, ind2};
		return new int[0]{};
	}

	public bool isEqual(Triangle T){

		if(ind1 == T.ind1 && ind2 == T.ind2 && ind3 == T.ind3) return true;
		if(ind1 == T.ind1 && ind2 == T.ind3 && ind3 == T.ind2) return true;
		if(ind1 == T.ind2 && ind2 == T.ind1 && ind3 == T.ind3) return true;
		if(ind1 == T.ind2 && ind2 == T.ind3 && ind3 == T.ind1) return true;
		if(ind1 == T.ind3 && ind2 == T.ind1 && ind3 == T.ind2) return true;
		if(ind1 == T.ind3 && ind2 == T.ind2 && ind3 == T.ind1) return true;
		return false;
	}

	public int getOpposite(Triangle T){
		if(T.contains(ind1, ind2)) return ind3;
		if(T.contains(ind1, ind3)) return ind2;
		if(T.contains(ind3, ind2)) return ind1;
		return 1000000000;
	}
}