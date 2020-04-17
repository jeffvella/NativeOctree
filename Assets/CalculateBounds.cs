using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Unity.Physics;
using System;

[ExecuteInEditMode]
public class CalculateBounds : MonoBehaviour
{
    public Transform Origin;
    public int Index;

    public AABB BoundsAABB;
    public Aabb BoundsAabb2;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        BoundsAABB = GetChildBounds(new AABB { Center = Origin.position, Extents = 1 }, Index);
        BoundsAabb2 = new Aabb { Min = BoundsAABB.Min, Max = BoundsAABB.Max };
        transform.position = BoundsAABB.Center;
    }

    static AABB GetChildBounds(AABB parentBounds, int childZIndex)
    {
        var half = parentBounds.Extents.x * .5f;
        switch (childZIndex)
        {
            case 0: return new AABB { Center = new float3(parentBounds.Center.x - half, parentBounds.Center.y + half, parentBounds.Center.z - half), Extents = half };
            case 1: return new AABB { Center = new float3(parentBounds.Center.x + half, parentBounds.Center.y + half, parentBounds.Center.z - half), Extents = half };
            case 2: return new AABB { Center = new float3(parentBounds.Center.x - half, parentBounds.Center.y - half, parentBounds.Center.z - half), Extents = half };
            case 3: return new AABB { Center = new float3(parentBounds.Center.x + half, parentBounds.Center.y - half, parentBounds.Center.z - half), Extents = half };
            case 4: return new AABB { Center = new float3(parentBounds.Center.x - half, parentBounds.Center.y + half, parentBounds.Center.z + half), Extents = half };
            case 5: return new AABB { Center = new float3(parentBounds.Center.x + half, parentBounds.Center.y + half, parentBounds.Center.z + half), Extents = half };
            case 6: return new AABB { Center = new float3(parentBounds.Center.x - half, parentBounds.Center.y - half, parentBounds.Center.z + half), Extents = half };
            case 7: return new AABB { Center = new float3(parentBounds.Center.x + half, parentBounds.Center.y - half, parentBounds.Center.z + half), Extents = half };
            default: return default;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(BoundsAabb2.Min, 0.1f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(BoundsAabb2.Max, 0.1f);
    }
}
