using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

[System.Serializable] public class Sketch { public string identifier; public string filename; public string timestamp; }
[System.Serializable] public class SketchList { public Sketch[] sketches; }

[System.Serializable] public class FlockLimit { public GameObject prefab; public int maxInstances = -1; }

public class FlockManager : MonoBehaviour
{
    public static FlockManager FM;

    [Header("Setup")]
    public GameObject flockUnitPrefab;
    public GameObject[] flockUnitPrefabs;
    public int flockUnitCount = 10;
    public int maxFlockUnits = 20;
    public Vector3 swimLimits = new Vector3(5, 5, 5);

	[Header("Spawn & Despawn")]
    public Transform spawnPoint;
    public Transform despawnPoint;
    public float despawnDistance = 1.0f;

    [Header("Flock-Unit")]
    public float minSpeed = 0.5f;
    public float maxSpeed = 1f;
    public float rotationSpeed = 1f;
    public float neighbourDistance = 1.0f;

    [Header("API")]
    public float updateInterval = 5f;
    private List<string> loadedSketches = new List<string>();

    [HideInInspector]
    public List<GameObject> allFlockUnits = new List<GameObject>();
    
    private Dictionary<GameObject, List<GameObject>> flockUnitsByPrefab
        = new Dictionary<GameObject, List<GameObject>>();

    void Awake()
    {
        FM = this;
    }

    void Start()
    {
        if (flockUnitPrefabs.Length == 0)
        {
            Debug.LogError("Keine Prefabs in flockUnitPrefabs gesetzt!");
            return;
        }

        for (int i = 0; i < flockUnitCount; i++)
        {
            GameObject prefabToSpawn;

            float rand = Random.value;

            if (rand <= 0.9f)
            {
                prefabToSpawn = flockUnitPrefabs[0];
            }
            else
            {
                if (flockUnitPrefabs.Length > 1)
                {
                    int index = Random.Range(1, flockUnitPrefabs.Length);
                    prefabToSpawn = flockUnitPrefabs[index];
                }
                else
                {
                    prefabToSpawn = flockUnitPrefabs[0];
                }
            }
            
            if (i == 0)
            {
                prefabToSpawn = flockUnitPrefabs[1];
            }
            else
            {
                prefabToSpawn = flockUnitPrefabs[0];
            }

            SpawnFlockUnit(prefabToSpawn, null);
        }

        StartCoroutine(SketchUpdater());
    }
    
    void SpawnFlockUnit(GameObject prefab, string imageUrl, bool randomInBounds = false)
	{
        Vector3 position;
        bool needsToSwimIn = false;

        if (randomInBounds)
        {
            position = transform.position + new Vector3(
                Random.Range(-swimLimits.x, swimLimits.x),
                Random.Range(-swimLimits.y, swimLimits.y),
                Random.Range(-swimLimits.z, swimLimits.z));
        }
        else
        {
            position = spawnPoint ? spawnPoint.position : transform.position;
            needsToSwimIn = true;
        }

        GameObject flockUnit = Instantiate(prefab, position, Quaternion.Euler(0, Random.Range(0, 360), 0));
        allFlockUnits.Add(flockUnit);
        
        if (!flockUnitsByPrefab.ContainsKey(prefab))
            flockUnitsByPrefab[prefab] = new List<GameObject>();
        
        flockUnitsByPrefab[prefab].Add(flockUnit);
        
        FlockUnit flockUnitScript = flockUnit.GetComponent<FlockUnit>();
        if (flockUnitScript != null && needsToSwimIn)
        {
            flockUnitScript.StartSpawning();
        }
        
        if (string.IsNullOrEmpty(imageUrl))
            StartCoroutine(ApplyColorToFlockUnit(flockUnit));
        else
            StartCoroutine(ApplySketchToFlockUnit(flockUnit, imageUrl));

        EnforcePrefabLimit(prefab);
        
        if (allFlockUnits.Count > maxFlockUnits)
        {
            RemoveOldestFish();
        }
	}
    
