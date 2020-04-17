using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Physics;

namespace NativeOctree
{
    public unsafe struct Aabb4x2
	{
		public Aabb4 A;
		public Aabb4 B;

		public ref Aabb4 this[int i]
			=> ref UnsafeUtilityEx.ArrayElementAsRef<Aabb4>(UnsafeUtility.AddressOf(ref this), i);

		/// <summary>
		/// Splits a bounds into eight equal smaller bounds, grouped for SIMD.
		/// </summary>
		/// <param name="parent">the parent bounds to be split</param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Aabb4x2 CreateOctreeBounds(Aabb parent)
		{
			// Note: Physics Aabb.Extents is the edge length NOT the radius of circumscribed sphere.
			return CreateOctreeBounds(parent.Center, parent.Extents * 0.5f);
		}

		/// <summary>
		/// Splits a bounds into eight equal smaller bounds, grouped for SIMD.
		/// </summary>
		/// <param name="parent">the parent bounds to be split</param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Aabb4x2 CreateOctreeBounds(AABB parent)
		{
			return CreateOctreeBounds(parent.Center, parent.Extents);
		}

		/// <summary>
		/// Splits a bounds into eight equal smaller bounds, grouped for SIMD.
		/// </summary>
		/// <param name="center">the middle</param>
		/// <param name="extents">radius of circumscribed sphere</param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Aabb4x2 CreateOctreeBounds(float3 center, float3 extents)
		{
			Aabb4x2 result = default;
			float3 min = center - extents;
			result.A.MinXs = default;
			result.A.MinXs.xz = min.x;
			result.A.MinXs.yw = center.x;
			result.A.MinYs = default;
			result.A.MinYs.xy = center.y;
			result.A.MinYs.zw = min.y;
			result.A.MinZs = min.z;
			result.A.MaxXs = result.A.MinXs + extents.x;
			result.A.MaxYs = result.A.MinYs + extents.y;
			result.A.MaxZs = result.A.MinZs + extents.z;
			result.B = result.A;
			result.B.MinZs += extents.z;
			result.B.MaxZs += extents.z;
			return result;
		}
	}

}