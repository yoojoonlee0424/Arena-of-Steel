using System;
using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace VoxelDestructionPro.Vox
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class VoxVoxelObject : MonoBehaviour
    {
        [Header("Voxel Settings")]
        [SerializeField] private float voxelSize = 1f;

        [Header("Durability (Hardness)")]
        [Tooltip("복셀 1개의 내구도(체력). 0 이하이면 데미지에 관계없이 1회 타격에 즉시 파괴됩니다.")]
        [SerializeField] private float voxelMaxHealth = 20f;
        [Tooltip("켜면 공격 반경(radius)을 월드 거리가 아닌 복셀 개수 단위로 적용합니다.")]
        [SerializeField] private bool forceRadiusInVoxels = false;

        [Header("Rendering")]
        [SerializeField] private Material vertexColorMaterial;

        [Header("Audio")]
        [SerializeField] private AudioClip defaultDestroyClip;
        [Range(0f, 1f)]
        [SerializeField] private float destroyVolume = 1f;
        [SerializeField] private bool playSoundAtHitPoint = true;

        [Header("Structural Integrity & Ground Anchor")]
        [SerializeField] private bool anchorToGround = true;
        [SerializeField] private int groundAnchorY = 0;

        [Header("Collapse Settings")]
        [SerializeField] private int collapseThreshold = 8;
        [Range(0f, 0.3f)]
        [SerializeField] private float collapseRatio = 0f;

        [Header("Debris & Fragments")]
        [SerializeField] private bool emitDebrisParticles = true;
        [SerializeField] private int minClusterSize = 4;
        [SerializeField] private int permanentFragmentMinSize = 25;
        [SerializeField] private int maxFragmentsPerCall = 8;
        [SerializeField] private float defaultFragmentLifetime = 8f;
        [SerializeField] private string autoRootName = "FragmentsRoot";

        [Header("Test Wall Generator")]
        [SerializeField] private bool generateWallOnStart = false;
        [SerializeField] private Vector3Int wallDimensions = new Vector3Int(16, 12, 3);
        [SerializeField] private Color wallColor1 = new Color(0.6f, 0.6f, 0.65f);
        [SerializeField] private Color wallColor2 = new Color(0.45f, 0.45f, 0.5f);

        [Header("Performance")]
        [SerializeField] private int chunkSize = 32;

        public static int MaxGlobalFragments { get; set; } = 64;

        [SerializeField, HideInInspector] private VoxVoxelData voxelData;
        [SerializeField, HideInInspector] private VoxSubChunk[] subChunks;

        private bool HasSubChunks => subChunks != null && subChunks.Length > 0;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MeshCollider meshCollider;
        private NavMeshObstacle navObstacle;
        private NavMeshModifier navModifier;
        private Mesh runtimeMesh;
        private byte[] runtimeDamage;
        private int[] groundVisited;
        private int groundedVoxelCount;

        private static Transform s_FragmentRoot;
        private int initialVoxelCount;
        private int activeVoxelCount;
        private bool isFragment = false;
        private bool isPermanentFragment = false;
        private Coroutine settleCoroutine;

        private bool hasPendingDamage = false;
        private bool hasDirtyBounds = false;
        private int pendingDestroyedCount = 0;
        private Vector3 pendingHitPoint;
        private VoxelDamageArgs pendingArgs;
        private Vector3Int pendingDirtyMin;
        private Vector3Int pendingDirtyMax;

        private const MeshColliderCookingOptions FastCookingOptions =
            MeshColliderCookingOptions.CookForFasterSimulation |
            MeshColliderCookingOptions.EnableMeshCleaning |
            MeshColliderCookingOptions.WeldColocatedVertices |
            MeshColliderCookingOptions.UseFastMidphase;

        private static int[] visitedBuffer;
        private static int currentRunId = 0;
        private static int s_GroundRunId = 0;
        private static readonly Queue<int> s_BfsQueue = new Queue<int>(1024);
        private static readonly Stack<List<int>> s_ClusterListPool = new Stack<List<int>>(32);
        private static readonly List<ClusterData> s_Clusters = new List<ClusterData>(16);
        private static readonly List<int> s_FragmentsToSpawn = new List<int>(16);
        private static readonly List<GameObject> activeFragments = new List<GameObject>(64);
        private static ParticleSystem sharedParticleSystem;
        private static Mesh cachedCubeMesh;
        private static readonly List<Collider> s_PlayerColliders = new List<Collider>(8);

        private static NavMeshSurface s_NavSurface;
        private static bool s_SearchedNavSurface = false;
        private static float s_NextNavUpdateTime = 0f;
        private static bool s_NavUpdatePending = false;
        private static bool s_NavHookRegistered = false;
        private static AsyncOperation s_NavUpdateOp;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStaticState()
        {
            if (s_NavHookRegistered)
            {
                NavMesh.onPreUpdate -= ProcessPendingNavMeshUpdate;
                s_NavHookRegistered = false;
            }
            s_NavSurface = null;
            s_SearchedNavSurface = false;
            s_NextNavUpdateTime = 0f;
            s_NavUpdatePending = false;
            s_NavUpdateOp = null;
            s_FragmentRoot = null;
            sharedParticleSystem = null;
            s_PlayerColliders.Clear();
            activeFragments.Clear();
        }

        public static void IgnorePlayerCollision(Collider col) => SetPlayerCollisionIgnore(col, true);

        public static void SetPlayerCollisionIgnore(Collider col, bool ignore)
        {
            if (col == null) return;
            RefreshPlayerColliders();

            for (int i = 0; i < s_PlayerColliders.Count; i++)
            {
                if (s_PlayerColliders[i] != null && s_PlayerColliders[i].enabled)
                {
                    Physics.IgnoreCollision(col, s_PlayerColliders[i], ignore);
                }
            }
        }

        private static void RefreshPlayerColliders()
        {
            if (s_PlayerColliders.Count == 0 || s_PlayerColliders[0] == null)
            {
                s_PlayerColliders.Clear();
                var players = GameObject.FindGameObjectsWithTag("Player");
                for (int i = 0; i < players.Length; i++)
                {
                    s_PlayerColliders.AddRange(players[i].GetComponentsInChildren<Collider>(true));
                }
            }
        }

        private static bool IsBoundsOverlappingPlayer(Collider col)
        {
            if (col == null || !col.enabled) return false;
            RefreshPlayerColliders();
            Bounds b = col.bounds;
            b.Expand(0.15f);

            for (int i = 0; i < s_PlayerColliders.Count; i++)
            {
                Collider pCol = s_PlayerColliders[i];
                if (pCol == null || !pCol.enabled || pCol.isTrigger) continue;
                if (b.Intersects(pCol.bounds)) return true;
            }
            return false;
        }

        private static readonly List<NavMeshBuildSource> s_NavSources = new List<NavMeshBuildSource>(64);
        private static readonly List<NavMeshBuildMarkup> s_NavMarkups = new List<NavMeshBuildMarkup>(32);

        private static NavMeshSurface GetNavSurface()
        {
            if (!s_SearchedNavSurface || s_NavSurface == null)
            {
                s_SearchedNavSurface = true;
                s_NavSurface = FindFirstObjectByType<NavMeshSurface>();
                if (s_NavSurface != null)
                    s_NavSurface.ignoreNavMeshObstacle = false;
            }
            return s_NavSurface;
        }

        public static void RequestNavMeshUpdate()
        {
            if (!Application.isPlaying) return;
            var surface = GetNavSurface();
            if (surface == null || surface.navMeshData == null) return;
            s_NavUpdatePending = true;
            if (!s_NavHookRegistered)
            {
                NavMesh.onPreUpdate += ProcessPendingNavMeshUpdate;
                s_NavHookRegistered = true;
            }
        }

        private static void ProcessPendingNavMeshUpdate()
        {
            if (!s_NavUpdatePending)
            {
                if (s_NavHookRegistered)
                {
                    NavMesh.onPreUpdate -= ProcessPendingNavMeshUpdate;
                    s_NavHookRegistered = false;
                }
                return;
            }

            if (Time.unscaledTime >= s_NextNavUpdateTime && (s_NavUpdateOp == null || s_NavUpdateOp.isDone))
            {
                var surface = GetNavSurface();
                s_NavUpdatePending = false;
                if (s_NavHookRegistered)
                {
                    NavMesh.onPreUpdate -= ProcessPendingNavMeshUpdate;
                    s_NavHookRegistered = false;
                }
                if (surface != null && surface.navMeshData != null)
                {
                    s_NextNavUpdateTime = Time.unscaledTime + 0.2f;
                    s_NavUpdateOp = UpdateSurfaceFiltered(surface);
                }
            }
        }

        private static AsyncOperation UpdateSurfaceFiltered(NavMeshSurface surface)
        {
            s_NavSources.Clear();
            s_NavMarkups.Clear();

            var modifiers = NavMeshModifier.activeModifiers;
            for (int i = 0; i < modifiers.Count; i++)
            {
                var m = modifiers[i];
                if ((surface.layerMask & (1 << m.gameObject.layer)) == 0 || !m.AffectsAgentType(surface.agentTypeID))
                    continue;

                s_NavMarkups.Add(new NavMeshBuildMarkup
                {
                    root = m.transform,
                    overrideArea = m.overrideArea,
                    area = m.area,
                    ignoreFromBuild = m.ignoreFromBuild,
                    applyToChildren = m.applyToChildren,
                    overrideGenerateLinks = m.overrideGenerateLinks,
                    generateLinks = m.generateLinks
                });
            }

            Transform collectRoot = surface.collectObjects == CollectObjects.Children ? surface.transform : null;
            NavMeshBuilder.CollectSources(collectRoot, surface.layerMask, surface.useGeometry, surface.defaultArea, s_NavMarkups, s_NavSources);

            s_NavSources.RemoveAll(src =>
            {
                if (src.shape == NavMeshBuildSourceShape.Mesh && src.sourceObject is Mesh m && !m.isReadable) return true;
                if (src.component == null) return false;
                GameObject go = src.component.gameObject;
                if (go.GetComponentInParent<NavMeshAgent>() != null) return true;
                if (go.GetComponentInParent<CharacterController>() != null) return true;
                if (go.GetComponentInParent<Rigidbody>() is Rigidbody rb && !rb.isKinematic) return true;
                if (go.GetComponentInParent<Collider>() == null && go.GetComponentInChildren<Collider>() == null) return true;
                return false;
            });

            Bounds bounds = CalculateSurfaceBounds(surface, s_NavSources);
            return NavMeshBuilder.UpdateNavMeshDataAsync(surface.navMeshData, surface.GetBuildSettings(), s_NavSources, bounds);
        }

        private static Bounds CalculateSurfaceBounds(NavMeshSurface surface, List<NavMeshBuildSource> sources)
        {
            if (surface.collectObjects == CollectObjects.Volume)
                return new Bounds(surface.center, new Vector3(Mathf.Abs(surface.size.x), Mathf.Abs(surface.size.y), Mathf.Abs(surface.size.z)));

            Matrix4x4 worldToLocal = Matrix4x4.TRS(surface.transform.position, surface.transform.rotation, Vector3.one).inverse;
            Bounds result = default;
            bool hasAny = false;

            for (int i = 0; i < sources.Count; i++)
            {
                var src = sources[i];
                Bounds b;
                if (src.shape == NavMeshBuildSourceShape.Mesh && src.sourceObject is Mesh m)
                    b = TransformBounds(worldToLocal * src.transform, m.bounds);
                else if (src.shape == NavMeshBuildSourceShape.Box || src.shape == NavMeshBuildSourceShape.Sphere || src.shape == NavMeshBuildSourceShape.Capsule || src.shape == NavMeshBuildSourceShape.ModifierBox)
                    b = TransformBounds(worldToLocal * src.transform, new Bounds(Vector3.zero, src.size));
                else
                    continue;

                if (!hasAny) { result = b; hasAny = true; }
                else result.Encapsulate(b);
            }

            result.Expand(0.1f);
            return result;
        }

        private static Bounds TransformBounds(Matrix4x4 mat, Bounds bounds)
        {
            Vector3 ax = mat.MultiplyVector(Vector3.right);
            Vector3 ay = mat.MultiplyVector(Vector3.up);
            Vector3 az = mat.MultiplyVector(Vector3.forward);
            Vector3 size = new Vector3(Mathf.Abs(ax.x), Mathf.Abs(ax.y), Mathf.Abs(ax.z)) * bounds.size.x +
                           new Vector3(Mathf.Abs(ay.x), Mathf.Abs(ay.y), Mathf.Abs(ay.z)) * bounds.size.y +
                           new Vector3(Mathf.Abs(az.x), Mathf.Abs(az.y), Mathf.Abs(az.z)) * bounds.size.z;
            return new Bounds(mat.MultiplyPoint(bounds.center), size);
        }

        public event Action<VoxelDamageResult> OnVoxelDamaged;

        public VoxVoxelData VoxelData => voxelData;
        public float VoxelSize => voxelSize;
        public Material VertexColorMaterial => vertexColorMaterial;

        private void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            meshCollider = GetComponent<MeshCollider>();
            if (meshCollider == null) meshCollider = gameObject.AddComponent<MeshCollider>();
            navObstacle = GetComponent<NavMeshObstacle>();
            navModifier = GetComponent<NavMeshModifier>();
            if (navModifier == null) navModifier = gameObject.AddComponent<NavMeshModifier>();
            navModifier.overrideArea = true;
            navModifier.area = 1;

            if (HasSubChunks)
            {
                meshCollider.enabled = false;
                if (meshRenderer != null) meshRenderer.enabled = false;
                EnsureMaterial();
                for (int i = 0; i < subChunks.Length; i++)
                {
                    if (subChunks[i].meshCollider != null)
                    {
                        GameObject chunkGo = subChunks[i].meshCollider.gameObject;
                        chunkGo.layer = gameObject.layer;
                        chunkGo.tag = gameObject.tag;
                        subChunks[i].meshCollider.cookingOptions = FastCookingOptions;
                        subChunks[i].meshCollider.enabled = true;
                        subChunks[i].meshCollider.convex = false;
                    }
                }
            }
            else
            {
                bool isDynamic = TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic;
                meshCollider.cookingOptions = FastCookingOptions;
                meshCollider.enabled = true;
                meshCollider.convex = isDynamic;
                if (isDynamic) SetPlayerCollisionIgnore(meshCollider, true);
                EnsureMaterial();
            }
        }

        private void Start()
        {
            if (!isFragment)
            {
                navModifier.ignoreFromBuild = false;
                if (generateWallOnStart)
                {
                    GenerateTestWall();
                }
                else if (voxelData != null)
                {
                    activeVoxelCount = voxelData.CountActive();
                    initialVoxelCount = activeVoxelCount;

                    if (HasSubChunks)
                    {
                        for (int i = 0; i < subChunks.Length; i++)
                        {
                            ref var sc = ref subChunks[i];
                            if (sc.meshFilter == null || sc.meshFilter.sharedMesh == null)
                                RebuildSingleSubChunk(ref sc);
                            else if (sc.meshCollider != null && sc.meshCollider.sharedMesh == null)
                                sc.meshCollider.sharedMesh = sc.meshFilter.sharedMesh;
                        }
                    }
                    else if (meshFilter == null || meshFilter.sharedMesh == null || meshFilter.sharedMesh.vertexCount == 0)
                    {
                        RebuildMesh();
                    }
                    else if (meshCollider != null && meshCollider.sharedMesh == null)
                    {
                        meshCollider.sharedMesh = meshFilter.sharedMesh;
                    }
                }
            }

            if (navObstacle != null && GetNavSurface() != null)
            {
                navObstacle.enabled = false;
                Destroy(navObstacle);
                navObstacle = null;
            }
        }

        private void LateUpdate()
        {
            if (hasPendingDamage)
            {
                CommitPendingDamage();
            }

            if (s_NavUpdatePending)
            {
                ProcessPendingNavMeshUpdate();
            }
        }

        private void OnDestroy()
        {
            if (runtimeMesh != null)
            {
                if (Application.isPlaying) Destroy(runtimeMesh);
                else DestroyImmediate(runtimeMesh);
                runtimeMesh = null;
            }
            if (subChunks != null)
            {
                for (int i = 0; i < subChunks.Length; i++)
                {
                    if (subChunks[i].runtimeMesh != null)
                    {
                        if (Application.isPlaying) Destroy(subChunks[i].runtimeMesh);
                        else DestroyImmediate(subChunks[i].runtimeMesh);
                        subChunks[i].runtimeMesh = null;
                    }
                }
            }
            if (isFragment)
            {
                activeFragments.Remove(gameObject);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying || UnityEditor.EditorUtility.IsPersistent(this) || !gameObject.scene.IsValid()) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || Application.isPlaying || UnityEditor.EditorUtility.IsPersistent(this) || !gameObject.scene.IsValid()) return;
                if (HasSubChunks)
                {
                    EnsureMaterial();
                    return;
                }
                if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null && UnityEditor.EditorUtility.IsPersistent(meshFilter.sharedMesh))
                {
                    EnsureMaterial();
                    if (meshCollider == null) meshCollider = GetComponent<MeshCollider>();
                    if (meshCollider != null) meshCollider.sharedMesh = meshFilter.sharedMesh;
                    return;
                }
                RebuildMesh();
            };
        }
