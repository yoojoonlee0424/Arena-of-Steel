using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelDestructionPro.Vox
{
    [Serializable]
    public struct VoxVoxel : IEquatable<VoxVoxel>
    {
        public bool active;
        public int colorIndex;
        public int normal;

        public VoxVoxel(bool active, int colorIndex)
        {
            this.active = active;
            this.colorIndex = colorIndex;
            this.normal = 0;
        }

        public bool Equals(VoxVoxel other) => active == other.active && colorIndex == other.colorIndex && normal == other.normal;
        public override bool Equals(object obj) => obj is VoxVoxel other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(active, colorIndex, normal);
    }

    [Serializable]
    public struct VoxelDamageArgs
    {
        public float radius;
        public bool isolateFragments;
        public float explosionForce;
        public float fragmentLifetime;
        public Vector3 impactDirection;
        public AudioClip customClip;
        public float soundVolume;

        public static VoxelDamageArgs Default => new VoxelDamageArgs
        {
            radius = 1.5f,
            isolateFragments = true,
            explosionForce = 3f,
            fragmentLifetime = 8f,
            impactDirection = Vector3.zero,
            customClip = null,
            soundVolume = 1f
        };
    }

    public struct VoxelDamageResult
    {
        public bool destroyed;
        public int destroyedCount;
        public int spawnedFragmentsCount;
        public Vector3 hitPoint;
    }

    [Serializable]
    public class VoxVoxelData
    {
        public VoxVoxel[] voxels;
        public Color[] palette;
        public Vector3Int length;

        public int Volume => length.x * length.y * length.z;

        public VoxVoxelData(VoxVoxel[] voxels, Color[] palette, Vector3Int length)
        {
            this.voxels = voxels;
            this.palette = palette;
            this.length = length;
        }

        public bool InBounds(int x, int y, int z) => x >= 0 && y >= 0 && z >= 0 && x < length.x && y < length.y && z < length.z;
        public int ToIndex(int x, int y, int z) => x + length.x * (y + length.y * z);

        public void ToCoords(int index, out int x, out int y, out int z)
        {
            x = index % length.x;
            int yIdx = index / length.x;
            y = yIdx % length.y;
            z = yIdx / length.y;
        }

        public VoxVoxel GetVoxel(int x, int y, int z) => InBounds(x, y, z) ? voxels[ToIndex(x, y, z)] : default;
        public void SetVoxel(int x, int y, int z, VoxVoxel voxel)
        {
            if (InBounds(x, y, z))
                voxels[ToIndex(x, y, z)] = voxel;
        }

        public bool IsEmpty()
        {
            if (voxels == null) return true;
            for (int i = 0; i < voxels.Length; i++)
                if (voxels[i].active) return false;
            return true;
        }

        public int CountActive()
        {
            if (voxels == null) return 0;
            int count = 0;
            for (int i = 0; i < voxels.Length; i++)
                if (voxels[i].active) count++;
            return count;
        }

        public bool TryGetActiveBounds(out Vector3Int min, out Vector3Int max)
        {
            min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
            max = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
            if (voxels == null) return false;

            bool any = false;
            int lx = length.x;
            int ly = length.y;
            int lz = length.z;
            int idx = 0;

            for (int z = 0; z < lz; z++)
            {
                for (int y = 0; y < ly; y++)
                {
                    for (int x = 0; x < lx; x++, idx++)
                    {
                        if (!voxels[idx].active) continue;
                        any = true;
                        if (x < min.x) min.x = x;
                        if (y < min.y) min.y = y;
                        if (z < min.z) min.z = z;
                        if (x > max.x) max.x = x;
                        if (y > max.y) max.y = y;
                        if (z > max.z) max.z = z;
                    }
                }
            }
            return any;
        }

        public static VoxVoxelData CreateBox(int sizeX, int sizeY, int sizeZ, Color color1, Color color2)
        {
            if (sizeX <= 0 || sizeY <= 0 || sizeZ <= 0) return null;
            Vector3Int size = new Vector3Int(sizeX, sizeY, sizeZ);
            Color[] pal = new Color[] { color1, color2 };
            VoxVoxel[] v = new VoxVoxel[size.x * size.y * size.z];

            for (int z = 0; z < sizeZ; z++)
                for (int y = 0; y < sizeY; y++)
                    for (int x = 0; x < sizeX; x++)
                    {
                        int colorIdx = ((x / 2 + y / 2 + z) % 2 == 0) ? 0 : 1;
                        v[x + size.x * (y + size.y * z)] = new VoxVoxel(true, colorIdx);
                    }

            return new VoxVoxelData(v, pal, size);
        }

        public static VoxVoxelData FromVox(int sizeX, int sizeY, int sizeZ, VoxVoxLoader.VoxVoxel[] xyzis, Color32[] palette256)
        {
            if (sizeX <= 0 || sizeY <= 0 || sizeZ <= 0) return null;
            Vector3Int size = new Vector3Int(sizeX, sizeZ, sizeY);
            Color32[] pal = palette256 != null && palette256.Length == 256 ? palette256 : VoxVoxLoader.GetDefaultPalette256();

            Color[] palette = new Color[256];
            for (int i = 0; i < 256; i++)
                palette[i] = new Color(pal[i].r / 255f, pal[i].g / 255f, pal[i].b / 255f, 1f);

            VoxVoxel[] voxels = new VoxVoxel[size.x * size.y * size.z];
            if (xyzis != null)
            {
                for (int i = 0; i < xyzis.Length; i++)
                {
                    int x = xyzis[i].x;
                    int y = xyzis[i].z;
                    int z = xyzis[i].y;
                    if (x < 0 || y < 0 || z < 0 || x >= size.x || y >= size.y || z >= size.z) continue;
                    int paletteIndex = Mathf.Clamp(xyzis[i].colorIndex - 1, 0, 255);
                    voxels[x + size.x * (y + size.y * z)] = new VoxVoxel(true, paletteIndex);
                }
            }
            return new VoxVoxelData(voxels, palette, size);
        }
    }

    public static class VoxMesher
    {
        private static readonly List<Vector3> s_Vertices = new List<Vector3>(4096);
        private static readonly List<int> s_Triangles = new List<int>(6144);
        private static readonly List<Vector3> s_Normals = new List<Vector3>(4096);
        private static readonly List<Color> s_Colors = new List<Color>(4096);
        private static VoxVoxel[] s_MaskBuffer;
        private static readonly int[] s_Dims = new int[3];
        private static readonly int[] s_X = new int[3];
        private static readonly int[] s_Q = new int[3];
        private static readonly int[] s_Du = new int[3];
        private static readonly int[] s_Dv = new int[3];

        public static Mesh BuildMesh(VoxVoxelData data, float voxelSize, Mesh targetMesh = null)
        {
            Mesh mesh;
            if (targetMesh != null)
            {
                mesh = targetMesh;
                mesh.Clear();
            }
            else
            {
                mesh = new Mesh { name = "VoxVoxelMesh" };
                mesh.MarkDynamic();
            }

            if (data == null || data.voxels == null || data.voxels.Length == 0)
                return mesh;

            s_Vertices.Clear();
            s_Triangles.Clear();
            s_Normals.Clear();
            s_Colors.Clear();

            s_Dims[0] = data.length.x;
            s_Dims[1] = data.length.y;
            s_Dims[2] = data.length.z;

            int maxMaskSize = Mathf.Max(s_Dims[0] * s_Dims[1], s_Dims[1] * s_Dims[2], s_Dims[0] * s_Dims[2]);
            if (s_MaskBuffer == null || s_MaskBuffer.Length < maxMaskSize)
                s_MaskBuffer = new VoxVoxel[Mathf.NextPowerOfTwo(maxMaskSize)];

            VoxVoxel[] mask = s_MaskBuffer;
            VoxVoxel empty = new VoxVoxel(false, 0);
            Vector3 blockMinVertex = new Vector3(-0.5f, -0.5f, -0.5f);
            int lenX = data.length.x;
            int lenY = data.length.y;

            for (int d = 0; d < 3; d++)
            {
                int u = (d + 1) % 3;
                int v = (d + 2) % 3;
                s_X[0] = 0; s_X[1] = 0; s_X[2] = 0;
                s_Q[0] = 0; s_Q[1] = 0; s_Q[2] = 0;
                s_Q[d] = 1;

                int dimD = s_Dims[d];
                int dimU = s_Dims[u];
                int dimV = s_Dims[v];

                for (s_X[d] = -1; s_X[d] < dimD;)
                {
                    int n = 0;
                    for (s_X[v] = 0; s_X[v] < dimV; ++s_X[v])
                    {
                        for (s_X[u] = 0; s_X[u] < dimU; ++s_X[u])
                        {
                            VoxVoxel current = s_X[d] >= 0 ? data.voxels[s_X[0] + lenX * (s_X[1] + lenY * s_X[2])] : empty;
                            VoxVoxel compare = s_X[d] < dimD - 1 ? data.voxels[s_X[0] + s_Q[0] + lenX * (s_X[1] + s_Q[1] + lenY * (s_X[2] + s_Q[2]))] : empty;

                            if (current.active == compare.active)
                                mask[n++] = empty;
                            else if (!current.active)
                            {
                                compare.normal = 1;
                                mask[n++] = compare;
                            }
                            else
                            {
                                current.normal = 2;
                                mask[n++] = current;
                            }
                        }
                    }

                    s_X[d]++;
                    n = 0;

                    for (int j = 0; j < dimV; j++)
                    {
                        for (int i = 0; i < dimU;)
                        {
                            if (mask[n].active || mask[n].normal != 0)
                            {
                                int w = 1, h = 1;
                                for (; i + w < dimU && mask[n + w].Equals(mask[n]); w++) { }
                                bool done = false;
                                for (; j + h < dimV; h++)
                                {
                                    for (int k = 0; k < w; ++k)
                                    {
                                        if (mask[n + k + h * dimU].active || !mask[n + k + h * dimU].Equals(mask[n]))
                                        {
                                            done = true;
                                            break;
                                        }
                                    }
                                    if (done) break;
                                }

                                s_X[u] = i;
                                s_X[v] = j;
                                s_Du[0] = 0; s_Du[1] = 0; s_Du[2] = 0; s_Du[u] = w;
                                s_Dv[0] = 0; s_Dv[1] = 0; s_Dv[2] = 0; s_Dv[v] = h;

                                AddQuad(s_Vertices, s_Triangles, s_Normals, s_Colors,
                                    blockMinVertex + new Vector3(s_X[0], s_X[1], s_X[2]),
                                    blockMinVertex + new Vector3(s_X[0] + s_Du[0], s_X[1] + s_Du[1], s_X[2] + s_Du[2]),
                                    blockMinVertex + new Vector3(s_X[0] + s_Du[0] + s_Dv[0], s_X[1] + s_Du[1] + s_Dv[1], s_X[2] + s_Du[2] + s_Dv[2]),
                                    blockMinVertex + new Vector3(s_X[0] + s_Dv[0], s_X[1] + s_Dv[1], s_X[2] + s_Dv[2]),
                                    mask[n], AxisMask(d), data.palette, voxelSize);

                                for (int l = 0; l < h; ++l)
                                    for (int k = 0; k < w; ++k)
                                        mask[n + k + l * dimU] = empty;

                                i += w;
                                n += w;
                            }
                            else
                            {
                                i++;
                                n++;
                            }
                        }
                    }
                }
            }

            if (s_Vertices.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(s_Vertices);
            mesh.SetTriangles(s_Triangles, 0, false);
            mesh.SetNormals(s_Normals);
            mesh.SetColors(s_Colors);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 AxisMask(int d) => d == 0 ? Vector3.right : (d == 1 ? Vector3.up : Vector3.forward);

        private static void AddQuad(List<Vector3> verts, List<int> tris, List<Vector3> norms, List<Color> cols,
            Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br, VoxVoxel voxel, Vector3 axisMask, Color[] palette, float voxelSize)
        {
            if (palette == null || palette.Length == 0) return;
            int normal = voxel.normal == 2 ? -1 : voxel.normal;
            int idx = verts.Count;

            tris.Add(idx);
            tris.Add(idx + 2 - normal);
            tris.Add(idx + 2 + normal);
            tris.Add(idx + 3);
            tris.Add(idx + 1 + normal);
            tris.Add(idx + 1 - normal);

            verts.Add(bl * voxelSize);
            verts.Add(br * voxelSize);
            verts.Add(tl * voxelSize);
            verts.Add(tr * voxelSize);

            Color color = palette[Mathf.Clamp(voxel.colorIndex, 0, palette.Length - 1)];
            cols.Add(color); cols.Add(color); cols.Add(color); cols.Add(color);

            Vector3 norm = axisMask * -normal;
            norms.Add(norm); norms.Add(norm); norms.Add(norm); norms.Add(norm);
        }
    }

    public static class VoxVoxLoader
    {
        [Serializable]
        public struct VoxVoxel
        {
            public byte x, y, z, colorIndex;
        }

        private struct VoxModel
        {
            public int sizeX, sizeY, sizeZ;
            public VoxVoxel[] voxels;
            public Color32[] palette256;
        }

        public static VoxVoxelData LoadFromFile(string pathInProject, bool log = false)
        {
            if (string.IsNullOrWhiteSpace(pathInProject)) return null;
            string fullPath = GetFullProjectPath(pathInProject);
            if (!File.Exists(fullPath))
            {
                if (log) Debug.LogWarning("Vox file not found at: " + fullPath);
                return null;
            }

            try
            {
                using var fs = File.OpenRead(fullPath);
                VoxModel model = ReadVoxFirstModel(fs);
                if (model.sizeX <= 0 || model.sizeY <= 0 || model.sizeZ <= 0 || model.voxels == null) return null;
                return VoxVoxelData.FromVox(model.sizeX, model.sizeY, model.sizeZ, model.voxels, model.palette256);
            }
            catch (Exception e)
            {
                if (log) Debug.LogError("VoxLoader Exception: " + e.Message);
                return null;
            }
        }

        private static VoxModel ReadVoxFirstModel(Stream stream)
        {
            VoxModel model = new VoxModel();
            using var br = new BinaryReader(stream);
            if (new string(br.ReadChars(4)) != "VOX ") throw new Exception("Not a VOX file");
            br.ReadInt32();
            if (new string(br.ReadChars(4)) != "MAIN") throw new Exception("VOX missing MAIN");

            int mainContent = br.ReadInt32();
            int mainChildren = br.ReadInt32();
            if (mainContent > 0) br.ReadBytes(mainContent);
            long mainEnd = br.BaseStream.Position + mainChildren;

            while (br.BaseStream.Position < mainEnd)
            {
                string id = new string(br.ReadChars(4));
                int contentSize = br.ReadInt32();
                int childrenSize = br.ReadInt32();
                long contentStart = br.BaseStream.Position;

                if (id == "SIZE")
                {
                    model.sizeX = br.ReadInt32();
                    model.sizeY = br.ReadInt32();
                    model.sizeZ = br.ReadInt32();
                }
                else if (id == "XYZI")
                {
                    int n = br.ReadInt32();
                    model.voxels = new VoxVoxel[n];
                    for (int i = 0; i < n; i++)
                    {
                        model.voxels[i].x = br.ReadByte();
                        model.voxels[i].y = br.ReadByte();
                        model.voxels[i].z = br.ReadByte();
                        model.voxels[i].colorIndex = br.ReadByte();
                    }
                }
                else if (id == "RGBA")
                {
                    Color32[] pal = new Color32[256];
                    for (int i = 0; i < 256; i++)
                        pal[i] = new Color32(br.ReadByte(), br.ReadByte(), br.ReadByte(), br.ReadByte());
                    model.palette256 = pal;
                }

                long toSkip = contentSize - (br.BaseStream.Position - contentStart);
                if (toSkip > 0) br.ReadBytes((int)toSkip);
                if (childrenSize > 0) br.ReadBytes(childrenSize);
            }
            return model;
        }

        public static Color32[] GetDefaultPalette256()
        {
            Color32[] p = new Color32[256];
            for (int i = 1; i < 256; i++) p[i] = new Color32((byte)i, (byte)i, (byte)i, 255);
            return p;
        }

        private static string GetFullProjectPath(string pathInProject)
        {
            string p = pathInProject.Replace('\\', '/').Trim();
            if (!p.EndsWith(".vox", StringComparison.OrdinalIgnoreCase)) p += ".vox";
            if (!p.StartsWith("Assets/")) p = "Assets/" + p.TrimStart('/');
            return Path.Combine(Application.dataPath, p.Substring("Assets/".Length));
        }
    }
}
