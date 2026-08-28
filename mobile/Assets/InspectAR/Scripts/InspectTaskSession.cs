using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// 当前未提交任务：仅内存。每点独立 xyz，退出 App 不写 PlayerPrefs。
/// </summary>
public sealed class DraftMarker
{
    public string localId;
    public GameObject cube;
    public ARAnchor anchor;
    public Vector3 position;
    public string title;
    public string description;
    public string priority;
    public bool submitted;
    public string issueId;
    public GameObject virtualGrid;
}

public sealed class InspectTaskSession
{
    public bool Active { get; private set; }
    public bool Locked { get; private set; }
    public readonly List<DraftMarker> Markers = new List<DraftMarker>();
    public DraftMarker Selected;

    public bool CanPlace => Active && !Locked;

    public bool HasUnsubmitted
    {
        get
        {
            for (var i = 0; i < Markers.Count; i++)
            {
                if (Markers[i] != null && !Markers[i].submitted)
                    return true;
            }

            return false;
        }
    }

    public void BeginNew()
    {
        Active = true;
        Locked = false;
        Selected = null;
        Markers.Clear();
    }

    public void Add(DraftMarker marker)
    {
        Markers.Add(marker);
        Selected = marker;
    }

    public void RemoveUnsubmitted(DraftMarker marker)
    {
        if (marker == null || marker.submitted)
            return;
        Markers.Remove(marker);
        if (Selected == marker)
            Selected = Markers.Count > 0 ? Markers[Markers.Count - 1] : null;
        DestroyVisual(marker);
    }

    public List<DraftMarker> Unsubmitted()
    {
        var list = new List<DraftMarker>();
        for (var i = 0; i < Markers.Count; i++)
        {
            var m = Markers[i];
            if (m != null && !m.submitted)
                list.Add(m);
        }

        return list;
    }

    public void CapturePositions()
    {
        for (var i = 0; i < Markers.Count; i++)
        {
            var m = Markers[i];
            if (m?.cube != null)
                m.position = m.cube.transform.position;
        }
    }

    public void LockAfterAllPosted()
    {
        Locked = true;
        Selected = null;
    }

    public void AbandonUnsubmitted()
    {
        var keep = new List<DraftMarker>();
        for (var i = 0; i < Markers.Count; i++)
        {
            var m = Markers[i];
            if (m == null)
                continue;
            if (m.submitted)
            {
                keep.Add(m);
                continue;
            }

            DestroyVisual(m);
        }

        Markers.Clear();
        Markers.AddRange(keep);
        Active = false;
        Locked = false;
        Selected = null;
    }

    static void DestroyVisual(DraftMarker marker)
    {
        if (marker.virtualGrid != null)
        {
            var filter = marker.virtualGrid.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
                UnityEngine.Object.Destroy(filter.sharedMesh);
            UnityEngine.Object.Destroy(marker.virtualGrid);
            marker.virtualGrid = null;
        }

        if (marker.anchor != null)
            UnityEngine.Object.Destroy(marker.anchor.gameObject);
        else if (marker.cube != null)
            UnityEngine.Object.Destroy(marker.cube);
        marker.cube = null;
        marker.anchor = null;
    }

    public static string FormatXyz(Vector3 p)
    {
        return $"X 坐标：{p.x:F2}\nY 坐标：{p.y:F2}\nZ 坐标：{p.z:F2}";
    }
}
