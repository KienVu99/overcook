using System.Collections.Generic;
using UnityEngine;
using System;

[RequireComponent(typeof(LineRenderer))]
public class SmoothLineDrawer : MonoBehaviour
{
    public float minDistance = 0.1f; // Khoảng cách tối thiểu để ghi nhận điểm mới
    public int segmentsPerCurve = 10; // Độ mượt (nhiều segment hơn thì cong hơn)

    private LineRenderer lineRenderer;
    private List<Vector3> points = new List<Vector3>();
    private List<Vector3> currentSmoothPoints = new List<Vector3>();

    public event Action<List<Vector3>> OnPathCompleted;
    public IReadOnlyList<Vector3> CurrentSmoothPath => currentSmoothPoints;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 0;
        lineRenderer.numCornerVertices = 5; // Bo góc đẹp hơn
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            points.Clear();
            currentSmoothPoints.Clear();
            lineRenderer.positionCount = 0;
            AddPoint(Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 10f)));
        }
        else if (Input.GetMouseButton(0))
        {
            Vector3 newPoint = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 10f));
            if (Vector3.Distance(newPoint, points[points.Count - 1]) > minDistance)
            {
                AddPoint(newPoint);
                UpdateLine();
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            FinalizePath();
        }
    }

    void AddPoint(Vector3 point)
    {
        points.Add(point);
    }

    void UpdateLine()
    {
        // Xử lý cả trường hợp ít điểm để vẫn hiển thị và cung cấp đường đi
        if (points.Count < 2)
        {
            currentSmoothPoints.Clear();
            currentSmoothPoints.AddRange(points);
            lineRenderer.positionCount = points.Count;
            if (points.Count > 0)
            {
                lineRenderer.SetPositions(points.ToArray());
            }
            return;
        }

        if (points.Count < 4)
        {
            currentSmoothPoints.Clear();
            currentSmoothPoints.AddRange(points);
            lineRenderer.positionCount = points.Count;
            lineRenderer.SetPositions(points.ToArray());
            return;
        }

        List<Vector3> smoothPoints = new List<Vector3>();

        for (int i = 0; i < points.Count - 3; i++)
        {
            Vector3 p0 = points[i];
            Vector3 p1 = points[i + 1];
            Vector3 p2 = points[i + 2];
            Vector3 p3 = points[i + 3];

            for (int j = 0; j <= segmentsPerCurve; j++)
            {
                float t = j / (float)segmentsPerCurve;
                Vector3 newPos = CatmullRom(p0, p1, p2, p3, t);
                smoothPoints.Add(newPos);
            }
        }

        // Đảm bảo chứa điểm bắt đầu và kết thúc gốc
        if (smoothPoints.Count > 0)
        {
            if (smoothPoints[0] != points[0])
            {
                smoothPoints.Insert(0, points[0]);
            }
            Vector3 lastOriginal = points[points.Count - 1];
            if (smoothPoints[smoothPoints.Count - 1] != lastOriginal)
            {
                smoothPoints.Add(lastOriginal);
            }
        }

        currentSmoothPoints = smoothPoints;
        lineRenderer.positionCount = currentSmoothPoints.Count;
        lineRenderer.SetPositions(currentSmoothPoints.ToArray());
    }

    void FinalizePath()
    {
        UpdateLine();
        if (currentSmoothPoints.Count > 0)
        {
            OnPathCompleted?.Invoke(new List<Vector3>(currentSmoothPoints));
        }
    }

    Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t
        );
    }
}