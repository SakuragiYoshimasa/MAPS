using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class CDT {
	
	public static List<Triangle> retriangulationFromRingByCDT(List<int> ring, List<Vector2> mapped_ring){
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
}
