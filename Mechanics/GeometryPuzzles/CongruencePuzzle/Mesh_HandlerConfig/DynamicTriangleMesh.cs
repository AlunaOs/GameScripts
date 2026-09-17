using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class DynamicTriangleMesh : Graphic
{
    private Vector2 pA = new Vector2(-50, 50);
    private Vector2 pB = new Vector2(-100, -50);
    private Vector2 pC = new Vector2(0, -50);

    // Reverted default scale multiplier back to 50f
    public void UpdateTriangleVertices(Vector3 vA, Vector3 vB, Vector3 vC, float scaleMultiplier = 50f)
    {
        pA = new Vector2(vA.x, vA.y) * scaleMultiplier;
        pB = new Vector2(vB.x, vB.y) * scaleMultiplier;
        pC = new Vector2(vC.x, vC.y) * scaleMultiplier;

        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        UIVertex vertA = UIVertex.simpleVert;
        vertA.color = color;
        vertA.position = pA;

        UIVertex vertB = UIVertex.simpleVert;
        vertB.color = color;
        vertB.position = pB;

        UIVertex vertC = UIVertex.simpleVert;
        vertC.color = color;
        vertC.position = pC;

        vh.AddVert(vertA);
        vh.AddVert(vertB);
        vh.AddVert(vertC);

        vh.AddTriangle(0, 1, 2);
    }
}