using System.Numerics;

namespace Celeste64;

/// <summary>A small torus in controller grip space, just behind the hand mesh.</summary>
public static class VrBraceletGeometry
{
	public readonly record struct MeshData(Vertex[] Vertices, int[] Indices);

	public static MeshData Create()
	{
		const int around = 12, tube = 6;
		const float radius = .50f, thickness = .15f, wristY = .42f;
		var vertices = new Vertex[around * tube];
		var indices = new int[around * tube * 6];
		for (int i = 0; i < around; i++)
		{
			float a = MathF.Tau * i / around;
			float ca = MathF.Cos(a), sa = MathF.Sin(a);
			for (int j = 0; j < tube; j++)
			{
				float b = MathF.Tau * j / tube;
				float cb = MathF.Cos(b), sb = MathF.Sin(b);
				var normal = new Vector3(ca * cb, sb, sa * cb);
				var position = new Vector3(ca * (radius + thickness * cb), wristY + thickness * sb, sa * (radius + thickness * cb));
				vertices[i * tube + j] = new Vertex(position, Vector2.Zero, Vector3.One, normal);
				int nextI = (i + 1) % around, nextJ = (j + 1) % tube;
				int k = (i * tube + j) * 6;
				indices[k] = i * tube + j;
				indices[k + 1] = nextI * tube + j;
				indices[k + 2] = nextI * tube + nextJ;
				indices[k + 3] = i * tube + j;
				indices[k + 4] = nextI * tube + nextJ;
				indices[k + 5] = i * tube + nextJ;
			}
		}
		return new(vertices, indices);
	}
}
