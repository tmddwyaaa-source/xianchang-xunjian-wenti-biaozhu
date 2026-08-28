using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// 巡检 AR：地面过滤 + 任务多标记 + 历史 PUT。真机填局域网 IP，不要用 localhost。
/// </summary>
public sealed class InspectARApp : MonoBehaviour
{
    public const string PlayerPrefsUrlKey = "inspect.backendBaseUrl";
    public const string PlayerPrefsJwtKey = "inspect.jwt";
    public const string PlayerPrefsUserIdKey = "inspect.userId";
    public const string PlayerPrefsRoleKey = "inspect.role";
    public const string PlayerPrefsUserNameKey = "inspect.username";
    public const string DefaultBackendUrl = "http://192.168.1.8:8080";
    const string ScanFloorHint = "请对准有纹理的地面缓慢平移，光线要足，避开瓷砖反光。";
    const string LowTextureHint = "白墙白地特征点太少，请扫到砖缝、脚印、工具，或直接点屏幕用临时平面。";
    const float MinFloorNormalDot = 0.85f;
    const float MinFloorArea = 0.01f;
    const float LowestFloorHysteresis = 0.08f;
    const float LowTextureHintSeconds = 4f;
    const float FeatureHudInterval = 0.3f;
    const int FeaturePointLowCount = 15;
    const float VirtualPlaneMeters = 1.5f;
    const float HandheldHeight = 1.2f;
    const float VirtualRayFallback = 1.4f;
    static readonly PlaneDetectionMode ScanDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
    static readonly TrackableType[] PlaceRayTypes =
    {
        TrackableType.PlaneWithinPolygon,
        TrackableType.PlaneEstimated,
        TrackableType.FeaturePoint,
        TrackableType.Depth
    };
    const float MaxHeightAboveFloor = 0.20f;
    const float IssuePollSeconds = 5f;
    const float IssuePollStartDelay = 1f;
    const float ToastSeconds = 2.5f;
    const float ToastFadeSeconds = 0.35f;
    const float FloorLockSeconds = 1f;
    const string SystemHint = "【系统提示】请尽快安排处理";
    static readonly string[] TitleKeywords = { "漏水", "裂缝", "冒烟", "异响", "脱落" };

    [SerializeField] string m_DefaultBackendUrl = DefaultBackendUrl;

    ARRaycastManager m_RaycastManager;
    ARPlaneManager m_PlaneManager;
    ARAnchorManager m_AnchorManager;
    ARPointCloudManager m_PointCloudManager;
    GameObject m_PointCloudPrefab;
    Material m_PointCloudParticleMat;
    ARPlane m_VisibleFloor;
    ARPlane m_LockedFloor;
    ARPlane m_CandidateFloor;
    float m_CandidateStableSince = -1f;
    bool m_FloorLocked;
    GameObject m_FrozenGrid;
    Material m_FrozenGridMat;
    bool m_Scanning;
    float m_ScanStartedAt = -1f;
    bool m_LowTextureHinted;
    float m_NextFeatureHudAt;
    Text m_FeatureHintText;

    readonly InspectTaskSession m_Task = new InspectTaskSession();
    readonly Dictionary<string, GameObject> m_MarkersById = new Dictionary<string, GameObject>();

    InspectUiTheme m_Theme;
    InspectHistoryPanel m_History;

    Canvas m_Canvas;
    CanvasScaler m_Scaler;
    GameObject m_LoginPanel;
    GameObject m_Hud;
    GameObject m_BottomBar;
    GameObject m_Drawer;
    GameObject m_EditorCard;
    GameObject m_ConfirmPanel;
    GameObject m_Toast;
    RectTransform m_TopBarRt;
    RectTransform m_BottomBarRt;
    RectTransform m_DrawerRt;
    RectTransform m_ToastRt;
    Transform m_MarkerListContent;
    Button m_NewTaskButton;
    Button m_StartScanButton;
    Button m_PauseScanButton;
    Button m_SubmitButton;
    Button m_MarkerToggleButton;
    Text m_StartScanLabel;
    Text m_PauseScanLabel;
    Text m_MarkerToggleLabel;
    Text m_UserNameText;
    Text m_TaskStateText;
    Text m_LoginStatusText;
    Text m_ConfirmSummary;
    Text m_ToastText;
    Image m_ToastImage;
    CanvasGroup m_ToastGroup;
    Coroutine m_ToastCo;
    InputField m_UrlField;
    InputField m_LoginUrlField;
    InputField m_UserField;
    InputField m_PassField;
    InputField m_TitleField;
    InputField m_DescField;
    Button[] m_PriorityButtons;
    bool m_Submitting;
    bool m_LoggingIn;
    bool m_SyncingEditor;
    bool m_DrawerOpen;
    bool m_Placing;
    bool m_ConfirmIsRealign;
    Text m_ConfirmTitle;
    Text m_ConfirmSendLabel;

    static readonly List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();
    static readonly List<RaycastResult> s_UiHits = new List<RaycastResult>();

    public string UserId => PlayerPrefs.GetString(PlayerPrefsUserIdKey, "");
    public string UserRole => PlayerPrefs.GetString(PlayerPrefsRoleKey, "");

    void Start()
    {
        m_Theme = new InspectUiTheme();
        m_RaycastManager = FindFirstObjectByType<ARRaycastManager>();
        m_PlaneManager = FindFirstObjectByType<ARPlaneManager>();
        if (m_PlaneManager != null)
        {
            m_PlaneManager.requestedDetectionMode = ScanDetectionMode;
            m_PlaneManager.trackablesChanged.AddListener(OnPlanesChanged);
        }

        if (m_RaycastManager != null)
            m_RaycastManager.enabled = false;
        if (m_PlaneManager != null)
            m_PlaneManager.enabled = false;
        m_Scanning = false;
        EnsureOriginManagers();
        if (m_RaycastManager == null)
            Debug.LogError("[InspectAR] 场景缺少 ARRaycastManager。请在编辑器执行菜单 InspectAR/Setup Project。");

        EnsureEventSystem();
        BuildUi();
        ApplyLayout();
        ApplySessionUi();
        SetStatus(HasJwt() ? "点「新建任务」后才能扫描放置。" : "请先登录。", false);
        StartCoroutine(PollIssuesLoop());
    }

    void OnDestroy()
    {
        if (m_ToastCo != null)
            StopCoroutine(m_ToastCo);
        DestroyFrozenGrid();
        if (m_FrozenGridMat != null)
        {
            Destroy(m_FrozenGridMat);
            m_FrozenGridMat = null;
        }
        if (m_PointCloudParticleMat != null)
        {
            Destroy(m_PointCloudParticleMat);
            m_PointCloudParticleMat = null;
        }
        if (m_PointCloudPrefab != null)
        {
            Destroy(m_PointCloudPrefab);
            m_PointCloudPrefab = null;
        }
        if (m_PlaneManager != null)
            m_PlaneManager.trackablesChanged.RemoveListener(OnPlanesChanged);
    }

    void EnsureOriginManagers()
    {
        var origin = FindFirstObjectByType<XROrigin>();
        if (origin != null)
        {
            m_AnchorManager = origin.GetComponent<ARAnchorManager>();
            if (m_AnchorManager == null)
                m_AnchorManager = origin.gameObject.AddComponent<ARAnchorManager>();
            EnsurePointCloudManager(origin);
            return;
        }

        m_AnchorManager = FindFirstObjectByType<ARAnchorManager>();
        m_PointCloudManager = FindFirstObjectByType<ARPointCloudManager>();
    }

    void EnsurePointCloudManager(XROrigin origin)
    {
        m_PointCloudManager = origin.GetComponent<ARPointCloudManager>();
        if (m_PointCloudManager == null)
            m_PointCloudManager = origin.gameObject.AddComponent<ARPointCloudManager>();
        if (m_PointCloudManager.pointCloudPrefab == null)
        {
            m_PointCloudPrefab = CreatePointCloudPrefab();
            m_PointCloudManager.pointCloudPrefab = m_PointCloudPrefab;
        }

        m_PointCloudManager.enabled = false;
    }

    GameObject CreatePointCloudPrefab()
    {
        var go = new GameObject("InspectPointCloudPrefab");
        go.SetActive(false);
        go.AddComponent<ARPointCloud>();
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = 1f;
        main.startSize = 0.018f;
        main.startColor = new Color(0.90f, 0.78f, 0.38f, 0.88f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 4000;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.allowOcclusionWhenDynamic = false;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Particles/Standard Unlit")
                         ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                m_PointCloudParticleMat = new Material(shader);
                var color = new Color(0.90f, 0.78f, 0.38f, 0.88f);
                if (m_PointCloudParticleMat.HasProperty("_BaseColor"))
                    m_PointCloudParticleMat.SetColor("_BaseColor", color);
                else
                    m_PointCloudParticleMat.color = color;
                renderer.sharedMaterial = m_PointCloudParticleMat;
            }
        }

