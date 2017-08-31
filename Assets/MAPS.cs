using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//definitions of structures
public struct MapsMesh {
	List<Vector3> P;
	Topologies K;
	List<int> featurePoints;

	public MapsMesh (List<Vector3> ps, Topologies topo, List<int> fps){
		P = ps;
		K = topo;
		featurePoints = fps;
	}
}

public struct Topologies {
	List<Vertex> vertices;
	List<Edge> edges;
	List<Triangle> triangles;

	public Topologies (List<Vertex> vs, List<Edge> es, List<Triangle> ts){
		vertices = vs;
		edges = es;
		triangles = ts;
	}
}

public struct Vertex {
	int ind;

	public Vertex (int i){
		ind = i;
	}
}

public struct Edge {
	int ind1;
	int ind2;

	public Edge (int i1, int i2){
		ind1 = i1;
		ind2 = i2;
	}
}

public struct Triangle {
	int ind1;
	int ind2;
	int ind3;

	public Triangle (int i1, int i2, int i3){
		ind1 = i1;
		ind2 = i2;
		ind3 = i3;
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
