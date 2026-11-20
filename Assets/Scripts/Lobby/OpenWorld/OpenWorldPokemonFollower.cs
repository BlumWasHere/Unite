using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public sealed class OpenWorldPokemonFollower : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float followDistance = 3f;
    [SerializeField, Min(0.1f)] private float stopDistance = 1.5f;
    [SerializeField, Min(0.05f)] private float updateInterval = 0.2f;
    [SerializeField, Min(0.1f)] private float sampleRadius = 3f;
    [SerializeField, Min(1f)] private float catchUpDistance = 25f;
    [SerializeField, Min(0.1f)] private float maxSpeed = 8f;
    [SerializeField, Min(0f)] private float catchUpGain = 2f;
    [SerializeField, Min(0.1f)] private float speedChangeRate = 12f;
    [SerializeField, Min(0.05f)] private float minMoveSpeed = 1.2f;

    private static readonly int Walking = Animator.StringToHash("Walking");
    private NavMeshAgent agent;
    private Transform target;
    private GameObject visual;
    private Animator[] animators;
    private float nextUpdate;
    private Vector3 lastTargetPosition;
    private float targetSpeed;
    private bool wantsToMove;

    public Transform Target => target;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.enabled = false;
    }

    public void Initialize(Transform trainer, GameObject model, float scale = 1f)
    {
        target = trainer;
        lastTargetPosition = trainer.position;
        targetSpeed = 0f;
        agent.stoppingDistance = stopDistance;
        agent.acceleration = 100f;
        visual = Instantiate(model, transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale *= scale;
        foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        var compatibleAnimators = new System.Collections.Generic.List<Animator>();
        foreach (var animator in visual.GetComponentsInChildren<Animator>(true))
        {
            animator.applyRootMotion = false;
            foreach (var parameter in animator.parameters)
                if (parameter.nameHash == Walking && parameter.type == AnimatorControllerParameterType.Bool)
                {
                    compatibleAnimators.Add(animator);
                    break;
                }
        }
        animators = compatibleAnimators.ToArray();
        if (!TryGetComponent<FakeMeshShadow>(out var fakeShadow))
            fakeShadow = gameObject.AddComponent<FakeMeshShadow>();
        fakeShadow.SetSource(visual.transform);
        visual.SetActive(false);
        TryPlaceNearTrainer();
    }

    private bool Sample(Vector3 position, out NavMeshHit hit)
    {
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        return NavMesh.SamplePosition(position, out hit, sampleRadius, filter);
    }

    private bool TryPlaceNearTrainer()
    {
        if (target == null || !Sample(target.position - target.forward * stopDistance, out var hit))
        {
            agent.enabled = false;
            visual.SetActive(false);
            return false;
        }
        agent.enabled = false;
        transform.SetPositionAndRotation(hit.position, target.rotation);
        agent.enabled = true;
        bool placed = agent.isOnNavMesh && agent.Warp(hit.position);
        visual.SetActive(placed);
        if (!placed) agent.enabled = false;
        return placed;
    }

    private void Update()
    {
        if (target == null || visual == null) return;
        if (Time.time >= nextUpdate)
        {
            nextUpdate = Time.time + updateInterval;
            if (!agent.enabled || !agent.isOnNavMesh ||
                (transform.position - target.position).sqrMagnitude > catchUpDistance * catchUpDistance)
            {
                SetWalking(false);
                TryPlaceNearTrainer();
                return;
            }

            if (wantsToMove && Sample(target.position, out var hit))
                agent.SetDestination(hit.position);
        }
        UpdateSpeed();
        if (wantsToMove && !agent.hasPath && !agent.pathPending && Sample(target.position, out var start))
            agent.SetDestination(start.position);
        SetWalking(agent.enabled && agent.isOnNavMesh && !agent.isStopped && agent.velocity.sqrMagnitude > 0.01f);
    }
    private void UpdateSpeed()
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;
        float dt = Time.deltaTime;
        if (dt > 0f)
        {
            Vector3 delta = target.position - lastTargetPosition;
            delta.y = 0f;
            targetSpeed = Mathf.Lerp(targetSpeed, delta.magnitude / dt, 1f - Mathf.Exp(-8f * dt));
        }
        lastTargetPosition = target.position;

        float distance = Vector3.Distance(transform.position, target.position);
        float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(stopDistance, followDistance, distance));
        float desired = Mathf.Clamp(targetSpeed * t + catchUpGain * Mathf.Max(0f, distance - stopDistance), 0f, maxSpeed);
        wantsToMove = desired >= minMoveSpeed;
        if (!wantsToMove)
        {
            agent.speed = minMoveSpeed;
            if (!agent.isOnOffMeshLink) agent.ResetPath();
            return;
        }
        agent.speed = Mathf.MoveTowards(Mathf.Max(agent.speed, minMoveSpeed), desired, speedChangeRate * dt);
    }

    private void SetWalking(bool walking)
    {
        if (animators == null) return;
        foreach (var animator in animators)
            if (animator != null && animator.isActiveAndEnabled) animator.SetBool(Walking, walking);
    }

    private void OnDisable()
    {
        SetWalking(false);
        if (agent != null) agent.enabled = false;
    }
}
