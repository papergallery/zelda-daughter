using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZeldaDaughter.World
{
    [Serializable]
    public class RegionConfigData
    {
        public string regionId;
        public string regionName;
        public BoundsData bounds;
        public int seed = 42;
        public List<PlacedObjectData> objects = new();
        public List<DecorationZoneData> decorationZones = new();
        public List<SpawnZoneData> spawnZones = new();
        public List<PathData> paths = new();
        public List<WaterAreaData> waterAreas = new();
    }

    [Serializable]
    public class PlacedObjectData
    {
        public string id;
        public string prefab;
        public Vec3Data position;
        public Vec3Data rotation;
        public Vec3Data scale = new() { x = 1, y = 1, z = 1 };
        public List<string> tags = new();
    }

    [Serializable]
    public class DecorationZoneData
    {
        public string zoneType;
        public Vec2Data center;
        public float radius;
        public float density;
        public List<string> prefabs = new();
        public List<float> prefabWeights = new();
        public float minScale = 0.8f;
        public float maxScale = 1.2f;
        public bool randomRotationY = true;
    }

    [Serializable]
    public class SpawnZoneData
    {
        public string id;
        public string enemyType;
        public Vec2Data center;
        public float radius;
        public int maxCount;
        public float respawnTimeSec = 120f;
    }

    [Serializable]
    public class PathData
    {
        public string id;
        public string pathType;
        public float width = 2f;
        public List<Vec3Data> waypoints = new();
    }

    [Serializable]
    public class WaterAreaData
    {
        public string id;
        public string waterType;
        public Vec2Data center;
        public float radius;
        public List<Vec2Data> polygon = new();
        public float depth = 1.5f;
    }

    [Serializable]
    public class Vec2Data
    {
        public float x;
        public float z;
        public Vector3 ToVector3(float y = 0f) => new(x, y, z);
    }

    [Serializable]
    public class Vec3Data
    {
        public float x;
        public float y;
        public float z;
        public Vector3 ToVector3() => new(x, y, z);
        public Quaternion ToQuaternion() => Quaternion.Euler(x, y, z);
    }

    [Serializable]
    public class BoundsData
    {
        public float minX = -100f;
        public float minZ = -100f;
        public float maxX = 100f;
        public float maxZ = 100f;
    }
}
