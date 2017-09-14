using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class CDT {
	
	public static List<Triangle> retriangulationFromRingByCDT(List<int> ring, List<Vector2> mapped_ring){

		//領域を覆う大きな三角形を生成する
		//制約線分に接するノードを一個ずつ選択し、以下の処理を行う
		//　ノードDを内部に含む三角形ABCをABD,BCD,CADに分割する
		//　ノードDに接するすべての三角形に対して変換処理２を行う、「変換される」、「変換されない」とも判断されなければ変換処理１を行う
		//　ノードDに接するすべての三角形に対して要素辺と制約線分との幾何的な交差判定を行う
		//　２辺が同一制約線分と交差している三角形が抽出された場合にはノードDの処理前の状態にメッシュを戻し、ノードD以外のノードを選択する
		//　すべてのノードについて処理が終了した時点で領域の外に生成された三角形を除去し、メッシュを完成する

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
