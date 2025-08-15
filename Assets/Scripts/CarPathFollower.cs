using System.Collections.Generic;
using UnityEngine;

public class CarPathFollower : MonoBehaviour
{
    [Header("References")]
    public SmoothLineDrawer lineDrawer;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float turnSpeed = 10f; // Larger = faster turning
    public bool startOnPathComplete = true;
    public bool alignYToCar = true; // Keep car's Y while following

    [Header("Start Behavior")]
    public bool snapToPathStart = true; // dịch chuyển ngay tới điểm bắt đầu
    public bool rotateToPathStart = true; // quay đầu về hướng điểm kế tiếp khi bắt đầu

    private readonly List<Vector3> pathPoints = new List<Vector3>();
    private int currentIndex = 0;
    private bool isMoving = false;

    void Awake()
    {
        if (lineDrawer != null)
        {
            lineDrawer.OnPathCompleted += HandlePathCompleted;
        }
    }

    void OnDestroy()
    {
        if (lineDrawer != null)
        {
            lineDrawer.OnPathCompleted -= HandlePathCompleted;
        }
    }

    void Update()
    {
        if (!isMoving || pathPoints.Count == 0 || currentIndex >= pathPoints.Count)
        {
            return;
        }

        Vector3 target = pathPoints[currentIndex];
        if (alignYToCar)
        {
            target.y = transform.position.y;
        }

        Vector3 toTarget = target - transform.position;
        float distance = toTarget.magnitude;

        if (distance < 0.05f)
        {
            currentIndex++;
            if (currentIndex >= pathPoints.Count)
            {
                isMoving = false;
                return;
            }
            target = pathPoints[currentIndex];
            if (alignYToCar)
            {
                target.y = transform.position.y;
            }
            toTarget = target - transform.position;
        }

        // Move forward towards target
        Vector3 moveStep = toTarget.normalized * moveSpeed * Time.deltaTime;
        if (moveStep.magnitude > toTarget.magnitude)
        {
            moveStep = toTarget;
        }
        transform.position += moveStep;

        // Rotate to face movement direction
        if (toTarget.sqrMagnitude > 0.0001f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, turnSpeed * Time.deltaTime);
        }
    }

    private void HandlePathCompleted(List<Vector3> points)
    {
        if (startOnPathComplete)
        {
            SetPathAndStart(points);
        }
    }

    public void SetPathAndStart(IEnumerable<Vector3> points)
    {
        pathPoints.Clear();
        foreach (var point in points)
        {
            Vector3 p = point;
            if (alignYToCar)
            {
                p.y = transform.position.y;
            }
            pathPoints.Add(p);
        }
        currentIndex = 0;

        if (pathPoints.Count > 0)
        {
            if (snapToPathStart)
            {
                transform.position = pathPoints[0];
            }
            if (rotateToPathStart && pathPoints.Count > 1)
            {
                Vector3 dir = (pathPoints[1] - pathPoints[0]).normalized;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                }
            }
        }

        isMoving = pathPoints.Count > 0;
    }

    public void StartFollowingCurrentLine()
    {
        if (lineDrawer == null) return;
        var currentPath = lineDrawer.CurrentSmoothPath;
        if (currentPath == null || currentPath.Count == 0) return;
        SetPathAndStart(currentPath);
    }
}