using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelDestructionPro.Vox
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class VoxVoxelObject : MonoBehaviour
    {
        [Header("Voxel Settings")]
        [SerializeField] private float voxelSize = 1f;

        [Header("Rendering")]
        [SerializeField] private Material vertexColorMaterial;

        [Header("Audio")]
        [SerializeField] private AudioClip defaultDestroyClip;
        [Range(0f, 1f)]
        [SerializeField] private float destroyVolume = 1f;
        [SerializeField] private bool playSoundAtHitPoint = true;

        [Header("Structural Integrity & Ground Anchor")]
        [Tooltip("바닥(Local Y <= groundAnchorY)에 닿은 덩어리를 메인 지탱 벽으로 유지합니다.")]
        [SerializeField] private bool anchorToGround = true;
        [SerializeField] private int groundAnchorY = 0;

        [Header("Collapse Settings")]
        [Tooltip("남은 복셀이 이 개수 이하가 되면 잔해를 남기지 않고 전면 붕괴(파티클 소멸)합니다.")]
        [SerializeField] private int collapseThreshold = 8;
        [Range(0f, 0.3f)]
        [SerializeField] private float collapseRatio = 0f;

        [Header("Debris & Fragments")]
        [SerializeField] private bool emitDebrisParticles = true;
        [SerializeField] private int minClusterSize = 4;
        [Tooltip("이 복셀 개수 이상의 큰 덩어리는 타이머로 사라지지 않고 영구 물리 오브젝트로 유지됩니다 (0이면 모든 파편에 타이머 적용).")]
        [SerializeField] private int permanentFragmentMinSize = 25;
        [SerializeField] private int maxFragmentsPerCall = 8;
        [SerializeField] private float defaultFragmentLifetime = 8f;
        [SerializeField] private string autoRootName = "FragmentsRoot";

        [Header("Test Wall Generator")]
        [SerializeField] private bool generateWallOnStart = false;
        [SerializeField] private Vector3Int wallDimensions = new Vector3Int(16, 12, 3);
        [SerializeField] private Color wallColor1 = new Color(0.6f, 0.6f, 0.65f);
        [SerializeField] private Color wallColor2 = new Color(0.45f, 0.45f, 0.5f);

        [Header("VOX Model")]
        [Tooltip("Assets 폴더 기준 .vox 파일 경로 (예: Assets/Art/PlaceHolderAssets/castle.vox)")]
        [SerializeField] private string voxAssetPath = "Assets/Art/PlaceHolderAssets/castle.vox";
        [SerializeField] private bool loadVoxOnStart = false;

        [Header("Performance")]
        [SerializeField] private int maxGlobalFragments = 40;

        [SerializeField] private VoxVoxelData voxelData;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MeshCollider meshCollider;
        private static Transform s_FragmentRoot;
        private int initialVoxelCount;
        private bool isFragment = false;
        private Coroutine settleCoroutine;

        private static int[] visitedBuffer;
        private static int currentRunId = 0;
        private static readonly Queue<int> s_BfsQueue = new Queue<int>();
        private static readonly List<GameObject> activeFragments = new List<GameObject>();
        private static ParticleSystem sharedParticleSystem;
        private static Mesh cachedCubeMesh;

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
            bool isDynamic = TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic;
            meshCollider.enabled = true;
            meshCollider.convex = isDynamic;
            EnsureMaterial();
        }

        private void Start()
        {
            if (loadVoxOnStart && !string.IsNullOrEmpty(voxAssetPath))
                LoadVoxModel();
            else if (generateWallOnStart && (voxelData == null || voxelData.IsEmpty()))
                GenerateTestWall();

            if (voxelData != null && !isFragment)
                initialVoxelCount = voxelData.CountActive();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            meshCollider = GetComponent<MeshCollider>();
            if (meshCollider == null) meshCollider = gameObject.AddComponent<MeshCollider>();
            EnsureMaterial();

            if (!Application.isPlaying && voxelData != null)
                RebuildMesh();
        }
