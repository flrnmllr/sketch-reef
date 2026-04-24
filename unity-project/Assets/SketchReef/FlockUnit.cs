using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class FlockUnit : MonoBehaviour
{
    private Vector3 velocity;
    private float speed;

    private enum FishState { Spawning, Normal, Despawning }
    private FishState state = FishState.Normal;

    private Vector3 targetPoint;
    private Vector3 randomDirection;
    private float changeDirectionTimer;
    
    private float obstacleAvoidanceRange = 1f;
    private float avoidanceForce = 5f;
    
    public WaterSurface waterSurface = null;

    private WaterSearchParameters waterSearchParameters = new WaterSearchParameters();
    private WaterSearchResult waterSearchResult = new WaterSearchResult();
    
    [Header("Flock-Unit")]
    public float minSpeed = 0.5f;
    public float maxSpeed = 1f;
    public float swimDepth = 2.5f;
    public float waterForce = 5f;
    public bool lockPitchAndRoll = true;
    public int maxInstances = -1;

    void Start()
    {
        speed = Random.Range(minSpeed, maxSpeed);
        velocity = transform.forward;
        PickNewRandomDirection();
        
        if (waterSurface == null)
            waterSurface = FindFirstObjectByType<WaterSurface>();
    }

    void Update()
    {
        switch (state)
        {
            case FishState.Spawning:
                HandleSpawning();
                break;
            case FishState.Despawning:
                HandleDespawning();
                break;
            case FishState.Normal:
                HandleFlockingLogic();
                break;
        }

        ApplyWaterConstraint();
        Move();
    }

    void HandleFlockingLogic()
    {
        changeDirectionTimer -= Time.deltaTime;
        if (changeDirectionTimer <= 0f) PickNewRandomDirection();

        Vector3 flockingDirection = CalculateFlocking();

        Vector3 avoidanceDirection = CalculateObstacleAvoidance();

        Vector3 boundingDirection = CalculateBoundsSteering();

        Vector3 finalDirection = flockingDirection;
        
        if (avoidanceDirection != Vector3.zero)
        {
            finalDirection = Vector3.Lerp(finalDirection, avoidanceDirection, 0.8f);
        }

        if (boundingDirection != Vector3.zero)
        {
            finalDirection = Vector3.Lerp(finalDirection, boundingDirection, 0.9f);
        }

        if (finalDirection != Vector3.zero)
        {
            velocity = Vector3.Slerp(velocity, finalDirection.normalized, Time.deltaTime * FlockManager.FM.rotationSpeed);
        }
    }

    Vector3 CalculateFlocking()
    {
        Vector3 alignment = Vector3.zero;
        Vector3 cohesion = Vector3.zero;
        Vector3 separation = Vector3.zero;
        int count = 0;

        foreach (GameObject other in FlockManager.FM.allFlockUnits)
        {
            if (other == null || other == gameObject) continue;

            float dist = Vector3.Distance(transform.position, other.transform.position);
            if (dist < 2.0f)
            {
                alignment += other.transform.forward;
                cohesion += other.transform.position;
                if (dist < 1.2f)
                    separation += (transform.position - other.transform.position) / dist;
                count++;
            }
        }

        if (count > 0)
        {
            alignment = (alignment / count).normalized;
            cohesion = ((cohesion / count) - transform.position).normalized;
            separation = (separation / count).normalized;
        }

        return (alignment * 1.0f) + (cohesion * 0.6f) + (separation * 1.5f) + (randomDirection * 0.2f);
    }

    Vector3 CalculateObstacleAvoidance()
    {
        RaycastHit hit;
        
        if (Physics.SphereCast(transform.position, 0.5f, transform.forward, out hit, obstacleAvoidanceRange))
        {
            return Vector3.Reflect(transform.forward, hit.normal);
        }
        return Vector3.zero;
    }

    Vector3 CalculateBoundsSteering()
    {
        Vector3 offset = transform.position - FlockManager.FM.transform.position;
        Vector3 limits = FlockManager.FM.swimLimits;
        Vector3 steerAway = Vector3.zero;

        if (Mathf.Abs(offset.x) > limits.x * 0.9f) steerAway.x = -offset.x;
        if (Mathf.Abs(offset.y) > limits.y * 0.9f) steerAway.y = -offset.y;
        if (Mathf.Abs(offset.z) > limits.z * 0.9f) steerAway.z = -offset.z;

        return steerAway.normalized;
    }

    void Move()
    {
        if (velocity == Vector3.zero) velocity = transform.forward;

        if (lockPitchAndRoll)
        {
            // Nur horizontale Richtung für Y-Rotation
            Vector3 flatVelocity = new Vector3(velocity.x, 0f, velocity.z);
            if (flatVelocity.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(flatVelocity);

                // Pitch leicht nach oben/unten aus velocity.y ableiten
                float pitchAngle = Mathf.Clamp(velocity.y * 30f, -20f, 20f); // 30 = Verstärkung
                targetRotation *= Quaternion.Euler(pitchAngle, 0f, 0f);

                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * FlockManager.FM.rotationSpeed);
            }
        }
        else
        {
            // Volle Rotation wie bisher
            Quaternion targetRotation = Quaternion.LookRotation(velocity);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * FlockManager.FM.rotationSpeed);
        }

        // Bewegung inkl. vertikalem Anteil
        transform.position += velocity * speed * Time.deltaTime;
    }

    void PickNewRandomDirection()
    {
        randomDirection = new Vector3(Random.Range(-1f, 1f), Random.Range(-0.4f, 0.4f), Random.Range(-1f, 1f)).normalized;
        changeDirectionTimer = Random.Range(2f, 6f);
    }
    
    void ApplyWaterConstraint()
    {
        if (waterSurface == null) return;

        waterSearchParameters.startPositionWS = waterSearchResult.candidateLocationWS;
        waterSearchParameters.targetPositionWS = transform.position;
        waterSearchParameters.error = 0.01f;
        waterSearchParameters.maxIterations = 8;

        if (waterSurface.ProjectPointOnWaterSurface(waterSearchParameters, out waterSearchResult))
        {
            float waterY = waterSearchResult.projectedPositionWS.y;
            float targetY = waterY - swimDepth;

            float differenceY = targetY - transform.position.y;
            
            velocity += Vector3.up * differenceY * waterForce * Time.deltaTime;
        }
    }

    void HandleSpawning()
    {
        Vector3 directionToCenter = (FlockManager.FM.transform.position - transform.position).normalized;
        velocity = Vector3.Slerp(velocity, directionToCenter, Time.deltaTime * 2f);
        
        Vector3 offset = transform.position - FlockManager.FM.transform.position;
        Vector3 limits = FlockManager.FM.swimLimits;
        if (Mathf.Abs(offset.x) < limits.x * 0.8f && Mathf.Abs(offset.y) < limits.y * 0.8f && Mathf.Abs(offset.z) < limits.z * 0.8f)
            state = FishState.Normal;
    }

    void HandleDespawning()
    {
        Vector3 direction = (targetPoint - transform.position).normalized;
        velocity = Vector3.Slerp(velocity, direction, Time.deltaTime * 3f);
        if (Vector3.Distance(transform.position, targetPoint) < 1f) Destroy(gameObject);
    }

    public void StartSpawning() => state = FishState.Spawning;
    public void StartDespawning(Vector3 target) { state = FishState.Despawning; targetPoint = target; }
}