#endif

        private void EnsureMaterial()
        {
            if (vertexColorMaterial == null) return;
            if (HasSubChunks)
            {
                for (int i = 0; i < subChunks.Length; i++)
                {
                    if (subChunks[i].meshRenderer != null)
                        subChunks[i].meshRenderer.sharedMaterial = vertexColorMaterial;
                }
                return;
            }
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null)
                meshRenderer.sharedMaterial = vertexColorMaterial;
        }

        public void CopySettingsFrom(VoxVoxelObject source)
        {
            if (source == null) return;
            voxelSize = source.voxelSize;
            voxelMaxHealth = source.voxelMaxHealth;
            forceRadiusInVoxels = source.forceRadiusInVoxels;
            vertexColorMaterial = source.vertexColorMaterial;
            defaultDestroyClip = source.defaultDestroyClip;
            destroyVolume = source.destroyVolume;
            anchorToGround = false;
            defaultFragmentLifetime = source.defaultFragmentLifetime;
            permanentFragmentMinSize = source.permanentFragmentMinSize;
            emitDebrisParticles = source.emitDebrisParticles;
            chunkSize = source.chunkSize;
            EnsureMaterial();
        }

        public void AssignVoxelData(VoxVoxelData data, int knownActiveCount = -1)
        {
            voxelData = data;
            runtimeDamage = null;
            activeVoxelCount = knownActiveCount >= 0 ? knownActiveCount : (data != null ? data.CountActive() : 0);
            if (!isFragment)
                initialVoxelCount = activeVoxelCount;
            if (meshFilter != null)
                meshFilter.sharedMesh = null;
            RebuildMesh();
#if UNITY_EDITOR
            if (!Application.isPlaying && !UnityEditor.EditorUtility.IsPersistent(this))
            {
                UnityEditor.EditorUtility.SetDirty(this);
                if (meshFilter != null) UnityEditor.EditorUtility.SetDirty(meshFilter);
                if (meshCollider != null) UnityEditor.EditorUtility.SetDirty(meshCollider);
                if (gameObject.scene.IsValid())
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        }

        /// <summary>
        /// 에디터 임포터에서 생성된 통합 복셀 데이터와 단일/서브청크 메쉬 참조를 초기화합니다.
        /// </summary>
        public void InitializeImported(VoxVoxelData data, float size, Material mat, AudioClip clip, bool anchor, Mesh importedMesh, VoxSubChunk[] importedSubChunks = null, int maxChunk = 32, float maxHealth = 20f, bool radiusInVox = false)
        {
            voxelData = data;
            voxelSize = size;
            voxelMaxHealth = maxHealth;
            forceRadiusInVoxels = radiusInVox;
            vertexColorMaterial = mat;
            defaultDestroyClip = clip;
            anchorToGround = anchor;
            chunkSize = maxChunk;
            subChunks = importedSubChunks;
            runtimeDamage = null;
            activeVoxelCount = data != null ? data.CountActive() : 0;
            initialVoxelCount = activeVoxelCount;

            bool hasChunks = importedSubChunks != null && importedSubChunks.Length > 0;
            if (TryGetComponent<MeshFilter>(out var mf)) mf.sharedMesh = hasChunks ? null : importedMesh;
            if (TryGetComponent<MeshRenderer>(out var mr))
            {
                if (mat != null) mr.sharedMaterial = mat;
                mr.enabled = !hasChunks;
            }
            if (TryGetComponent<MeshCollider>(out var mc))
            {
                mc.cookingOptions = FastCookingOptions;
                mc.sharedMesh = hasChunks ? null : importedMesh;
                mc.enabled = !hasChunks;
            }

            if (hasChunks)
            {
                for (int i = 0; i < subChunks.Length; i++)
                {
                    if (subChunks[i].meshCollider != null)
                        subChunks[i].meshCollider.cookingOptions = FastCookingOptions;
                }
            }
        }

        [ContextMenu("Generate Test Wall")]
        public void GenerateTestWall()
        {
            AssignVoxelData(VoxVoxelData.CreateBox(wallDimensions.x, wallDimensions.y, wallDimensions.z, wallColor1, wallColor2));
        }

        public void RebuildMesh()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (meshCollider == null) meshCollider = GetComponent<MeshCollider>();
            if (meshCollider == null) meshCollider = gameObject.AddComponent<MeshCollider>();
            if (navObstacle == null) navObstacle = GetComponent<NavMeshObstacle>();
            if (navModifier == null) navModifier = GetComponent<NavMeshModifier>();
            if (navModifier == null) navModifier = gameObject.AddComponent<NavMeshModifier>();
            navModifier.overrideArea = true;
            navModifier.area = 1;
            if (!isFragment)
                navModifier.ignoreFromBuild = false;

            EnsureMaterial();

            if (!Application.isPlaying && voxelData != null)
                activeVoxelCount = voxelData.CountActive();

            if (HasSubChunks)
            {
                meshCollider.sharedMesh = null;
                meshCollider.enabled = false;
                if (meshRenderer != null) meshRenderer.enabled = false;

                for (int i = 0; i < subChunks.Length; i++)
                    RebuildSingleSubChunk(ref subChunks[i]);

                Physics.SyncTransforms();
                UpdateNavState();
                return;
            }

            meshCollider.sharedMesh = null;

            if (voxelData != null && activeVoxelCount > 0)
            {
                if (!Application.isPlaying && runtimeMesh != null)
                {
                    DestroyImmediate(runtimeMesh);
                    runtimeMesh = null;
                }
                runtimeMesh = VoxMesher.BuildMesh(voxelData, voxelSize, runtimeMesh);
                meshFilter.sharedMesh = runtimeMesh;
            }
            else
            {
                if (runtimeMesh != null) runtimeMesh.Clear();
                meshFilter.sharedMesh = null;
            }

            bool isDynamic = TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic;
            meshCollider.cookingOptions = FastCookingOptions;
            meshCollider.enabled = true;
            meshCollider.convex = isDynamic;
            if (meshFilter.sharedMesh != null && meshFilter.sharedMesh.vertexCount > 0)
            {
                meshCollider.sharedMesh = meshFilter.sharedMesh;
                Physics.SyncTransforms();
            }
            if (Application.isPlaying && isFragment && isDynamic)
                SetPlayerCollisionIgnore(meshCollider, true);

            UpdateNavState();
        }

        private void RebuildDirtyChunks(Vector3Int dirtyMin, Vector3Int dirtyMax)
        {
            if (!HasSubChunks)
            {
                RebuildMesh();
                return;
            }

            EnsureMaterial();
            Vector3Int min = dirtyMin - Vector3Int.one;
            Vector3Int max = dirtyMax + Vector3Int.one;

            for (int i = 0; i < subChunks.Length; i++)
            {
                ref var sc = ref subChunks[i];
                if (max.x < sc.min.x || min.x >= sc.max.x ||
                    max.y < sc.min.y || min.y >= sc.max.y ||
                    max.z < sc.min.z || min.z >= sc.max.z)
                    continue;

                RebuildSingleSubChunk(ref sc);
            }

            Physics.SyncTransforms();
            UpdateNavState();
        }

        private void RebuildSingleSubChunk(ref VoxSubChunk sc)
        {
            if (sc.meshFilter == null || sc.meshCollider == null) return;
            sc.meshCollider.sharedMesh = null;

            if (voxelData != null && activeVoxelCount > 0 && voxelData.HasActiveInBounds(sc.min, sc.max))
            {
                if (!Application.isPlaying && sc.runtimeMesh != null)
                {
                    DestroyImmediate(sc.runtimeMesh);
                    sc.runtimeMesh = null;
                }
                sc.runtimeMesh = VoxMesher.BuildChunkMesh(voxelData, sc.min, sc.max, voxelSize, sc.runtimeMesh);
                sc.meshFilter.sharedMesh = sc.runtimeMesh;
                if (sc.runtimeMesh.vertexCount > 0)
                    sc.meshCollider.sharedMesh = sc.runtimeMesh;
            }
            else
            {
                if (sc.runtimeMesh != null) sc.runtimeMesh.Clear();
                sc.meshFilter.sharedMesh = null;
            }
        }

        private void UpdateNavState()
        {
            if (!Application.isPlaying) return;

            var surface = GetNavSurface();
            if (surface != null)
            {
                if (navObstacle != null)
                {
                    navObstacle.enabled = false;
                    Destroy(navObstacle);
                    navObstacle = null;
                }

                if (!isFragment || (isPermanentFragment && TryGetComponent<Rigidbody>(out var rb) && rb.isKinematic))
                    RequestNavMeshUpdate();
                return;
            }

            if (navObstacle == null) return;
            if (voxelData == null || activeVoxelCount <= collapseThreshold || !voxelData.TryGetActiveBounds(out Vector3Int min, out Vector3Int max))
            {
                navObstacle.enabled = false;
                return;
            }

            if (anchorToGround && min.y > groundAnchorY + 2)
            {
                navObstacle.enabled = false;
                return;
            }

            navObstacle.enabled = true;
            Vector3 localMin = ((Vector3)min - Vector3.one * 0.5f) * voxelSize;
            Vector3 localMax = ((Vector3)max + Vector3.one * 0.5f) * voxelSize;
            navObstacle.center = (localMin + localMax) * 0.5f;
            navObstacle.size = localMax - localMin;
        }

        public void StartSettlingCheck()
        {
            if (!isFragment || !Application.isPlaying) return;
            if (settleCoroutine != null) StopCoroutine(settleCoroutine);
            settleCoroutine = StartCoroutine(CheckSettlementRoutine());
        }

        private IEnumerator CheckSettlementRoutine()
        {
            yield return new WaitForSeconds(0.35f);
            var rb = GetComponent<Rigidbody>();
            if (rb == null) yield break;

            int stillCount = 0;
            while (rb != null && !rb.isKinematic)
            {
                yield return new WaitForSeconds(0.15f);
                if (rb == null) yield break;

                if (rb.IsSleeping() || (rb.linearVelocity.sqrMagnitude < 0.005f && rb.angularVelocity.sqrMagnitude < 0.005f))
                {
                    stillCount++;
                    if (rb.IsSleeping() || stillCount >= 4)
                    {
                        SettleFragment();
                        break;
                    }
                }
                else
                {
                    stillCount = 0;
                }
            }

            if (meshCollider != null)
            {
                if (IsBoundsOverlappingPlayer(meshCollider))
                {
                    SetPlayerCollisionIgnore(meshCollider, true);
                    while (meshCollider != null && IsBoundsOverlappingPlayer(meshCollider))
                    {
                        yield return new WaitForSeconds(0.15f);
                    }
                }
                if (meshCollider != null)
                {
                    SetPlayerCollisionIgnore(meshCollider, false);
                }
            }
        }

        private static void WakeNearbyFragments(Vector3 worldPoint, float radius)
        {
            float wakeRadius = Mathf.Max(radius + 2.0f, 3.0f);
            Bounds wakeBounds = new Bounds(worldPoint, Vector3.one * (wakeRadius * 2f));

            for (int i = activeFragments.Count - 1; i >= 0; i--)
            {
                GameObject go = activeFragments[i];
                if (go == null) continue;
                if (go.TryGetComponent<VoxVoxelObject>(out var frag) && frag.meshCollider != null)
                {
                    if (wakeBounds.Intersects(frag.meshCollider.bounds))
                        frag.UnsettleFragment();
                }
            }
        }

        public void SettleFragment()
        {
            if (!isFragment || !TryGetComponent<Rigidbody>(out var rb)) return;
            if (rb.isKinematic) return;

            rb.isKinematic = true;
            if (meshCollider != null)
            {
                meshCollider.sharedMesh = null;
                meshCollider.convex = false;
                if (meshFilter != null && meshFilter.sharedMesh != null && meshFilter.sharedMesh.vertexCount > 0)
                    meshCollider.sharedMesh = meshFilter.sharedMesh;
            }

            if (isPermanentFragment)
            {
                if (navModifier != null)
                    navModifier.ignoreFromBuild = false;
                RequestNavMeshUpdate();
            }
        }

        public void UnsettleFragment()
        {
            if (!isFragment || !TryGetComponent<Rigidbody>(out var rb)) return;
            if (!rb.isKinematic) return;

            if (navModifier != null)
                navModifier.ignoreFromBuild = true;

            if (meshCollider != null)
            {
                SetPlayerCollisionIgnore(meshCollider, true);
                meshCollider.sharedMesh = null;
                meshCollider.convex = true;
                if (meshFilter != null && meshFilter.sharedMesh != null && meshFilter.sharedMesh.vertexCount > 0)
                    meshCollider.sharedMesh = meshFilter.sharedMesh;
            }
            rb.isKinematic = false;
            rb.WakeUp();
            StartSettlingCheck();
        }

        public bool HasActiveVoxelNear(Vector3 worldPoint, float radius, out Vector3 nearestVoxelPos)
        {
            nearestVoxelPos = worldPoint;
            if (voxelData == null || activeVoxelCount <= 0) return false;

            float vSize = Mathf.Max(voxelSize, 0.0001f);
            Vector3 ls = transform.lossyScale;
            float scaleMax = Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z), 0.0001f);
            Vector3 local = transform.InverseTransformPoint(worldPoint) / vSize;

            int cx = Mathf.RoundToInt(local.x);
            int cy = Mathf.RoundToInt(local.y);
            int cz = Mathf.RoundToInt(local.z);

            int maxR = isFragment ? 8 : 4;
            int r = Mathf.Clamp(Mathf.CeilToInt(radius / (vSize * scaleMax)), 2, maxR);

            float minDistSq = float.MaxValue;
            bool found = false;
            Vector3 bestLocal = local;

            for (int z = cz - r; z <= cz + r; z++)
            {
                for (int y = cy - r; y <= cy + r; y++)
                {
                    for (int x = cx - r; x <= cx + r; x++)
                    {
                        if (!voxelData.InBounds(x, y, z)) continue;
                        var v = voxelData.GetVoxel(x, y, z);
                        if (v.active)
                        {
                            Vector3 voxCenter = new Vector3(x, y, z);
                            float distSq = (voxCenter - local).sqrMagnitude;
                            if (distSq < minDistSq)
                            {
                                minDistSq = distSq;
                                bestLocal = voxCenter;
                                found = true;
                            }
                        }
                    }
                }
            }

            if (found)
            {
                nearestVoxelPos = transform.TransformPoint(bestLocal * vSize);
                return true;
            }

            return false;
        }

        public bool RaycastVoxel(Ray worldRay, float maxDistance, out Vector3 hitPoint, out Vector3Int hitCoord)
        {
            hitPoint = Vector3.zero;
            hitCoord = Vector3Int.zero;

            if (voxelData == null || activeVoxelCount <= 0) return false;

            float vSize = Mathf.Max(voxelSize, 0.0001f);
            Vector3 ls = transform.lossyScale;
            float scaleMax = Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z), 0.0001f);
            Vector3 localOrigin = transform.InverseTransformPoint(worldRay.origin);
            Vector3 localDir = transform.InverseTransformDirection(worldRay.direction).normalized;

            Vector3 gridOrigin = localOrigin / vSize;
            Vector3 gridDir = localDir;

            Vector3 boxMin = new Vector3(-0.5f, -0.5f, -0.5f);
            Vector3 boxMax = (Vector3)voxelData.length - new Vector3(0.5f, 0.5f, 0.5f);

            if (!IntersectAABB(gridOrigin, gridDir, boxMin, boxMax, out float tMin, out float tMax))
                return false;

            float startT = Mathf.Max(0f, tMin);
            float endT = Mathf.Min(tMax, maxDistance / (vSize * scaleMax));
            if (startT > endT) return false;

            float step = 0.35f;
            for (float t = startT; t <= endT; t += step)
            {
                Vector3 p = gridOrigin + gridDir * t;
                int x = Mathf.RoundToInt(p.x);
                int y = Mathf.RoundToInt(p.y);
                int z = Mathf.RoundToInt(p.z);

                if (voxelData.InBounds(x, y, z))
                {
                    var vox = voxelData.GetVoxel(x, y, z);
                    if (vox.active)
                    {
                        hitCoord = new Vector3Int(x, y, z);
                        hitPoint = transform.TransformPoint(new Vector3(x, y, z) * vSize);
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IntersectAABB(Vector3 rayOrigin, Vector3 rayDir, Vector3 boxMin, Vector3 boxMax, out float tMin, out float tMax)
        {
            tMin = float.MinValue;
            tMax = float.MaxValue;

            for (int i = 0; i < 3; i++)
            {
                float invD = 1f / (Mathf.Abs(rayDir[i]) > 1e-6f ? rayDir[i] : (rayDir[i] >= 0 ? 1e-6f : -1e-6f));
                float t0 = (boxMin[i] - rayOrigin[i]) * invD;
                float t1 = (boxMax[i] - rayOrigin[i]) * invD;
                if (invD < 0f) { float tmp = t0; t0 = t1; t1 = tmp; }

                tMin = Mathf.Max(tMin, t0);
                tMax = Mathf.Min(tMax, t1);
                if (tMax < tMin) return false;
            }

            return true;
        }

        public VoxelDamageResult DamageSphere(Vector3 worldPosition, VoxelDamageArgs args)
        {
            return ExecuteSphereDamage(worldPosition, args);
        }

        public bool DestroySphere(Vector3 worldPosition, VoxelDamageArgs args)
        {
            return ExecuteSphereDamage(worldPosition, args).destroyed;
        }

        public VoxelDamageResult DamageBox(Vector3 worldCenter, Vector3 worldHalfExtents, Quaternion worldRotation, VoxelDamageArgs args)
        {
            if (voxelData == null || activeVoxelCount <= 0) return default;

            float vSize = Mathf.Max(voxelSize, 0.0001f);
            Vector3 ls = transform.lossyScale;
            float scaleMax = Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z), 0.0001f);

            Vector3 localCenter = transform.InverseTransformPoint(worldCenter);
            Vector3 centerVox = localCenter / vSize;
            float maxExt = Mathf.Max(worldHalfExtents.x, worldHalfExtents.y, worldHalfExtents.z) / (vSize * scaleMax);

            Vector3Int min = new Vector3Int(
                Mathf.FloorToInt(centerVox.x - maxExt - 0.5f),
                Mathf.FloorToInt(centerVox.y - maxExt - 0.5f),
                Mathf.FloorToInt(centerVox.z - maxExt - 0.5f)
            );
            Vector3Int max = new Vector3Int(
                Mathf.CeilToInt(centerVox.x + maxExt + 0.5f),
                Mathf.CeilToInt(centerVox.y + maxExt + 0.5f),
                Mathf.CeilToInt(centerVox.z + maxExt + 0.5f)
            );

            Matrix4x4 boxWorldToLocal = Matrix4x4.TRS(worldCenter, worldRotation, Vector3.one).inverse;
            int count = 0;
            int damagedCount = 0;
            Color sampleColor = Color.white;

            for (int z = min.z; z <= max.z; z++)
                for (int y = min.y; y <= max.y; y++)
                    for (int x = min.x; x <= max.x; x++)
                    {
                        if (!voxelData.InBounds(x, y, z)) continue;
                        Vector3 voxWorld = transform.TransformPoint(new Vector3(x, y, z) * vSize);
                        Vector3 inBox = boxWorldToLocal.MultiplyPoint3x4(voxWorld);

                        if (Mathf.Abs(inBox.x) <= worldHalfExtents.x &&
                            Mathf.Abs(inBox.y) <= worldHalfExtents.y &&
                            Mathf.Abs(inBox.z) <= worldHalfExtents.z)
                        {
                            if (ApplyVoxelDamage(x, y, z, args.damage, out Color col, out bool partial))
                            {
                                sampleColor = col;
                                count++;
                            }
                            else if (partial)
                            {
                                sampleColor = col;
                                damagedCount++;
                            }
                        }
                    }

            if (count > 0)
            {
                activeVoxelCount = Mathf.Max(0, activeVoxelCount - count);
                if (emitDebrisParticles)
                    EmitDebrisParticles(worldCenter, sampleColor, Mathf.Clamp(count, 6, 25));
            }
            else if (damagedCount > 0 && emitDebrisParticles)
            {
                EmitDebrisParticles(worldCenter, sampleColor, Mathf.Clamp(damagedCount, 2, 6));
            }

            VoxelDamageResult res = QueueDamageCommit(worldCenter, count, args);
            res.damagedCount = damagedCount;
            return res;
        }

        public VoxelDamageResult DamageRay(Ray ray, float maxDistance, VoxelDamageArgs args)
        {
            int stepCount = Mathf.Max(1, Mathf.CeilToInt(maxDistance / (voxelSize * 0.8f)));
            float stepDist = maxDistance / stepCount;
            int totalDestroyed = 0;
            int totalDamaged = 0;
            Vector3 firstHit = ray.origin;
            bool hitAny = false;

            for (int i = 0; i <= stepCount; i++)
            {
                Vector3 pt = ray.GetPoint(i * stepDist);
                int removed = InternalSphereRemove(pt, args, out Color col, out int damaged);
                totalDamaged += damaged;
                if (removed > 0)
                {
                    if (!hitAny) { firstHit = pt; hitAny = true; }
                    totalDestroyed += removed;
                    if (emitDebrisParticles)
                        EmitDebrisParticles(pt, col, Mathf.Clamp(removed * 2, 4, 15));
                }
            }

            VoxelDamageResult res = QueueDamageCommit(firstHit, totalDestroyed, args);
            res.damagedCount = totalDamaged;
            return res;
        }

        private VoxelDamageResult ExecuteSphereDamage(Vector3 worldPosition, VoxelDamageArgs args)
        {
            int destroyed = InternalSphereRemove(worldPosition, args, out Color col, out int damagedCount);
            if (destroyed == 0 && damagedCount == 0)
            {
                Vector3 searchDir = args.impactDirection;
                if (searchDir == Vector3.zero && voxelData != null)
                {
                    Vector3 centerWorld = transform.TransformPoint((Vector3)voxelData.length * 0.5f * voxelSize);
                    searchDir = (centerWorld - worldPosition).normalized;
                }

                if (searchDir != Vector3.zero)
                {
                    float worldRadius = (args.radiusInVoxels || forceRadiusInVoxels) ? args.radius * voxelSize : args.radius;
                    float maxD = Mathf.Max(worldRadius * 2.5f, voxelSize * 2f);
                    float step = Mathf.Max(voxelSize * 0.5f, 0.2f);
                    for (float d = step; d <= maxD; d += step)
                    {
                        Vector3 deeper = worldPosition + searchDir.normalized * d;
                        destroyed = InternalSphereRemove(deeper, args, out col, out damagedCount);
                        if (destroyed > 0 || damagedCount > 0)
                        {
                            worldPosition = deeper;
                            break;
                        }
                    }
                }

                if (destroyed == 0 && damagedCount == 0 && HasActiveVoxelNear(worldPosition, voxelSize * 6f, out Vector3 nearestPos))
                {
                    worldPosition = nearestPos;
                    destroyed = InternalSphereRemove(worldPosition, args, out col, out damagedCount);
                }
            }

            if (emitDebrisParticles)
            {
                if (destroyed > 0)
                    EmitDebrisParticles(worldPosition, col, Mathf.Clamp(destroyed * 2, 8, 30));
                else if (damagedCount > 0)
                    EmitDebrisParticles(worldPosition, col, Mathf.Clamp(damagedCount, 2, 6));
            }

            VoxelDamageResult res = QueueDamageCommit(worldPosition, destroyed, args);
            res.damagedCount = damagedCount;
            if (destroyed == 0 && damagedCount > 0)
                res.hitPoint = worldPosition;
            return res;
        }

        private void ExpandDirtyBounds(int x, int y, int z)
        {
            if (!hasDirtyBounds)
            {
                hasDirtyBounds = true;
                pendingDirtyMin = new Vector3Int(x, y, z);
                pendingDirtyMax = new Vector3Int(x, y, z);
                return;
            }
            if (x < pendingDirtyMin.x) pendingDirtyMin.x = x;
            if (y < pendingDirtyMin.y) pendingDirtyMin.y = y;
            if (z < pendingDirtyMin.z) pendingDirtyMin.z = z;
            if (x > pendingDirtyMax.x) pendingDirtyMax.x = x;
            if (y > pendingDirtyMax.y) pendingDirtyMax.y = y;
            if (z > pendingDirtyMax.z) pendingDirtyMax.z = z;
        }

        private bool ApplyVoxelDamage(int x, int y, int z, float hitDamage, out Color voxelColor, out bool tookPartialDamage)
        {
            voxelColor = Color.white;
            tookPartialDamage = false;

            int idx = voxelData.ToIndex(x, y, z);
            VoxVoxel v = voxelData.voxels[idx];
            if (!v.active) return false;

            if (voxelData.palette != null && v.colorIndex < voxelData.palette.Length)
                voxelColor = voxelData.palette[v.colorIndex];

            if (voxelMaxHealth > 0f && hitDamage > 0f)
            {
                int dmgByte = Mathf.Clamp(Mathf.RoundToInt((hitDamage / voxelMaxHealth) * 255f), 1, 255);
                int prevDmg = runtimeDamage != null && idx < runtimeDamage.Length ? runtimeDamage[idx] : 0;
                int nextDmg = prevDmg + dmgByte;
                if (nextDmg < 255)
                {
                    if (runtimeDamage == null || runtimeDamage.Length < voxelData.Volume)
                        runtimeDamage = new byte[voxelData.Volume];
                    runtimeDamage[idx] = (byte)nextDmg;
                    tookPartialDamage = true;
                    return false;
                }
            }

            v.active = false;
            voxelData.voxels[idx] = v;
            if (runtimeDamage != null && idx < runtimeDamage.Length)
                runtimeDamage[idx] = 0;
            ExpandDirtyBounds(x, y, z);
            return true;
        }

        private int InternalSphereRemove(Vector3 worldPosition, VoxelDamageArgs args, out Color lastColor, out int damagedCount)
        {
            lastColor = Color.white;
            damagedCount = 0;
            if (voxelData == null || activeVoxelCount <= 0) return 0;

            Vector3 localPoint = transform.InverseTransformPoint(worldPosition);
            float vSize = Mathf.Max(voxelSize, 0.0001f);
            Vector3 centerVox = localPoint / vSize;

            Vector3 ls = transform.lossyScale;
            float scaleMax = Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z), 0.0001f);

            bool useVoxRadius = args.radiusInVoxels || forceRadiusInVoxels;
            float radiusVox = useVoxRadius ? Mathf.Max(0.5f, args.radius) : (args.radius / (vSize * scaleMax));
            float radiusVoxSq = radiusVox * radiusVox;
            float invRadiusVox = radiusVox > 0.0001f ? 1f / radiusVox : 0f;

            Vector3Int min = new Vector3Int(
                Mathf.FloorToInt(centerVox.x - radiusVox - 0.5f),
                Mathf.FloorToInt(centerVox.y - radiusVox - 0.5f),
                Mathf.FloorToInt(centerVox.z - radiusVox - 0.5f)
            );
            Vector3Int max = new Vector3Int(
                Mathf.CeilToInt(centerVox.x + radiusVox + 0.5f),
                Mathf.CeilToInt(centerVox.y + radiusVox + 0.5f),
                Mathf.CeilToInt(centerVox.z + radiusVox + 0.5f)
            );

            int count = 0;
            for (int z = min.z; z <= max.z; z++)
                for (int y = min.y; y <= max.y; y++)
                    for (int x = min.x; x <= max.x; x++)
                    {
                        if (!voxelData.InBounds(x, y, z)) continue;
                        Vector3 voxelCenter = new Vector3(x, y, z);
                        float distSq = (voxelCenter - centerVox).sqrMagnitude;
                        if (distSq > radiusVoxSq) continue;

                        float dmg = args.damage;
                        if (dmg > 0f && args.distanceFalloff > 0f)
                        {
                            float distRatio = Mathf.Sqrt(distSq) * invRadiusVox;
                            dmg *= Mathf.Clamp01(1f - distRatio * Mathf.Clamp01(args.distanceFalloff));
                        }

                        if (ApplyVoxelDamage(x, y, z, dmg, out Color col, out bool partial))
                        {
                            lastColor = col;
                            count++;
                        }
                        else if (partial)
                        {
                            lastColor = col;
                            damagedCount++;
                        }
                    }

            if (count > 0)
                activeVoxelCount = Mathf.Max(0, activeVoxelCount - count);

            return count;
        }

        private VoxelDamageResult QueueDamageCommit(Vector3 hitPoint, int count, VoxelDamageArgs args)
        {
            if (count == 0) return default;

            if (!Application.isPlaying)
            {
                pendingDestroyedCount = count;
                pendingHitPoint = hitPoint;
                pendingArgs = args;
                return CommitPendingDamage();
            }

            pendingDestroyedCount += count;
            pendingHitPoint = hitPoint;
            pendingArgs = args;
            hasPendingDamage = true;

            return new VoxelDamageResult
            {
                destroyed = true,
                destroyedCount = count,
                hitPoint = hitPoint
            };
        }

        private VoxelDamageResult CommitPendingDamage()
        {
            hasPendingDamage = false;
            hasDirtyBounds = false;
            int count = pendingDestroyedCount;
            Vector3 hitPoint = pendingHitPoint;
            VoxelDamageArgs args = pendingArgs;
            Vector3Int dirtyMin = pendingDirtyMin;
            Vector3Int dirtyMax = pendingDirtyMax;
            pendingDestroyedCount = 0;

            if (count == 0) return default;

            if (isFragment)
                UnsettleFragment();
            WakeNearbyFragments(hitPoint, args.radius);

            int remaining = activeVoxelCount;
            int thresh = isFragment ? 2 : (initialVoxelCount > 0 ? Mathf.Min(collapseThreshold, Mathf.Max(2, initialVoxelCount / 10)) : collapseThreshold);
            float ratio = isFragment ? 0f : collapseRatio;

            if (remaining <= thresh || (initialVoxelCount > 0 && ratio > 0f && (float)remaining / initialVoxelCount <= ratio))
            {
                TriggerCollapse(hitPoint, args);
                return new VoxelDamageResult { destroyed = true, destroyedCount = count + remaining, hitPoint = hitPoint };
            }

            PlayDestroyAudio(hitPoint, args);

            int spawnedFragments = 0;
            if (args.isolateFragments)
                spawnedFragments = IsolateFragments(hitPoint, args, ref dirtyMin, ref dirtyMax);

            if (activeVoxelCount <= thresh)
            {
                TriggerCollapse(hitPoint, args);
                return new VoxelDamageResult { destroyed = true, destroyedCount = count + activeVoxelCount, spawnedFragmentsCount = spawnedFragments, hitPoint = hitPoint };
            }

            if (TryGetComponent<Rigidbody>(out var myRb) && !myRb.isKinematic)
            {
                myRb.mass = Mathf.Clamp(activeVoxelCount * 0.25f, 0.5f, 50f);
                if (args.explosionForce > 0f)
                {
                    Vector3 push = args.impactDirection != Vector3.zero ? args.impactDirection : (transform.position - hitPoint).normalized;
                    myRb.AddForceAtPosition(push * args.explosionForce, hitPoint, ForceMode.Impulse);
                }
            }

            RebuildDirtyChunks(dirtyMin, dirtyMax);

            VoxelDamageResult result = new VoxelDamageResult
            {
                destroyed = true,
                destroyedCount = count,
                spawnedFragmentsCount = spawnedFragments,
                hitPoint = hitPoint
            };

            OnVoxelDamaged?.Invoke(result);
            return result;
        }

        private void TriggerCollapse(Vector3 hitPoint, VoxelDamageArgs args)
        {
            if (emitDebrisParticles)
                EmitDebrisParticles(transform.position, Color.gray, isFragment ? 15 : 30);

            PlayDestroyAudio(hitPoint, args);
            if (meshFilter != null) meshFilter.sharedMesh = null;
            if (meshCollider != null) meshCollider.sharedMesh = null;
            if (subChunks != null)
            {
                for (int i = 0; i < subChunks.Length; i++)
                {
                    if (subChunks[i].meshFilter != null) subChunks[i].meshFilter.sharedMesh = null;
                    if (subChunks[i].meshCollider != null) subChunks[i].meshCollider.sharedMesh = null;
                }
            }
            RequestNavMeshUpdate();
            Destroy(gameObject);
        }

        private void PlayDestroyAudio(Vector3 worldPos, VoxelDamageArgs args)
        {
            AudioClip clip = args.customClip != null ? args.customClip : defaultDestroyClip;
            if (clip == null) return;
            float vol = args.soundVolume > 0 ? args.soundVolume : destroyVolume;

            if (AudioManager.Instance != null)
            {
                if (playSoundAtHitPoint)
                    AudioManager.Instance.PlaySFXAt(clip, worldPos, vol);
                else
                    AudioManager.Instance.PlaySFX2D(clip, vol);
                return;
            }

            if (playSoundAtHitPoint)
                AudioSource.PlayClipAtPoint(clip, worldPos, vol);
            else if (TryGetComponent<AudioSource>(out var src))
                src.PlayOneShot(clip, vol);
        }

        private static Mesh GetCubeMesh()
        {
            if (cachedCubeMesh != null) return cachedCubeMesh;

            Mesh mesh = new Mesh { name = "ProceduralVoxelCube" };
            Vector3[] vertices = new Vector3[24];
            int[] triangles = new int[36];

            Vector3[] faceNormals = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            Vector3[] faceTangents = { Vector3.right, Vector3.right, Vector3.forward, Vector3.back, Vector3.right, Vector3.left };

            for (int f = 0; f < 6; f++)
            {
                Vector3 n = faceNormals[f];
                Vector3 u = faceTangents[f];
                Vector3 v = Vector3.Cross(n, u);

                int vIdx = f * 4;
                vertices[vIdx + 0] = (n - u - v) * 0.5f;
                vertices[vIdx + 1] = (n + u - v) * 0.5f;
                vertices[vIdx + 2] = (n + u + v) * 0.5f;
                vertices[vIdx + 3] = (n - u + v) * 0.5f;

                int tIdx = f * 6;
                triangles[tIdx + 0] = vIdx + 0;
                triangles[tIdx + 1] = vIdx + 2;
                triangles[tIdx + 2] = vIdx + 1;
                triangles[tIdx + 3] = vIdx + 0;
                triangles[tIdx + 4] = vIdx + 3;
                triangles[tIdx + 5] = vIdx + 2;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            cachedCubeMesh = mesh;
            return cachedCubeMesh;
        }

        private void EmitDebrisParticles(Vector3 worldPoint, Color color, int count)
        {
            ParticleSystem ps = GetOrCreateSharedParticleSystem();
            if (ps == null) return;

            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                position = worldPoint,
                startColor = color,
                startSize = voxelSize * 0.25f
            };

            for (int i = 0; i < count; i++)
            {
                emitParams.position = worldPoint + UnityEngine.Random.insideUnitSphere * (voxelSize * 0.4f);
                emitParams.velocity = (UnityEngine.Random.insideUnitSphere + Vector3.up * 0.2f).normalized * UnityEngine.Random.Range(2f, 5f);
                ps.Emit(emitParams, 1);
            }
        }

        private ParticleSystem GetOrCreateSharedParticleSystem()
        {
            if (sharedParticleSystem != null)
            {
                var r = sharedParticleSystem.GetComponent<ParticleSystemRenderer>();
                if (r != null && r.sharedMaterial == null && vertexColorMaterial != null)
                    r.sharedMaterial = vertexColorMaterial;
                return sharedParticleSystem;
            }

            GameObject go = GameObject.Find("VoxelDebrisParticleSystem");
            if (go == null)
            {
                go = new GameObject("VoxelDebrisParticleSystem");
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.loop = false;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = 2.5f;
                main.startLifetime = 1.0f;
                main.startSize = 0.2f;

                var collision = ps.collision;
                collision.enabled = true;
                collision.type = ParticleSystemCollisionType.World;
                collision.mode = ParticleSystemCollisionMode.Collision3D;
                collision.dampen = 0.4f;
                collision.bounce = 0.3f;

                var em = ps.emission;
                em.enabled = false;

                var psr = go.GetComponent<ParticleSystemRenderer>();
                if (psr != null)
                {
                    psr.renderMode = ParticleSystemRenderMode.Mesh;
                    psr.mesh = GetCubeMesh();
                    if (vertexColorMaterial != null)
                        psr.sharedMaterial = vertexColorMaterial;
                }

                sharedParticleSystem = ps;
            }
            else
            {
                sharedParticleSystem = go.GetComponent<ParticleSystem>();
            }
            return sharedParticleSystem;
        }

        private struct ClusterData
        {
            public List<int> voxels;
            public bool isGrounded;
        }

        private static List<int> RentClusterList()
        {
            if (s_ClusterListPool.Count > 0)
            {
                var list = s_ClusterListPool.Pop();
                list.Clear();
                return list;
            }
            return new List<int>(64);
        }

        private static void ReleaseClusters()
        {
            for (int i = 0; i < s_Clusters.Count; i++)
            {
                if (s_Clusters[i].voxels != null)
                {
                    s_Clusters[i].voxels.Clear();
                    s_ClusterListPool.Push(s_Clusters[i].voxels);
                }
            }
            s_Clusters.Clear();
        }

        private int IsolateFragments(Vector3 damageCenter, VoxelDamageArgs args, ref Vector3Int dirtyMin, ref Vector3Int dirtyMax)
        {
            if (voxelData == null || activeVoxelCount <= 0) return 0;

            if (anchorToGround && !isFragment)
                return IsolateSingleGroundedFragments(damageCenter, args, ref dirtyMin, ref dirtyMax);

            int vol = voxelData.Volume;
            if (visitedBuffer == null || visitedBuffer.Length < vol)
                visitedBuffer = new int[Mathf.NextPowerOfTwo(vol)];

            currentRunId++;
            if (currentRunId == int.MaxValue)
            {
                Array.Clear(visitedBuffer, 0, visitedBuffer.Length);
                currentRunId = 1;
            }

            ReleaseClusters();
            s_FragmentsToSpawn.Clear();

            Vector3Int searchMin = new Vector3Int(
                Mathf.Max(0, dirtyMin.x - 2),
                Mathf.Max(0, dirtyMin.y - 2),
                Mathf.Max(0, dirtyMin.z - 2)
            );
            Vector3Int searchMax = new Vector3Int(
                Mathf.Min(voxelData.length.x - 1, dirtyMax.x + 2),
                Mathf.Min(voxelData.length.y - 1, dirtyMax.y + 2),
                Mathf.Min(voxelData.length.z - 1, dirtyMax.z + 2)
            );

            for (int z = searchMin.z; z <= searchMax.z; z++)
                for (int y = searchMin.y; y <= searchMax.y; y++)
                    for (int x = searchMin.x; x <= searchMax.x; x++)
                    {
                        int idx = voxelData.ToIndex(x, y, z);
                        if (visitedBuffer[idx] == currentRunId || !voxelData.voxels[idx].active) continue;
                        s_Clusters.Add(FloodFill(idx));
                    }

            if (s_Clusters.Count <= 1)
            {
                ReleaseClusters();
                return 0;
            }

            int largestIdx = 0;
            for (int i = 1; i < s_Clusters.Count; i++)
                if (s_Clusters[i].voxels.Count > s_Clusters[largestIdx].voxels.Count) largestIdx = i;

            for (int i = 0; i < s_Clusters.Count; i++)
                if (i != largestIdx) s_FragmentsToSpawn.Add(i);

            int spawned = SpawnSelectedClusters(damageCenter, args, ref dirtyMin, ref dirtyMax);
            ReleaseClusters();
            return spawned;
        }

        private int IsolateSingleGroundedFragments(Vector3 damageCenter, VoxelDamageArgs args, ref Vector3Int dirtyMin, ref Vector3Int dirtyMax)
        {
            int vol = voxelData.Volume;
            if (groundVisited == null || groundVisited.Length < vol)
                groundVisited = new int[vol];

            s_GroundRunId++;
            if (s_GroundRunId == int.MaxValue)
            {
                Array.Clear(groundVisited, 0, groundVisited.Length);
                s_GroundRunId = 1;
            }

            int runId = s_GroundRunId;
            groundedVoxelCount = 0;
            s_BfsQueue.Clear();

            int lx = voxelData.length.x;
            int ly = voxelData.length.y;
            int lz = voxelData.length.z;
            int strideZ = lx * ly;
            int maxSeedY = Mathf.Min(ly - 1, groundAnchorY);
            VoxVoxel[] voxArr = voxelData.voxels;

            for (int z = 0; z < lz; z++)
                for (int y = 0; y <= maxSeedY; y++)
                {
                    int rowStart = lx * (y + ly * z);
                    for (int x = 0; x < lx; x++)
                    {
                        int idx = rowStart + x;
                        if (voxArr[idx].active && groundVisited[idx] != runId)
                        {
                            groundVisited[idx] = runId;
                            groundedVoxelCount++;
                            s_BfsQueue.Enqueue(idx);
                        }
                    }
                }

            while (s_BfsQueue.Count > 0)
            {
                int cur = s_BfsQueue.Dequeue();
                int x = cur % lx;
                int yIdx = cur / lx;
                int y = yIdx % ly;
                int z = yIdx / ly;

                if (x + 1 < lx) { int n = cur + 1; if (groundVisited[n] != runId && voxArr[n].active) { groundVisited[n] = runId; groundedVoxelCount++; s_BfsQueue.Enqueue(n); } }
                if (x > 0)      { int n = cur - 1; if (groundVisited[n] != runId && voxArr[n].active) { groundVisited[n] = runId; groundedVoxelCount++; s_BfsQueue.Enqueue(n); } }
                if (y + 1 < ly) { int n = cur + lx; if (groundVisited[n] != runId && voxArr[n].active) { groundVisited[n] = runId; groundedVoxelCount++; s_BfsQueue.Enqueue(n); } }
                if (y > 0)      { int n = cur - lx; if (groundVisited[n] != runId && voxArr[n].active) { groundVisited[n] = runId; groundedVoxelCount++; s_BfsQueue.Enqueue(n); } }
                if (z + 1 < lz) { int n = cur + strideZ; if (groundVisited[n] != runId && voxArr[n].active) { groundVisited[n] = runId; groundedVoxelCount++; s_BfsQueue.Enqueue(n); } }
                if (z > 0)      { int n = cur - strideZ; if (groundVisited[n] != runId && voxArr[n].active) { groundVisited[n] = runId; groundedVoxelCount++; s_BfsQueue.Enqueue(n); } }
            }

            if (groundedVoxelCount >= activeVoxelCount)
                return 0;

            return ExtractAndSpawnUngrounded(damageCenter, args, runId, ref dirtyMin, ref dirtyMax);
        }

        private int ExtractAndSpawnUngrounded(Vector3 damageCenter, VoxelDamageArgs args, int groundRunId, ref Vector3Int dirtyMin, ref Vector3Int dirtyMax)
        {
            if (voxelData == null || activeVoxelCount <= 0) return 0;

            int vol = voxelData.Volume;
            if (visitedBuffer == null || visitedBuffer.Length < vol)
                visitedBuffer = new int[Mathf.NextPowerOfTwo(vol)];

            currentRunId++;
            if (currentRunId == int.MaxValue)
            {
                Array.Clear(visitedBuffer, 0, visitedBuffer.Length);
                currentRunId = 1;
            }

            ReleaseClusters();
            s_FragmentsToSpawn.Clear();

            VoxVoxel[] voxArr = voxelData.voxels;
            int runId = currentRunId;

            for (int idx = 0; idx < vol; idx++)
            {
                if (!voxArr[idx].active || groundVisited[idx] == groundRunId || visitedBuffer[idx] == runId) continue;
                ClusterData cluster = FloodFillUngrounded(idx, groundRunId);
                s_FragmentsToSpawn.Add(s_Clusters.Count);
                s_Clusters.Add(cluster);
            }

            if (s_FragmentsToSpawn.Count == 0)
            {
                ReleaseClusters();
                return 0;
            }

            int spawned = SpawnSelectedClusters(damageCenter, args, ref dirtyMin, ref dirtyMax);
            ReleaseClusters();
            return spawned;
        }

        private int SpawnSelectedClusters(Vector3 damageCenter, VoxelDamageArgs args, ref Vector3Int dirtyMin, ref Vector3Int dirtyMax)
        {
            if (s_FragmentsToSpawn.Count > 1)
                s_FragmentsToSpawn.Sort((a, b) => s_Clusters[b].voxels.Count.CompareTo(s_Clusters[a].voxels.Count));

            int spawned = 0;
            int permMin = permanentFragmentMinSize;
            if (initialVoxelCount > 0 && permanentFragmentMinSize > 0)
                permMin = Mathf.Min(permanentFragmentMinSize, Mathf.Max(3, Mathf.RoundToInt(initialVoxelCount * 0.35f)));

            int minSize = isFragment ? 2 : (initialVoxelCount > 0 ? Mathf.Min(minClusterSize, Mathf.Max(1, initialVoxelCount / 15)) : minClusterSize);

            for (int f = 0; f < s_FragmentsToSpawn.Count; f++)
            {
                int idx = s_FragmentsToSpawn[f];
                List<int> clusterVoxels = s_Clusters[idx].voxels;

                if (spawned < maxFragmentsPerCall && clusterVoxels.Count >= minSize)
                {
                    VoxVoxelData fragData = ExtractCluster(clusterVoxels, ref dirtyMin, ref dirtyMax, out Vector3 offset);
                    if (fragData != null)
                    {
                        float life = (permMin > 0 && clusterVoxels.Count >= permMin)
                            ? 0f
                            : (args.fragmentLifetime > 0 ? args.fragmentLifetime : defaultFragmentLifetime);

                        spawned += SpawnFragmentOrSlices(fragData, offset, damageCenter, args, life, clusterVoxels.Count);
                    }
                }
                else
                {
                    VoxVoxelData fragData = ExtractCluster(clusterVoxels, ref dirtyMin, ref dirtyMax, out Vector3 offset);
                    if (fragData != null && emitDebrisParticles)
                    {
                        Vector3 wPos = transform.TransformPoint(offset * voxelSize);
                        EmitDebrisParticles(wPos, Color.gray, Mathf.Clamp(clusterVoxels.Count * 2, 4, 20));
                    }
                }
            }

            return spawned;
        }

        private int SpawnFragmentOrSlices(VoxVoxelData fragData, Vector3 baseOffset, Vector3 damageCenter, VoxelDamageArgs args, float life, int totalCount)
        {
            int cSize = chunkSize > 0 ? chunkSize : 32;
            if (totalCount <= 8192 || (fragData.length.x <= cSize && fragData.length.y <= cSize && fragData.length.z <= cSize))
            {
                SpawnFragmentObject(fragData, baseOffset, damageCenter, args, life, totalCount);
                return 1;
            }

            int spawned = 0;
            int lx = fragData.length.x, ly = fragData.length.y, lz = fragData.length.z;
            for (int cz = 0; cz < lz; cz += cSize)
            {
                int ez = Mathf.Min(cz + cSize, lz);
                for (int cy = 0; cy < ly; cy += cSize)
                {
                    int ey = Mathf.Min(cy + cSize, ly);
                    for (int cx = 0; cx < lx; cx += cSize)
                    {
                        int ex = Mathf.Min(cx + cSize, lx);
                        if (!TrySliceFragmentBlock(fragData, cx, cy, cz, ex, ey, ez, out VoxVoxelData blockData, out Vector3Int localMin, out int blockCount))
                            continue;
                        SpawnFragmentObject(blockData, baseOffset + (Vector3)localMin, damageCenter, args, life, blockCount);
                        spawned++;
                    }
                }
            }
            return spawned;
        }

        private static bool TrySliceFragmentBlock(VoxVoxelData src, int sx, int sy, int sz, int ex, int ey, int ez, out VoxVoxelData blockData, out Vector3Int minCoord, out int count)
        {
            blockData = null;
            minCoord = Vector3Int.zero;
            count = 0;

            int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue, maxZ = int.MinValue;

            for (int z = sz; z < ez; z++)
                for (int y = sy; y < ey; y++)
                    for (int x = sx; x < ex; x++)
                    {
                        if (!src.GetVoxel(x, y, z).active) continue;
                        count++;
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                        if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
                    }

            if (count == 0) return false;

            minCoord = new Vector3Int(minX, minY, minZ);
            Vector3Int size = new Vector3Int(maxX - minX + 1, maxY - minY + 1, maxZ - minZ + 1);
            VoxVoxel[] voxels = new VoxVoxel[size.x * size.y * size.z];

            for (int z = minZ; z <= maxZ; z++)
                for (int y = minY; y <= maxY; y++)
                    for (int x = minX; x <= maxX; x++)
                    {
                        VoxVoxel v = src.GetVoxel(x, y, z);
                        if (v.active)
                            voxels[(x - minX) + size.x * ((y - minY) + size.y * (z - minZ))] = v;
                    }

            blockData = new VoxVoxelData(voxels, src.palette, size);
            return true;
        }

        private ClusterData FloodFillUngrounded(int startIdx, int groundRunId)
        {
            ClusterData cluster = new ClusterData { voxels = RentClusterList(), isGrounded = false };
            s_BfsQueue.Clear();
            s_BfsQueue.Enqueue(startIdx);
            visitedBuffer[startIdx] = currentRunId;

            int lx = voxelData.length.x;
            int ly = voxelData.length.y;
            int lz = voxelData.length.z;
            int strideZ = lx * ly;
            VoxVoxel[] voxArr = voxelData.voxels;
            int runId = currentRunId;

            while (s_BfsQueue.Count > 0)
            {
                int cur = s_BfsQueue.Dequeue();
                cluster.voxels.Add(cur);

                int x = cur % lx;
                int yIdx = cur / lx;
                int y = yIdx % ly;
                int z = yIdx / ly;

                if (x + 1 < lx) { int n = cur + 1; if (visitedBuffer[n] != runId && groundVisited[n] != groundRunId && voxArr[n].active) { visitedBuffer[n] = runId; s_BfsQueue.Enqueue(n); } }
                if (x > 0)      { int n = cur - 1; if (visitedBuffer[n] != runId && groundVisited[n] != groundRunId && voxArr[n].active) { visitedBuffer[n] = runId; s_BfsQueue.Enqueue(n); } }
                if (y + 1 < ly) { int n = cur + lx; if (visitedBuffer[n] != runId && groundVisited[n] != groundRunId && voxArr[n].active) { visitedBuffer[n] = runId; s_BfsQueue.Enqueue(n); } }
                if (y > 0)      { int n = cur - lx; if (visitedBuffer[n] != runId && groundVisited[n] != groundRunId && voxArr[n].active) { visitedBuffer[n] = runId; s_BfsQueue.Enqueue(n); } }
                if (z + 1 < lz) { int n = cur + strideZ; if (visitedBuffer[n] != runId && groundVisited[n] != groundRunId && voxArr[n].active) { visitedBuffer[n] = runId; s_BfsQueue.Enqueue(n); } }
                if (z > 0)      { int n = cur - strideZ; if (visitedBuffer[n] != runId && groundVisited[n] != groundRunId && voxArr[n].active) { visitedBuffer[n] = runId; s_BfsQueue.Enqueue(n); } }
            }
            return cluster;
        }

        private ClusterData FloodFill(int startIdx)
        {
            ClusterData cluster = new ClusterData { voxels = RentClusterList(), isGrounded = false };
            s_BfsQueue.Clear();
            s_BfsQueue.Enqueue(startIdx);
            visitedBuffer[startIdx] = currentRunId;

            int lx = voxelData.length.x;
            int ly = voxelData.length.y;
            int lz = voxelData.length.z;
            int strideZ = lx * ly;
            VoxVoxel[] voxArr = voxelData.voxels;
            int runId = currentRunId;

            while (s_BfsQueue.Count > 0)
            {
                int cur = s_BfsQueue.Dequeue();
                cluster.voxels.Add(cur);

                int x = cur % lx;
                int yIdx = cur / lx;
                int y = yIdx % ly;
                int z = yIdx / ly;

                if (x + 1 < lx) { int n = cur + 1; if (visitedBuffer[n] != runId && voxArr[n].active) { visitedBuffer[n] = runId; s_BfsQueue.Enqueue(n); } }
                if (x > 0)      { int n = cur - 1; if (visitedBuffer[n] != runId && voxArr[n].active) { visitedBuffer[n] = runId; s_BfsQueue.Enqueue(n); } }
                if (y + 1 < ly) { int n = cur + lx; if (visitedBuffer[n] != runId && voxArr[n].active) { visitedBuffer[n] = runId; s_BfsQueue.Enqueue(n); } }
                if (y > 0)      { int n = cur - lx; if (visitedBuffer[n] != runId && voxArr[n].active) { visitedBuffer[n] = runId; s_BfsQueue.Enqueue(n); } }
                if (z + 1 < lz) { int n = cur + strideZ; if (visitedBuffer[n] != runId && voxArr[n].active) { visitedBuffer[n] = runId; s_BfsQueue.Enqueue(n); } }
                if (z > 0)      { int n = cur - strideZ; if (visitedBuffer[n] != runId && voxArr[n].active) { visitedBuffer[n] = runId; s_BfsQueue.Enqueue(n); } }
            }
            return cluster;
        }

        private VoxVoxelData ExtractCluster(List<int> cluster, ref Vector3Int dirtyMin, ref Vector3Int dirtyMax, out Vector3 offset)
        {
            offset = Vector3.zero;
            if (cluster == null || cluster.Count == 0) return null;

            Vector3Int min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
            Vector3Int max = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);

            for (int i = 0; i < cluster.Count; i++)
            {
                voxelData.ToCoords(cluster[i], out int x, out int y, out int z);
                if (x < min.x) min.x = x;
                if (y < min.y) min.y = y;
                if (z < min.z) min.z = z;
                if (x > max.x) max.x = x;
                if (y > max.y) max.y = y;
                if (z > max.z) max.z = z;
            }

            dirtyMin = Vector3Int.Min(dirtyMin, min);
            dirtyMax = Vector3Int.Max(dirtyMax, max);

            Vector3Int size = max - min + Vector3Int.one;
            VoxVoxel[] voxels = new VoxVoxel[size.x * size.y * size.z];

            for (int i = 0; i < cluster.Count; i++)
            {
                int cIdx = cluster[i];
                voxelData.ToCoords(cIdx, out int x, out int y, out int z);
                VoxVoxel v = voxelData.voxels[cIdx];
                v.active = true;
                voxels[(x - min.x) + size.x * ((y - min.y) + size.y * (z - min.z))] = v;

                v.active = false;
                voxelData.voxels[cIdx] = v;
            }

            activeVoxelCount = Mathf.Max(0, activeVoxelCount - cluster.Count);
            offset = new Vector3(min.x, min.y, min.z);
            return new VoxVoxelData(voxels, voxelData.palette, size);
        }

        private static void EnforceGlobalFragmentLimit()
        {
            if (MaxGlobalFragments <= 0) return;
            for (int i = activeFragments.Count - 1; i >= 0; i--)
            {
                if (activeFragments[i] == null)
                    activeFragments.RemoveAt(i);
            }

            while (activeFragments.Count >= MaxGlobalFragments)
            {
                GameObject oldest = activeFragments[0];
                activeFragments.RemoveAt(0);
                if (oldest != null)
                    Destroy(oldest);
            }
        }

        private void SpawnFragmentObject(VoxVoxelData data, Vector3 offset, Vector3 damageCenter, VoxelDamageArgs args, float lifetime, int initialCount)
        {
            EnforceGlobalFragmentLimit();

            if (s_FragmentRoot == null)
            {
                GameObject rootGo = GameObject.Find(autoRootName);
                if (rootGo == null) rootGo = new GameObject(autoRootName);
                s_FragmentRoot = rootGo.transform;
            }

            bool isPermanent = lifetime <= 0f;

            GameObject go = new GameObject("VoxFragment");
            activeFragments.Add(go);
            go.layer = gameObject.layer;
            go.tag = gameObject.tag;
            go.transform.SetParent(s_FragmentRoot, true);
            go.transform.position = transform.TransformPoint(offset * voxelSize);
            go.transform.rotation = transform.rotation;
            go.transform.localScale = transform.lossyScale;

            NavMeshModifier mod = go.AddComponent<NavMeshModifier>();
            mod.ignoreFromBuild = true;
            mod.overrideArea = true;
            mod.area = 1;

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            MeshCollider mc = go.AddComponent<MeshCollider>();
            mc.cookingOptions = FastCookingOptions;
            mc.convex = true;
            SetPlayerCollisionIgnore(mc, true);

            VoxVoxelObject frag = go.AddComponent<VoxVoxelObject>();
            frag.navModifier = mod;
            frag.isFragment = true;
            frag.isPermanentFragment = isPermanent;
            frag.collapseThreshold = 2;
            frag.collapseRatio = 0f;
            frag.CopySettingsFrom(this);
            frag.initialVoxelCount = initialCount;
            frag.AssignVoxelData(data, initialCount);

            rb.mass = Mathf.Clamp(initialCount * 0.25f, 0.5f, 80f);
            frag.StartSettlingCheck();

            if (args.explosionForce > 0f)
            {
                Vector3 pushDir;
                if (args.impactDirection != Vector3.zero)
                    pushDir = (args.impactDirection.normalized + Vector3.down * 0.2f).normalized;
                else
                    pushDir = (go.transform.position - damageCenter).normalized;

                float massScale = Mathf.Max(1f, Mathf.Sqrt(rb.mass * 0.5f));
                rb.AddForce(pushDir * (args.explosionForce * massScale), ForceMode.Impulse);
                rb.AddTorque(UnityEngine.Random.insideUnitSphere * (args.explosionForce * 0.4f * massScale), ForceMode.Impulse);
            }

            if (lifetime > 0f)
                Destroy(go, lifetime);
        }
    }
}
