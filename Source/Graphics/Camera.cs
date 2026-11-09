
namespace Celeste64;

public struct Camera()
{
	private IDrawableTarget target = null!;
	private Vec3 position =  Vec3.Zero;
	private Vec3 lookAt = new(0, 1, 0);
	private float nearPlane = 0.01f;
	private float farPlane = 100f;
	private float fovMultiplier = 1.0f;
	private Vec3 viewUp = Vec3.UnitZ;
	private Matrix? projectionOverride;

	private Vec3? normal;
	private Matrix? view;
	private Matrix? projection;
	private Matrix? viewProjection;
	private Quaternion? rotation;
	private Vec3? upwards;
	private Vec3? leftwards;
	private BoundingFrustum? frustum;

	public IDrawableTarget Target
	{
		readonly get => target ?? throw new Exception("Camera must assign a Target");
		set
		{
			if (target != value)
			{
				target = value;
				MakeDirty();
			}
		}
	}

	public Vec3 Position
	{
		readonly get => position;
		set
		{
			if (position != value)
			{
				position = value;
				MakeDirty();
			}
		}
	}

	public float FOVMultiplier
	{
		readonly get => fovMultiplier;
		set
		{
			if (fovMultiplier != value)
			{
				fovMultiplier = value;
				MakeDirty();
			}
		}
	}

	public Vec3 LookAt
	{
		readonly get => lookAt;
		set
		{
			if (lookAt != value)
			{
				lookAt = value;
				MakeDirty();
			}
		}
	}

	/// <summary>Up direction for the view, including any headset roll.</summary>
	public Vec3 ViewUp
	{
		readonly get => viewUp;
		set
		{
			if (viewUp != value)
			{
				viewUp = value;
				MakeDirty();
			}
		}
	}

	/// <summary>An optional per-eye projection; null uses the normal game projection.</summary>
	public Matrix? ProjectionOverride
	{
		readonly get => projectionOverride;
		set
		{
			projectionOverride = value;
			MakeDirty();
		}
	}

	public float NearPlane
	{
		readonly get => nearPlane;
		set
		{
			if (nearPlane != value)
			{
				nearPlane = value;
				MakeDirty();
			}
		}
	}

	public float FarPlane
	{
		readonly get => farPlane;
		set
		{
			if (farPlane != value)
			{
				farPlane = value;
				MakeDirty();
			}
		}
	}

	public Vec3 Normal
	{
		get
		{
			if (!normal.HasValue)
			{
				var delta = LookAt - Position;
				float lengthSquared = delta.LengthSquared();
				normal = float.IsFinite(lengthSquared) && lengthSquared > 0.000001f
					? delta / MathF.Sqrt(lengthSquared) : Vec3.UnitY;
			}
			return normal.Value;
		}
	}

	public Matrix View
	{
		get
		{
			if (!view.HasValue)
			{
				var forward = Normal;
				var preferredUp = ViewUp;
				float upLength = preferredUp.LengthSquared();
				if (!float.IsFinite(upLength) || upLength < 0.000001f)
					preferredUp = Vec3.UnitZ;
				var right = Vec3.Cross(forward, preferredUp);
				if (!float.IsFinite(right.LengthSquared()) || right.LengthSquared() < 0.000001f)
					right = Vec3.Cross(forward, MathF.Abs(forward.Z) < .9f ? Vec3.UnitZ : Vec3.UnitY);
				var up = Vec3.Normalize(Vec3.Cross(Vec3.Normalize(right), forward));
				view = Matrix.CreateLookAt(Position, Position + forward, up);
			}
			return view.Value;
		}
	}

	public Matrix Projection
	{
		get
		{
			if (!projection.HasValue)
			{
				if (projectionOverride.HasValue)
					projection = projectionOverride.Value;
				else
				{
					var width = target.WidthInPixels;
					var height = target.HeightInPixels;
					projection = Matrix.CreatePerspectiveFieldOfView(MathF.PI / 4.0f * fovMultiplier, width / (float)height, nearPlane, farPlane);
				}
			}
			return projection.Value;
		}
	}

	public Matrix ViewProjection
	{
		get
		{
			if (!viewProjection.HasValue)
				viewProjection = View * Projection;
			return viewProjection.Value;
		}
	}

	public Quaternion Rotation
	{
		get
		{
			if (!rotation.HasValue)
				rotation = Quaternion.CreateFromRotationMatrix(View);
			return rotation.Value;
		}
	}

	public BoundingFrustum Frustum
	{
		get
		{
			if (!frustum.HasValue)
				frustum = new BoundingFrustum(ViewProjection);
			return frustum.Value;
		}
	}

	public Vec3 Forward => Normal;

	public Vec3 Up
	{
		get
		{
			if (!upwards.HasValue)
				upwards = Vec3.Transform(Vec3.UnitY, Rotation.Conjugated());
			return upwards.Value;
		}
	}

	public Vec3 Left
	{
		get
		{
			if (!leftwards.HasValue)
				leftwards = Vec3.Transform(-Vec3.UnitX, Rotation.Conjugated());
			return leftwards.Value;
		}
	}

	private void MakeDirty()
	{
		normal = null;
		view = null;
		projection = null;
		viewProjection = null;
		rotation = null;
		upwards = null;
		leftwards = null;
		frustum = null;
	}
}
