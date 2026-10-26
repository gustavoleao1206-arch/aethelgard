using System;
using System.Collections.Generic;
using UnityEngine;

namespace JogoDaTrupe.Core.World
{
    /// <summary>Malha de uma fita ao longo de uma polilinha do plano, drapeada numa função de altura.</summary>
    public static class PathRibbon
    {
        private const float MinPointDistance = 0.0001f;

        public struct RibbonMesh
        {
            public Vector3[] Vertices;
            public Vector2[] Uvs;
            public int[] Triangles;
            public bool IsEmpty => Vertices == null || Vertices.Length == 0;
        }

        /// <summary>
        /// Gera somente geometria. Entradas inválidas retornam uma fita vazia.
        /// O determinismo da altura depende de heightAt também ser uma função determinística.
        /// </summary>
        /// <param name="points">Polilinha no plano: (x, z).</param>
        /// <param name="width">Largura total da fita, em metros.</param>
        /// <param name="heightAt">Altura do chão em (x, z).</param>
        /// <param name="lift">Elevação acima do chão para evitar z-fighting.</param>
        /// <param name="step">Espaçamento máximo dos centros dos anéis ao longo da polilinha.</param>
        /// <param name="uvRepeat">Metros por repetição da textura ao longo do comprimento.</param>
        public static RibbonMesh Build(IReadOnlyList<Vector2> points, float width,
            Func<float, float, float> heightAt, float lift = 0.03f, float step = 1f, float uvRepeat = 4f)
        {
            if (points == null || points.Count < 2 || heightAt == null
                || !IsFinite(width) || width <= 0f || !IsFinite(step) || step <= 0f
                || !IsFinite(uvRepeat) || uvRepeat <= 0f || !IsFinite(lift))
                return default(RibbonMesh);

            var clean = new List<Vector2>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 point = points[i];
                if (!IsFinite(point.x) || !IsFinite(point.y))
                    return default(RibbonMesh);
                // Compara com o último ponto retido: pequenas repetições não criam direções nulas.
                if (clean.Count == 0 || (point - clean[clean.Count - 1]).sqrMagnitude
                    >= MinPointDistance * MinPointDistance)
                    clean.Add(point);
            }
            if (clean.Count < 2)
                return default(RibbonMesh);

            int segmentCount = clean.Count - 1;
            var directions = new Vector2[segmentCount];
            var lengths = new float[segmentCount];
            var divisions = new int[segmentCount];
            long ringCount = 1;
            for (int i = 0; i < segmentCount; i++)
            {
                Vector2 delta = clean[i + 1] - clean[i];
                float length = delta.magnitude;
                if (!IsFinite(length) || length <= 0f)
                    return default(RibbonMesh);

                // Ceiling em double evita arredondar a razão para baixo antes de subdividir.
                double count = Math.Ceiling((double)length / step);
                if (count > int.MaxValue)
                    return default(RibbonMesh);
                divisions[i] = Math.Max(1, (int)count);
                ringCount += divisions[i];
                // Cada intervalo precisa de seis índices; recusa tamanhos que não cabem em arrays.
                if (ringCount > int.MaxValue / 6L + 1L)
                    return default(RibbonMesh);
                lengths[i] = length;
                directions[i] = delta / length;
            }

            int vertexCount = (int)ringCount * 2;
            var mesh = new RibbonMesh
            {
                Vertices = new Vector3[vertexCount],
                Uvs = new Vector2[vertexCount],
                Triangles = new int[((int)ringCount - 1) * 6]
            };

