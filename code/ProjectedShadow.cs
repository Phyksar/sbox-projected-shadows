using Sandbox;
using System;

namespace Phyksar.ProjectedShadows;

/// <summary>
/// Creates a shadow texture which is projected towards the up vector of this entity.
/// </summary>
public class ProjectedShadow : Entity
{
	/// <summary>
	/// The side dimentions of the projection.
	/// </summary>
	public Vector2 Size { get; set; } = new Vector2(256.0f, 256.0f);

	/// <summary>
	/// The minimum depth of projection and the distance from the entity position along the up vector, can be negative.
	/// </summary>
	public float MinDepth { get; set; } = -128.0f;

	/// <summary>
	/// The maxmimum depth of projection and the distance from the entity position along the up vector, can be negative.
	/// </summary>
	public float MaxDepth { get; set; } = 128.0f;

	/// <summary>
	/// The material used for projection.
	/// </summary>
	public Material Material {
		get => ProjectionMaterial;
		set => ProjectionBox?.SetMaterialOverride(ProjectionMaterial = value);
	}

	private SceneObject ProjectionBox;
	private Material ProjectionMaterial;

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
		ProjectionBox = new SceneObject(Scene, CreateProjectionBox(Vector2.One, 0.0f, 1.0f));
		ProjectionBox.Flags.CastShadows = false;
		ProjectionBox.Flags.IsOpaque = false;
		ProjectionBox.Flags.IsTranslucent = true;
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		ProjectionBox?.Delete();
		ProjectionBox = null;
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
		var depthRange = MaxDepth - MinDepth;
		ProjectionBox.Transform = Transform;
		ProjectionBox.Attributes.Set(
			"ProjectionTransformMatrix",
			Matrix.CreateScale(new Vector3(Size.x, Size.y, depthRange))
				* Matrix.CreateTranslation(new Vector3(0.0f, 0.0f, MinDepth))
		);
		ProjectionBox.Attributes.Set(
			"InvertTransformMatrix",
			Matrix.CreateTranslation(-Position + Rotation.Down * MinDepth)
				* Matrix.CreateRotation(Rotation.Inverse)
				* Matrix.CreateScale(new Vector3(1.0f / Size.x, 1.0f / Size.y, 1.0f / depthRange))
		);
	}
}
