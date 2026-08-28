using System;
using System.IO;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.ARCore;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARCore;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

/// <summary>
/// 装配 AR 场景、启用 ARCore Loader、对齐 Android 设置。
/// 批处理：Unity.exe -batchmode -projectPath mobile -executeMethod InspectARSetup.Run -quit
/// </summary>
public static class InspectARSetup
{
    const string ScenePath = "Assets/Scenes/InspectAR.unity";
    const string PlanePrefabPath = "Assets/InspectAR/Prefabs/ARPlane.prefab";
    const string PlaneMatPath = "Assets/InspectAR/Materials/ARPlane.mat";
    const string LogPrefix = "[InspectARSetup]";

    [MenuItem("InspectAR/Setup Project")]
    public static void Run()
    {
        try
        {
            ConfigureAndroid();
            EnableArCoreLoader();
            EnsureArCoreSettings();
            EnsureArUrp();
            BuildScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var ok = Verify(out var report);
            Debug.Log($"{LogPrefix} {report}");
            if (!ok)
                throw new Exception("InspectAR setup verification failed.\n" + report);
        }
        catch (Exception ex)
        {
            Debug.LogError($"{LogPrefix} FAIL: {ex}");
            throw;
        }
    }

    [MenuItem("InspectAR/Verify")]
    public static void VerifyMenu()
    {
        Verify(out var report);
        Debug.Log($"{LogPrefix} {report}");
    }

