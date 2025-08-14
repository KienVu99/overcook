using System.Collections.Generic;
using UnityEngine;

public class PathFollower : MonoBehaviour
{
    public float moveSpeed = 3.5f;
    public float rotationSpeed = 540f; // degrees per second
    public bool alignToPath = true;

    private readonly List<Vector3> pathPoints = new List<Vector3>();
    private int targetIndex = 0;
    private bool isMoving = false;

    public void SetPath(IReadOnlyList<Vector3> points)
    {
        pathPoints.Clear();
        if (points == null || points.Count == 0)
        {
            isMoving = false;
            return;
        }
        pathPoints.AddRange(points);
        targetIndex = 0;
        transform.position = pathPoints[0];
        isMoving = pathPoints.Count > 1;
    }

    void Update()
    {
        if (!isMoving || pathPoints.Count == 0) return;
        if (targetIndex >= pathPoints.Count) { isMoving = false; return; }

        Vector3 target = pathPoints[targetIndex];
        Vector3 toTarget = target - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < 0.01f)
        {
            targetIndex++;
            if (targetIndex >= pathPoints.Count)
            {
                isMoving = false;
            }
            return;
        }

        Vector3 direction = toTarget.normalized;
        transform.position += direction * moveSpeed * Time.deltaTime;

        if (alignToPath && direction.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }
    }
}