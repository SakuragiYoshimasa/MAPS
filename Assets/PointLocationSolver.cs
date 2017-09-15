using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class PointLocationSolver {

	public static QuadEdge? SolvePointLocation(ref MapsMesh mmesh, Vector3 target, int startIndex, List<QuadEdge> quadedges, Vector3 n){
		//Function: BF_Locate
		//In: X: Point whose loaction is to be found
		//	  T: Triangulation in which point is to be located 
		//Out: e: Edge on which X lies, or which has the triangle containing X on its left

		//begin
		//e = some edge of T
		//if RightOf(X, e) then e = e.Sym endif
		//while(true)
		//	if X = e.Org or e.Dest then return e
		//	else 
		//		whichop = 0
		//		if not RightOf(X,e.Onext) then whichop +=1 endif
		//		if not RightOf(X,e.Dprev) then whichop +=2 endif
		//		case whichop of
		//			when 0: return e
		//			when 1: e = e.Onext
		//			when 2: e = e.Dprev
		//			when 3:
		//				if dist(e.Onext.X) < dist(e.Dprev, X) then e = e.Onext
		//				else e = e.Dprev

		QuadEdge e = quadedges[startIndex];

		if (RightOf(ref mmesh, target, e.e, n)) e = e.sym;
		while(true){
			int whichop = 0;
			if(!RightOf(ref mmesh, target, e.Onext, n)) whichop +=1;
			if(!RightOf(ref mmesh, target, e.Dprev, n)) whichop +=2;
			int? ind = null;
			switch(whichop){
				case 0:
					return e;
				case 1:
					Debug.Log("case1");
					ind = FindOnextIndex(quadedges, e.Onext);
					break;
				case 2:
					Debug.Log("case2");
					ind = FindDprevIndex(quadedges, e.Dprev);
					break;
				case 3:
					Debug.Log("case3");
					ind = selectByDist(ref mmesh, quadedges, e, target);
					break;
				default:
					break;
			}
			if(ind == null) return null;
			e = quadedges[ind.Value];
		}
	}

	static int? selectByDist(ref MapsMesh mmesh, List<QuadEdge> quadedges, QuadEdge e, Vector3 target){
		Vector3 eo = mmesh.P[e.Onext.ind1] - mmesh.P[e.Onext.ind2];
		Vector3 xe = target - mmesh.P[e.Onext.ind2];
		float inner_product = Vector3.Dot(eo, xe);

		if(inner_product < 0){ 
			return FindDprevIndex(quadedges, e.Dprev);
		} else { 
			return FindOnextIndex(quadedges, e.Onext);
		}
	}

	static int? FindOnextIndex(List<QuadEdge> quadedges, Edge onext){
		for(int i = 0; i < quadedges.Count; i++){
			if(quadedges[i].e.isEqual(onext)) return i;
		}
		return null;
	}

	static int? FindDprevIndex(List<QuadEdge> quadedges, Edge dprev){
		for(int i = 0; i < quadedges.Count; i++){
			if(quadedges[i].e.isEqual(dprev)) return i;
		}
		return null;
	}

	static bool RightOf(ref MapsMesh mmesh, Vector3 X, Edge e, Vector3 n){
		Vector3 orig = mmesh.getProjectedPoints(new List<int>{e.ind1})[0];
		Vector3 dest = mmesh.getProjectedPoints(new List<int>{e.ind2})[0];
		Vector3 v0 = dest - orig;
		Vector3 v1 = X - orig;
		Vector3 cross = Vector3.Cross(v0, v1);
		return Vector3.Dot(cross, n) > 0;
	}
	
}