        go.AddComponent<ARPointCloudParticleVisualizer>();
        foreach (var col in go.GetComponentsInChildren<Collider>(true))
            col.enabled = false;
        return go;
    }

    void Update()
    {
        ApplyLayout();
        if (m_Scanning && !m_FloorLocked)
            TickFloorLock();
        if (m_Scanning)
        {
            TickLowTextureHint();
            TickFeatureHud();
        }
        if (!TryGetPress(out var screenPos))
            return;
        TryPlaceFromPress(screenPos);
    }

    void TryPlaceFromPress(Vector2 screenPos)
    {
        if (m_Placing)
            return;
        if (m_LoginPanel != null && m_LoginPanel.activeSelf)
            return;
        if (m_ConfirmPanel != null && m_ConfirmPanel.activeSelf)
            return;
        if (m_History != null && m_History.IsOpen)
            return;
        if (IsPointerOverBlockingUi(screenPos))
            return;
        if (!m_Task.CanPlace)
        {
            SetStatus(m_Task.Active ? "当前任务已锁定，不能再放标记。" : "未建任务：请先点「新建任务」。", true);
            return;
        }

        if (!m_Scanning)
        {
            SetStatus("未开始扫描：请先点「开始扫描」。", true);
            return;
        }

        if (m_RaycastManager == null || !m_RaycastManager.enabled)
        {
            SetStatus("未命中平面：射线未启用。", true);
            return;
        }

        if (TryHitArPose(screenPos, out var arPose))
        {
            StartCoroutine(PlaceAt(arPose, null, false));
            return;
        }

        if (TryVirtualFloorPose(screenPos, out var virtualPose))
        {
            var grid = CreateVirtualFloorGrid(virtualPose.position);
            StartCoroutine(PlaceAt(virtualPose, grid, true));
            return;
        }

        SetStatus("未命中平面：无法生成临时平面。", true);
    }

    bool TryHitArPose(Vector2 screenPos, out Pose pose)
    {
        pose = default;
        for (var i = 0; i < PlaceRayTypes.Length; i++)
        {
            var type = PlaceRayTypes[i];
            if (type == TrackableType.Depth && !SupportsDepthRaycast())
                continue;
            if (!m_RaycastManager.Raycast(screenPos, s_Hits, type))
                continue;
            for (var h = 0; h < s_Hits.Count; h++)
            {
                var hit = s_Hits[h];
                if (type == TrackableType.PlaneWithinPolygon)
                {
                    var plane = m_PlaneManager != null ? m_PlaneManager.GetPlane(hit.trackableId) : null;
                    if (plane == null || !IsStableFloor(plane))
                        continue;
                }

                pose = hit.pose;
                return true;
            }
        }

        return false;
    }

    bool SupportsDepthRaycast()
    {
        var desc = m_RaycastManager != null ? m_RaycastManager.descriptor : null;
        return desc != null && (desc.supportedTrackableTypes & TrackableType.Depth) != 0;
    }

    bool TryVirtualFloorPose(Vector2 screenPos, out Pose pose)
    {
        pose = default;
        var cam = ResolveCamera();
        if (cam == null)
            return false;
        var ray = cam.ScreenPointToRay(screenPos);
        var planeY = cam.transform.position.y - HandheldHeight;
        var floor = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
        Vector3 point;
        if (floor.Raycast(ray, out var enter) && enter > 0.05f)
            point = ray.GetPoint(enter);
        else
        {
            var along = ray.GetPoint(VirtualRayFallback);
            point = new Vector3(along.x, planeY, along.z);
        }

        pose = new Pose(point, Quaternion.LookRotation(Vector3.forward, Vector3.up));
        return true;
    }

    Camera ResolveCamera()
    {
        var origin = FindFirstObjectByType<XROrigin>();
        if (origin != null && origin.Camera != null)
            return origin.Camera;
        return Camera.main;
    }

    GameObject CreateVirtualFloorGrid(Vector3 worldPos)
    {
        var mesh = new Mesh { name = "VirtualFloor" };
        BuildFallbackFloorMesh(mesh, new Vector2(VirtualPlaneMeters, VirtualPlaneMeters));
        var go = new GameObject("VirtualFloorGrid");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = FrozenFloorMaterial();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        var origin = FindFirstObjectByType<XROrigin>();
        if (origin != null)
            go.transform.SetParent(origin.transform, true);
        go.transform.SetPositionAndRotation(worldPos, Quaternion.LookRotation(Vector3.forward, Vector3.up));
        go.transform.localScale = Vector3.one;
        return go;
    }

    void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
    {
        foreach (var plane in args.added)
            SetPlaneVisible(plane, false);
        if (m_FloorLocked)
        {
            HideAllLivePlaneVisuals();
            return;
        }

        if (!m_Scanning)
        {
            HideAllLivePlaneVisuals();
            m_VisibleFloor = null;
            return;
        }

        RefreshFloorVisibility();
    }

    void SetScanning(bool scanning)
    {
        if (scanning && !m_Task.CanPlace)
        {
            SetStatus("请先「新建任务」再开始扫描。", true);
            return;
        }

        m_Scanning = scanning;
        if (m_RaycastManager != null)
            m_RaycastManager.enabled = scanning;
        if (m_PlaneManager != null)
        {
            m_PlaneManager.enabled = scanning;
            if (scanning)
                m_PlaneManager.requestedDetectionMode = ScanDetectionMode;
        }

        SetPointCloudActive(scanning);
        RefreshScanButtons();
        if (scanning)
        {
            UnlockFloor();
            s_Hits.Clear();
            m_ScanStartedAt = Time.unscaledTime;
            m_LowTextureHinted = false;
            m_NextFeatureHudAt = 0f;
            if (m_PlaneManager != null)
                m_PlaneManager.requestedDetectionMode = ScanDetectionMode;
            RefreshFloorVisibility();
            SetStatus(ScanFloorHint, false);
        }
        else
        {
            s_Hits.Clear();
            m_CandidateFloor = null;
            m_CandidateStableSince = -1f;
            HideAllLivePlaneVisuals();
            if (!m_FloorLocked)
                m_VisibleFloor = null;
            if (m_FeatureHintText != null)
                m_FeatureHintText.text = "";
            if (!m_Submitting)
                SetStatus(m_Task.Active ? "暂停中" : "点「新建任务」后才能扫描放置。", false);
        }

        RefreshTopBar();
    }

    void SetPointCloudActive(bool active)
    {
        if (m_PointCloudManager == null)
            return;
        m_PointCloudManager.enabled = active;
        if (!active)
            m_PointCloudManager.SetTrackablesActive(false);
    }

    void TickLowTextureHint()
    {
        if (m_LowTextureHinted || m_ScanStartedAt < 0f)
            return;
        if (Time.unscaledTime - m_ScanStartedAt < LowTextureHintSeconds)
            return;
        if (HasQualifiedFloor())
            return;
        m_LowTextureHinted = true;
        SetStatus(LowTextureHint, false);
    }

    void TickFeatureHud()
    {
        if (Time.unscaledTime < m_NextFeatureHudAt)
            return;
        m_NextFeatureHudAt = Time.unscaledTime + FeatureHudInterval;
        var n = CountFeaturePoints();
        if (m_FeatureHintText == null)
            return;
        m_FeatureHintText.text = n < FeaturePointLowCount
            ? "特征点偏少，请对准裂缝/脚印/工具缓慢平移"
            : "特征点 " + n + "，可继续扫描";
    }

    int CountFeaturePoints()
    {
        if (m_PointCloudManager == null || !m_PointCloudManager.enabled)
            return 0;
        var n = 0;
        foreach (var cloud in m_PointCloudManager.trackables)
        {
            if (cloud != null && cloud.positions.HasValue)
                n += cloud.positions.Value.Length;
        }

        return n;
    }

    bool HasQualifiedFloor()
    {
        if (m_VisibleFloor != null && IsStableFloor(m_VisibleFloor))
            return true;
        if (m_PlaneManager == null)
            return false;
        foreach (var plane in m_PlaneManager.trackables)
        {
            if (IsStableFloor(plane))
                return true;
        }

        return false;
    }

    void RefreshScanButtons()
    {
        if (m_StartScanButton != null)
        {
            m_StartScanButton.interactable = m_Task.CanPlace && !m_Scanning;
            m_Theme.StylePrimaryButton(m_StartScanButton, m_StartScanButton.GetComponent<Image>(), m_StartScanLabel);
            if (m_Scanning)
                m_StartScanButton.GetComponent<Image>().color = new Color(m_Theme.Secondary.r, m_Theme.Secondary.g, m_Theme.Secondary.b, 0.45f);
        }

        if (m_PauseScanButton != null)
        {
            m_PauseScanButton.interactable = m_Scanning;
            m_Theme.StyleSecondaryButton(m_PauseScanButton, m_PauseScanButton.GetComponent<Image>(), m_PauseScanLabel);
        }

        if (m_StartScanLabel != null)
            m_StartScanLabel.text = m_Scanning ? "扫描中" : "开始扫描";
        if (m_PauseScanLabel != null)
            m_PauseScanLabel.text = m_Scanning ? "暂停扫描" : "暂停中";
        if (m_SubmitButton != null)
            m_SubmitButton.interactable = HasJwt() && m_Task.HasUnsubmitted && !m_Submitting;
        RefreshTopBar();
    }

    void HideAllLivePlaneVisuals()
    {
        if (m_PlaneManager == null)
            return;
        foreach (var plane in m_PlaneManager.trackables)
            SetPlaneVisible(plane, false);
    }

    void TickFloorLock()
    {
        RefreshFloorVisibility();
        if (m_VisibleFloor == null)
        {
            m_CandidateFloor = null;
            m_CandidateStableSince = -1f;
            return;
        }

        if (m_CandidateFloor == null || m_CandidateFloor.trackableId != m_VisibleFloor.trackableId)
        {
            m_CandidateFloor = m_VisibleFloor;
            m_CandidateStableSince = Time.unscaledTime;
            return;
        }

        if (Time.unscaledTime - m_CandidateStableSince >= FloorLockSeconds)
            LockFloor(m_CandidateFloor);
    }

    void LockFloor(ARPlane plane)
    {
        if (plane == null || m_FloorLocked)
            return;
        m_FloorLocked = true;
        m_LockedFloor = plane;
        m_VisibleFloor = plane;
        m_CandidateFloor = plane;
        HideAllLivePlaneVisuals();
        FreezeGridFrom(plane);
        if (m_PlaneManager != null)
            m_PlaneManager.requestedDetectionMode = ScanDetectionMode;
        if (!m_Submitting)
            SetStatus("地面网格已冻结。可继续点蓝色地面放置。", false);
    }

    void UnlockFloor()
    {
        DestroyFrozenGrid();
        m_FloorLocked = false;
        m_LockedFloor = null;
        m_CandidateFloor = null;
        m_CandidateStableSince = -1f;
        m_VisibleFloor = null;
    }

    void FreezeGridFrom(ARPlane plane)
    {
        DestroyFrozenGrid();
        if (plane == null)
            return;
        var src = plane.GetComponent<MeshFilter>();
        var mesh = new Mesh { name = "FrozenFloor" };
        if (src != null && src.sharedMesh != null && src.sharedMesh.vertexCount >= 3)
        {
            var srcMesh = src.sharedMesh;
            mesh.vertices = srcMesh.vertices;
            mesh.normals = srcMesh.normals;
            mesh.triangles = srcMesh.triangles;
            if (srcMesh.uv != null && srcMesh.uv.Length == srcMesh.vertexCount)
                mesh.uv = srcMesh.uv;
            mesh.RecalculateBounds();
        }
        else
        {
            BuildFallbackFloorMesh(mesh, plane.size);
        }

        m_FrozenGrid = new GameObject("FrozenFloorGrid");
        m_FrozenGrid.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = m_FrozenGrid.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = FrozenFloorMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var origin = FindFirstObjectByType<XROrigin>();
        if (origin != null)
            m_FrozenGrid.transform.SetParent(origin.transform, false);
        m_FrozenGrid.transform.SetPositionAndRotation(plane.transform.position, plane.transform.rotation);
        m_FrozenGrid.transform.localScale = Vector3.one;
    }

    static void BuildFallbackFloorMesh(Mesh mesh, Vector2 size)
    {
        var hx = Mathf.Max(0.15f, size.x * 0.5f);
        var hz = Mathf.Max(0.15f, size.y * 0.5f);
        mesh.vertices = new[]
        {
            new Vector3(-hx, 0f, -hz),
            new Vector3(hx, 0f, -hz),
            new Vector3(hx, 0f, hz),
            new Vector3(-hx, 0f, hz)
        };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        mesh.RecalculateBounds();
    }

    Material FrozenFloorMaterial()
    {
        if (m_FrozenGridMat != null)
            return m_FrozenGridMat;
        var shader = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Sprites/Default");
        m_FrozenGridMat = shader != null ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
        var color = new Color(0.15f, 0.50f, 0.95f, 0.32f);
        if (m_FrozenGridMat.HasProperty("_BaseColor"))
            m_FrozenGridMat.SetColor("_BaseColor", color);
        else
            m_FrozenGridMat.color = color;
        m_FrozenGridMat.SetOverrideTag("RenderType", "Transparent");
        m_FrozenGridMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m_FrozenGridMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m_FrozenGridMat.SetInt("_ZWrite", 0);
        m_FrozenGridMat.renderQueue = 3000;
        m_FrozenGridMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return m_FrozenGridMat;
    }

    void DestroyFrozenGrid()
    {
        if (m_FrozenGrid == null)
            return;
        var filter = m_FrozenGrid.GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null)
            Destroy(filter.sharedMesh);
        Destroy(m_FrozenGrid);
        m_FrozenGrid = null;
    }

    void RefreshFloorVisibility()
    {
        if (m_PlaneManager == null || m_FloorLocked)
            return;
        if (!m_Scanning)
        {
            HideAllLivePlaneVisuals();
            m_VisibleFloor = null;
            return;
        }

        ARPlane lowest = null;
        var lowestY = float.PositiveInfinity;
        foreach (var plane in m_PlaneManager.trackables)
        {
            if (!IsStableFloor(plane))
            {
                SetPlaneVisible(plane, false);
                continue;
            }

            if (plane.center.y < lowestY)
            {
                lowestY = plane.center.y;
                lowest = plane;
            }
        }

        if (m_VisibleFloor != null && IsStableFloor(m_VisibleFloor))
        {
            if (lowest == null || lowest.trackableId == m_VisibleFloor.trackableId)
            {
                lowest = m_VisibleFloor;
                lowestY = m_VisibleFloor.center.y;
            }
            else if (lowest.center.y > m_VisibleFloor.center.y - LowestFloorHysteresis)
            {
                lowest = m_VisibleFloor;
                lowestY = m_VisibleFloor.center.y;
            }
        }

        m_VisibleFloor = null;
        foreach (var plane in m_PlaneManager.trackables)
        {
            if (plane == null)
                continue;
            if (!IsStableFloor(plane) || plane.center.y > lowestY + MaxHeightAboveFloor)
            {
                SetPlaneVisible(plane, false);
                continue;
            }

            var show = plane == lowest;
            SetPlaneVisible(plane, show);
            if (show)
                m_VisibleFloor = plane;
        }
    }

    bool IsStableFloor(ARPlane plane)
    {
        if (plane == null || plane.subsumedBy != null)
            return false;
        if (plane.trackingState != TrackingState.Tracking && plane.trackingState != TrackingState.Limited)
            return false;
        if (plane.alignment != PlaneAlignment.HorizontalUp)
            return false;
        if (Vector3.Dot(plane.normal, Vector3.up) < MinFloorNormalDot)
            return false;
        var area = plane.size.x * plane.size.y;
        if (area <= 0f)
            area = plane.extents.x * plane.extents.y * 4f;
        return area >= MinFloorArea;
    }

    static void SetPlaneVisible(ARPlane plane, bool visible)
    {
        if (plane == null)
            return;
        var visualizer = plane.GetComponent<ARPlaneMeshVisualizer>();
        if (visualizer != null)
            visualizer.enabled = visible;
        var meshRenderer = plane.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
            meshRenderer.enabled = visible;
        var lineRenderer = plane.GetComponent<LineRenderer>();
        if (lineRenderer != null)
            lineRenderer.enabled = visible;
        var collider = plane.GetComponent<Collider>();
        if (collider != null)
            collider.enabled = visible;
    }

    IEnumerator PlaceAt(Pose pose, GameObject virtualGrid, bool fromVirtual)
    {
        if (m_Placing)
        {
            DestroyVirtualGridObject(virtualGrid);
            yield break;
        }

        m_Placing = true;
        if (m_AnchorManager == null)
        {
            SetStatus("锚定失败：缺少 ARAnchorManager。", true);
            DestroyVirtualGridObject(virtualGrid);
            m_Placing = false;
            yield break;
        }

        var op = m_AnchorManager.TryAddAnchorAsync(pose);
        yield return new WaitUntil(() => op.GetAwaiter().IsCompleted);
        Result<ARAnchor> result;
        try
        {
            result = op.GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            SetStatus("锚定失败：" + ex.Message, true);
            DestroyVirtualGridObject(virtualGrid);
            m_Placing = false;
            yield break;
        }

        if (!result.status.IsSuccess() || result.value == null)
        {
            SetStatus("锚定失败，请再点一次地面。", true);
            DestroyVirtualGridObject(virtualGrid);
            m_Placing = false;
            yield break;
        }

        CompletePlacement(result.value, virtualGrid, fromVirtual);
        m_Placing = false;
    }

    static void DestroyVirtualGridObject(GameObject grid)
    {
        if (grid == null)
            return;
        var filter = grid.GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null)
            Destroy(filter.sharedMesh);
        Destroy(grid);
    }

    void CompletePlacement(ARAnchor anchor, GameObject virtualGrid, bool fromVirtual)
    {
        if (anchor == null || !m_Task.CanPlace)
        {
            DestroyVirtualGridObject(virtualGrid);
            return;
        }

        var cube = CreateMarker();
        cube.transform.SetParent(anchor.transform, false);
        cube.transform.localPosition = Vector3.zero;
        cube.transform.localRotation = Quaternion.identity;
        cube.transform.localScale = Vector3.one * 0.12f;
        var n = m_Task.Markers.Count + 1;
        var draft = new DraftMarker
        {
            localId = Guid.NewGuid().ToString("N"),
            cube = cube,
            anchor = anchor,
            position = cube.transform.position,
            title = "标记 " + n,
            description = "",
            priority = "medium",
            submitted = false,
            virtualGrid = virtualGrid
        };
        ApplyMarkerColor(cube, draft.priority, "open");
        m_Task.Add(draft);
        RefreshMarkerListUi();
        FillEditorFromSelected();
        if (fromVirtual)
            SetStatus("已用临时平面放置（现场缺少真实平面）", false);
        else
            SetStatus("已添加标记 " + n + "。可继续点地，或点「标记」编辑后提交。", false);
    }

    public static Color ColorForIssue(string priority, string status)
    {
        if (status == "in_progress" || status == "resolved")
            return new Color(0.55f, 0.55f, 0.55f);
        if (priority == "low")
            return new Color(0.20f, 0.75f, 0.30f);
        if (priority == "medium")
            return new Color(0.95f, 0.80f, 0.15f);
        return new Color(0.95f, 0.25f, 0.20f);
    }

    public void ApplyMarkerColor(GameObject marker, string priority, string status)
    {
        if (marker == null)
            return;
        var markerRenderer = marker.GetComponent<Renderer>();
        if (markerRenderer == null)
            return;
        var color = ColorForIssue(priority, status);
        var mat = markerRenderer.material;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        else
            mat.color = color;
    }

    static GameObject CreateMarker()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "IssueMarker";
        go.transform.localScale = Vector3.one * 0.12f;
        var markerRenderer = go.GetComponent<Renderer>();
        var shader = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Sprites/Default");
        if (shader != null)
        {
            var mat = new Material(shader);
            var color = ColorForIssue("medium", "open");
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else
                mat.color = color;
            markerRenderer.sharedMaterial = mat;
        }

        return go;
    }

    void BuildUi()
    {
        var canvasGo = new GameObject("InspectCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        m_Canvas = canvasGo.GetComponent<Canvas>();
        m_Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        m_Canvas.pixelPerfect = false;
        m_Scaler = canvasGo.GetComponent<CanvasScaler>();
        m_Scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        m_Scaler.referenceResolution = new Vector2(1080, 1920);
        m_Scaler.matchWidthOrHeight = 1f;

        var root = canvasGo.transform;
        BuildTopBar(root);
        BuildBottomBar(root);
        BuildToast(root);
        BuildMarkerDrawer(root);
        BuildConfirmOverlay(root);
        BuildLoginOverlay(root);
        m_History = new InspectHistoryPanel(m_Theme, this, root);
        RefreshScanButtons();
    }

    void BuildTopBar(Transform canvas)
    {
        m_Hud = m_Theme.CreateGlassBar(canvas, "TopBar", false);
        m_TopBarRt = m_Hud.GetComponent<RectTransform>();
        var inner = new GameObject("TopInner", typeof(RectTransform));
        inner.transform.SetParent(m_Hud.transform, false);
        InspectUiTheme.StretchFull(inner.GetComponent<RectTransform>());
        m_UserNameText = m_Theme.CreateAnchoredText(inner.transform, "UserName", "",
            new Vector2(0f, 0.48f), new Vector2(0.55f, 1f), 22, m_Theme.OnSecondary, TextAnchor.MiddleLeft);
        m_TaskStateText = m_Theme.CreateAnchoredText(inner.transform, "TaskState", "未建任务",
            new Vector2(0.55f, 0.48f), new Vector2(1f, 1f), 22, m_Theme.OnSecondary, TextAnchor.MiddleRight);
        m_FeatureHintText = m_Theme.CreateAnchoredText(inner.transform, "FeatureHint", "",
            new Vector2(0f, 0f), new Vector2(1f, 0.52f), 18, m_Theme.OnSecondary, TextAnchor.MiddleLeft);
    }

    void BuildBottomBar(Transform canvas)
    {
        m_BottomBar = m_Theme.CreateGlassBar(canvas, "BottomBar", false);
        m_BottomBarRt = m_BottomBar.GetComponent<RectTransform>();
        m_BottomBar.AddComponent<RectMask2D>();
        var inner = new GameObject("BottomInner", typeof(RectTransform));
        inner.transform.SetParent(m_BottomBar.transform, false);
        var innerRt = inner.GetComponent<RectTransform>();
        innerRt.anchorMin = Vector2.zero;
        innerRt.anchorMax = Vector2.one;
        innerRt.offsetMin = Vector2.zero;
        innerRt.offsetMax = Vector2.zero;

        var row1 = MakeBottomRow(inner.transform, "Row1", -InspectUiTheme.CompactGap);
        var row2 = MakeBottomRow(inner.transform, "Row2", -(InspectUiTheme.CompactGap + InspectUiTheme.CompactRowH + InspectUiTheme.CompactGap));
        const float pad = 4f;
        const int font = 22;
        m_NewTaskButton = m_Theme.CreateSplitButton(row1.transform, "新建任务", 0f, 0.333f, m_Theme.StylePrimaryButton, pad, 4f, font);
        m_StartScanButton = m_Theme.CreateSplitButton(row1.transform, "开始扫描", 0.333f, 0.666f, m_Theme.StylePrimaryButton, pad, 4f, font);
        m_PauseScanButton = m_Theme.CreateSplitButton(row1.transform, "暂停扫描", 0.666f, 1f, m_Theme.StyleSecondaryButton, pad, 4f, font);
        m_StartScanLabel = m_StartScanButton.GetComponentInChildren<Text>();
        m_PauseScanLabel = m_PauseScanButton.GetComponentInChildren<Text>();
        m_NewTaskButton.onClick.AddListener(OnNewTask);
        m_StartScanButton.onClick.AddListener(() => SetScanning(true));
        m_PauseScanButton.onClick.AddListener(() => SetScanning(false));

        var history = m_Theme.CreateSplitButton(row2.transform, "历史", 0f, 0.333f, m_Theme.StyleSecondaryButton, pad, 4f, font);
        history.onClick.AddListener(OnHistoryClicked);
        m_MarkerToggleButton = m_Theme.CreateSplitButton(row2.transform, "标记", 0.333f, 0.666f, m_Theme.StyleSecondaryButton, pad, 4f, font);
        m_MarkerToggleLabel = m_MarkerToggleButton.GetComponentInChildren<Text>();
        m_MarkerToggleButton.onClick.AddListener(ToggleMarkerDrawer);
        m_SubmitButton = m_Theme.CreateSplitButton(row2.transform, "提交", 0.666f, 1f, m_Theme.StylePrimaryButton, pad, 4f, font);
        m_SubmitButton.onClick.AddListener(OnSubmitTaskClicked);
    }

    GameObject MakeBottomRow(Transform parent, string name, float yFromTop)
    {
        var row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var rt = row.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, yFromTop);
        rt.sizeDelta = new Vector2(0f, InspectUiTheme.CompactRowH);
        return row;
    }

    void BuildToast(Transform canvas)
    {
        m_Toast = new GameObject("Toast", typeof(RectTransform), typeof(CanvasGroup));
        m_Toast.transform.SetParent(canvas, false);
        m_ToastRt = m_Toast.GetComponent<RectTransform>();
        m_ToastGroup = m_Toast.GetComponent<CanvasGroup>();
        m_ToastGroup.blocksRaycasts = false;
        m_ToastGroup.interactable = false;
        m_ToastGroup.alpha = 1f;
        var imgGo = new GameObject("Bg", typeof(RectTransform), typeof(Image));
        imgGo.transform.SetParent(m_Toast.transform, false);
        InspectUiTheme.StretchFull(imgGo.GetComponent<RectTransform>());
        m_ToastImage = imgGo.GetComponent<Image>();
        m_Theme.StyleGlass(m_ToastImage, m_Theme.Primary, InspectUiTheme.GlassMaxA, false);
        m_ToastText = m_Theme.CreateAnchoredText(m_Toast.transform, "ToastText", "",
            Vector2.zero, Vector2.one, 22, m_Theme.OnPrimary, TextAnchor.MiddleCenter);
        m_Toast.SetActive(false);
    }

    void BuildMarkerDrawer(Transform canvas)
    {
        m_Drawer = m_Theme.CreateGlassBar(canvas, "MarkerDrawer", true);
        m_DrawerRt = m_Drawer.GetComponent<RectTransform>();
        m_DrawerOpen = false;
        m_Drawer.SetActive(false);

        var header = new GameObject("DrawerHeader", typeof(RectTransform));
        header.transform.SetParent(m_Drawer.transform, false);
        var hRt = header.GetComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0f, 1f);
        hRt.anchorMax = new Vector2(1f, 1f);
        hRt.pivot = new Vector2(0.5f, 1f);
        hRt.anchoredPosition = Vector2.zero;
        hRt.sizeDelta = new Vector2(0f, 48f);
        var hy = -8f;
        m_Theme.CreateLabel(header.transform, "本任务标记", ref hy, 24, m_Theme.OnSecondary);

        var scroll = new GameObject("ListScroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask));
        scroll.transform.SetParent(m_Drawer.transform, false);
        var sRt = scroll.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0f, 0.42f);
        sRt.anchorMax = new Vector2(1f, 1f);
        sRt.offsetMin = new Vector2(8f, 4f);
        sRt.offsetMax = new Vector2(-8f, -52f);
        m_Theme.StyleGlass(scroll.GetComponent<Image>(), m_Theme.BgCoolGray, InspectUiTheme.GlassCoolA, true);
        var vp = new GameObject("Viewport", typeof(RectTransform));
        vp.transform.SetParent(scroll.transform, false);
        InspectUiTheme.StretchFull(vp.GetComponent<RectTransform>());
        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(vp.transform, false);
        var cRt = content.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0f, 1f);
        cRt.anchorMax = new Vector2(1f, 1f);
        cRt.pivot = new Vector2(0.5f, 1f);
        cRt.sizeDelta = new Vector2(0f, 20f);
        m_MarkerListContent = content.transform;
        var sr = scroll.GetComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.viewport = vp.GetComponent<RectTransform>();
        sr.content = cRt;
        sr.movementType = ScrollRect.MovementType.Clamped;

        m_EditorCard = new GameObject("EditorScroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask));
        m_EditorCard.transform.SetParent(m_Drawer.transform, false);
        var edRt = m_EditorCard.GetComponent<RectTransform>();
        edRt.anchorMin = new Vector2(0f, 0f);
        edRt.anchorMax = new Vector2(1f, 0.42f);
        edRt.offsetMin = new Vector2(8f, 8f);
        edRt.offsetMax = new Vector2(-8f, -4f);
        m_Theme.StyleGlass(m_EditorCard.GetComponent<Image>(), m_Theme.BgWarmGray, InspectUiTheme.GlassCoolA, true);
        var edVp = new GameObject("Viewport", typeof(RectTransform));
        edVp.transform.SetParent(m_EditorCard.transform, false);
        InspectUiTheme.StretchFull(edVp.GetComponent<RectTransform>());
        var edContent = new GameObject("Content", typeof(RectTransform));
        edContent.transform.SetParent(edVp.transform, false);
        var edCRt = edContent.GetComponent<RectTransform>();
        edCRt.anchorMin = new Vector2(0f, 1f);
        edCRt.anchorMax = new Vector2(1f, 1f);
        edCRt.pivot = new Vector2(0.5f, 1f);
        var ey = -8f;
        m_Theme.CreateLabel(edContent.transform, "编辑选中标记", ref ey, 22, m_Theme.OnSecondary);
        m_UrlField = m_Theme.CreateInput(edContent.transform, "后端地址 http://电脑局域网IP:8080", LoadBackendUrl(), ref ey);
        m_TitleField = m_Theme.CreateInput(edContent.transform, "标题（必填）", "", ref ey);
        m_DescField = m_Theme.CreateInput(edContent.transform, "描述（可选）", "", ref ey);
        m_TitleField.onValueChanged.AddListener(OnTitleChanged);
        m_DescField.onValueChanged.AddListener(_ => WriteEditorToSelected());
        CreatePriorityRow(edContent.transform, ref ey);
        var abandon = m_Theme.CreateDanger(edContent.transform, "放弃任务", ref ey);
        abandon.onClick.AddListener(OnAbandonTask);
        var realign = m_Theme.CreateSecondary(edContent.transform, "重新对准", ref ey);
        realign.onClick.AddListener(OnRealignClicked);
        edCRt.sizeDelta = new Vector2(0f, Mathf.Abs(ey) + 16f);
        var edSr = m_EditorCard.GetComponent<ScrollRect>();
        edSr.horizontal = false;
        edSr.vertical = true;
        edSr.viewport = edVp.GetComponent<RectTransform>();
        edSr.content = edCRt;
        edSr.movementType = ScrollRect.MovementType.Clamped;
    }

    void ApplyLayout()
    {
        var landscape = Screen.width > Screen.height;
        if (m_Scaler != null)
        {
            m_Scaler.referenceResolution = landscape ? new Vector2(1920, 1080) : new Vector2(1080, 1920);
            m_Scaler.matchWidthOrHeight = landscape ? 0f : 1f;
        }

        var scale = m_Canvas != null ? m_Canvas.scaleFactor : 1f;
        if (scale <= 0.01f)
            scale = 1f;
        var sa = Screen.safeArea;
        var topPx = Mathf.Max(8f, (Screen.height - sa.yMax) / scale);
        var botPx = Mathf.Max(8f, sa.yMin / scale);

        if (landscape)
        {
            SetRect(m_TopBarRt, 0f, 0.90f, 0.46f, 1f, new Vector2(8f, 0f), new Vector2(-8f, 0f));
            SetRect(m_BottomBarRt, 0f, 0f, 1f, 0.20f, Vector2.zero, Vector2.zero);
            SetRect(m_ToastRt, 0.48f, 0.82f, 0.98f, 0.89f, Vector2.zero, Vector2.zero);
            SetRect(m_DrawerRt, 0.58f, 0.22f, 0.98f, 0.88f, Vector2.zero, Vector2.zero);
        }
        else
        {
            SetRect(m_TopBarRt, 0f, 0.92f, 1f, 1f, Vector2.zero, Vector2.zero);
            SetRect(m_BottomBarRt, 0f, 0f, 1f, 0.22f, Vector2.zero, Vector2.zero);
            SetRect(m_ToastRt, 0.15f, 0.84f, 0.98f, 0.91f, Vector2.zero, Vector2.zero);
            SetRect(m_DrawerRt, 0.62f, 0.24f, 0.98f, 0.90f, Vector2.zero, Vector2.zero);
        }

        PadInner(m_Hud, topPx, 0f);
        PadInner(m_BottomBar, 0f, botPx);
    }

    static void SetRect(RectTransform rt, float minX, float minY, float maxX, float maxY, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (rt == null)
            return;
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    static void PadInner(GameObject bar, float topPx, float botPx)
    {
        if (bar == null)
            return;
        var inner = bar.transform.Find("TopInner") ?? bar.transform.Find("BottomInner");
        if (inner == null)
            return;
        var rt = inner.GetComponent<RectTransform>();
        rt.offsetMin = new Vector2(8f, botPx);
        rt.offsetMax = new Vector2(-8f, -topPx);
    }

    void OnHistoryClicked()
    {
        if (!HasJwt())
        {
            SetStatus("请先登录。", true);
            ApplySessionUi();
            return;
        }

        m_History.Show();
    }

    void ToggleMarkerDrawer()
    {
        if (!HasJwt())
        {
            SetStatus("请先登录。", true);
            ApplySessionUi();
            return;
        }

        if (!m_Task.Active)
        {
            SetStatus("请先「新建任务」。", true);
            return;
        }

        m_DrawerOpen = !m_DrawerOpen;
        ApplyTaskUi();
    }

    void CreatePriorityRow(Transform parent, ref float y)
    {
        m_Theme.CreateLabel(parent, "优先级", ref y, 22, m_Theme.OnSecondary);
        var row = new GameObject("PriorityRow", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var rt = row.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(-32f, 48f);
        y -= 48f + InspectUiTheme.Gap;
        var labels = new[] { "low", "medium", "high" };
        m_PriorityButtons = new Button[labels.Length];
        for (var i = 0; i < labels.Length; i++)
        {
            var label = labels[i];
            var btn = m_Theme.CreateSplitButton(row.transform, label, i / 3f, (i + 1) / 3f, m_Theme.StyleSecondaryButton);
            var captured = label;
            btn.onClick.AddListener(() => SetSelectedPriority(captured));
            m_PriorityButtons[i] = btn;
        }
    }

    void BuildConfirmOverlay(Transform canvas)
    {
        m_ConfirmPanel = new GameObject("SubmitConfirm", typeof(RectTransform), typeof(Image));
        m_ConfirmPanel.transform.SetParent(canvas, false);
        InspectUiTheme.StretchFull(m_ConfirmPanel.GetComponent<RectTransform>());
        var dim = m_ConfirmPanel.GetComponent<Image>();
        m_Theme.StyleDim(dim, m_Theme.BgCoolGray, InspectUiTheme.GlassMaxA);
        var card = m_Theme.CreateCard(m_ConfirmPanel.transform, "ConfirmCard", InspectUiTheme.WithAlpha(m_Theme.BgWarmGray, InspectUiTheme.GlassPanelA));
        var cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.06f, 0.18f);
        cardRt.anchorMax = new Vector2(0.94f, 0.82f);
        cardRt.offsetMin = Vector2.zero;
        cardRt.offsetMax = Vector2.zero;
        var y = -16f;
        m_ConfirmTitle = m_Theme.CreateLabel(card.transform, "确认发送本任务？", ref y, 30, m_Theme.OnSecondary);
        m_ConfirmSummary = m_Theme.CreateLabel(card.transform, "", ref y, 22, m_Theme.OnSecondary);
        m_ConfirmSummary.horizontalOverflow = HorizontalWrapMode.Wrap;
        m_ConfirmSummary.verticalOverflow = VerticalWrapMode.Overflow;
        var sumRt = m_ConfirmSummary.rectTransform;
        sumRt.sizeDelta = new Vector2(sumRt.sizeDelta.x, 280f);
        y -= 220f;
        var send = m_Theme.CreatePrimary(card.transform, "确认发送", ref y);
        m_ConfirmSendLabel = send.GetComponentInChildren<Text>();
        send.onClick.AddListener(OnConfirmPrimaryClicked);
        var cancel = m_Theme.CreateSecondary(card.transform, "取消", ref y);
        cancel.onClick.AddListener(OnConfirmCancelClicked);
        ShowSubmitConfirm(false);
    }

    void BuildLoginOverlay(Transform canvas)
    {
        m_LoginPanel = new GameObject("LoginOverlay", typeof(RectTransform), typeof(Image));
        m_LoginPanel.transform.SetParent(canvas, false);
        InspectUiTheme.StretchFull(m_LoginPanel.GetComponent<RectTransform>());
        var dim = m_LoginPanel.GetComponent<Image>();
        m_Theme.StyleDim(dim, m_Theme.BgCoolGray, InspectUiTheme.GlassMaxA);
        var card = m_Theme.CreateCard(m_LoginPanel.transform, "LoginCard", InspectUiTheme.WithAlpha(m_Theme.BgWarmGray, InspectUiTheme.GlassPanelA));
        var cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.08f, 0.22f);
        cardRt.anchorMax = new Vector2(0.92f, 0.82f);
        cardRt.offsetMin = Vector2.zero;
        cardRt.offsetMax = Vector2.zero;
        var y = -20f;
        m_Theme.CreateLabel(card.transform, "巡检登录", ref y, 32, m_Theme.OnSecondary);
        m_LoginUrlField = m_Theme.CreateInput(card.transform, "后端地址 http://电脑局域网IP:8080", LoadBackendUrl(), ref y);
        m_UserField = m_Theme.CreateInput(card.transform, "用户名", "inspector", ref y);
        m_PassField = m_Theme.CreateInput(card.transform, "密码", "", ref y);
        m_PassField.contentType = InputField.ContentType.Password;
        m_PassField.text = "";
        var login = m_Theme.CreatePrimary(card.transform, "登录", ref y);
        login.onClick.AddListener(OnLoginClicked);
        m_LoginStatusText = m_Theme.CreateLabel(card.transform, "请使用 inspector / inspect123。", ref y, 22, m_Theme.OnSecondary);
    }

    void ShowSubmitConfirm(bool show)
    {
        if (m_ConfirmPanel != null)
            m_ConfirmPanel.SetActive(show);
    }

    void OnConfirmPrimaryClicked()
    {
        ShowSubmitConfirm(false);
        if (m_ConfirmIsRealign)
            DoRealign();
        else
            StartCoroutine(PostAllUnsubmitted());
    }

    void OnConfirmCancelClicked()
    {
        ShowSubmitConfirm(false);
        SetStatus(m_ConfirmIsRealign ? "已取消对准。" : "已取消发送，可继续改标记。", false);
    }

    void OnRealignClicked()
    {
        m_ConfirmIsRealign = true;
        if (m_ConfirmTitle != null)
            m_ConfirmTitle.text = "重新对准？";
        if (m_ConfirmSummary != null)
            m_ConfirmSummary.text = "会清除未提交标记和已有锚点，并重置 AR 会话。确定？";
        if (m_ConfirmSendLabel != null)
            m_ConfirmSendLabel.text = "确定对准";
        ShowSubmitConfirm(true);
    }

    void DoRealign()
    {
        if (m_Scanning)
            SetScanning(false);
        UnlockFloor();
        m_Task.AbandonUnsubmitted();
        m_DrawerOpen = false;
        ApplyTaskUi();
        var session = FindFirstObjectByType<ARSession>();
        if (session != null)
            session.Reset();
        SetStatus("已重新对准。请再点「开始扫描」。光线要足，地面要有纹理。", false);
    }

    void ApplySessionUi()
    {
        var loggedIn = HasJwt();
        if (m_LoginPanel != null)
            m_LoginPanel.SetActive(!loggedIn);
        if (m_Hud != null)
            m_Hud.SetActive(loggedIn);
        if (m_BottomBar != null)
            m_BottomBar.SetActive(loggedIn);
        if (!loggedIn)
        {
            SetScanning(false);
            ShowSubmitConfirm(false);
            m_DrawerOpen = false;
            if (m_Drawer != null)
                m_Drawer.SetActive(false);
            if (m_History != null && m_History.IsOpen)
                m_History.Hide();
        }

        ApplyTaskUi();
    }

    void ApplyTaskUi()
    {
        if (m_Drawer != null)
            m_Drawer.SetActive(HasJwt() && m_Task.Active && m_DrawerOpen);
        if (m_NewTaskButton != null)
            m_NewTaskButton.interactable = HasJwt() && (!m_Task.Active || m_Task.Locked);
        if (m_SubmitButton != null)
            m_SubmitButton.interactable = HasJwt() && m_Task.HasUnsubmitted && !m_Submitting;
        RefreshScanButtons();
        RefreshMarkerListUi();
        FillEditorFromSelected();
        RefreshTopBar();
        RefreshMarkerToggleLabel();
        if (m_TitleField != null)
            m_TitleField.interactable = m_Task.CanPlace;
        if (m_DescField != null)
            m_DescField.interactable = m_Task.CanPlace;
    }

    void RefreshTopBar()
    {
        if (m_UserNameText != null)
            m_UserNameText.text = LoadUserName();
        if (m_TaskStateText == null)
            return;
        if (!m_Task.Active)
            m_TaskStateText.text = "未建任务";
        else if (m_Scanning)
            m_TaskStateText.text = "扫描中";
        else
            m_TaskStateText.text = "暂停中";
        if (!m_Scanning && m_FeatureHintText != null)
            m_FeatureHintText.text = "";
    }

    void RefreshMarkerToggleLabel()
    {
        if (m_MarkerToggleLabel == null)
            return;
        var n = m_Task.Markers.Count;
        m_MarkerToggleLabel.text = n > 0 ? "标记 " + n : "标记";
    }

    static string LoadUserName()
    {
        var name = PlayerPrefs.GetString(PlayerPrefsUserNameKey, "");
        if (!string.IsNullOrEmpty(name))
            return name;
        var role = PlayerPrefs.GetString(PlayerPrefsRoleKey, "");
        return string.IsNullOrEmpty(role) ? "已登录" : role;
    }

    void OnNewTask()
    {
        if (m_Task.Active && m_Task.HasUnsubmitted)
        {
            SetStatus("请先提交或放弃当前任务。", true);
            return;
        }

        m_Task.BeginNew();
        m_DrawerOpen = false;
        UnlockFloor();
        ApplyTaskUi();
        SetStatus("任务已创建。开始扫描并点击地面放置多个标记。", false);
    }

    void OnAbandonTask()
    {
        if (m_Scanning)
            SetScanning(false);
        UnlockFloor();
        m_Task.AbandonUnsubmitted();
        m_DrawerOpen = false;
        ApplyTaskUi();
        SetStatus("已放弃未提交标记。", false);
    }

    void RefreshMarkerListUi()
    {
        if (m_MarkerListContent == null)
            return;
        for (var i = m_MarkerListContent.childCount - 1; i >= 0; i--)
            Destroy(m_MarkerListContent.GetChild(i).gameObject);

        float y = -8f;
        for (var i = 0; i < m_Task.Markers.Count; i++)
        {
            var marker = m_Task.Markers[i];
            if (marker == null)
                continue;
            y = AddMarkerRow(marker, y);
        }

        var cRt = m_MarkerListContent.GetComponent<RectTransform>();
        cRt.sizeDelta = new Vector2(0f, Mathf.Max(40f, Mathf.Abs(y) + 16f));
        RefreshMarkerToggleLabel();
    }

    float AddMarkerRow(DraftMarker marker, float y)
    {
        var selected = m_Task.Selected == marker;
        var rowBg = selected
            ? InspectUiTheme.WithAlpha(m_Theme.BgCoolGray, InspectUiTheme.GlassPanelA)
            : InspectUiTheme.WithAlpha(m_Theme.BgWarmGray, InspectUiTheme.GlassPanelA);
        var row = m_Theme.CreateCard(m_MarkerListContent, marker.localId, rowBg);
        var rt = row.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(-8f, 72f);
        y -= 72f + InspectUiTheme.Gap;

        var p = marker.cube != null ? marker.cube.transform.position : marker.position;
        marker.position = p;
        var tag = marker.submitted ? "已提交" : "未提交";
        var innerY = -6f;
        m_Theme.CreateLabel(row.transform, $"{marker.title}  [{tag}]", ref innerY, 22, m_Theme.OnSecondary);
        m_Theme.CreateLabel(row.transform, $"X {p.x:F2}  Y {p.y:F2}  Z {p.z:F2}", ref innerY, 18, m_Theme.OnSecondary);

        var hit = row.GetComponent<Button>() ?? row.AddComponent<Button>();
        InspectUiTheme.StyleColorTint(hit);
        var captured = marker;
        hit.onClick.AddListener(() =>
        {
            if (m_Task.Locked)
                return;
            m_Task.Selected = captured;
            FillEditorFromSelected();
            RefreshMarkerListUi();
        });

        if (!marker.submitted && !m_Task.Locked)
        {
            var del = m_Theme.CreateSplitButton(row.transform, "删除", 0.72f, 1f, m_Theme.StyleDangerButton);
            del.onClick.AddListener(() =>
            {
                m_Task.RemoveUnsubmitted(captured);
                RefreshMarkerListUi();
                FillEditorFromSelected();
                SetStatus("已删除本地标记。", false);
            });
        }

        return y;
    }

    void FillEditorFromSelected()
    {
        m_SyncingEditor = true;
        var m = m_Task.Selected;
        if (m_TitleField != null)
            m_TitleField.text = m != null ? (m.title ?? "") : "";
        if (m_DescField != null)
            m_DescField.text = m != null ? (m.description ?? "") : "";
        RefreshPriorityButtons(m != null ? m.priority : "medium");
        m_SyncingEditor = false;
        ApplyTitleKeywordRules();
    }

    void OnTitleChanged(string _)
    {
        if (m_SyncingEditor)
            return;
        ApplyTitleKeywordRules();
        WriteEditorToSelected();
    }

    void WriteEditorToSelected()
    {
        if (m_SyncingEditor || m_Task.Locked)
            return;
        var m = m_Task.Selected;
        if (m == null || m.submitted)
            return;
        if (m_TitleField != null)
            m.title = m_TitleField.text;
        if (m_DescField != null)
            m.description = m_DescField.text;
    }

    void ApplyTitleKeywordRules()
    {
        var m = m_Task.Selected;
        var title = m_TitleField != null ? m_TitleField.text : (m != null ? m.title : "");
        var hit = TitleHasKeyword(title);
        var canEdit = m != null && !m.submitted && m_Task.CanPlace;
        if (canEdit && hit)
        {
            m.priority = "high";
            ApplyMarkerColor(m.cube, "high", "open");
            AppendSystemHint(m);
            if (m_DescField != null && m_DescField.text != m.description)
            {
                m_SyncingEditor = true;
                m_DescField.text = m.description ?? "";
                m_SyncingEditor = false;
            }
        }

        RefreshPriorityButtons(m != null ? m.priority : "medium");
        SetPriorityButtonsInteractable(canEdit && !hit);
    }

    static bool TitleHasKeyword(string title)
    {
        if (string.IsNullOrEmpty(title))
            return false;
        for (var i = 0; i < TitleKeywords.Length; i++)
        {
            if (title.IndexOf(TitleKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    static void AppendSystemHint(DraftMarker marker)
    {
        if (marker == null)
            return;
        var desc = marker.description ?? "";
        if (desc.IndexOf(SystemHint, StringComparison.Ordinal) >= 0)
            return;
        marker.description = string.IsNullOrEmpty(desc) ? SystemHint : desc + "\n" + SystemHint;
    }

    void SetSelectedPriority(string priority)
    {
        if (m_Task.Locked)
            return;
        var m = m_Task.Selected;
        if (m == null || m.submitted)
            return;
        var title = m_TitleField != null ? m_TitleField.text : m.title;
        if (TitleHasKeyword(title))
            return;
        m.priority = priority;
        ApplyMarkerColor(m.cube, priority, "open");
        RefreshPriorityButtons(priority);
        RefreshMarkerListUi();
    }

    void RefreshPriorityButtons(string current)
    {
        if (m_PriorityButtons == null)
            return;
        var labels = new[] { "low", "medium", "high" };
        for (var i = 0; i < m_PriorityButtons.Length; i++)
        {
            var img = m_PriorityButtons[i].GetComponent<Image>();
            var label = m_PriorityButtons[i].GetComponentInChildren<Text>();
            if (labels[i] == current)
                m_Theme.StylePrimaryButton(m_PriorityButtons[i], img, label);
            else
                m_Theme.StyleSecondaryButton(m_PriorityButtons[i], img, label);
        }
    }

    void SetPriorityButtonsInteractable(bool on)
    {
        if (m_PriorityButtons == null)
            return;
        for (var i = 0; i < m_PriorityButtons.Length; i++)
            m_PriorityButtons[i].interactable = on;
    }

    void OnSubmitTaskClicked()
    {
        if (m_Submitting)
            return;
        if (!HasJwt())
        {
            SetStatus("提交失败：请先登录。", true);
            ApplySessionUi();
            return;
        }

        WriteEditorToSelected();
        m_Task.CapturePositions();
        var pending = m_Task.Unsubmitted();
        if (pending.Count == 0)
        {
            SetStatus("提交失败：没有未提交的标记。", true);
            return;
        }

        for (var i = 0; i < pending.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(pending[i].title))
            {
                SetStatus($"提交失败：第 {i + 1} 条标题不能为空。", true);
                return;
            }
        }

        SaveBackendUrl();
        var sb = new StringBuilder();
        sb.AppendLine("共 " + pending.Count + " 个标记：");
        for (var i = 0; i < pending.Count; i++)
        {
            var m = pending[i];
            var p = m.position;
            sb.AppendLine($"{i + 1}. {m.title}");
            sb.AppendLine($"X 坐标：{p.x:F2}");
            sb.AppendLine($"Y 坐标：{p.y:F2}");
            sb.AppendLine($"Z 坐标：{p.z:F2}");
        }

        if (m_ConfirmSummary != null)
            m_ConfirmSummary.text = sb.ToString();
        m_ConfirmIsRealign = false;
        if (m_ConfirmTitle != null)
            m_ConfirmTitle.text = "确认发送本任务？";
        if (m_ConfirmSendLabel != null)
            m_ConfirmSendLabel.text = "确认发送";
        ShowSubmitConfirm(true);
    }

    IEnumerator PostAllUnsubmitted()
    {
        m_Submitting = true;
        if (m_Scanning)
            SetScanning(false);
        var pending = m_Task.Unsubmitted();
        var baseUrl = CurrentBaseUrl();
        for (var i = 0; i < pending.Count; i++)
        {
            SetStatus($"正在提交 {i + 1}/{pending.Count}…", false);
            yield return PostOne(baseUrl, pending[i]);
            if (!pending[i].submitted)
            {
                SetStatus($"第 {i + 1} 条「{pending[i].title}」提交失败，已成功的已锁定，其余仍可改。", true);
                m_Submitting = false;
                RefreshMarkerListUi();
                yield break;
            }
        }

        m_Task.LockAfterAllPosted();
        ApplyTaskUi();
        SetStatus("全部提交成功。可查看历史，或新建下一任务。", false);
        m_Submitting = false;
    }

    IEnumerator PostOne(string baseUrl, DraftMarker marker)
    {
        var p = marker.cube != null ? marker.cube.transform.position : marker.position;
        marker.position = p;
        var body = new InspectIssueRequest
        {
            title = (marker.title ?? "").Trim(),
            description = marker.description ?? "",
            priority = string.IsNullOrEmpty(marker.priority) ? "medium" : marker.priority,
            position = new InspectPositionDto { x = p.x, y = p.y, z = p.z }
        };
        using (var req = new UnityWebRequest(baseUrl + "/api/issues", UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            AttachAuth(req);
            req.timeout = 15;
            yield return req.SendWebRequest();
            if (req.responseCode == 401)
            {
                HandleUnauthorized();
                yield break;
            }

            if (req.result == UnityWebRequest.Result.Success && req.responseCode == 201)
            {
                marker.submitted = true;
                try
                {
                    var created = JsonUtility.FromJson<InspectIssueDto>(req.downloadHandler.text);
                    if (created != null && !string.IsNullOrEmpty(created.id))
                    {
                        marker.issueId = created.id;
                        m_MarkersById[created.id] = marker.cube;
                        ApplyMarkerColor(marker.cube, created.priority, created.status);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[InspectAR] 解析提交响应失败: " + ex.Message);
                }
            }
            else
            {
                var err = TryReadError(req.downloadHandler != null ? req.downloadHandler.text : "");
                Debug.LogWarning("[InspectAR] POST 失败 " + req.responseCode + " " + err + " " + req.error);
            }
        }
    }

    public void StartHistoryLoad()
    {
        StartCoroutine(LoadHistory());
    }

    public void StartHistorySave(string id, string title, string description, string priority)
    {
        StartCoroutine(PutHistory(id, title, description, priority));
    }

    public bool CanEditIssue(InspectIssueDto issue)
    {
        if (issue == null || !HasJwt())
            return false;
        if (UserRole == "admin")
            return true;
        if (UserRole == "inspector")
            return issue.submitterId == UserId;
        return false;
    }

    IEnumerator LoadHistory()
    {
        if (!HasJwt())
        {
            HandleUnauthorized();
            yield break;
        }

        using (var req = UnityWebRequest.Get(CurrentBaseUrl() + "/api/issues"))
        {
            req.timeout = 15;
            AttachAuth(req);
            yield return req.SendWebRequest();
            if (req.responseCode == 401)
            {
                HandleUnauthorized();
                yield break;
            }

            if (req.result != UnityWebRequest.Result.Success || req.responseCode != 200)
            {
                m_History.Render(null, "加载失败：" + (req.error ?? req.responseCode.ToString()));
                yield break;
            }

            try
            {
                var list = JsonUtility.FromJson<InspectIssueListDto>(req.downloadHandler.text);
                var issues = list != null ? list.issues : null;
                SyncMarkersFromList(issues);
                m_History.Render(issues, issues == null || issues.Length == 0 ? "暂无记录。" : "共 " + issues.Length + " 条。");
            }
            catch (Exception ex)
            {
                m_History.Render(null, "解析失败：" + ex.Message);
            }
        }
    }

    IEnumerator PutHistory(string id, string title, string description, string priority)
    {
        if (!HasJwt())
        {
            HandleUnauthorized();
            yield break;
        }

        title = (title ?? "").Trim();
        if (string.IsNullOrEmpty(title))
        {
            SetStatus("保存失败：标题不能为空。", true);
            yield break;
        }

        var body = new InspectPutRequest
        {
            title = title,
            description = description ?? "",
            priority = string.IsNullOrEmpty(priority) ? "medium" : priority
        };
        using (var req = new UnityWebRequest(CurrentBaseUrl() + "/api/issues/" + id, "PUT"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            AttachAuth(req);
            req.timeout = 15;
            yield return req.SendWebRequest();
            if (req.responseCode == 401)
            {
                HandleUnauthorized();
                yield break;
            }

            if (req.responseCode == 403)
            {
                SetStatus("保存失败：没有权限。", true);
                yield break;
            }

            if (req.result == UnityWebRequest.Result.Success && req.responseCode == 200)
            {
                SetStatus("已保存。", false);
                yield return LoadHistory();
            }
            else
            {
                var err = TryReadError(req.downloadHandler != null ? req.downloadHandler.text : "");
                SetStatus("保存失败：" + (string.IsNullOrEmpty(err) ? req.responseCode.ToString() : err), true);
            }
        }
    }

    void OnLoginClicked()
    {
        if (m_LoggingIn)
            return;
        var user = m_UserField != null ? m_UserField.text.Trim() : "";
        var pass = m_PassField != null ? m_PassField.text : "";
        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        {
            SetStatus("登录失败：请填写用户名和密码。", true);
            return;
        }

        if (m_LoginUrlField != null && m_UrlField != null)
            m_UrlField.text = m_LoginUrlField.text;
        SaveBackendUrl();
        StartCoroutine(PostLogin(user, pass));
    }

    IEnumerator PostLogin(string username, string password)
    {
        m_LoggingIn = true;
        SetStatus("正在登录…", false);
        var baseUrl = CurrentBaseUrl();
        var json = JsonUtility.ToJson(new InspectLoginRequest { username = username, password = password });
        using (var req = new UnityWebRequest(baseUrl + "/api/auth/login", UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 15;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success && req.responseCode == 200)
            {
                var body = JsonUtility.FromJson<InspectLoginResponse>(req.downloadHandler.text);
                if (body == null || string.IsNullOrEmpty(body.token))
                {
                    SetStatus("登录失败：响应没有 token。", true);
                }
                else
                {
                    PlayerPrefs.SetString(PlayerPrefsJwtKey, body.token);
                    if (body.user != null)
                    {
                        PlayerPrefs.SetString(PlayerPrefsUserIdKey, body.user.id ?? "");
                        PlayerPrefs.SetString(PlayerPrefsRoleKey, body.user.role ?? "");
                        PlayerPrefs.SetString(PlayerPrefsUserNameKey, body.user.username ?? "");
                    }

                    PlayerPrefs.Save();
                    ApplySessionUi();
                    SetStatus("登录成功。点「新建任务」开始。", false);
                }
            }
            else
            {
                var serverError = TryReadError(req.downloadHandler != null ? req.downloadHandler.text : "");
                if (!string.IsNullOrEmpty(serverError))
                    SetStatus("登录失败：" + serverError, true);
                else if (req.result != UnityWebRequest.Result.Success)
                    SetStatus($"登录失败：网络错误 {req.error}。地址 {baseUrl}", true);
                else
                    SetStatus("登录失败：HTTP " + req.responseCode, true);
            }
        }

        m_LoggingIn = false;
    }

    IEnumerator PollIssuesLoop()
    {
        yield return new WaitForSeconds(IssuePollStartDelay);
        while (true)
        {
            if (HasJwt())
                yield return FetchAndSyncIssues(true);
            yield return new WaitForSeconds(IssuePollSeconds);
        }
    }

    IEnumerator FetchAndSyncIssues(bool silent)
    {
        if (!HasJwt())
            yield break;
        using (var req = UnityWebRequest.Get(CurrentBaseUrl() + "/api/issues"))
        {
            req.timeout = 15;
            AttachAuth(req);
            yield return req.SendWebRequest();
            if (req.responseCode == 401)
            {
                HandleUnauthorized();
                yield break;
            }

            if (req.result != UnityWebRequest.Result.Success || req.responseCode != 200)
            {
                if (!silent)
                    SetStatus("刷新失败。", true);
                yield break;
            }

            try
            {
                var list = JsonUtility.FromJson<InspectIssueListDto>(req.downloadHandler.text);
                SyncMarkersFromList(list != null ? list.issues : null);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[InspectAR] 解析列表失败: " + ex.Message);
            }
        }
    }

    void SyncMarkersFromList(InspectIssueDto[] issues)
    {
        if (issues == null)
            return;
        for (var i = 0; i < issues.Length; i++)
        {
            var issue = issues[i];
            if (issue == null || string.IsNullOrEmpty(issue.id))
                continue;
            if (m_MarkersById.TryGetValue(issue.id, out var existing) && existing != null)
            {
                ApplyMarkerColor(existing, issue.priority, issue.status);
                continue;
            }

            var spawned = SpawnIssueMarker(issue);
            m_MarkersById[issue.id] = spawned;
        }
    }

    GameObject SpawnIssueMarker(InspectIssueDto issue)
    {
        var go = CreateMarker();
        go.name = "IssueMarker_" + issue.id;
        var pos = issue.position;
        go.transform.position = pos != null ? new Vector3(pos.x, pos.y, pos.z) : Vector3.zero;
        go.transform.localScale = Vector3.one * 0.12f;
        ApplyMarkerColor(go, issue.priority, issue.status);
        return go;
    }

    static string TryReadError(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        try
        {
            var err = JsonUtility.FromJson<InspectErrorDto>(text);
            return err != null ? err.error : "";
        }
        catch
        {
            return "";
        }
    }

    public void AttachAuth(UnityWebRequest req)
    {
        var jwt = LoadJwt();
        if (!string.IsNullOrEmpty(jwt))
            req.SetRequestHeader("Authorization", "Bearer " + jwt);
    }

    void HandleUnauthorized()
    {
        PlayerPrefs.DeleteKey(PlayerPrefsJwtKey);
        PlayerPrefs.DeleteKey(PlayerPrefsUserIdKey);
        PlayerPrefs.DeleteKey(PlayerPrefsRoleKey);
        PlayerPrefs.DeleteKey(PlayerPrefsUserNameKey);
        PlayerPrefs.Save();
        if (m_History != null)
            m_History.Hide();
        ApplySessionUi();
        SetStatus("登录已过期，请重新登录。", true);
    }

    public static bool HasJwt()
    {
        return !string.IsNullOrEmpty(LoadJwt());
    }

    static string LoadJwt()
    {
        return PlayerPrefs.GetString(PlayerPrefsJwtKey, "");
    }

    void SaveBackendUrl()
    {
        var fromLogin = m_LoginPanel != null && m_LoginPanel.activeSelf && m_LoginUrlField != null;
        var url = NormalizeBaseUrl(fromLogin
            ? m_LoginUrlField.text
            : (m_UrlField != null ? m_UrlField.text : m_DefaultBackendUrl));
        PlayerPrefs.SetString(PlayerPrefsUrlKey, url);
        PlayerPrefs.Save();
        if (m_UrlField != null)
            m_UrlField.text = url;
        if (m_LoginUrlField != null)
            m_LoginUrlField.text = url;
    }

    string LoadBackendUrl()
    {
        var stored = PlayerPrefs.GetString(PlayerPrefsUrlKey, "");
        return string.IsNullOrEmpty(stored) ? m_DefaultBackendUrl : stored;
    }

    public string CurrentBaseUrl()
    {
        if (m_LoginPanel != null && m_LoginPanel.activeSelf && m_LoginUrlField != null && !string.IsNullOrWhiteSpace(m_LoginUrlField.text))
            return NormalizeBaseUrl(m_LoginUrlField.text);
        if (m_UrlField != null && !string.IsNullOrWhiteSpace(m_UrlField.text))
            return NormalizeBaseUrl(m_UrlField.text);
        return NormalizeBaseUrl(LoadBackendUrl());
    }

    static string NormalizeBaseUrl(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";
        return raw.Trim().TrimEnd('/');
    }

    void SetStatus(string message, bool isError)
    {
        if (m_LoginStatusText != null && m_LoginPanel != null && m_LoginPanel.activeSelf)
        {
            m_LoginStatusText.text = message;
            m_LoginStatusText.color = isError ? m_Theme.OnDanger : m_Theme.OnSecondary;
        }

        ShowToast(message, isError);
    }

    void ShowToast(string message, bool isError)
    {
        if (m_Toast == null || m_Theme == null)
        {
            Debug.Log("[InspectAR] " + message);
            return;
        }

        if (m_ToastCo != null)
        {
            StopCoroutine(m_ToastCo);
            m_ToastCo = null;
        }

        m_Toast.SetActive(true);
        m_Toast.transform.SetAsLastSibling();
        if (m_ConfirmPanel != null && m_ConfirmPanel.activeSelf)
            m_ConfirmPanel.transform.SetAsLastSibling();
        var history = m_Toast.transform.parent != null ? m_Toast.transform.parent.Find("HistoryOverlay") : null;
        if (history != null && history.gameObject.activeSelf)
            history.SetAsLastSibling();
        if (m_LoginPanel != null && m_LoginPanel.activeSelf)
            m_LoginPanel.transform.SetAsLastSibling();
        if (m_ToastGroup != null)
            m_ToastGroup.alpha = 1f;
        if (m_ToastImage != null)
        {
            var rgb = isError ? m_Theme.Danger : m_Theme.Primary;
            m_Theme.StyleGlass(m_ToastImage, rgb, InspectUiTheme.GlassMaxA, false);
        }

        if (m_ToastText != null)
        {
            m_ToastText.text = message ?? "";
            m_ToastText.color = isError ? m_Theme.OnDanger : m_Theme.OnPrimary;
        }

        m_ToastCo = StartCoroutine(ToastHideRoutine());
    }

    IEnumerator ToastHideRoutine()
    {
        yield return new WaitForSeconds(ToastSeconds);
        if (m_ToastGroup != null)
        {
            var t = 0f;
            while (t < ToastFadeSeconds)
            {
                t += Time.deltaTime;
                m_ToastGroup.alpha = 1f - Mathf.Clamp01(t / ToastFadeSeconds);
                yield return null;
            }

            m_ToastGroup.alpha = 0f;
        }

        if (m_Toast != null)
            m_Toast.SetActive(false);
        m_ToastCo = null;
    }

    static bool TryGetPress(out Vector2 screenPos)
    {
        screenPos = default;
        var touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
        {
            screenPos = touch.primaryTouch.position.ReadValue();
            return true;
        }

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            screenPos = mouse.position.ReadValue();
            return true;
        }

        return false;
    }

    static bool IsPointerOverBlockingUi(Vector2 screenPos)
    {
        if (EventSystem.current == null)
            return false;
        var data = new PointerEventData(EventSystem.current) { position = screenPos };
        s_UiHits.Clear();
        EventSystem.current.RaycastAll(data, s_UiHits);
        for (var i = 0; i < s_UiHits.Count; i++)
        {
            var go = s_UiHits[i].gameObject;
            if (go == null)
                continue;
            if (go.GetComponent<Button>() != null || go.GetComponent<InputField>() != null)
                return true;
            if (go.GetComponentInParent<Button>() != null || go.GetComponentInParent<InputField>() != null)
                return true;
            var t = go.transform;
            while (t != null)
            {
                var n = t.name;
                if (n == "TopBar" || n == "BottomBar" || n == "Toast")
                    break;
                if (n == "MarkerDrawer" || n == "HistoryOverlay" || n == "LoginOverlay" || n == "SubmitConfirm")
                    return true;
                t = t.parent;
            }
        }

        return false;
    }

    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;
        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        DontDestroyOnLoad(go);
    }
}
