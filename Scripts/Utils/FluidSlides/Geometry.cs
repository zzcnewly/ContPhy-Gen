using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;

namespace GeometryIn2D{

public struct Point
{
    public double x, y;
}

class LineSegmentDistance
{

    static double Distance(Point a, Point b)
    {
        double dx = a.x - b.x;
        double dy = a.y - b.y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    static bool LineSegmentsIntersect(Point p1, Point q1, Point p2, Point q2)
    {
        double x1 = p1.x, y1 = p1.y, x2 = q1.x, y2 = q1.y;
        double x3 = p2.x, y3 = p2.y, x4 = q2.x, y4 = q2.y;
        bool onSides1 = ((x2 - x1) * (y3 - y1) - (x3 - x1) * (y2 - y1)) * ((x2 - x1) * (y4 - y1) - (x4 - x1) * (y2 - y1)) <= 0;
        bool onSides2 = ((x4 - x3) * (y1 - y3) - (x1 - x3) * (y4 - y3)) * ((x4 - x3) * (y2 - y3) - (x2 - x3) * (y4 - y3)) <= 0;
        return onSides1 && onSides2;
    }

    static double PointToSegmentDistance(Point p, Point a, Point b)
    {
        double lengthSq = (b.x - a.x) * (b.x - a.x) + (b.y - a.y) * (b.y - a.y);
        if (lengthSq == 0) return Distance(p, a);

        double t = ((p.x - a.x) * (b.x - a.x) + (p.y - a.y) * (b.y - a.y)) / lengthSq;
        t = Math.Max(0, Math.Min(1, t));

        Point projection = new Point { x = a.x + t * (b.x - a.x), y = a.y + t * (b.y - a.y) };
        return Distance(p, projection);
    }

    static public double LineSegmentDist(Point p1, Point q1, Point p2, Point q2)
    {
        if (LineSegmentsIntersect(p1, q1, p2, q2))
            return 0;

        double minDistance = Math.Min(
            Math.Min(PointToSegmentDistance(p1, p2, q2), PointToSegmentDistance(q1, p2, q2)),
            Math.Min(PointToSegmentDistance(p2, p1, q1), PointToSegmentDistance(q2, p1, q1))
        );

        return minDistance; 
    }

    static public bool PointInsideTriangle(Point p, List<Point> T) { // done
        double x = p.x, y = p.y;
        double xa = T[0].x, ya = T[0].y, xb = T[1].x, yb = T[1].y, xc = T[2].x, yc = T[2].y;
        bool same_c = ((xb - xa) * (y - ya) - (x - xa) * (yb - ya)) * ((xb - xa) * (yc - ya) - (xc - xa) * (yb - ya)) >= 0;
        bool same_a = ((xc - xb) * (y - yb) - (x - xb) * (yc - yb)) * ((xc - xb) * (ya - yb) - (xa - xb) * (yc - yb)) >= 0;
        bool same_b = ((xa - xc) * (y - yc) - (x - xc) * (ya - yc)) * ((xa - xc) * (yb - yc) - (xb - xc) * (ya - yc)) >= 0;
        return same_a && same_b && same_c;
    }

    static public bool TrianglesIntersect(List<Point> A, List<Point> B) {
        for (int i = 0; i < 3; i++)
        {
            if (PointInsideTriangle(A[i], B) || PointInsideTriangle(B[i], A)) return true;
        }
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                if (LineSegmentsIntersect(A[i], A[(i + 1) % 3], B[j], B[(j + 1) % 3]))
                    return true;
        return false;
    }
    // line- triangle intersection
    static public bool LineTriangleIntersect(List<Point> T, Point p1, Point p2) {
        for (int i = 0; i < 3; i++)
            if (LineSegmentsIntersect(T[i], T[(i + 1) % 3], p1, p2))
                return true;
        return false;
    }

}

}
