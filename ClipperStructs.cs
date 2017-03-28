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
* library has been split into multiple files, any classes or functionality not
* used by StagPoint have been deleted, and numerous (mostly insignificant) changes
* have been made to the codebase.
*                                                                              
*******************************************************************************/

//use_xyz: adds a Z member to IntPoint. Adds a minor cost to performance.
#define use_xyz

namespace ClipperLib
{
	using System;

	using cInt = System.Int64;

	public struct DoublePoint
	{
		public double X;
		public double Y;

		public DoublePoint( double x = 0, double y = 0 )
		{
			this.X = x;
			this.Y = y;
		}
		public DoublePoint( DoublePoint dp )
		{
			this.X = dp.X;
			this.Y = dp.Y;
		}
		public DoublePoint( IntPoint ip )
		{
			this.X = ip.X;
			this.Y = ip.Y;
		}
	};

	//------------------------------------------------------------------------------
	// Int128 struct (enables safe math on signed 64bit integers)
	// eg Int128 val1((Int64)9223372036854775807); //ie 2^63 -1
	//    Int128 val2((Int64)9223372036854775807);
	//    Int128 val3 = val1 * val2;
	//    val3.ToString => "85070591730234615847396907784232501249" (8.5e+37)
	//------------------------------------------------------------------------------

	internal struct Int128
	{
		private Int64 hi;
		private UInt64 lo;

		public Int128( Int64 _lo )
		{
			lo = (UInt64)_lo;
			if( _lo < 0 )
				hi = -1;
			else
				hi = 0;
		}

		public Int128( Int64 _hi, UInt64 _lo )
		{
			lo = _lo;
			hi = _hi;
		}

		public Int128( Int128 val )
		{
			hi = val.hi;
			lo = val.lo;
		}

		public bool IsNegative()
		{
			return hi < 0;
		}

		public static bool operator ==( Int128 val1, Int128 val2 )
		{
			if( (object)val1 == (object)val2 )
				return true;
			else if( (object)val1 == null || (object)val2 == null )
				return false;
			return ( val1.hi == val2.hi && val1.lo == val2.lo );
		}

		public static bool operator !=( Int128 val1, Int128 val2 )
		{
			return !( val1 == val2 );
		}

		public override bool Equals( System.Object obj )
		{
			if( obj == null || !( obj is Int128 ) )
				return false;
			Int128 i128 = (Int128)obj;
			return ( i128.hi == hi && i128.lo == lo );
		}

		public override int GetHashCode()
		{
			return hi.GetHashCode() ^ lo.GetHashCode();
		}

		public static bool operator >( Int128 val1, Int128 val2 )
		{
			if( val1.hi != val2.hi )
				return val1.hi > val2.hi;
			else
				return val1.lo > val2.lo;
		}

		public static bool operator <( Int128 val1, Int128 val2 )
		{
			if( val1.hi != val2.hi )
				return val1.hi < val2.hi;
			else
				return val1.lo < val2.lo;
		}

		public static Int128 operator +( Int128 lhs, Int128 rhs )
		{
			lhs.hi += rhs.hi;
			lhs.lo += rhs.lo;
			if( lhs.lo < rhs.lo )
				lhs.hi++;
			return lhs;
		}

		public static Int128 operator -( Int128 lhs, Int128 rhs )
		{
			return lhs + -rhs;
		}

		public static Int128 operator -( Int128 val )
		{
			if( val.lo == 0 )
				return new Int128( -val.hi, 0 );
			else
				return new Int128( ~val.hi, ~val.lo + 1 );
		}

		public static explicit operator double( Int128 val )
		{
			const double shift64 = 18446744073709551616.0; //2^64
			if( val.hi < 0 )
			{
				if( val.lo == 0 )
					return (double)val.hi * shift64;
				else
					return -(double)( ~val.lo + ~val.hi * shift64 );
			}
			else
				return (double)( val.lo + val.hi * shift64 );
		}

