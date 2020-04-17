using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using static Unity.Mathematics.math;

namespace NativeOctree
{
	public unsafe partial struct NativeOctree<T> where T : unmanaged
	{
		public void RangeQuery(AABB bounds, NativeList<OctElement<T>> results)
		{
			var targetBounds = new Aabb { Min = bounds.Min, Max = bounds.Max };
			var outputResults = (UnsafeList*)NativeListUnsafeUtility.GetInternalListDataPtrUnchecked(ref results);

			if (Data->TotalElements > outputResults->Capacity)
			{
				// todo: find out why its possible to return more elements than exists
				// should be able to use 'TotalElements' for capacity here. 10/100 work, 
				// but 1000 yields 1012-1018 elements...
				outputResults->Resize<OctElement<T>>(Data->TotalElements * 2); 
			}

			var resultsPtr = (byte*)outputResults->Ptr;
			var nodesPtr = (byte*)Data->Nodes->Ptr;
			var elementPtr = (byte*)Data->Elements->Ptr;
			var quadPtr = (byte*)Data->Quads->Ptr;

			var quadCount = Data->Quads->Length;
			var maxLeaves = quadCount * 16 * Data->TotalDepth;

			int resultCount = 0;
			int containCount = 0;
			int overlapCount = 0;

			// Work around c# enforcing use of Span<T>.
			var useStack = maxLeaves <= 256;
			var size = useStack ? sizeof(int) * maxLeaves : 1;
			var ptr1 = stackalloc int[size];
			var ptr2 = stackalloc int[size];

			size = sizeof(int) * maxLeaves;
			var containOffsets = useStack ? ptr1 : (int*)UnsafeUtility.Malloc(size, sizeof(int), Allocator.Temp);
			var overlapOffsets = useStack ? ptr2 : (int*)UnsafeUtility.Malloc(size, sizeof(int), Allocator.Temp);

			for (int i = 0; i < quadCount; i++)
			{
				QuadGroup quad = *(QuadGroup*)(quadPtr + i * sizeof(QuadGroup));

				bool4 isContainedA = quad.Bounds.ContainedBy(targetBounds);
				bool4 isOverlapA = quad.Bounds.OverlappedBy(targetBounds);
				bool4 skipMaskA = !isContainedA & !isOverlapA;
				bool4 containedLeafMaskA = !skipMaskA & isContainedA;
				bool4 intersectedMaskA = !containedLeafMaskA & isOverlapA;

				containCount = compress(containOffsets, containCount, quad.Offsets, containedLeafMaskA);
				overlapCount = compress(overlapOffsets, overlapCount, quad.Offsets, intersectedMaskA);
			}

			for (int i = 0; i < containCount; i++)
			{
				ref var node = ref UnsafeUtilityEx.ArrayElementAsRef<OctNode>(nodesPtr, containOffsets[i]);
				var src = elementPtr + node.firstChildIndex * UnsafeUtility.SizeOf<OctElement<T>>();
				var dst = resultsPtr + resultCount * UnsafeUtility.SizeOf<OctElement<T>>();
				UnsafeUtility.MemCpy(dst, src, node.count * UnsafeUtility.SizeOf<OctElement<T>>());
				resultCount += node.count;
			}

			for (int i = 0; i < overlapCount; i++)
			{
				ref var node = ref UnsafeUtilityEx.ArrayElementAsRef<OctNode>(nodesPtr, overlapOffsets[i]);
				for (int k = 0; k < node.count; k++)
				{
					var element = UnsafeUtility.ReadArrayElement<OctElement<T>>(elementPtr, node.firstChildIndex + k);
					if (targetBounds.Contains(element.pos))
					{
						UnsafeUtility.WriteArrayElement(resultsPtr, resultCount++, element);
					}
				}
			}

			outputResults->Length = resultCount;
		}

	}

}