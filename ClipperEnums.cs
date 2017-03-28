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

namespace ClipperLib
{
	public enum ClipType { Intersection, Union, Difference, Xor };
	public enum PolyType { Subject, Clip };

	internal enum EdgeSide { Left, Right };
	internal enum Direction { RightToLeft, LeftToRight };

	internal enum NodeType { Any, Open, Closed };

	public enum PolyFillType 
	{
		//By far the most widely used winding rules for polygon filling are
		//EvenOdd & NonZero (GDI, GDI+, XLib, OpenGL, Cairo, AGG, Quartz, SVG, Gr32)
		//Others rules include Positive, Negative and ABS_GTR_EQ_TWO (only in OpenGL)
		//see http://glprogramming.com/red/chapter11.html

		EvenOdd, 
		NonZero, 
		Positive, 
		Negative 
	};
}