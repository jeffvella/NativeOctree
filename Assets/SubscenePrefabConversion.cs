using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public struct CubeTag : IComponentData { }

public class SubscenePrefabConversion : MonoBehaviour, IConvertGameObjectToEntity
{
    public bool StripTransform;
    public bool LinkChildren;
    public bool AddPrefabComponent;
    public bool AddUnprocessedTag;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponent<CubeTag>(entity);
        dstManager.AddComponent<Prefab>(entity);
    }
}

