/*******************************************************************************
*                                                                              *
* Author    :  Angus Johnson                                                   *
* Version   :  6.4.2                                                           *
* Date      :  27 February 2017                                                *
* Website   :  http://www.angusj.com                                           *
* Copyright :  Angus Johnson 2010-2017                                         *
*                                                                              *
* License:                                                                     *
* Use, modification & distribution is subject to Boost Software License Ver 1. *
* http://www.boost.org/LICENSE_1_0.txt                                         *
*                                                                              *
* Attributions:                                                                *
* The code in this library is an extension of Bala Vatti's clipping algorithm: *
* "A generic solution to polygon clipping"                                     *
* Communications of the ACM, Vol 35, Issue 7 (July 1992) pp 56-63.             *
* http://portal.acm.org/citation.cfm?id=129906                                 *
*                                                                              *
* Computer graphics and geometric modeling: implementation and algorithms      *
* By Max K. Agoston                                                            *
* Springer; 1 edition (January 4, 2005)                                        *
* http://books.google.com/books?q=vatti+clipping+agoston                       *
*                                                                              *
* See also:                                                                    *
* "Polygon Offsetting by Computing Winding Numbers"                            *
* Paper no. DETC2005-85513 pp. 565-575                                         *
* ASME 2005 International Design Engineering Technical Conferences             *
* and Computers and Information in Engineering Conference (IDETC/CIE2005)      *
* September 24-28, 2005 , Long Beach, California, USA                          *
* http://www.me.berkeley.edu/~mcmains/pubs/DAC05OffsetPolygon.pdf              *
*                                                                              *
*******************************************************************************/

/*******************************************************************************
*                                                                              *
* This is a translation of the Delphi Clipper library and the naming style     *
* used has retained a Delphi flavour.                                          *
*                                                                              *
*******************************************************************************/

/*******************************************************************************
*                                                                              
* NOTE: This version of Clipper has been heavily modified by StagPoint Software
* to best match the current project and use cases. The original single-file 
* library has been split into multiple files, any classes or functions not
* used by StagPoint have been deleted, and numerous (mostly insignificant) changes
* have been made to the codebase.
*                                                                              
*******************************************************************************/

namespace ClipperLib
{
	using System;
	using System.Collections.Generic;

	using cInt = System.Int64;

	public class Path : List<IntPoint>
	{
		public Path()
			: base()
		{
		}

		public Path( int capacity )
			: base( capacity )
		{
		}

		public override string ToString()
		{
			return string.Format( "Count: {0}", this.Count );
		}
	}

	internal class EdgeList : List<TEdge>
	{
		public EdgeList()
			: base()
		{
		}

		public EdgeList( int capacity )
			: base( capacity )
		{
		}

		public override string ToString()
		{
			return string.Format( "Count: {0}", this.Count );
		}
	}

	public class PolyTree : PolyNode
	{
		internal List<PolyNode> AllPolygons = new List<PolyNode>();

		public override void Clear()
		{
			for( int i = 0; i < AllPolygons.Count; i++ )
			{
				AllPolygons[ i ] = null;
			}

			AllPolygons.Clear();

			base.Clear();
		}

		public PolyNode GetFirst()
		{
			if( m_childNodes.Count > 0 )
				return m_childNodes[ 0 ];
			else
				return null;
		}

		public int Total
		{
			get
			{
				int result = AllPolygons.Count;
				//with negative offsets, ignore the hidden outer polygon ...
				if( result > 0 && m_childNodes[ 0 ] != AllPolygons[ 0 ] )
					result--;
				return result;
			}
		}
	}

	public class PolyNode : IPooledObject
	{
		protected PolyNode m_parent;
		protected Path m_polygon = new Path();
		protected int m_index;

		protected List<PolyNode> m_childNodes = new List<PolyNode>();

		public List<PolyNode> ChildNodes
		{
			get { return m_childNodes; }
		}

		public PolyNode Parent
		{
			get { return m_parent; }
		}

		public bool IsHole
		{
			get { return IsHoleNode(); }
		}

		public bool IsOpen { get; set; }

		public int ChildCount
		{
			get { return m_childNodes.Count; }
		}

		public Path Contour
		{
			get { return m_polygon; }
		}

		public void AddChild( PolyNode Child )
		{
			int cnt = m_childNodes.Count;

			m_childNodes.Add( Child );

			Child.m_parent = this;
			Child.m_index = cnt;
		}