    void EnforcePrefabLimit(GameObject prefab)
    {
        FlockUnit flockUnit = prefab.GetComponent<FlockUnit>();
        int maxInstances = (flockUnit == null) ? -1 : flockUnit.maxInstances;

        if (maxInstances < 0) return;

        List<GameObject> list;
        if (!flockUnitsByPrefab.TryGetValue(prefab, out list))
            return;

        while (list.Count > maxInstances)
        {
            GameObject oldestFlockUnit = list[0];
            list.RemoveAt(0);

            if (oldestFlockUnit == null) continue;

            allFlockUnits.Remove(oldestFlockUnit);

            FlockUnit flockUnitInstance = oldestFlockUnit.GetComponent<FlockUnit>();
            if (flockUnitInstance != null)
                flockUnitInstance.StartDespawning(despawnPoint.position);
            else
                Destroy(oldestFlockUnit);
        }
    }

	void RemoveOldestFish()
    {
        GameObject oldestFlockUnit = allFlockUnits[0];
        allFlockUnits.RemoveAt(0);
        
        RemoveFromPrefabLimitTracking(oldestFlockUnit);

        FlockUnit flockUnitScript = oldestFlockUnit.GetComponent<FlockUnit>();
        if (flockUnitScript != null)
        {
            flockUnitScript.StartDespawning(despawnPoint.position);
        }
        else
        {
            Destroy(oldestFlockUnit);
        }
    }
    
    void RemoveFromPrefabLimitTracking(GameObject obj)
    {
        foreach (var kvp in flockUnitsByPrefab)
        {
            if (kvp.Value.Remove(obj))
                break;
        }
    }

    IEnumerator SketchUpdater()
    {
        while (true)
        {
            yield return StartCoroutine(GetSketches());
            yield return new WaitForSeconds(updateInterval);
        }
    }

    IEnumerator GetSketches()
    {
        using (UnityWebRequest uwr = UnityWebRequest.Get("http://localhost:5000/api"))
        {
            yield return uwr.SendWebRequest();

            if (uwr.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("API Error: " + uwr.error);
            }
            else
            {
                string jsonString = uwr.downloadHandler.text;
                SketchList data = JsonUtility.FromJson<SketchList>(jsonString);

                foreach (Sketch sketch in data.sketches)
                {
                    if (loadedSketches.Contains(sketch.timestamp)) continue;

                    loadedSketches.Add(sketch.timestamp);
                    string imageUrl = "http://localhost:5000/api/" + sketch.filename;

                    string identifier = sketch.identifier;
                    int id = int.Parse(identifier[^1].ToString()) - 1;

                    if (flockUnitPrefabs[id] != null)
                    {
                        GameObject prefab = flockUnitPrefabs[id];
                        SpawnFlockUnit(prefab, imageUrl);    
                    }
                }
            }
        }
    }

    IEnumerator ApplySketchToFlockUnit(GameObject flockUnit, string url)
    {
        using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(url))
        {
            yield return uwr.SendWebRequest();
            if (uwr.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(uwr);
                ApplyTextureToAllRenderers(flockUnit, texture);
            }
        }
    }

    IEnumerator ApplyColorToFlockUnit(GameObject flockUnit)
    {
        yield return new WaitForEndOfFrame();

        Color colorA = Random.ColorHSV(0f, 1f, 0.7f, 1f, 0.8f, 1f);
        Color colorB = Random.ColorHSV(0f, 1f, 0.7f, 1f, 0.8f, 1f);

        Texture2D gradientTex = new Texture2D(1, 2);
        gradientTex.SetPixels(new Color[] { colorB, colorA });
        gradientTex.Apply();

        ApplyTextureToAllRenderers(flockUnit, gradientTex);
    }

    void ApplyTextureToAllRenderers(GameObject flockUnit, Texture texture)
    {
        Renderer[] renderers = flockUnit.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            Material material = renderer.material; 
            if (material.HasProperty("_BaseColorMap"))
                material.SetTexture("_BaseColorMap", texture);
            else if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, swimLimits * 2);
    }
}