    [MenuItem("InspectAR/Fix URP Camera Background")]
    public static void FixUrpCameraBackgroundMenu()
    {
        EnsureArUrp();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"{LogPrefix} URP AR camera background ready (ARBackgroundRendererFeature + GLES3).");
    }

    static void EnsureArUrp()
    {
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        EnsureArBackgroundOnRenderer("Assets/Settings/Mobile_Renderer.asset");
        EnsureArBackgroundOnRenderer("Assets/Settings/PC_Renderer.asset");

        var mobileRp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
        if (mobileRp != null)
        {
            mobileRp.renderScale = 1f;
            EditorUtility.SetDirty(mobileRp);
        }
    }

    static void EnsureArBackgroundOnRenderer(string path)
    {
        var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
        if (data == null)
            return;

        var so = new SerializedObject(data);
        var nativePass = so.FindProperty("m_UseNativeRenderPass");
        if (nativePass != null)
            nativePass.boolValue = false;
        var intermediate = so.FindProperty("m_IntermediateTextureMode");
        if (intermediate != null)
            intermediate.intValue = 1;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (data.rendererFeatures != null && data.rendererFeatures.Any(f => f is ARBackgroundRendererFeature))
        {
            EditorUtility.SetDirty(data);
            return;
        }

        var feature = ScriptableObject.CreateInstance<ARBackgroundRendererFeature>();
        feature.name = "ARBackgroundRendererFeature";
        AssetDatabase.AddObjectToAsset(feature, data);
        data.rendererFeatures.Add(feature);
        EditorUtility.SetDirty(feature);
        EditorUtility.SetDirty(data);
    }

    static void ConfigureAndroid()
    {
        PlayerSettings.companyName = "Inspect";
        PlayerSettings.productName = "InspectAR";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.inspect.ar");
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.forceInternetPermission = true;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[]
        {
            GraphicsDeviceType.OpenGLES3
        });
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            Debug.Log($"{LogPrefix} active build target is {EditorUserBuildSettings.activeBuildTarget}; pass -buildTarget Android");
        }
        Debug.Log($"{LogPrefix} Android: minSdk=25 ARM64 IL2CPP HTTP allowed identifier=com.inspect.ar");
    }

    static void EnableArCoreLoader()
    {
        Directory.CreateDirectory("Assets/XR/Loaders");
        Directory.CreateDirectory("Assets/XR/Settings");

        var perTarget = LoadOrCreatePerTargetSettings();
        if (!perTarget.HasSettingsForBuildTarget(BuildTargetGroup.Android))
            perTarget.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
        if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
            perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);

        var general = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
        general.InitManagerOnStart = true;
        var manager = general.AssignedSettings;
        var assigned = XRPackageMetadataStore.AssignLoader(
            manager,
            typeof(ARCoreLoader).FullName,
            BuildTargetGroup.Android);
        if (!assigned)
        {
            var loaderPath = "Assets/XR/Loaders/ARCoreLoader.asset";
            var loader = AssetDatabase.LoadAssetAtPath<ARCoreLoader>(loaderPath);
            if (loader == null)
            {
                loader = ScriptableObject.CreateInstance<ARCoreLoader>();
                AssetDatabase.CreateAsset(loader, loaderPath);
            }

            if (!manager.activeLoaders.OfType<ARCoreLoader>().Any())
            {
                if (!manager.TryAddLoader(loader))
                    throw new Exception("Failed to add ARCoreLoader to XR manager settings.");
            }
        }

        EditorUtility.SetDirty(perTarget);
        EditorUtility.SetDirty(general);
        EditorUtility.SetDirty(manager);
        Debug.Log($"{LogPrefix} ARCore loader assigned={manager.activeLoaders.OfType<ARCoreLoader>().Any()}");
    }

    static XRGeneralSettingsPerBuildTarget LoadOrCreatePerTargetSettings()
    {
        if (EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget existing) && existing != null)
            return existing;

        const string path = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
        var created = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(path);
        if (created == null)
        {
            created = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(created, path);
        }

        EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, created, true);
        return created;
    }

    static void EnsureArCoreSettings()
    {
        var settings = ARCoreSettings.currentSettings;
        if (settings == null)
        {
            const string path = "Assets/XR/Settings/ARCoreSettings.asset";
            settings = AssetDatabase.LoadAssetAtPath<ARCoreSettings>(path);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<ARCoreSettings>();
                AssetDatabase.CreateAsset(settings, path);
            }

            ARCoreSettings.currentSettings = settings;
        }

        settings.requirement = ARCoreSettings.Requirement.Required;
        settings.depth = ARCoreSettings.Requirement.Optional;
        EditorUtility.SetDirty(settings);
    }

    static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white;

        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var origin = CreateXrOrigin();
        var planePrefab = CreatePlanePrefab();
        var planeManager = origin.gameObject.AddComponent<ARPlaneManager>();
        planeManager.planePrefab = planePrefab;
        planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
        origin.gameObject.AddComponent<ARRaycastManager>();
        origin.gameObject.AddComponent<ARAnchorManager>();
        var pointCloudManager = origin.gameObject.AddComponent<ARPointCloudManager>();
        pointCloudManager.enabled = false;

        var sessionGo = new GameObject("AR Session");
        sessionGo.AddComponent<ARSession>();
        sessionGo.AddComponent<ARInputManager>();

        var appGo = new GameObject("InspectAR");
        appGo.AddComponent<InspectARApp>();

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ScenePath, true)
        };
        Debug.Log($"{LogPrefix} wrote {ScenePath} origin={origin != null}");
    }

    static XROrigin CreateXrOrigin()
    {
        var originGo = new GameObject("XR Origin", typeof(XROrigin));
        var offsetGo = new GameObject("Camera Offset");
        offsetGo.transform.SetParent(originGo.transform, false);

        var cameraGo = new GameObject(
            "Main Camera",
            typeof(Camera),
            typeof(AudioListener),
            typeof(ARCameraManager),
            typeof(ARCameraBackground),
            typeof(TrackedPoseDriver));
        cameraGo.transform.SetParent(offsetGo.transform, false);
        cameraGo.tag = "MainCamera";

        var camera = cameraGo.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 20f;

        var trackedPoseDriver = cameraGo.GetComponent<TrackedPoseDriver>();
        var positionAction = new InputAction("Position", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
        positionAction.AddBinding("<HandheldARInputDevice>/devicePosition");
        var rotationAction = new InputAction("Rotation", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
        rotationAction.AddBinding("<HandheldARInputDevice>/deviceRotation");
        trackedPoseDriver.positionInput = new InputActionProperty(positionAction);
        trackedPoseDriver.rotationInput = new InputActionProperty(rotationAction);

        var origin = originGo.GetComponent<XROrigin>();
        origin.CameraFloorOffsetObject = offsetGo;
        origin.Camera = camera;
        return origin;
    }

    static GameObject CreatePlanePrefab()
    {
        Directory.CreateDirectory("Assets/InspectAR/Prefabs");
        Directory.CreateDirectory("Assets/InspectAR/Materials");

        var shader = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Sprites/Default");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(PlaneMatPath);
        if (mat == null)
        {
            mat = new Material(shader);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", new Color(0.25f, 0.65f, 1f, 0.35f));
            else
                mat.color = new Color(0.25f, 0.65f, 1f, 0.35f);
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            AssetDatabase.CreateAsset(mat, PlaneMatPath);
        }

        var go = new GameObject("AR Plane");
        go.AddComponent<ARPlane>();
        go.AddComponent<ARPlaneMeshVisualizer>();
        go.AddComponent<MeshFilter>();
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        go.AddComponent<MeshCollider>();
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = mat;
        line.startWidth = 0.01f;
        line.endWidth = 0.01f;
        line.loop = true;

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, PlanePrefabPath);
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    public static bool Verify(out string report)
    {
        var lines = new System.Text.StringBuilder();
        var ok = true;

        void Check(string name, bool pass, string detail = "")
        {
            if (!pass)
                ok = false;
            lines.AppendLine($"- {(pass ? "PASS" : "FAIL")} {name}{(string.IsNullOrEmpty(detail) ? "" : ": " + detail)}");
        }

        Check("scene asset", File.Exists(ScenePath), ScenePath);
        Check("minSdk >= 25", PlayerSettings.Android.minSdkVersion >= AndroidSdkVersions.AndroidApiLevel25,
            PlayerSettings.Android.minSdkVersion.ToString());
        Check("ARM64", (PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) != 0);
        Check("IL2CPP", PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) == ScriptingImplementation.IL2CPP);
        Check("internet permission", PlayerSettings.Android.forceInternetPermission);
        Check("cleartext HTTP", PlayerSettings.insecureHttpOption == InsecureHttpOption.AlwaysAllowed);

        var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
        var hasLoader = general != null
                        && general.AssignedSettings != null
                        && general.AssignedSettings.activeLoaders.OfType<ARCoreLoader>().Any();
        Check("ARCore loader", hasLoader);

        var mobileRenderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>("Assets/Settings/Mobile_Renderer.asset");
        var hasArBackground = mobileRenderer != null
                              && mobileRenderer.rendererFeatures.Any(f => f is ARBackgroundRendererFeature);
        Check("URP ARBackgroundRendererFeature", hasArBackground);

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var origin = UnityEngine.Object.FindFirstObjectByType<XROrigin>();
        Check("XROrigin", origin != null);
        Check("ARAnchorManager on XR Origin", origin != null && origin.GetComponent<ARAnchorManager>() != null);
        Check("ARPointCloudManager on XR Origin", origin != null && origin.GetComponent<ARPointCloudManager>() != null);
        Check("single XROrigin", UnityEngine.Object.FindObjectsByType<XROrigin>(FindObjectsSortMode.None).Length <= 1);
        Check("ARSession", UnityEngine.Object.FindFirstObjectByType<ARSession>() != null);
        Check("single ARSession", UnityEngine.Object.FindObjectsByType<ARSession>(FindObjectsSortMode.None).Length <= 1);
        Check("ARPlaneManager", UnityEngine.Object.FindFirstObjectByType<ARPlaneManager>() != null);
        Check("ARRaycastManager", UnityEngine.Object.FindFirstObjectByType<ARRaycastManager>() != null);
        Check("InspectARApp", UnityEngine.Object.FindFirstObjectByType<InspectARApp>() != null);
        Check("build scene is InspectAR",
            EditorBuildSettings.scenes.Any(s => s.enabled && s.path == ScenePath));

        var arcore = ARCoreSettings.currentSettings
                     ?? AssetDatabase.LoadAssetAtPath<ARCoreSettings>("Assets/XR/Settings/ARCoreSettings.asset");
        Check("ARCore requirement Required",
            arcore != null && arcore.requirement == ARCoreSettings.Requirement.Required,
            arcore != null ? arcore.requirement.ToString() : "missing");
        Check("ARCore depth Optional",
            arcore != null && arcore.depth == ARCoreSettings.Requirement.Optional,
            arcore != null ? arcore.depth.ToString() : "missing");
        lines.AppendLine($"scene={scene.path}");
        report = "Verify\n" + lines;
        return ok;
    }
}