		public PolyNode GetNext()
		{
			if( m_childNodes.Count > 0 )
				return m_childNodes[ 0 ];
			else
				return GetNextSiblingUp();
		}

		internal PolyNode GetNextSiblingUp()
		{
			if( m_parent == null )
				return null;
			else if( m_index == m_parent.m_childNodes.Count - 1 )
				return m_parent.GetNextSiblingUp();
			else
				return m_parent.m_childNodes[ m_index + 1 ];
		}

		private bool IsHoleNode()
		{
			bool result = true;
			PolyNode node = m_parent;

			while( node != null )
			{
				result = !result;
				node = node.m_parent;
			}

			return result;
		}

		public virtual void Clear()
		{
			for( int i = 0; i < m_childNodes.Count; i++ )
			{
				m_childNodes[ i ].Clear();
			}
			m_childNodes.Clear();

			m_parent = null;
			m_polygon.Clear();
		}

		internal void EnsureChildCapacity( int count )
		{
			m_childNodes.Capacity = Math.Max( m_childNodes.Capacity, count );
		}
		
		internal void EnsurePolygonCapacity( int count )
		{
			m_polygon.Capacity = Math.Max( m_polygon.Capacity, count );
		}

		internal void AddToPolygon( ref IntPoint intPoint )
		{
			m_polygon.Add( intPoint );
		}

		#region IPooledObject Members

		/// <summary>
		/// Reset all fields to default values in preparation for object recycling
		/// </summary>
		public void PrepareForRecycle()
		{
			Clear();
		}

		#endregion
	}

	internal class TEdge : IPooledObject
	{
		internal IntPoint Bot;
		internal IntPoint Curr; //current (updated for every new scanbeam)
		internal IntPoint Top;
		internal IntPoint Delta;
		internal double Dx;

		internal PolyType PolyTyp;
		internal EdgeSide Side; //side only refers to current side of solution poly

		internal int WindDelta; //1 or -1 depending on winding direction
		internal int WindCnt;
		internal int WindCnt2; //winding count of the opposite polytype
		internal int OutIdx;

		internal TEdge Next;
		internal TEdge Prev;
		internal TEdge NextInLML;
		internal TEdge NextInAEL;
		internal TEdge PrevInAEL;
		internal TEdge NextInSEL;
		internal TEdge PrevInSEL;

		#region IPooledObject Members

		/// <summary>
		/// Reset all fields to default values in preparation for object recycling
		/// </summary>
		public void PrepareForRecycle()
		{
			PolyTyp = PolyType.Subject;
			Side = EdgeSide.Left;

			Bot = Curr = Top = Delta = new IntPoint();

			Dx = 0;
			WindDelta = 0;
			WindCnt = 0;
			WindCnt2 = 0;
			OutIdx = 0;

			Next = Prev = null;
			PrevInAEL = PrevInSEL = null;
			NextInLML = NextInAEL = NextInSEL = null;
		}

		#endregion

		#region Enforced object pooling

		private TEdge()
		{
			// Force instantiation to occur through the object pool
		}

		internal class Pool : LocalObjectPool<TEdge>
		{
			protected override TEdge AllocateNewInstance()
			{
				return new TEdge();
			}
		}

		#endregion 
	}

	internal class LocalMinima : IPooledObject
	{
		internal cInt Y;
		internal TEdge LeftBound;
		internal TEdge RightBound;
		internal LocalMinima Next;

		#region IPooledObject Members

		/// <summary>
		/// Reset all fields to default values in preparation for object recycling
		/// </summary>
		public void PrepareForRecycle()
		{
			Y = new cInt();
			LeftBound = RightBound = null;
			Next = null;
		}

		#endregion
	
		#region Enforced object pooling

		private LocalMinima()
		{
			// Force instantiation to occur through the object pool
		}

		internal class Pool : LocalObjectPool<LocalMinima>
		{
			protected override LocalMinima AllocateNewInstance()
			{
				return new LocalMinima();
			}
		}

		#endregion 
	}

	internal class Scanbeam : IPooledObject
	{
		internal cInt Y;
		internal Scanbeam Next;

		#region IPooledObject Members

		/// <summary>
		/// Reset all fields to default values in preparation for object recycling
		/// </summary>
		public void PrepareForRecycle()
		{
			Y = new cInt();
			Next = null;
		}

		#endregion

		#region Enforced object pooling

		private Scanbeam()
		{
			// Force instantiation to occur through the object pool
		}

