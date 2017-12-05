using System.Collections;
using System.Collections.Generic;
using UnityEngine;

	public struct QuadEdge {
		Edge orgdest;
		Edge onext;
		Edge dprev;

		public QuadEdge(Edge orgdest, Edge onext, Edge dprev){
			this.orgdest = orgdest;
			this.onext = onext;
			this.dprev = dprev;
		}

		public bool contain(int ind){
			return ind == orgdest.ind1 || ind == orgdest.ind2;
		}

		public Edge e{
			get{
				return orgdest;
			}
		}

		public Vert Org{
			get {
				return new Vert(orgdest.ind1);
			}
		}

		public Vert Dest{
			get {
				return new Vert(orgdest.ind2);
			}
		}

		public Edge Onext{
			get {
				return onext;
			}
		}

		public Edge Dprev{
			get {
				return dprev;
			}
		}
		
		public QuadEdge sym{
			get{ 
				Edge od = new Edge(orgdest.ind2, orgdest.ind1);
				Edge on = new Edge(dprev.ind2, dprev.ind1);
				Edge dp = new Edge(onext.ind2, onext.ind1);
				return new QuadEdge(od, on, dp);
			}
		}
	}
