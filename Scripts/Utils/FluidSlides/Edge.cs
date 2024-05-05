using UnityEngine;
public struct Edge
{
    public int P0 { get; }
    public int P1 { get; }

    public Edge(int p0, int p1)
    {
        P0 = p0;
        P1 = p1;
    }

    public override bool Equals(object obj)
    {
        return obj is Edge edge &&
               ((P0 == edge.P0 && P1 == edge.P1) || (P0 == edge.P1 && P1 == edge.P0));
    }

    public override int GetHashCode()
    {
        int hash = 17;
        hash = hash * 31 + Mathf.Min(P0, P1);
        hash = hash * 31 + Mathf.Max(P0, P1);
        return hash;
    }
}