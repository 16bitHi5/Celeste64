using SharpGLTF.Schema2;

namespace Celeste64;

/// <summary>Extracts left and right hand triangles from the original player model.</summary>
public static class VrHandGeometry
{
	public readonly record struct Hand(Vertex[] Vertices, int[] Indices);
	public readonly record struct Pair(Hand Left, Hand Right, int MaterialIndex);

	public static Pair Extract(ModelRoot root)
	{
		var mesh = root.LogicalNodes.First(node => node.Name == "Hands").Mesh
			?? throw new InvalidOperationException("The player model has no Hands mesh.");
		var primitive = mesh.Primitives[0];
		var positions = primitive.GetVertexAccessor("POSITION").AsVector3Array();
		var uvs = primitive.GetVertexAccessor("TEXCOORD_0").AsVector2Array();
		var normals = primitive.GetVertexAccessor("NORMAL").AsVector3Array();
		var indices = primitive.GetIndices().Select(index => (int)index).ToArray();
		return new(Create(true), Create(false), primitive.Material?.LogicalIndex ?? 0);

		Hand Create(bool isLeft)
		{
			bool Belongs(Vec3 position) => isLeft ? position.X > 0 : position.X < 0;
			var selected = positions.Where(Belongs).ToArray();
			if (selected.Length == 0) throw new InvalidOperationException("Could not separate the original hand mesh.");
			var center = (selected.Aggregate(Vec3.Min) + selected.Aggregate(Vec3.Max)) / 2;
			var vertices = new Vertex[positions.Count];
			for (int i = 0; i < positions.Count; i++)
				vertices[i] = new Vertex((positions[i] - center) * 3, uvs[i], Vec3.One, normals[i]);
			var handIndices = new List<int>();
			for (int i = 0; i + 2 < indices.Length; i += 3)
				if (Belongs(positions[indices[i]]) && Belongs(positions[indices[i + 1]]) && Belongs(positions[indices[i + 2]]))
					handIndices.AddRange([indices[i], indices[i + 1], indices[i + 2]]);
			if (handIndices.Count == 0) throw new InvalidOperationException("The original hand mesh has no triangles.");
			return new(vertices, [..handIndices]);
		}
	}
}
