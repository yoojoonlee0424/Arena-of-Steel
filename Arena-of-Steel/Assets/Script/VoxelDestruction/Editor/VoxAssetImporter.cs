using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace VoxelDestructionPro.Vox
{
    [ScriptedImporter(7, "vox")]
    public class VoxAssetImporter : ScriptedImporter
    {
        private const string DefaultMaterialPath = "Assets/Script/VoxelDestruction/VoxMat.mat";
        private const string DefaultClipPath = "Assets/Script/VoxelDestruction/Break.mp3";

        [Header("Axis & Scale")]
        [Tooltip("MagicaVoxel(Z-Up) 데이터를 Unity(Y-Up)에 맞게 Z축과 Y축을 스왑합니다.")]
        [SerializeField] private bool swapYZ = true;
        [Tooltip("모델 주변의 빈 캔버스 여백을 제거하여 메모리와 에디터 직렬화 부하를 줄입니다.")]
        [SerializeField] private bool trimEmptySpace = true;
        [Tooltip("복셀 해상도 축소 배율 (1 = 원본, 2 = 1/2 축소). 대형 VOX 파일의 렉을 줄일 때 사용합니다.")]
        [Range(1, 8)]
        [SerializeField] private int downsample = 1;
        [Tooltip("다운샘플링 시 복셀 크기를 배율만큼 키워 전체 월드 스케일을 동일하게 유지합니다.")]
        [SerializeField] private bool preserveScaleOnDownsample = true;
        [SerializeField] private float voxelSize = 1f;

        [Header("Durability (Hardness)")]
        [Tooltip("복셀 1개의 내구도(체력). 0 이하이면 데미지에 관계없이 즉시 파괴됩니다.")]
        [SerializeField] private float voxelMaxHealth = 20f;
        [Tooltip("켜면 공격 반경(radius)을 월드 거리가 아닌 복셀 개수 단위로 적용합니다.")]
        [SerializeField] private bool forceRadiusInVoxels = false;

        [Header("Chunk Splitting")]
        [Tooltip("대형 모델을 지정 크기(예: 32) 단위의 서브 청크 메쉬로 분할해 파괴 시 리빌드 렉을 방지합니다. (0 이하이면 분할 안 함)")]
        [SerializeField] private int maxChunkSize = 32;

        [Header("Object Defaults")]
        [SerializeField] private Material vertexColorMaterial;
        [SerializeField] private AudioClip defaultDestroyClip;
        [SerializeField] private bool anchorToGround = true;

        /// <summary>
        /// .vox 에셋을 읽어 단일 루트 VoxVoxelObject(대형 모델은 자식 서브 청크 메쉬 포함) 프리팹으로 임포트합니다.
        /// </summary>
        public override void OnImportAsset(AssetImportContext ctx)
        {
            int step = Mathf.Max(1, downsample);
            VoxVoxelData data = VoxLoader.LoadFromFile(ctx.assetPath, swapYZ, trimEmptySpace, step, true);
            if (data == null)
            {
                ctx.LogImportError("Failed to parse VOX file: " + ctx.assetPath);
                return;
            }

            string assetName = Path.GetFileNameWithoutExtension(ctx.assetPath);
            float size = Mathf.Max(0.01f, voxelSize) * (preserveScaleOnDownsample ? step : 1f);

            Material mat = vertexColorMaterial != null
                ? vertexColorMaterial
                : AssetDatabase.LoadAssetAtPath<Material>(DefaultMaterialPath);

            AudioClip clip = defaultDestroyClip != null
                ? defaultDestroyClip
                : AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultClipPath);

            GameObject go = new GameObject(assetName);
            var rootNavMod = go.AddComponent<NavMeshModifier>();
            rootNavMod.overrideArea = true;
            rootNavMod.area = 1;
            rootNavMod.ignoreFromBuild = false;

            bool needsChunking = maxChunkSize > 0 &&
                (data.length.x > maxChunkSize || data.length.y > maxChunkSize || data.length.z > maxChunkSize);

            if (needsChunking)
            {
                var boundsList = VoxLoader.GetNonEmptyChunkBounds(data, maxChunkSize);
                if (boundsList.Count > 1)
                {
                    VoxSubChunk[] subChunks = new VoxSubChunk[boundsList.Count];
                    for (int i = 0; i < boundsList.Count; i++)
                    {
                        var b = boundsList[i];
                        Mesh chunkMesh = new Mesh { name = $"{assetName}_Chunk_{i}_Mesh" };
                        VoxMesher.BuildChunkMesh(data, b.min, b.max, size, chunkMesh);
                        ctx.AddObjectToAsset($"mesh_{i}", chunkMesh);

                        GameObject child = new GameObject($"{assetName}_Chunk_{i}");
                        child.transform.SetParent(go.transform, false);

                        var mf = child.AddComponent<MeshFilter>();
                        var mr = child.AddComponent<MeshRenderer>();
                        var mc = child.AddComponent<MeshCollider>();
                        mf.sharedMesh = chunkMesh;
                        mr.sharedMaterial = mat;
                        mc.sharedMesh = chunkMesh;

                        subChunks[i] = new VoxSubChunk
                        {
                            min = b.min,
                            max = b.max,
                            meshFilter = mf,
                            meshRenderer = mr,
                            meshCollider = mc
                        };
                    }

                    var chunkedVoxObj = go.AddComponent<VoxVoxelObject>();
                    chunkedVoxObj.InitializeImported(data, size, mat, clip, anchorToGround, null, subChunks, maxChunkSize, voxelMaxHealth, forceRadiusInVoxels);

                    ctx.AddObjectToAsset("main", go);
                    ctx.SetMainObject(go);
                    return;
                }
            }

            Mesh mesh = new Mesh { name = assetName + "_Mesh" };
            VoxMesher.BuildMesh(data, size, mesh);
            ctx.AddObjectToAsset("mesh", mesh);

            var rootVoxObj = go.AddComponent<VoxVoxelObject>();
            rootVoxObj.InitializeImported(data, size, mat, clip, anchorToGround, mesh, null, maxChunkSize, voxelMaxHealth, forceRadiusInVoxels);

            ctx.AddObjectToAsset("main", go);
            ctx.SetMainObject(go);
        }
    }
}
