using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class SmoothLineDrawer : MonoBehaviour
{
	public float minDistance = 0.1f; // Khoảng cách tối thiểu để ghi nhận điểm mới
	public int segmentsPerCurve = 12; // Độ mượt (nhiều segment hơn thì cong hơn)
	[Range(0f, 1f)]
	public float alpha = 0.5f; // 0: uniform, 0.5: centripetal, 1: chordal
	public bool useCentripetal = true; // Bật/tắt Catmull–Rom kiểu centripetal
	public bool smoothInput = true; // Làm mượt input để giảm rung tay
	[Range(0f, 1f)]
	public float inputSmoothFactor = 0.4f; // Mức làm mượt input (EMA)
	public float zDistance = 10f; // Khoảng cách z khi quy đổi ScreenToWorld
	public bool rebuildAllOnMouseUp = true; // Build lại toàn bộ khi nhả chuột để đạt độ mượt tối đa

	private LineRenderer lineRenderer;
	private Camera mainCamera;
	private readonly List<Vector3> points = new List<Vector3>();
	private readonly List<Vector3> smoothPoints = new List<Vector3>();
	private int lastGeneratedCurveIndex = -1; // Theo dõi curve đã sinh gần nhất
	private bool hasSmoothedPoint = false;
	private Vector3 lastSmoothedPoint;

	void Awake()
	{
		lineRenderer = GetComponent<LineRenderer>();
		lineRenderer.positionCount = 0;
		lineRenderer.numCornerVertices = 8; // Bo góc mượt hơn
		lineRenderer.numCapVertices = 8; // Đầu/cuối tròn hơn
		mainCamera = Camera.main;
	}

	void OnEnable()
	{
		if (mainCamera == null) mainCamera = Camera.main; // Cache Camera.main
	}

	void Update()
	{
		if (Input.GetMouseButtonDown(0))
		{
			points.Clear();
			smoothPoints.Clear();
			lineRenderer.positionCount = 0;
			lastGeneratedCurveIndex = -1;
			hasSmoothedPoint = false;
			AddPoint(ScreenToWorld(Input.mousePosition));
			UpdateRendererForFewPoints();
		}
		else if (Input.GetMouseButton(0) && points.Count > 0)
		{
			Vector3 raw = ScreenToWorld(Input.mousePosition);
			Vector3 candidate = raw;
			if (smoothInput)
			{
				if (!hasSmoothedPoint)
				{
					lastSmoothedPoint = raw;
					hasSmoothedPoint = true;
				}
				else
				{
					lastSmoothedPoint = Vector3.Lerp(lastSmoothedPoint, raw, inputSmoothFactor);
				}
				candidate = lastSmoothedPoint;
			}

			float minDistSqr = minDistance * minDistance;
			if ((candidate - points[points.Count - 1]).sqrMagnitude > minDistSqr)
			{
				AddPoint(candidate);
				if (points.Count < 4)
				{
					UpdateRendererForFewPoints();
				}
				else
				{
					IncrementalUpdateLastCurve();
				}
			}
		}

		if (Input.GetMouseButtonUp(0) && rebuildAllOnMouseUp)
		{
			RebuildAllCurves();
		}
	}

	private Vector3 ScreenToWorld(Vector3 screenPos)
	{
		if (mainCamera == null) mainCamera = Camera.main;
		screenPos.z = zDistance;
		return mainCamera.ScreenToWorldPoint(screenPos);
	}

	void AddPoint(Vector3 point)
	{
		points.Add(point);
	}

	private void UpdateRendererForFewPoints()
	{
		if (points.Count <= 1)
		{
			lineRenderer.positionCount = points.Count;
			if (points.Count == 1)
			{
				lineRenderer.SetPosition(0, points[0]);
			}
			return;
		}

		if (points.Count < 4)
		{
			lineRenderer.positionCount = points.Count;
			for (int i = 0; i < points.Count; i++)
			{
				lineRenderer.SetPosition(i, points[i]);
			}
		}
	}

	private void IncrementalUpdateLastCurve()
	{
		int curveIndex = points.Count - 4;
		if (curveIndex < 0) return;

		if (curveIndex == 0 && lastGeneratedCurveIndex == -1)
		{
			smoothPoints.Clear();
			AppendCurvePoints(curveIndex, includeFirstPoint: true);
			lastGeneratedCurveIndex = 0;
			lineRenderer.positionCount = smoothPoints.Count;
			for (int i = 0; i < smoothPoints.Count; i++)
			{
				lineRenderer.SetPosition(i, smoothPoints[i]);
			}
			return;
		}

		if (curveIndex == lastGeneratedCurveIndex + 1)
		{
			int oldCount = smoothPoints.Count;
			AppendCurvePoints(curveIndex, includeFirstPoint: false);
			lineRenderer.positionCount = smoothPoints.Count;
			for (int i = oldCount; i < smoothPoints.Count; i++)
			{
				lineRenderer.SetPosition(i, smoothPoints[i]);
			}
			lastGeneratedCurveIndex = curveIndex;
			return;
		}

		// Trường hợp hiếm khi chỉ số lệch, build lại toàn bộ
		RebuildAllCurves();
	}

	private void RebuildAllCurves()
	{
		smoothPoints.Clear();
		if (points.Count < 4)
		{
			UpdateRendererForFewPoints();
			return;
		}

		for (int i = 0; i <= points.Count - 4; i++)
		{
			AppendCurvePoints(i, includeFirstPoint: i == 0);
		}
		lastGeneratedCurveIndex = points.Count - 4;
		lineRenderer.positionCount = smoothPoints.Count;
		for (int i = 0; i < smoothPoints.Count; i++)
		{
			lineRenderer.SetPosition(i, smoothPoints[i]);
		}
	}

	private void AppendCurvePoints(int i, bool includeFirstPoint)
	{
		Vector3 p0 = points[i];
		Vector3 p1 = points[i + 1];
		Vector3 p2 = points[i + 2];
		Vector3 p3 = points[i + 3];

		int startJ = includeFirstPoint ? 0 : 1; // Tránh trùng điểm seam giữa các curve
		for (int j = startJ; j <= segmentsPerCurve; j++)
		{
			float t = j / (float)segmentsPerCurve;
			Vector3 newPos = useCentripetal && alpha > 0f
				? CatmullRomCentripetal(p0, p1, p2, p3, t, alpha)
				: CatmullRomUniform(p0, p1, p2, p3, t);
			smoothPoints.Add(newPos);
		}
	}

	private static Vector3 CatmullRomUniform(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
	{
		float t2 = t * t;
		float t3 = t2 * t;
		return 0.5f * (
			(2f * p1) +
			(-p0 + p2) * t +
			(2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
			(-p0 + 3f * p1 - 3f * p2 + p3) * t3
		);
	}

	private static Vector3 CatmullRomCentripetal(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t, float alpha)
	{
		float GetT(float ti, Vector3 pa, Vector3 pb)
		{
			float a = (pb - pa).magnitude;
			float b = Mathf.Pow(a, alpha);
			return b + ti;
		}

		float t0 = 0f;
		float t1 = GetT(t0, p0, p1);
		float t2 = GetT(t1, p1, p2);
		float t3 = GetT(t2, p2, p3);

		float s = Mathf.Lerp(t1, t2, t);

		Vector3 A1 = Interp(p0, p1, t0, t1, s);
		Vector3 A2 = Interp(p1, p2, t1, t2, s);
		Vector3 A3 = Interp(p2, p3, t2, t3, s);

		Vector3 B1 = Interp(A1, A2, t0, t2, s);
		Vector3 B2 = Interp(A2, A3, t1, t3, s);

		return Interp(B1, B2, t1, t2, s);
	}

	private static Vector3 Interp(Vector3 a, Vector3 b, float ta, float tb, float t)
	{
		float denom = tb - ta;
		if (Mathf.Abs(denom) < 1e-4f) return a;
		float wA = (tb - t) / denom;
		float wB = (t - ta) / denom;
		return a * wA + b * wB;
	}
}