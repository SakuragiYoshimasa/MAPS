using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class CDTtest : MonoBehaviour {

	// Use this for initialization
	void Start () {
		Mesh m = new Mesh();
		List<int> ring = new List<int>(){0,1,2,3,4,5,6,7,8,9};
		Dictionary<int, Vector2> mapped_ring = new Dictionary<int, Vector2>();

		for(int i = 0; i < 10; i++) mapped_ring.Add(ring[i],new Vector2(2.0f * Mathf.Cos((float)i / 5.0f * Mathf.PI) , 10.0f * Mathf.Sin((float)i / 5.0f * Mathf.PI)));
		
		List<Triangle> _tris = CDT.retriangulationFromRingByCDT(ring,mapped_ring, false);
		List<List<int>> dev_tris = _tris.Select(t => new List<int>(){t.ind1, t.ind2, t.ind3}).ToList();
		List<int> tris = new List<int>();
		foreach(List<int> t in dev_tris) tris = tris.Concat(t).ToList();

		Debug.Log(_tris.Count);

		m.vertices = mapped_ring.Select(v => new Vector3(v.Value.x, v.Value.y, 0)).ToArray();
		m.triangles = tris.ToArray();
		m.RecalculateNormals();
		m.RecalculateBounds();
		m.RecalculateTangents();

		MeshFilter mf =  this.gameObject.AddComponent<MeshFilter>();
		MeshRenderer mr = this.gameObject.AddComponent<MeshRenderer>();
		mf.mesh = m;
	}
}