		//nb: Constructing two new Int128 objects every time we want to multiply longs  
		//is slow. So, although calling the Int128Mul method doesn't look as clean, the 
		//code runs significantly faster than if we'd used the * operator.

		public static Int128 Int128Mul( Int64 lhs, Int64 rhs )
		{
			bool negate = ( lhs < 0 ) != ( rhs < 0 );
			if( lhs < 0 )
				lhs = -lhs;
			if( rhs < 0 )
				rhs = -rhs;
			UInt64 int1Hi = (UInt64)lhs >> 32;
			UInt64 int1Lo = (UInt64)lhs & 0xFFFFFFFF;
			UInt64 int2Hi = (UInt64)rhs >> 32;
			UInt64 int2Lo = (UInt64)rhs & 0xFFFFFFFF;

			//nb: see comments in clipper.pas
			UInt64 a = int1Hi * int2Hi;
			UInt64 b = int1Lo * int2Lo;
			UInt64 c = int1Hi * int2Lo + int1Lo * int2Hi;

			UInt64 lo;
			Int64 hi;
			hi = (Int64)( a + ( c >> 32 ) );

			unchecked { lo = ( c << 32 ) + b; }
			if( lo < b )
				hi++;
			Int128 result = new Int128( hi, lo );
			return negate ? -result : result;
		}

	};

	//------------------------------------------------------------------------------
	//------------------------------------------------------------------------------

	public struct IntPoint
	{
		public cInt X;
		public cInt Y;
#if use_xyz
		public cInt Z;

		public IntPoint( cInt x, cInt y, cInt z = 0 )
		{
			this.X = x;
			this.Y = y;
			this.Z = z;
		}

		public IntPoint( double x, double y, double z )
		{
			this.X = (cInt)x;
			this.Y = (cInt)y;
			this.Z = (cInt)z;
		}

		public IntPoint( DoublePoint dp )
		{
			this.X = (cInt)dp.X;
			this.Y = (cInt)dp.Y;
			this.Z = 0;
		}

		public IntPoint( IntPoint pt )
		{
			this.X = pt.X;
			this.Y = pt.Y;
			this.Z = pt.Z;
		}
#else
		public IntPoint( cInt X, cInt Y )
		{
			this.X = X;
			this.Y = Y;
		}

		public IntPoint( double x, double y )
		{
			this.X = (cInt)x;
			this.Y = (cInt)y;
		}

		public IntPoint( IntPoint pt )
		{
			this.X = pt.X;
			this.Y = pt.Y;
		}
#endif

		public static bool operator ==( IntPoint a, IntPoint b )
		{
			return a.X == b.X && a.Y == b.Y;
		}

		public static bool operator !=( IntPoint a, IntPoint b )
		{
			return a.X != b.X || a.Y != b.Y;
		}

		public override bool Equals( object obj )
		{
			if( obj == null )
				return false;
			if( obj is IntPoint )
			{
				IntPoint a = (IntPoint)obj;
				return ( X == a.X ) && ( Y == a.Y );
			}
			else
				return false;
		}

		public override int GetHashCode()
		{
			//simply prevents a compiler warning
			return base.GetHashCode();
		}

		public override string ToString()
		{
#if use_xyz
			return string.Format( "[{0}, {1}, {2}]", this.X, this.Y, this.Z );
#else
			return string.Format( "[{0}, {1}]", this.X, this.Y );
#endif
		}

	}// end struct IntPoint

	public struct IntRect
	{
		public cInt left;
		public cInt top;
		public cInt right;
		public cInt bottom;

		public IntRect( cInt l, cInt t, cInt r, cInt b )
		{
			this.left = l;
			this.top = t;
			this.right = r;
			this.bottom = b;
		}
		public IntRect( IntRect ir )
		{
			this.left = ir.left;
			this.top = ir.top;
			this.right = ir.right;
			this.bottom = ir.bottom;
		}
	}
}