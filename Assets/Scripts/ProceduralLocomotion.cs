using UnityEngine;

public class ProceduralLocomotion : MonoBehaviour
{
    [System.Serializable]
    public class Foot
    {
        public Transform target;

        // Local to Player
        public Vector3 probeOffset;

        public Vector3 placementOffset;

        [HideInInspector] public Vector3 plantedPosition;
        [HideInInspector] public Vector3 desiredPosition;

        [HideInInspector] public Vector3 stepStart;
        [HideInInspector] public Vector3 stepEnd;

        [HideInInspector] public Quaternion stepStartRotation;
        [HideInInspector] public Quaternion stepEndRotation;

        [HideInInspector] public float stepProgress;
        [HideInInspector] public bool isStepping;
    }


    [Header("Feet")]
    public Foot leftFoot;
    public Foot rightFoot;


    [Header("Ground Detection")]
    public LayerMask layerMask;
    public float rayDistance = 2f;
    public float footProbeRadius = 0.15f;


    [Header("Step Settings")]
    public float minimumStepDistance = 0.5f;
    public float stepOvershoot = 0.2f;
    public float stepDuration = 0.25f;
    public float stepHeight = 0.3f;


    [Header("Movement Prediction")]
    public float velocitySharpness = 10f;
    public float speedForFullOvershoot = 2f;
    public float movementDeadzone = 0.02f;


    [Header("Debug")]
    public bool drawProbes = true;


    private Vector3 previousRootPosition;
    private Vector3 smoothedVelocity;


    void Start()
    {
        leftFoot.plantedPosition = leftFoot.target.position;
        rightFoot.plantedPosition = rightFoot.target.position;

        previousRootPosition = transform.position;
    }


    void Update()
    {
        // ============================================================
        // PLAYER VELOCITY
        // ============================================================

        Vector3 rootDelta =
            transform.position - previousRootPosition;

        Vector3 rawVelocity = Vector3.zero;

        if (Time.deltaTime > 0f)
        {
            rawVelocity =
                rootDelta / Time.deltaTime;
        }

        rawVelocity.y = 0f;


        float velocityBlend =
            1f - Mathf.Exp(
                -velocitySharpness * Time.deltaTime
            );


        smoothedVelocity =
            Vector3.Lerp(
                smoothedVelocity,
                rawVelocity,
                velocityBlend
            );


        float speed =
            smoothedVelocity.magnitude;


        Vector3 movementDirection =
            Vector3.zero;


        if (speed > movementDeadzone)
        {
            movementDirection =
                smoothedVelocity / speed;
        }


        // ============================================================
        // UPDATE BOTH FEET
        // ============================================================

        UpdateFoot(
            leftFoot,
            rightFoot,
            movementDirection,
            speed
        );

        UpdateFoot(
            rightFoot,
            leftFoot,
            movementDirection,
            speed
        );


        previousRootPosition =
            transform.position;
    }


    void UpdateFoot(
        Foot foot,
        Foot otherFoot,
        Vector3 movementDirection,
        float speed)
    {
        // ============================================================
        // DESIRED FOOT POSITION
        // ============================================================

        Vector3 probeOrigin =
            transform.TransformPoint(
                foot.probeOffset
            );


        if (Physics.Raycast(
            probeOrigin,
            Vector3.down,
            out RaycastHit groundHit,
            rayDistance,
            layerMask))
        {
            foot.desiredPosition =
                groundHit.point +
                transform.TransformVector(
                    foot.placementOffset
                );


            // ========================================================
            // STEP TRIGGER
            // ========================================================

            Vector3 footError =
                foot.desiredPosition -
                foot.plantedPosition;


            Vector3 horizontalFootError =
                Vector3.ProjectOnPlane(
                    footError,
                    Vector3.up
                );


            bool footTooFar =
                horizontalFootError.sqrMagnitude >
                minimumStepDistance *
                minimumStepDistance;


            // NEW IMPORTANT PART:
            //
            // This foot can only begin stepping if:
            //
            // 1. It isn't already stepping
            // 2. The other foot isn't stepping
            // 3. It has moved far enough from its ideal position
            if (!foot.isStepping &&
                !otherFoot.isStepping &&
                footTooFar)
            {
                TryStartStep(
                    foot,
                    probeOrigin,
                    movementDirection,
                    speed
                );
            }


            if (drawProbes)
            {
                Debug.DrawRay(
                    probeOrigin,
                    Vector3.down * groundHit.distance,
                    Color.yellow
                );
            }
        }


        // ============================================================
        // PERFORM ACTIVE STEP
        // ============================================================

        if (foot.isStepping)
        {
            PerformStep(foot);
        }
    }


    void TryStartStep(
        Foot foot,
        Vector3 probeOrigin,
        Vector3 movementDirection,
        float speed)
    {
        float speed01 =
            Mathf.InverseLerp(
                movementDeadzone,
                speedForFullOvershoot,
                speed
            );


        float safeOvershoot =
            Mathf.Min(
                stepOvershoot,
                minimumStepDistance * 0.8f
            );


        float actualOvershoot =
            safeOvershoot * speed01;


        Vector3 landingProbeOrigin =
            probeOrigin +
            movementDirection *
            actualOvershoot;


        if (Physics.SphereCast(
            landingProbeOrigin,
            footProbeRadius,
            Vector3.down,
            out RaycastHit landingHit,
            rayDistance,
            layerMask))
        {
            foot.stepStart =
                foot.plantedPosition;


            foot.stepEnd =
                landingHit.point +
                transform.TransformVector(
                    foot.placementOffset
                );


            foot.stepProgress = 0f;
            foot.isStepping = true;


            // Capture rotation at beginning of step
            foot.stepStartRotation =
                foot.target.rotation;


            Quaternion groundAlignment =
                Quaternion.FromToRotation(
                    foot.target.up,
                    landingHit.normal
                );


            foot.stepEndRotation =
                groundAlignment *
                foot.target.rotation;


            if (drawProbes)
            {
                Debug.DrawRay(
                    landingProbeOrigin,
                    Vector3.down *
                    landingHit.distance,
                    Color.cyan
                );
            }
        }
    }


    void PerformStep(Foot foot)
    {
        foot.stepProgress +=
            Time.deltaTime /
            stepDuration;


        float t =
            Mathf.Clamp01(
                foot.stepProgress
            );


        // ============================================================
        // POSITION
        // ============================================================

        Vector3 position =
            Vector3.Lerp(
                foot.stepStart,
                foot.stepEnd,
                t
            );


        float lift =
            Mathf.Sin(
                t * Mathf.PI
            ) * stepHeight;


        position +=
            Vector3.up * lift;


        foot.target.position =
            position;


        // ============================================================
        // ROTATION
        // ============================================================

        foot.target.rotation =
            Quaternion.Slerp(
                foot.stepStartRotation,
                foot.stepEndRotation,
                t
            );


        // ============================================================
        // FINISH
        // ============================================================

        if (foot.stepProgress >= 1f)
        {
            foot.target.position =
                foot.stepEnd;

            foot.target.rotation =
                foot.stepEndRotation;

            foot.plantedPosition =
                foot.stepEnd;

            foot.isStepping =
                false;
        }
    }
}