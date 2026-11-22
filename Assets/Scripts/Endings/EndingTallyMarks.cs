using System.Collections.Generic;
using UnityEngine;

namespace DeadLetter.Endings
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class EndingTallyMarks : MonoBehaviour
    {
        [SerializeField, Range(10, 150)] private int normalCount = 48;
        [SerializeField, Range(100, 600)] private int trueCount = 240;
        private Mesh generated;
        public int NormalCount => normalCount;
        public int TrueCount => trueCount;

        // XY unit rectangle, +Z faces into the room. A single double-sided mesh, no glyph objects.
        public void Show(bool dense, float lastDiagonal = 1f)
        {
            if (generated == null) { generated = new Mesh { name = "Ending tally strokes (runtime)" }; GetComponent<MeshFilter>().sharedMesh = generated; }
            Build(generated, dense ? trueCount : normalCount, dense ? 1f : lastDiagonal);
            GetComponent<MeshRenderer>().enabled = true;
        }
        public static void Build(Mesh mesh, int count, float lastDiagonal)
        {
            count = Mathf.Clamp(count, 1, 600);
            int columns = Mathf.CeilToInt(Mathf.Sqrt(count * 1.25f));
            int rows = Mathf.CeilToInt((float)count / columns);
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int glyph = 0; glyph < count; glyph++)
            {
                Vector2 origin = new(-0.48f + (glyph % columns) * .96f / columns, -.46f + (glyph / columns) * .92f / rows);
                Vector2 size = new(.75f / columns, .65f / rows);
                for (int stroke = 0; stroke < 5; stroke++)
                {
                    float progress = stroke == 4 && glyph == count - 1 ? Mathf.Clamp01(lastDiagonal) : 1f;
                    if (progress <= 0) continue;
                    Vector2 a = stroke < 4 ? new(size.x * (.12f + stroke * .25f), 0) : new(0, size.y * .15f);
                    Vector2 b = stroke < 4 ? new(a.x + size.x * .035f, size.y) : new(size.x, size.y * .85f);
                    b = Vector2.Lerp(a, b, progress);
                    for (int part = 0; part < 4; part++)
                    {
                        Vector2 p = origin + Vector2.Lerp(a,b,part / 4f);
                        Vector2 q = origin + Vector2.Lerp(a,b,(part+1) / 4f);
                        float rough = .8f + .35f * Mathf.Sin(glyph * 17 + stroke * 5 + part);
                        Vector2 side = new Vector2(-(q-p).y,(q-p).x).normalized * size.x * .028f * rough;
                        int i = vertices.Count;
                        vertices.Add(p-side); vertices.Add(p+side); vertices.Add(q+side); vertices.Add(q-side);
                        triangles.AddRange(new[] {i,i+1,i+2,i,i+2,i+3,i+2,i+1,i,i+3,i+2,i});
                    }
                }
            }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        }
        private void OnDestroy() { if (generated != null) Destroy(generated); }
    }
}
