using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

	public int contains(int ind){
		if(ind == ind1) return 1;
		if(ind == ind2) return 2;
		if(ind == ind3) return 3;
		return 0;
	}
}


public class MAPS : MonoBehaviour {

	public Mesh mesh;
	public int numOfFeaturePoints; 

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
		List<float> priorities = new List<float>();
		float lambda = 0.5f;

		
		for(int i = 0; i < candidate.Count; i++){
			
			float area = 0;
			float curvature = 360f;

			for(int j = 0; j < mmesh.K.triangles.Count; j++){

				int pos = mmesh.K.triangles[j].contains(candidate[i]);
				
				if(pos != 0){
					
				}
			}

			areas.Add(area);
			curvatures.Add(curvature);
		}

		for(int i = 0; i < candidate.Count; i++){
			priorities.Add();
		}
				
		//by the priorities, select the maximum independent set

		//remove the maximum independent set and retrianglation


		return false;
	}

	// Use this for initialization
	void Start () {
		mmesh = TransformMesh2MapsMesh(mesh, numOfFeaturePoints);
		while(levelDown()){}


	}
	
	// Update is called once per frame
	void Update () {
		
	}
}
