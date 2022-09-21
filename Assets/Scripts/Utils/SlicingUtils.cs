using UnityEngine;

public struct Triangle
{
    public Vector3[] vertices;
    public Vector3[] normals;
    public int[] tri;
    public bool positive;
}


public struct PlaneSegInter
{
    public bool aPositive;
    public bool bPositive;
    public float interTime;
    public Vector3? intersection;


    public PlaneSegInter(bool aPositive, bool bPositive, float interTime, Vector3? intersection)
    {
        this.aPositive = aPositive;
        this.bPositive = bPositive;
        this.interTime = interTime;
        this.intersection = intersection;
    }
}


public struct PlaneTriInter
{
    public Triangle a;
    public Triangle? b;
    public Triangle? c;
}