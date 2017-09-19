using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class MAPStest : MonoBehaviour {

	public GameObject myu_pi_point;

	public void testMappedRing(List<Vector2> mapped_ring, Vector2[] checkLocation, Vector2 myu_pi){
		MeshFilter mf = this.gameObject.AddComponent<MeshFilter>();
		MeshRenderer mr = this.gameObject.AddComponent<MeshRenderer>();

		Mesh m = new Mesh();
		List<Vector3> vs = mapped_ring.Select(v => new Vector3(v.x, v.y, 0)).ToList();
		vs.Add(new Vector3(0,0,0));
		
		foreach(Vector2 v in checkLocation){
			vs.Add(new Vector3(v.x, v.y, 0));
		}

		m.vertices = vs.ToArray();
		//m.SetIndices( Enumerable.Range(0, mapped_ring.Count + 1).ToArray() , MeshTopology.Points, 0);

		List<int> tris = new List<int>();

		for(int i = 0; i < mapped_ring.Count; i++){
			tris.Add(i);
			tris.Add((i + 1 != mapped_ring.Count) ? i + 1 : 0);
			tris.Add(mapped_ring.Count);
		}

		tris.Add(mapped_ring.Count + 1);
		tris.Add(mapped_ring.Count + 2);
		tris.Add(mapped_ring.Count + 3);


		m.SetIndices(tris.ToArray(), MeshTopology.Triangles, 0);
		
		List<Color> colors = Enumerable.Range(0, mapped_ring.Count + 1).Select(v => new Color(0f, 1.0f, 0, 1.0f)).ToList();

		foreach(Vector2 v in checkLocation){
			colors.Add(new Color(0f, 0, 1.0f, 1.0f));
		}

		Debug.Log(colors.Count);
		m.SetColors(colors);
		mf.mesh = m;

		Debug.LogFormat("MAPS_TEST:{0}", MathUtility.checkContain(checkLocation, myu_pi) ? "TRUE" : "FALSE");

		GameObject.Instantiate(myu_pi_point, new Vector3(myu_pi.x, myu_pi.y, 0), Quaternion.identity);
	}
}
