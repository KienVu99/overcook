using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class PathDrawer : MonoBehaviour
{
    [Header("Drawing")]
    public float minPointDistance = 0.15f;
    public float maxPathLength = 20f;
    public int segmentsPerCurve = 8;
    public LayerMask groundMask;
    public LayerMask obstacleMask;
    public bool stopOnObstacle = true;
    public float rayMaxDistance = 100f;

    [Header("Visual")]
    public float lineWidth = 0.2f;

    public event Action<IReadOnlyList<Vector3>> OnPathFinalized;

    public PathFollower follower; // optional, send finalized path to a follower

    private LineRenderer lineRenderer;
    private readonly List<Vector3> rawPoints = new List<Vector3>();
    private readonly List<Vector3> smoothPoints = new List<Vector3>();
    private float currentLength;
    private bool isDrawing;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 0;
        lineRenderer.useWorldSpace = true;
        lineRenderer.widthMultiplier = lineWidth;
    }

    void Update()
    {
        if (HasPointerDown())
        {
            BeginPath();
        }
        else if (isDrawing && HasPointer())
        {
            AddPointFromPointer();
        }
        else if (isDrawing && HasPointerUp())
        {
            FinalizePath();
        }
    }

    bool HasPointer()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        return Input.GetMouseButton(0);
#else
        return Input.touchCount > 0 && (Input.GetTouch(0).phase == TouchPhase.Moved || Input.GetTouch(0).phase == TouchPhase.Stationary);
#endif
    }

    bool HasPointerDown()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        return Input.GetMouseButtonDown(0);
#else
        return Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began;
#endif
    }

    bool HasPointerUp()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        return Input.GetMouseButtonUp(0);
#else
        if (Input.touchCount == 0) return false;
        var phase = Input.GetTouch(0).phase;
        return phase == TouchPhase.Ended || phase == TouchPhase.Canceled;
#endif
    }

    bool TryGetPointerWorld(out Vector3 world)
    {
        Vector2 screen;
#if UNITY_EDITOR || UNITY_STANDALONE
        screen = Input.mousePosition;
#else
        if (Input.touchCount == 0) { world = Vector3.zero; return false; }
        screen = Input.GetTouch(0).position;
#endif
        Ray ray = Camera.main.ScreenPointToRay(screen);
        if (Physics.Raycast(ray, out RaycastHit hit, rayMaxDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            world = hit.point;
            return true;
        }
        world = Vector3.zero;
        return false;
    }

    void BeginPath()
    {
        isDrawing = true;
        rawPoints.Clear();
        smoothPoints.Clear();
        currentLength = 0f;
        lineRenderer.positionCount = 0;

        if (TryGetPointerWorld(out Vector3 w))
        {
            rawPoints.Add(w);
            UpdateLineRenderer();
        }
    }

    void AddPointFromPointer()
    {
        if (!TryGetPointerWorld(out Vector3 w)) return;
        if (rawPoints.Count == 0)
        {
            rawPoints.Add(w);
            UpdateLineRenderer();
            return;
        }

        Vector3 last = rawPoints[rawPoints.Count - 1];
        Vector3 delta = w - last;
        delta.y = 0f; // keep level on ground plane
        float dist = delta.magnitude;

        if (dist < minPointDistance) return;
        if (currentLength + dist > maxPathLength)
        {
            Vector3 clamped = last + delta.normalized * (maxPathLength - currentLength);
            TryAddSegment(last, clamped, true);
            return;
        }

        TryAddSegment(last, w, false);
    }

    void TryAddSegment(Vector3 from, Vector3 to, bool finalize)
    {
        // stop on obstacles
        if (Physics.Linecast(from + Vector3.up * 0.05f, to + Vector3.up * 0.05f, out RaycastHit hit, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            to = hit.point;
            finalize = stopOnObstacle || finalize;
        }

        currentLength += Vector3.Distance(from, to);
        rawPoints.Add(to);
        UpdateLineRenderer();

        if (finalize)
        {
            FinalizePath();
        }
    }

    void FinalizePath()
    {
        isDrawing = false;
        List<Vector3> path = new List<Vector3>(GetPath());
        OnPathFinalized?.Invoke(path);
        if (follower != null)
        {
            follower.SetPath(path);
        }
    }

    void UpdateLineRenderer()
    {
        smoothPoints.Clear();
        if (rawPoints.Count < 4)
        {
            smoothPoints.AddRange(rawPoints);
        }
        else
        {
            for (int i = 0; i < rawPoints.Count - 3; i++)
            {
                Vector3 p0 = rawPoints[i];
                Vector3 p1 = rawPoints[i + 1];
                Vector3 p2 = rawPoints[i + 2];
                Vector3 p3 = rawPoints[i + 3];
                for (int j = 0; j <= segmentsPerCurve; j++)
                {
                    float t = j / (float)segmentsPerCurve;
                    smoothPoints.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }
        }
        lineRenderer.positionCount = smoothPoints.Count;
        if (smoothPoints.Count > 0)
        {
            lineRenderer.SetPositions(smoothPoints.ToArray());
        }
    }

    public IReadOnlyList<Vector3> GetPath()
    {
        return smoothPoints.Count > 1 ? smoothPoints : rawPoints;
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t
        );
    }

    public void Clear()
    {
        rawPoints.Clear();
        smoothPoints.Clear();
        lineRenderer.positionCount = 0;
    }
}