#endif

        private void EnsureMaterial()
        {
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null && vertexColorMaterial != null)
                meshRenderer.sharedMaterial = vertexColorMaterial;
        }

        public void CopySettingsFrom(VoxVoxelObject source)
        {
            if (source == null) return;
            voxelSize = source.voxelSize;
            vertexColorMaterial = source.vertexColorMaterial;
            defaultDestroyClip = source.defaultDestroyClip;
            destroyVolume = source.destroyVolume;
            anchorToGround = false;
            defaultFragmentLifetime = source.defaultFragmentLifetime;
            permanentFragmentMinSize = source.permanentFragmentMinSize;
            emitDebrisParticles = source.emitDebrisParticles;
            maxGlobalFragments = source.maxGlobalFragments;
            EnsureMaterial();
        }

        public void AssignVoxelData(VoxVoxelData data)
        {
            voxelData = data;
            if (!isFragment)
                initialVoxelCount = data != null ? data.CountActive() : 0;
            RebuildMesh();
        }

        /// <summary>
        /// 테스트용 벽면 복셀을 생성합니다.
        /// </summary>
        [ContextMenu("Generate Test Wall")]
        public void GenerateTestWall()
        {
            AssignVoxelData(VoxVoxelData.CreateBox(wallDimensions.x, wallDimensions.y, wallDimensions.z, wallColor1, wallColor2));
        }

        /// <summary>
        /// 지정된 .vox 파일을 로드하여 복셀 메쉬를 생성합니다.
        /// </summary>
        [ContextMenu("Load VOX Model")]
        public bool LoadVoxModel()
        {
            VoxVoxelData data = VoxVoxLoader.LoadFromFile(voxAssetPath, true);
            if (data != null)
            {
                AssignVoxelData(data);
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEditor.EditorUtility.SetDirty(this);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
                }
#endif
                return true;
            }
            return false;
        }

        /// <summary>
        /// 복셀 데이터를 기반으로 메쉬 및 콜라이더를 재구성합니다.
        /// </summary>
        public void RebuildMesh()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (meshCollider == null) meshCollider = GetComponent<MeshCollider>();
            if (meshCollider == null) meshCollider = gameObject.AddComponent<MeshCollider>();

            EnsureMaterial();

            Mesh mesh = (voxelData != null && !voxelData.IsEmpty())
                ? VoxMesher.BuildMesh(voxelData, voxelSize)
                : null;

            meshFilter.sharedMesh = mesh;

            bool isDynamic = TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic;
            meshCollider.enabled = true;
            meshCollider.sharedMesh = null;
            meshCollider.convex = isDynamic;
            if (mesh != null)
                meshCollider.sharedMesh = mesh;
        }


        /// <summary>
        /// 파편의 낙하 정지 감지 코루틴을 시작합니다.
        /// </summary>
        public void StartSettlingCheck()
        {
            if (!isFragment || !Application.isPlaying) return;
            if (settleCoroutine != null) StopCoroutine(settleCoroutine);
            settleCoroutine = StartCoroutine(CheckSettlementRoutine());
        }

        private System.Collections.IEnumerator CheckSettlementRoutine()
        {
            yield return new WaitForSeconds(0.4f);
            var rb = GetComponent<Rigidbody>();
            if (rb == null) yield break;

            int stillCount = 0;
            while (rb != null && !rb.isKinematic)
            {
                yield return new WaitForSeconds(0.2f);
                if (rb == null) yield break;

                if (rb.linearVelocity.sqrMagnitude < 0.05f && rb.angularVelocity.sqrMagnitude < 0.05f)
                {
                    stillCount++;
                    if (stillCount >= 2)
                    {
                        SettleFragment();
                        yield break;
                    }
                }
                else
                {
                    stillCount = 0;
                }
            }
        }

        /// <summary>
        /// 바닥에 멈춘 파편을 Kinematic으로 전환하고 원래의 정밀 오목 메쉬 콜라이더로 복원합니다.
        /// </summary>
        public void SettleFragment()
        {
            if (!isFragment || !TryGetComponent<Rigidbody>(out var rb)) return;
            if (rb.isKinematic) return;

            rb.isKinematic = true;
            if (meshCollider != null)
            {
                meshCollider.sharedMesh = null;
                meshCollider.convex = false;
                meshCollider.sharedMesh = meshFilter.sharedMesh;
            }
        }

        /// <summary>
        /// 피해를 입거나 외부 충격 시 물리 낙하 및 볼록 콜라이더를 재개합니다.
        /// </summary>
        public void UnsettleFragment()
        {
            if (!isFragment || !TryGetComponent<Rigidbody>(out var rb)) return;
            if (!rb.isKinematic) return;

            if (meshCollider != null)
            {
                meshCollider.sharedMesh = null;
                meshCollider.convex = true;
                meshCollider.sharedMesh = meshFilter.sharedMesh;
            }
            rb.isKinematic = false;
            rb.WakeUp();
            StartSettlingCheck();
        }

        /// <summary>
        /// 구형 범위의 복셀을 파괴하고 결과를 반환합니다.
        /// </summary>
        public VoxelDamageResult DamageSphere(Vector3 worldPosition, VoxelDamageArgs args)
        {
            return ExecuteSphereDamage(worldPosition, args);
        }

        /// <summary>
        /// 구형 범위 파괴 (기존 호환용).
        /// </summary>
        public bool DestroySphere(Vector3 worldPosition, VoxelDamageArgs args)
        {
            return ExecuteSphereDamage(worldPosition, args).destroyed;
        }

        /// <summary>
        /// 직육면체(Box) 범위의 복셀을 파괴합니다.
        /// </summary>
        public VoxelDamageResult DamageBox(Vector3 worldCenter, Vector3 worldHalfExtents, Quaternion worldRotation, VoxelDamageArgs args)
        {
            if (voxelData == null || voxelData.IsEmpty()) return default;

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
                            VoxVoxel v = voxelData.GetVoxel(x, y, z);
                            if (v.active)
                            {
                                v.active = false;
                                voxelData.SetVoxel(x, y, z, v);
                                if (voxelData.palette != null && v.colorIndex < voxelData.palette.Length)
                                    sampleColor = voxelData.palette[v.colorIndex];
                                count++;
                            }
                        }
                    }

            if (emitDebrisParticles && count > 0)
                EmitDebrisParticles(worldCenter, sampleColor, Mathf.Clamp(count, 6, 25));

            return FinishDamage(worldCenter, count, args);
        }

        /// <summary>
        /// 레이(Ray)를 따라 관통하며 복셀을 파괴합니다.
        /// </summary>
        public VoxelDamageResult DamageRay(Ray ray, float maxDistance, VoxelDamageArgs args)
        {
            int stepCount = Mathf.Max(1, Mathf.CeilToInt(maxDistance / (voxelSize * 0.8f)));
            float stepDist = maxDistance / stepCount;
            int totalDestroyed = 0;
            Vector3 firstHit = ray.origin;
            bool hitAny = false;

            for (int i = 0; i <= stepCount; i++)
            {
                Vector3 pt = ray.GetPoint(i * stepDist);
                int removed = InternalSphereRemove(pt, args.radius, out Color col);
                if (removed > 0)
                {
                    if (!hitAny) { firstHit = pt; hitAny = true; }
                    totalDestroyed += removed;
                    if (emitDebrisParticles)
                        EmitDebrisParticles(pt, col, Mathf.Clamp(removed * 2, 4, 15));
                }
            }

            return FinishDamage(firstHit, totalDestroyed, args);
        }

        private VoxelDamageResult ExecuteSphereDamage(Vector3 worldPosition, VoxelDamageArgs args)
        {
            if (isFragment)
                UnsettleFragment();

            int destroyed = InternalSphereRemove(worldPosition, args.radius, out Color col);
            if (destroyed == 0)
            {
                Vector3 searchDir = args.impactDirection;
                if (searchDir == Vector3.zero && voxelData != null)
                {
                    Vector3 centerWorld = transform.TransformPoint((Vector3)voxelData.length * 0.5f * voxelSize);
                    searchDir = (centerWorld - worldPosition).normalized;
                }

                if (searchDir != Vector3.zero)
                {
                    float maxD = Mathf.Max(args.radius * 2.5f, voxelSize * 2f);
                    float step = Mathf.Max(voxelSize * 0.5f, 0.2f);
                    for (float d = step; d <= maxD; d += step)
                    {
                        Vector3 deeper = worldPosition + searchDir.normalized * d;
                        destroyed = InternalSphereRemove(deeper, args.radius, out col);
                        if (destroyed > 0)
                        {
                            worldPosition = deeper;
                            break;
                        }
                    }
                }
            }

            if (emitDebrisParticles && destroyed > 0)
                EmitDebrisParticles(worldPosition, col, Mathf.Clamp(destroyed * 2, 8, 30));

            return FinishDamage(worldPosition, destroyed, args);
        }

        private int InternalSphereRemove(Vector3 worldPosition, float radius, out Color lastColor)
        {
            lastColor = Color.white;
            if (voxelData == null || voxelData.IsEmpty()) return 0;

            Vector3 localPoint = transform.InverseTransformPoint(worldPosition);
            float vSize = Mathf.Max(voxelSize, 0.0001f);
            Vector3 centerVox = localPoint / vSize;

            Vector3 ls = transform.lossyScale;
            float scaleMax = Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z), 0.0001f);

            float radiusVox = radius / (vSize * scaleMax);
            float radiusVoxSq = radiusVox * radiusVox;

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
                        if ((voxelCenter - centerVox).sqrMagnitude > radiusVoxSq) continue;

                        VoxVoxel v = voxelData.GetVoxel(x, y, z);
                        if (!v.active) continue;

                        v.active = false;
                        voxelData.SetVoxel(x, y, z, v);
                        if (voxelData.palette != null && v.colorIndex < voxelData.palette.Length)
                            lastColor = voxelData.palette[v.colorIndex];
                        count++;
                    }

            return count;
        }

        private VoxelDamageResult FinishDamage(Vector3 hitPoint, int count, VoxelDamageArgs args)
        {
            if (count == 0) return default;

            int remaining = voxelData.CountActive();
            int thresh = isFragment ? 2 : (initialVoxelCount > 0 ? Mathf.Min(collapseThreshold, Mathf.Max(2, initialVoxelCount / 10)) : collapseThreshold);
            float ratio = isFragment ? 0f : collapseRatio;

            if (remaining <= thresh || (initialVoxelCount > 0 && ratio > 0f && (float)remaining / initialVoxelCount <= ratio))
            {
                TriggerCollapse(hitPoint, args);
                return new VoxelDamageResult { destroyed = true, destroyedCount = count + remaining, hitPoint = hitPoint };
            }

            PlayDestroyAudio(hitPoint, args);

            if (TryGetComponent<Rigidbody>(out var myRb))
            {
                myRb.mass = Mathf.Clamp(remaining * 0.25f, 0.5f, 50f);
                if (args.explosionForce > 0f)
                {
                    Vector3 push = args.impactDirection != Vector3.zero ? args.impactDirection : (transform.position - hitPoint).normalized;
                    myRb.AddForceAtPosition(push * args.explosionForce, hitPoint, ForceMode.Impulse);
                }
            }

            int spawnedFragments = 0;
            if (args.isolateFragments)
                spawnedFragments = IsolateFragments(hitPoint, args);

            RebuildMesh();
            WakeNearbyFragments(hitPoint, args.radius + voxelSize * 4f);

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
            WakeNearbyFragments(transform.position, Mathf.Max(voxelSize * 10f, 5f));
            Destroy(gameObject);
        }

        private static void WakeNearbyFragments(Vector3 center, float radius)
        {
            float rSq = radius * radius;
            for (int i = activeFragments.Count - 1; i >= 0; i--)
            {
                var fragGo = activeFragments[i];
                if (fragGo == null)
                {
                    activeFragments.RemoveAt(i);
                    continue;
                }
                if ((fragGo.transform.position - center).sqrMagnitude <= rSq)
                {
                    if (fragGo.TryGetComponent<VoxVoxelObject>(out var frag))
                        frag.UnsettleFragment();
                }
            }
        }

        private void PlayDestroyAudio(Vector3 worldPos, VoxelDamageArgs args)
        {
            AudioClip clip = args.customClip != null ? args.customClip : defaultDestroyClip;
            if (clip == null) return;
            float vol = args.soundVolume > 0 ? args.soundVolume : destroyVolume;

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

        private int IsolateFragments(Vector3 damageCenter, VoxelDamageArgs args)
        {
            if (voxelData == null || voxelData.IsEmpty()) return 0;

            int vol = voxelData.Volume;
            if (visitedBuffer == null || visitedBuffer.Length < vol)
                visitedBuffer = new int[Mathf.NextPowerOfTwo(vol)];

            currentRunId++;
            if (currentRunId == int.MaxValue)
            {
                Array.Clear(visitedBuffer, 0, visitedBuffer.Length);
                currentRunId = 1;
            }

            List<ClusterData> clusters = new List<ClusterData>();

            Vector3 localCenter = transform.InverseTransformPoint(damageCenter);
            float vSize = Mathf.Max(voxelSize, 0.0001f);
            Vector3 centerVox = localCenter / vSize;
            Vector3 ls = transform.lossyScale;
            float scaleMax = Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z), 0.0001f);
            float scanRadiusVox = (args.radius / (vSize * scaleMax)) + 2f;

            Vector3Int searchMin = new Vector3Int(
                Mathf.Max(0, Mathf.FloorToInt(centerVox.x - scanRadiusVox)),
                Mathf.Max(0, Mathf.FloorToInt(centerVox.y - scanRadiusVox)),
                Mathf.Max(0, Mathf.FloorToInt(centerVox.z - scanRadiusVox))
            );
            Vector3Int searchMax = new Vector3Int(
                Mathf.Min(voxelData.length.x - 1, Mathf.CeilToInt(centerVox.x + scanRadiusVox)),
                Mathf.Min(voxelData.length.y - 1, Mathf.CeilToInt(centerVox.y + scanRadiusVox)),
                Mathf.Min(voxelData.length.z - 1, Mathf.CeilToInt(centerVox.z + scanRadiusVox))
            );

            for (int z = searchMin.z; z <= searchMax.z; z++)
                for (int y = searchMin.y; y <= searchMax.y; y++)
                    for (int x = searchMin.x; x <= searchMax.x; x++)
                    {
                        int idx = voxelData.ToIndex(x, y, z);
                        if (visitedBuffer[idx] == currentRunId || !voxelData.voxels[idx].active) continue;
                        ClusterData cluster = FloodFill(idx);
                        clusters.Add(cluster);
                    }

            if (clusters.Count <= 1) return 0;

            List<int> fragmentsToSpawn = new List<int>();

            if (anchorToGround && !isFragment)
            {
                bool hasGrounded = false;
                for (int i = 0; i < clusters.Count; i++)
                {
                    if (clusters[i].isGrounded)
                        hasGrounded = true;
                    else
                        fragmentsToSpawn.Add(i);
                }

                if (!hasGrounded)
                {
                    fragmentsToSpawn.Clear();
                    int largestIdx = 0;
                    for (int i = 1; i < clusters.Count; i++)
                        if (clusters[i].voxels.Count > clusters[largestIdx].voxels.Count) largestIdx = i;

                    for (int i = 0; i < clusters.Count; i++)
                        if (i != largestIdx) fragmentsToSpawn.Add(i);
                }
            }
            else
            {
                int largestIdx = 0;
                for (int i = 1; i < clusters.Count; i++)
                    if (clusters[i].voxels.Count > clusters[largestIdx].voxels.Count) largestIdx = i;

                for (int i = 0; i < clusters.Count; i++)
                    if (i != largestIdx) fragmentsToSpawn.Add(i);
            }

            if (fragmentsToSpawn.Count == 0) return 0;

            int spawned = 0;
            int permMin = permanentFragmentMinSize;
            if (initialVoxelCount > 0 && permanentFragmentMinSize > 0)
                permMin = Mathf.Min(permanentFragmentMinSize, Mathf.Max(3, Mathf.RoundToInt(initialVoxelCount * 0.35f)));

            int minSize = isFragment ? 2 : (initialVoxelCount > 0 ? Mathf.Min(minClusterSize, Mathf.Max(1, initialVoxelCount / 15)) : minClusterSize);

            foreach (int idx in fragmentsToSpawn)
            {
                if (spawned >= maxFragmentsPerCall) break;
                List<int> clusterVoxels = clusters[idx].voxels;

                if (clusterVoxels.Count >= minSize)
                {
                    VoxVoxelData fragData = ExtractCluster(clusterVoxels, out Vector3 offset);
                    if (fragData != null)
                    {
                        float life = (permMin > 0 && clusterVoxels.Count >= permMin)
                            ? 0f
                            : (args.fragmentLifetime > 0 ? args.fragmentLifetime : defaultFragmentLifetime);

                        SpawnFragmentObject(fragData, offset, damageCenter, args, life, clusterVoxels.Count);
                        spawned++;
                    }
                }
                else
                {
                    VoxVoxelData fragData = ExtractCluster(clusterVoxels, out Vector3 offset);
                    if (fragData != null && emitDebrisParticles)
                    {
                        Vector3 wPos = transform.TransformPoint(offset * voxelSize);
                        EmitDebrisParticles(wPos, Color.gray, clusterVoxels.Count * 2);
                    }
                }
            }

            return spawned;
        }

        private ClusterData FloodFill(int startIdx)
        {
            ClusterData cluster = new ClusterData { voxels = new List<int>(), isGrounded = false };
            s_BfsQueue.Clear();
            s_BfsQueue.Enqueue(startIdx);
            visitedBuffer[startIdx] = currentRunId;

            while (s_BfsQueue.Count > 0)
            {
                int cur = s_BfsQueue.Dequeue();
                cluster.voxels.Add(cur);
                voxelData.ToCoords(cur, out int x, out int y, out int z);

                if (anchorToGround && y <= groundAnchorY)
                    cluster.isGrounded = true;

                EnqueueIfActive(s_BfsQueue, x + 1, y, z);
                EnqueueIfActive(s_BfsQueue, x - 1, y, z);
                EnqueueIfActive(s_BfsQueue, x, y + 1, z);
                EnqueueIfActive(s_BfsQueue, x, y - 1, z);
                EnqueueIfActive(s_BfsQueue, x, y, z + 1);
                EnqueueIfActive(s_BfsQueue, x, y, z - 1);
            }
            return cluster;
        }

        private void EnqueueIfActive(Queue<int> q, int x, int y, int z)
        {
            if (!voxelData.InBounds(x, y, z)) return;
            int idx = voxelData.ToIndex(x, y, z);
            if (visitedBuffer[idx] == currentRunId || !voxelData.voxels[idx].active) return;
            visitedBuffer[idx] = currentRunId;
            q.Enqueue(idx);
        }

        private VoxVoxelData ExtractCluster(List<int> cluster, out Vector3 offset)
        {
            offset = Vector3.zero;
            if (cluster == null || cluster.Count == 0) return null;

            Vector3Int min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
            Vector3Int max = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);

            for (int i = 0; i < cluster.Count; i++)
            {
                voxelData.ToCoords(cluster[i], out int x, out int y, out int z);
                min = Vector3Int.Min(min, new Vector3Int(x, y, z));
                max = Vector3Int.Max(max, new Vector3Int(x, y, z));
            }

            Vector3Int size = max - min + Vector3Int.one;
            VoxVoxel[] voxels = new VoxVoxel[size.x * size.y * size.z];

            for (int i = 0; i < cluster.Count; i++)
            {
                voxelData.ToCoords(cluster[i], out int x, out int y, out int z);
                VoxVoxel v = voxelData.GetVoxel(x, y, z);
                v.active = true;
                voxels[(x - min.x) + size.x * ((y - min.y) + size.y * (z - min.z))] = v;

                VoxVoxel empty = v;
                empty.active = false;
                voxelData.SetVoxel(x, y, z, empty);
            }

            offset = new Vector3(min.x, min.y, min.z);
            return new VoxVoxelData(voxels, voxelData.palette, size);
        }

        private void EnforceGlobalFragmentLimit()
        {
            if (maxGlobalFragments <= 0) return;
            while (activeFragments.Count >= maxGlobalFragments)
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

            GameObject go = new GameObject("VoxFragment");
            activeFragments.Add(go);
            go.layer = gameObject.layer;
            go.tag = gameObject.tag;
            go.transform.SetParent(s_FragmentRoot, true);
            go.transform.position = transform.TransformPoint(offset * voxelSize);
            go.transform.rotation = transform.rotation;
            go.transform.localScale = transform.lossyScale;

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            MeshCollider mc = go.AddComponent<MeshCollider>();
            mc.convex = true;

            VoxVoxelObject frag = go.AddComponent<VoxVoxelObject>();
            frag.isFragment = true;
            frag.collapseThreshold = 2;
            frag.collapseRatio = 0f;
            frag.CopySettingsFrom(this);
            frag.AssignVoxelData(data);

            frag.initialVoxelCount = initialCount;
            rb.mass = Mathf.Clamp(initialCount * 0.25f, 0.5f, 80f);
            frag.StartSettlingCheck();

            if (args.explosionForce > 0f)
            {
                Vector3 pushDir;
                if (args.impactDirection != Vector3.zero)
                    pushDir = (args.impactDirection.normalized + Vector3.down * 0.2f).normalized;
                else
                    pushDir = (go.transform.position - damageCenter).normalized;

                rb.AddForce(pushDir * args.explosionForce, ForceMode.Impulse);
                rb.AddTorque(UnityEngine.Random.insideUnitSphere * (args.explosionForce * 0.3f), ForceMode.Impulse);
            }

            if (lifetime > 0f)
                Destroy(go, lifetime);
        }
    }
}
