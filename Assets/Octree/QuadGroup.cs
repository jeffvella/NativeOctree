using Unity.Mathematics;

namespace NativeOctree
{
    public unsafe struct QuadGroup
	{
		public Aabb4 Bounds;
		public int4 Offsets;
	}

}