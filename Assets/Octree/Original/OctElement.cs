using Unity.Mathematics;

namespace NativeOctree
{
    public struct OctElement<T> where T : unmanaged
	{
		public float3 pos;
		public T element;
	}
}
