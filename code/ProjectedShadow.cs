using Sandbox;
using System;

namespace Phyksar.ProjectedShadows;

public class ProjectedShadow : Entity
{
	public Material Material {
		get => ProjectionMaterial;
		set => ProjectionBox?.SetMaterialOverride(ProjectionMaterial = value);
	}
	public Vector2 Size { get; set; } = new Vector2(256.0f, 256.0f);
	public float MinDepth { get; set; } = -128.0f;
	public float MaxDepth { get; set; } = 128.0f;

	private SceneObject ProjectionBox;
	private Material ProjectionMaterial;
	private int ProjectionBoxHash;

	internal struct Vertex
	{
		public Vector3 Position;

		public Vertex(float x, float y, float z)
		{
			Position = new Vector3(x, y, z);
		}
	}

	public ProjectedShadow()
	{
		ProjectionBox = new SceneObject(Scene, CreateProjectionBox(Size, MinDepth, MaxDepth));
		ProjectionBoxHash = GenerateProjectionBoxHash(Size, MinDepth, MaxDepth);
		ProjectionBox.Flags.CastShadows = false;
		ProjectionBox.Flags.IsOpaque = false;
		ProjectionBox.Flags.IsTranslucent = true;
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		if (ProjectionBox != null) {
			ProjectionBox.Delete();
			ProjectionBox = null;
		}
	}

	private Model CreateProjectionBox(in Vector2 size, float minDepth, float maxDepth)
	{
		var halfSize = 0.5f * size;
		var vertexLayout = new VertexAttribute[] {
			new VertexAttribute(VertexAttributeType.Position, VertexAttributeFormat.Float32)
		};
		Span<Vertex> vertices = new Vertex[] {
			new Vertex(-halfSize.x, -halfSize.y, minDepth),
			new Vertex(-halfSize.x, halfSize.y, minDepth),
			new Vertex(halfSize.x, -halfSize.y, minDepth),
			new Vertex(halfSize.x, halfSize.y, minDepth),
			new Vertex(-halfSize.x, -halfSize.y, maxDepth),
			new Vertex(-halfSize.x, halfSize.y, maxDepth),
			new Vertex(halfSize.x, -halfSize.y, maxDepth),
			new Vertex(halfSize.x, halfSize.y, maxDepth)
		};
		Span<int> indices = new int[] {
			0, 3, 1,  0, 2, 3,
			0, 1, 4,  1, 5, 4,
			1, 3, 5,  3, 7, 5,
			0, 4, 2,  2, 4, 6,
			3, 2, 6,  3, 6, 7,
			4, 5, 7,  4, 7, 6
		};
		var projectionBox = new Mesh();
		projectionBox.CreateVertexBuffer(vertices.Length, vertexLayout, vertices);
		projectionBox.CreateIndexBuffer(indices.Length, indices);
		var builder = new ModelBuilder();
		builder.AddMesh(projectionBox);
		return builder.Create();
	}

	private int GenerateProjectionBoxHash(in Vector2 size, float minDepth, float maxDepth)
	{
		return HashCode.Combine(size, minDepth, maxDepth);
	}

	[GameEvent.PreRender]
	private void UpdateProjectionBox()
	{
		var hash = GenerateProjectionBoxHash(Size, MinDepth, MaxDepth);
		if (hash != ProjectionBoxHash) {
			ProjectionBox.Model = CreateProjectionBox(Size, MinDepth, MaxDepth);
			ProjectionBoxHash = hash;
		}
		ProjectionBox.Transform = Transform;
		var depthRange = MaxDepth - MinDepth;
		var invTransformMatrix = Matrix.CreateTranslation(-Position + Rotation.Down * MinDepth)
			* Matrix.CreateRotation(Rotation.Inverse)
			* Matrix.CreateScale(new Vector3(1.0f / Size.x, 1.0f / Size.y, 1.0f / depthRange));
		ProjectionBox.Attributes.Set("InvProjectionBoxMatrix", invTransformMatrix);
	}
}
