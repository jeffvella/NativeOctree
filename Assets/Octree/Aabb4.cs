using System.Runtime.CompilerServices;
using Unity.Mathematics;
using Unity.Physics;

namespace NativeOctree
{
    public struct Aabb4
	{
		public float4 MinXs;
		public float4 MaxXs;
		public float4 MinYs;
		public float4 MaxYs;
		public float4 MinZs;
		public float4 MaxZs;

		public unsafe Aabb this[int i]
		{
			get => new Aabb
			{
				Min = new float3(MinXs[i], MinYs[i], MinZs[i]),
				Max = new float3(MaxXs[i], MaxYs[i], MaxZs[i])
			};
			set
			{
				MinXs[i] = value.Min.x;
				MinYs[i] = value.Min.y;
				MinZs[i] = value.Min.z;
				MaxXs[i] = value.Max.x;
				MaxYs[i] = value.Max.y;
				MaxZs[i] = value.Max.z;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool4 ContainedBy(Aabb aabb4)
		{
			return (aabb4.Min.x <= MinXs) & (aabb4.Max.x >= MaxXs) 
				& (aabb4.Min.y <= MinYs) & (aabb4.Max.y >= MaxYs) 
				& (aabb4.Min.z <= MinZs) & (aabb4.Max.z >= MaxZs);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool4 OverlappedBy(Aabb aabb)
		{
			return (aabb.Max.x >= MinXs) & (aabb.Min.x <= MaxXs) 
				& (aabb.Max.y >= MinYs) & (aabb.Min.y <= MaxYs) 
				& (aabb.Max.z >= MinZs) & (aabb.Min.z <= MaxZs);
		}
	}

}