            int ring = 0;
            float distanceBefore = 0f;
            for (int segment = 0; segment < segmentCount; segment++)
            {
                Vector2 start = clean[segment];
                Vector2 end = clean[segment + 1];
                int count = divisions[segment];
                // A junta já foi escrita como o último anel do segmento anterior.
                for (int part = segment == 0 ? 0 : 1; part <= count; part++)
                {
                    float t = (float)part / count;
                    Vector2 center = part == count ? end : start + (end - start) * t;
                    Vector2 direction = directions[segment];
                    if (part == count && segment + 1 < segmentCount)
                    {
                        Vector2 sum = direction + directions[segment + 1];
                        // Numa reversão de 180 graus a bissetriz não existe; mantém a entrada
                        // como convenção determinística, sem normalizar um vetor nulo.
                        if (sum.sqrMagnitude > 0f)
                            direction = sum / sum.magnitude;
                    }

                    // Sem extensão de miter: o deslocamento fica em meia largura mesmo em
                    // curvas fechadas, evitando espetos e mantendo a seção transversal limitada.
                    Vector2 offset = new Vector2(-direction.y, direction.x) * (width * 0.5f);
                    Vector2 left = center + offset;
                    Vector2 right = center - offset;
                    int vertex = ring * 2;
                    mesh.Vertices[vertex] = new Vector3(left.x, heightAt(left.x, left.y) + lift, left.y);
                    mesh.Vertices[vertex + 1] = new Vector3(right.x, heightAt(right.x, right.y) + lift, right.y);
                    float v = (distanceBefore + lengths[segment] * t) / uvRepeat;
                    mesh.Uvs[vertex] = new Vector2(0f, v);
                    mesh.Uvs[vertex + 1] = new Vector2(1f, v);
                    ring++;
                }
                distanceBefore += lengths[segment];
            }

            for (int i = 0; i < ring - 1; i++)
            {
                int left = i * 2;
                int nextLeft = left + 2;
                WriteUpwardTriangle(mesh, i * 6, left, nextLeft, left + 1);
                WriteUpwardTriangle(mesh, i * 6 + 3, left + 1, nextLeft, nextLeft + 1);
            }
            return mesh;
        }

        /// <summary>Concatena fitas, deslocando índices e pulando as vazias, sem alterar as entradas.</summary>
        public static RibbonMesh Combine(IReadOnlyList<RibbonMesh> ribbons)
        {
            if (ribbons == null || ribbons.Count == 0)
                return default(RibbonMesh);

            long vertexCount = 0;
            long indexCount = 0;
            for (int i = 0; i < ribbons.Count; i++)
            {
                RibbonMesh ribbon = ribbons[i];
                if (ribbon.IsEmpty)
                    continue;
                vertexCount += ribbon.Vertices.Length;
                indexCount += ribbon.Triangles.Length;
                if (vertexCount > int.MaxValue || indexCount > int.MaxValue)
                    return default(RibbonMesh);
            }
            if (vertexCount == 0)
                return default(RibbonMesh);

            var combined = new RibbonMesh
            {
                Vertices = new Vector3[(int)vertexCount],
                Uvs = new Vector2[(int)vertexCount],
                Triangles = new int[(int)indexCount]
            };
            int vertexOffset = 0;
            int indexOffset = 0;
            for (int i = 0; i < ribbons.Count; i++)
            {
                RibbonMesh ribbon = ribbons[i];
                if (ribbon.IsEmpty)
                    continue;
                Array.Copy(ribbon.Vertices, 0, combined.Vertices, vertexOffset, ribbon.Vertices.Length);
                Array.Copy(ribbon.Uvs, 0, combined.Uvs, vertexOffset, ribbon.Uvs.Length);
                for (int j = 0; j < ribbon.Triangles.Length; j++)
                    combined.Triangles[indexOffset + j] = ribbon.Triangles[j] + vertexOffset;
                vertexOffset += ribbon.Vertices.Length;
                indexOffset += ribbon.Triangles.Length;
            }
            return combined;
        }

        private static void WriteUpwardTriangle(RibbonMesh mesh, int index, int a, int b, int c)
        {
            Vector3 ab = mesh.Vertices[b] - mesh.Vertices[a];
            Vector3 ac = mesh.Vertices[c] - mesh.Vertices[a];
            // A borda interna pode voltar sobre si em curvas estreitas; verifica cada triângulo
            // no plano XZ, para que o drapeamento não mude a convenção de orientação do chão.
            if (ab.z * ac.x - ab.x * ac.z < 0f)
            {
                int swap = b;
                b = c;
                c = swap;
            }
            mesh.Triangles[index] = a;
            mesh.Triangles[index + 1] = b;
            mesh.Triangles[index + 2] = c;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}