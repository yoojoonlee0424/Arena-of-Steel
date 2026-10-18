using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VoxelDestructionPro.Vox
{
    public static class VoxLoader
    {
        public struct VoxChunkBounds
        {
            public Vector3Int min;
            public Vector3Int max;
        }

        private struct RawVoxVoxel
        {
            public byte x, y, z, colorIndex;
        }

        private struct VoxModel
        {
            public int sizeX, sizeY, sizeZ;
            public RawVoxVoxel[] voxels;
            public Color32[] palette256;
        }

        /// <summary>
        /// .vox 파일 경로에서 복셀 데이터를 읽어와 축 변환, 여백 제거, 다운샘플링을 적용합니다.
        /// </summary>
        public static VoxVoxelData LoadFromFile(string path, bool swapYZ = true, bool trimEmptySpace = true, int downsample = 1, bool log = false)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            string fullPath = Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
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
                return ToVoxelData(model, swapYZ, trimEmptySpace, Mathf.Max(1, downsample));
            }
            catch (Exception e)
            {
                if (log) Debug.LogError("VoxLoader Exception: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// 단일 복셀 데이터에서 활성 복셀이 존재하는 청크 영역(min~max) 목록을 반환합니다.
        /// </summary>
        public static List<VoxChunkBounds> GetNonEmptyChunkBounds(VoxVoxelData source, int maxChunkSize)
        {
            var result = new List<VoxChunkBounds>();
            if (source == null || source.voxels == null || maxChunkSize <= 0) return result;

            int lx = source.length.x;
            int ly = source.length.y;
            int lz = source.length.z;

            for (int cz = 0; cz < lz; cz += maxChunkSize)
            {
                int endZ = Mathf.Min(cz + maxChunkSize, lz);
                for (int cy = 0; cy < ly; cy += maxChunkSize)
                {
                    int endY = Mathf.Min(cy + maxChunkSize, ly);
                    for (int cx = 0; cx < lx; cx += maxChunkSize)
                    {
                        int endX = Mathf.Min(cx + maxChunkSize, lx);
                        Vector3Int min = new Vector3Int(cx, cy, cz);
                        Vector3Int max = new Vector3Int(endX, endY, endZ);
                        if (source.HasActiveInBounds(min, max))
                            result.Add(new VoxChunkBounds { min = min, max = max });
                    }
                }
            }

            return result;
        }

        private static VoxVoxelData ToVoxelData(VoxModel model, bool swapYZ, bool trimEmptySpace, int step)
        {
            RawVoxVoxel[] raw = model.voxels;
            if (raw == null || raw.Length == 0) return null;

            int minX = 0, minY = 0, minZ = 0;
            Vector3Int size;

            if (trimEmptySpace)
            {
                minX = int.MaxValue; minY = int.MaxValue; minZ = int.MaxValue;
                int maxX = int.MinValue, maxY = int.MinValue, maxZ = int.MinValue;

                for (int i = 0; i < raw.Length; i++)
                {
                    int x = raw[i].x / step;
                    int y = (swapYZ ? raw[i].z : raw[i].y) / step;
                    int z = (swapYZ ? raw[i].y : raw[i].z) / step;
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                    if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
                }

                size = new Vector3Int(maxX - minX + 1, maxY - minY + 1, maxZ - minZ + 1);
            }
            else
            {
                int sx = Mathf.Max(1, (model.sizeX + step - 1) / step);
                int sy = Mathf.Max(1, ((swapYZ ? model.sizeZ : model.sizeY) + step - 1) / step);
                int sz = Mathf.Max(1, ((swapYZ ? model.sizeY : model.sizeZ) + step - 1) / step);
                size = new Vector3Int(sx, sy, sz);
            }

            Color32[] pal = model.palette256 != null && model.palette256.Length == 256 ? model.palette256 : GetDefaultPalette256();
            Color[] palette = new Color[256];
            for (int i = 0; i < 256; i++)
                palette[i] = new Color(pal[i].r / 255f, pal[i].g / 255f, pal[i].b / 255f, 1f);

            VoxVoxel[] voxels = new VoxVoxel[size.x * size.y * size.z];
            for (int i = 0; i < raw.Length; i++)
            {
                int x = (raw[i].x / step) - minX;
                int y = ((swapYZ ? raw[i].z : raw[i].y) / step) - minY;
                int z = ((swapYZ ? raw[i].y : raw[i].z) / step) - minZ;
                if (x < 0 || y < 0 || z < 0 || x >= size.x || y >= size.y || z >= size.z) continue;
                int paletteIndex = Mathf.Clamp(raw[i].colorIndex - 1, 0, 255);
                voxels[x + size.x * (y + size.y * z)] = new VoxVoxel(true, paletteIndex);
            }

            return new VoxVoxelData(voxels, palette, size);
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
                    model.voxels = new RawVoxVoxel[n];
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

        private static Color32[] GetDefaultPalette256()
        {
            Color32[] p = new Color32[256];
            for (int i = 1; i < 256; i++) p[i] = new Color32((byte)i, (byte)i, (byte)i, 255);
            return p;
        }
    }
}
