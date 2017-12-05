using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public static class CDT {
	
	public static List<Triangle> retriangulationFromRingByCDT(List<int> ring, Dictionary<int,Vector2> mapped_ring, bool on_boundary){
		//CDTの適用は擬似indicesで行い、最後に元に戻して返す
		List<Triangle> added_triangles = new List<Triangle>();
		List<int> used_nodes = new List<int>();
		//領域を覆う大きな三角形を生成する
		//制約線分に接するノードを一個ずつ選択し、以下の処理を行う
		//　ノードDを内部に含む三角形ABCをABD,BCD,CADに分割する
		//　ノードDに接するすべての三角形に対して変換処理２を行う、「変換される」、「変換されない」とも判断されなければ変換処理１を行う
		//　ノードDに接するすべての三角形に対して要素辺と制約線分との幾何的な交差判定を行う
		//　２辺が同一制約線分と交差している三角形が抽出された場合にはノードDの処理前の状態にメッシュを戻し、ノードD以外のノードを選択する
		//　すべてのノードについて処理が終了した時点で領域の外に生成された三角形を除去し、メッシュを完成する

		//Make super triangle
		List<Vector2> temp_mapped_ring = new List<Vector2>();
		for(int i = 0; i < ring.Count; i++){
			temp_mapped_ring.Add(mapped_ring[ring[i]]);
		}

		float super_rad = mapped_ring.Select(pos => pos.Value.magnitude).Max() * 100.0f;

		for(int i = 0; i < 3; i++){
			used_nodes.Add(ring.Count + i);	
			temp_mapped_ring.Add(new Vector2(super_rad * Mathf.Cos(Mathf.PI * 2.0f / 3.0f * (float)i), super_rad * Mathf.Sin(Mathf.PI * 2.0f / 3.0f * (float)i)));
		}
		added_triangles.Add(new Triangle(ring.Count, ring.Count + 1, ring.Count + 2));
		
		Queue<int> node_queue = new Queue<int>();
		for(int i = 0; i < ring.Count; i++) node_queue.Enqueue(i);

		//foreach all ring point, 
		while(node_queue.Count > 0){
			int target = node_queue.Dequeue();
			
			//if(target == 4) break;
			//	find a triangle which contain the point
			int? triangle_ind = null;
			
			for(int i = 0; i < added_triangles.Count; i++){
				Triangle T = added_triangles[i];
				Vector2[] points = new Vector2[3]{temp_mapped_ring[T.ind1], temp_mapped_ring[T.ind2], temp_mapped_ring[T.ind3]};
				if(MathUtility.checkContain(points, temp_mapped_ring[target])){
					triangle_ind = i;
					break;
				}
			}
			if(triangle_ind == null){
				Debug.Log("NULL");
			}
			
		
			//復元用
			int add_count = 0;
			List<Triangle> removed_tris = new List<Triangle>();

			//	devide into 3 triangles
			Triangle devided_tri = added_triangles[triangle_ind.Value];
			List<Triangle> new_tris = new List<Triangle>(){new Triangle(target, devided_tri.ind1, devided_tri.ind2), new Triangle(target, devided_tri.ind2, devided_tri.ind3), new Triangle(target, devided_tri.ind3, devided_tri.ind1)}; 
			added_triangles.RemoveAt(triangle_ind.Value);
			//added_triangles.Add(new_tris[0]);
			//added_triangles.Add(new_tris[1]);
			//added_triangles.Add(new_tris[2]);
			removed_tris.Add(devided_tri);

			//Transformation
			transform(new_tris, ref temp_mapped_ring, ref added_triangles, ref add_count, ref removed_tris);

			//cross discrimination
			//revert or not
			//if(isCrossingConstrainedEdge(target, ring.Count, ref mapped_ring, ref added_triangles)){
			//	revert(ref added_triangles, removed_tris, add_count);				
				
			//	node_queue.Enqueue(target);
			//}
		}

		//extract super triangle and neighboring edges 
		removeSuperTriangle(ring.Count, ref added_triangles, ref temp_mapped_ring);

		//transform to real indices 
		List<Triangle> converted_added_triangles = new List<Triangle>();

		foreach(Triangle T in added_triangles){
			converted_added_triangles.Add(new Triangle(ring[T.ind1],ring[T.ind2],ring[T.ind3]));
		}

		return converted_added_triangles;
	}

	

	public static void transform(List<Triangle> new_tris, ref List<Vector2> mapped_ring, ref List<Triangle> added_triangles, ref int add_count, ref List<Triangle> removed_tris){
		//List<Triangle> result = new List<Triangle>();

		for(int i = 0; i < new_tris.Count; i++){
				
			Triangle T1 = new_tris[i];
			Triangle? neighb_tri = null;
			int? neighb_tri_ind = null;

			//Find neighbor triangles
			for(int j = 0; j < added_triangles.Count; j++){
				Triangle T = added_triangles[j];

				if(T.contains(T1.ind2, T1.ind3)){
					neighb_tri = T;
					neighb_tri_ind = j;
					break;
				}
			}

			//No neighbor
			if(neighb_tri == null){
				added_triangles.Add(T1);
				add_count += 1;
				continue;
			}

			//Pattern2
			/* 
			if(Mathf.Abs(T1.ind2 - T1.ind3) == 1 || Mathf.Abs(T1.ind2 - T1.ind3) == mapped_ring.Count - 4){
				//No trans if constrained edge
				added_triangles.Add(T1);
				add_count += 1;
				continue;
			}else if(Mathf.Abs(T1.ind1 - neighb_tri.Value.getOpposite(T1)) == 1 || Mathf.Abs(T1.ind1 - neighb_tri.Value.getOpposite(T1)) == mapped_ring.Count - 4){
				//remove T1 and neighb, make new triangles
				added_triangles.RemoveAt(neighb_tri_ind.Value);
				removed_tris.Add(neighb_tri.Value);
				int opposite = neighb_tri.Value.getOpposite(T1);
				
				List<Triangle> next = new List<Triangle>(){new Triangle(T1.ind1, T1.ind2, opposite), new Triangle(T1.ind1, opposite, T1.ind3)};
				transform(next, ref mapped_ring, ref added_triangles, ref add_count, ref removed_tris);
				continue;
			}
			*/
			//Pattern1
			if(needRemesh(T1, neighb_tri.Value, ref mapped_ring) || needRemesh(neighb_tri.Value, T1, ref mapped_ring)){
				
				added_triangles.RemoveAt(neighb_tri_ind.Value);
				removed_tris.Add(neighb_tri.Value);
				int opposite = neighb_tri.Value.getOpposite(T1);
				List<Triangle> next = new List<Triangle>(){new Triangle(T1.ind1, T1.ind2, opposite), new Triangle(T1.ind1, opposite, T1.ind3)};
				transform(next, ref mapped_ring, ref added_triangles, ref add_count, ref removed_tris);
				continue;
			}else{
				added_triangles.Add(T1);
				add_count += 1;
				continue;
			}
		}
	}

	public static bool isCrossingConstrainedEdge(int target, int ring_count, ref List<Vector2> mapped_ring, ref List<Triangle> added_triangles){

		List<Edge> constrained_edges = new List<Edge>();
		for(int i = 0; i < ring_count - 1; i++){
			constrained_edges.Add(new Edge(i , i + 1 != ring_count - 1 ? i + 1 : 0));
		}

		for(int i = 0; i < added_triangles.Count; i++){
			if(added_triangles[i].contains(target) == 0) continue;

			for(int j = 0; j < constrained_edges.Count; j++){
				if(crossTest(mapped_ring, added_triangles[i], constrained_edges[j]) >= 2) return true;
			}
		}

		return false;
	}

	public static int crossTest(List<Vector2> mapped_ring, Triangle triangle, Edge constrained_edge){
		
		//http://www5d.biglobe.ne.jp/~tomoya03/shtml/algorithm/Intersection.htm
		int intersection_count = 0;
		if(isIntersection(mapped_ring[triangle.ind1], mapped_ring[triangle.ind2], mapped_ring[constrained_edge.ind1], mapped_ring[constrained_edge.ind2])) intersection_count += 1;
		if(isIntersection(mapped_ring[triangle.ind2], mapped_ring[triangle.ind3], mapped_ring[constrained_edge.ind1], mapped_ring[constrained_edge.ind2])) intersection_count += 1;
		if(isIntersection(mapped_ring[triangle.ind3], mapped_ring[triangle.ind1], mapped_ring[constrained_edge.ind1], mapped_ring[constrained_edge.ind2])) intersection_count += 1;
		return intersection_count;
	}

	public static bool isIntersection(Vector2 a, Vector2 b, Vector2 c, Vector2 d){
		var ta = (c.x - d.x) * (a.y - c.y) + (c.y - d.y) * (c.x - a.x);
 		var tb = (c.x - d.x) * (b.y - c.y) + (c.y - d.y) * (c.x - b.x);
  		var tc = (a.x - b.x) * (c.y - a.y) + (a.y - b.y) * (a.x - c.x);
  		var td = (a.x - b.x) * (d.y - a.y) + (a.y - b.y) * (a.x - d.x);
  		return tc * td < 0 && ta * tb < 0;
	}

	public static void revert(ref List<Triangle> added_triangles, List<Triangle> removed_tris, int add_count){
		Debug.LogFormat("{0}, {1}", add_count, added_triangles.Count);
		added_triangles.RemoveRange(added_triangles.Count - 1 - add_count, add_count);
		Debug.LogFormat("{0}, {1}", add_count, added_triangles.Count);
		added_triangles.AddRange(removed_tris);
	}

	public static void removeSuperTriangle(int ring_count, ref List<Triangle> added_triangles, ref List<Vector2> mapped_ring){
		for(int i = ring_count; i < ring_count + 3; i++){
	
			mapped_ring.RemoveAt(ring_count);
			for(int j = 0; j < added_triangles.Count; j++){
				if(added_triangles[j].contains(i) != 0){
					added_triangles.RemoveAt(j);
					j--;
				}
			}
		}
	}

	//referenced http://tercel-sakuragaoka.blogspot.jp/2011/06/processingdelaunay_3958.html
	public static bool needRemesh(Triangle T1, Triangle T2, ref List<Vector2> mapped_ring){
		
		float x1 = mapped_ring[T1.ind1].x;  
		float y1 = mapped_ring[T1.ind1].y;  
		float x2 = mapped_ring[T1.ind2].x;  
		float y2 = mapped_ring[T1.ind2].y;  
		float x3 = mapped_ring[T1.ind3].x;  
		float y3 = mapped_ring[T1.ind3].y;  
			
		float c = 2.0f * ((x2 - x1) * (y3 - y1) - (y2 - y1) * (x3 - x1));  
		float x = ((y3 - y1) * (x2 * x2 - x1 * x1 + y2 * y2 - y1 * y1)  
				+ (y1 - y2) * (x3 * x3 - x1 * x1 + y3 * y3 - y1 * y1))/c;  
		float y = ((x1 - x3) * (x2 * x2 - x1 * x1 + y2 * y2 - y1 * y1)  
				+ (x2 - x1) * (x3 * x3 - x1 * x1 + y3 * y3 - y1 * y1))/c;  
		Vector2 center = new Vector2(x, y);  
		float r = Vector2.Distance(center, mapped_ring[T1.ind1]);
		
		int opposite = T2.getOpposite(T1);
		Vector2 op = mapped_ring[opposite];

		return r > Vector2.Distance(op, center);
	}
}