		internal class Pool : LocalObjectPool<Scanbeam>
		{
			protected override Scanbeam AllocateNewInstance()
			{
				return new Scanbeam();
			}
		}

		#endregion 
	}

	internal class Maxima : IPooledObject
	{
		internal cInt X;
		internal Maxima Next;
		internal Maxima Prev;

		#region IPooledObject Members

		/// <summary>
		/// Reset all fields to default values in preparation for object recycling
		/// </summary>
		public void PrepareForRecycle()
		{
			X = new cInt();
			Next = Prev = null;
		}

		#endregion

		#region Enforced object pooling

		private Maxima()
		{
			// Force instantiation to occur through the object pool
		}

		internal class Pool : LocalObjectPool<Maxima>
		{
			protected override Maxima AllocateNewInstance()
			{
				return new Maxima();
			}
		}

		#endregion 
	}

	//OutRec: contains a path in the clipping solution. Edges in the AEL will
	//carry a pointer to an OutRec when they are part of the clipping solution.
	internal class OutRec : IPooledObject
	{
		internal int Idx;
		internal bool IsHole;
		internal bool IsOpen;
		internal OutRec FirstLeft; //see comments in clipper.pas
		internal OutPt Pts;
		internal OutPt BottomPt;
		internal PolyNode PolyNode;

		#region IPooledObject Members

		/// <summary>
		/// Reset all fields to default values in preparation for object recycling
		/// </summary>
		public void PrepareForRecycle()
		{
			Idx = 0;
			IsHole = IsOpen = false;

			FirstLeft = null;
			Pts = BottomPt = null;
			PolyNode = null;
		}

		#endregion

		#region Enforced object pooling

		private OutRec()
		{
			// Force instantiation to occur through the object pool
		}

		internal class Pool : LocalObjectPool<OutRec>
		{
			protected override OutRec AllocateNewInstance()
			{
				return new OutRec();
			}
		}

		#endregion 
	}

	internal class OutPt : IPooledObject
	{
		internal int Idx;
		internal IntPoint Pt;
		internal OutPt Next;
		internal OutPt Prev;

		#region IPooledObject Members

		/// <summary>
		/// Reset all fields to default values in preparation for object recycling
		/// </summary>
		public void PrepareForRecycle()
		{
			Idx = 0;
			Pt = new IntPoint();
			Next = Prev = null;
		}

		#endregion

		#region Enforced object pooling

		private OutPt()
		{
			// Force instantiation to occur through the object pool
		}

		internal class Pool : LocalObjectPool<OutPt>
		{
			protected override OutPt AllocateNewInstance()
			{
				return new OutPt();
			}
		}

		#endregion 
	}

	internal class Join : IPooledObject
	{
		internal OutPt OutPt1;
		internal OutPt OutPt2;
		internal IntPoint OffPt;

		#region IPooledObject Members

		/// <summary>
		/// Reset all fields to default values in preparation for object recycling
		/// </summary>
		public void PrepareForRecycle()
		{
			OutPt1 = null;
			OutPt2 = null;
			OffPt = new IntPoint();
		}

		#endregion

		#region Enforced object pooling 

		private Join() 
		{ 
			// Force instantiation to occur through the object pool
		}

		internal class Pool : LocalObjectPool<Join>
		{
			protected override Join AllocateNewInstance()
			{
				return new Join();
			}
		}

		#endregion 
	}

	internal class IntersectNode : IPooledObject
	{
		internal TEdge Edge1;
		internal TEdge Edge2;
		internal IntPoint Pt;

		#region IPooledObject Members

		/// <summary>
		/// Reset all fields to default values in preparation for object recycling
		/// </summary>
		public void PrepareForRecycle()
		{
			Edge1 = null;
			Edge2 = null;
			Pt = new IntPoint();
		}

		#endregion

		#region Nested types 

		public struct Comparer : IComparer<IntersectNode>
		{
			public int Compare( IntersectNode node1, IntersectNode node2 )
			{
				cInt i = node2.Pt.Y - node1.Pt.Y;
				if( i > 0 )
					return 1;
				else if( i < 0 )
					return -1;
				else
					return 0;
			}
		}
		
		#endregion 

		#region Enforced object pooling

		private IntersectNode()
		{
			// Force instantiation to occur through the object pool
		}

		internal class Pool : LocalObjectPool<IntersectNode>
		{
			protected override IntersectNode AllocateNewInstance()
			{
				return new IntersectNode();
			}
		}

		#endregion 
	}
}