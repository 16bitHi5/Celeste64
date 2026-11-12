namespace Celeste64;

/// <summary>A world-space ring marking a valid first-person ledge landing.</summary>
internal sealed class VrLedgeIndicator : IDisposable
{
	private readonly SimpleModel ring;

	public VrLedgeIndicator()
	{
		const int segments = 32;
		const float innerRadius = 2.0f, outerRadius = 2.45f;
		var vertices = new Vertex[segments * 2];
		var indices = new int[segments * 6];
		for (int i = 0; i < segments; i++)
		{
			float angle = MathF.Tau * i / segments;
			var direction = new Vec3(MathF.Cos(angle), MathF.Sin(angle), 0);
			vertices[i * 2] = new Vertex(direction * innerRadius, Vec2.Zero, Vec3.One, Vec3.UnitZ);
			vertices[i * 2 + 1] = new Vertex(direction * outerRadius, Vec2.Zero, Vec3.One, Vec3.UnitZ);
			int next = (i + 1) % segments;
			int index = i * 6;
			indices[index] = i * 2;
			indices[index + 1] = i * 2 + 1;
			indices[index + 2] = next * 2 + 1;
			indices[index + 3] = i * 2;
			indices[index + 4] = next * 2 + 1;
			indices[index + 5] = next * 2;
		}
		ring = new SimpleModel { CullMode = CullMode.None };
		ring.Materials.Add(new DefaultMaterial(Assets.Textures["white"]) { Color = new Color(0x64f5e6), Effects = .25f });
		ring.Mesh.SetVertices(vertices);
		ring.Mesh.SetIndices(indices);
		ring.Parts.Add(new SimpleModel.Part(0, 0, indices.Length));
	}

	public void Render(ref RenderState state, Vec3 position, Vec3 normal)
	{
		// Align with sloped floors and lift slightly to avoid depth flicker.
		normal = Vec3.Normalize(normal);
		var x = Vec3.Normalize(new Vec3(normal.Z, 0, -normal.X));
		var y = Vec3.Cross(normal, x);
		var center = position + normal * .12f;
		var ringState = state;
		ringState.ModelMatrix = new Matrix(x.X, x.Y, x.Z, 0, y.X, y.Y, y.Z, 0,
			normal.X, normal.Y, normal.Z, 0, center.X, center.Y, center.Z, 1);
		ringState.VerticalFogColor = new Color(0x64f5e6);
		ring.Render(ref ringState);
	}

	public void Dispose() => ring.Mesh.Dispose();
}
