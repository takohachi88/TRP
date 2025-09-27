//以下を移植。
//https://github.com/JasonMa0012/OutlineNormalSmoother
// Copyright (c) Jason Ma

using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using Trp;

namespace TrpEditor.OutlineNormalSmoother
{
	public static class OutlineNormalBaker
	{
		private const int BATCH_COUNT = 256; // How many vertices a job processes
		private const int MAX_COINCIDENT_VERTICES = 32;


		private struct CollectWeightedNormalJob : IJobParallelFor
		{
			[ReadOnly] internal NativeArray<int> indices;
			[ReadOnly] internal NativeArray<Vector3> vertices;

			[NativeDisableParallelForRestriction] internal NativeArray<UnsafeParallelHashMap<Vector3, float3>.ParallelWriter> outPositionNormalHashMapArray;

			void IJobParallelFor.Execute(int vertexIndexInSubMesh)
			{
				int vertexIndexInTriangle = vertexIndexInSubMesh % 3;
				var position = vertices[indices[vertexIndexInSubMesh]];
				float3 p1 = vertices[indices[vertexIndexInSubMesh - vertexIndexInTriangle]];
				float3 p2 = vertices[indices[vertexIndexInSubMesh - vertexIndexInTriangle + 1]];
				float3 p3 = vertices[indices[vertexIndexInSubMesh - vertexIndexInTriangle + 2]];

				CalculateWeightedAngle(p1, p2, p3, vertexIndexInTriangle, out var normal, out var angle);
				var angleWeightedNormal = normal * angle;

				for (int i = 0; i < outPositionNormalHashMapArray.Length + 1; i++)
				{
					if (i == outPositionNormalHashMapArray.Length)
					{
						Debug.LogError(
							$"[OutlineNormalSmoother] The number of coincident vertices ({i}) exceeds the limit ({MAX_COINCIDENT_VERTICES})!");
						break;
					}

					if (outPositionNormalHashMapArray[i].TryAdd(position, angleWeightedNormal))
					{
						break;
					}
				}
			}
		}

		private struct BakeNormalJob : IJobParallelFor
		{
			[ReadOnly] internal NativeArray<int> indices;
			[ReadOnly] internal NativeArray<Vector3> vertices, normals;
			[ReadOnly] internal NativeArray<Vector4> tangents;
			[ReadOnly] internal NativeArray<UnsafeParallelHashMap<Vector3, float3>> positionNormalHashMapArray;

			[NativeDisableParallelForRestriction] internal NativeArray<Vector3> outSmoothedNormalTangentSpace;

			void IJobParallelFor.Execute(int vertexIndexInSubMesh)
			{
				var vertexIndex = indices[vertexIndexInSubMesh];
				var position = vertices[vertexIndex];

				float3 smoothedNormal = 0;
				{
					for (int i = 0; i < positionNormalHashMapArray.Length; i++)
					{
						if (positionNormalHashMapArray[i].TryGetValue(position, out var angleWeightedNormal))
							smoothedNormal += angleWeightedNormal;
						else
							break;
					}
					smoothedNormal = math.normalizesafe(smoothedNormal);
				}

				float3 smoothedNormalTS = 0;
				{
					float3 normal = math.normalizesafe(normals[vertexIndex]);
					float4 tangent = tangents[vertexIndex];
					tangent.xyz = math.normalizesafe(tangent.xyz);
					var binormal = math.normalizesafe(math.cross(normal, tangent.xyz) * tangent.w);

					var tangentToObject = new float3x3(
						tangent.xyz,
						binormal,
						normal);

					var objectToTangent = math.transpose(tangentToObject);
					smoothedNormalTS = math.normalizesafe(math.mul(objectToTangent, smoothedNormal));
				}

				outSmoothedNormalTangentSpace[vertexIndex] = smoothedNormalTS;
			}
		}

		internal static void BakeSmoothedNormalTangentSpaceToMesh(List<Mesh> meshes, int texcoordIndex)
		{
			for (int i = 0; i < meshes.Count; i++)
			{
				var mesh = meshes[i];
				var vertexCount = mesh.vertexCount;
				var vertices = mesh.vertices;
				var normals = mesh.normals;
				var tangents = mesh.tangents;
				{
					if (normals.Length == 0)
					{
						mesh.RecalculateNormals();
						normals = mesh.normals;
						Debug.Log($"OutlineNormalSmoother: ({ i + 1 } / { meshes.Count }) Recalculate Normals: { mesh.name }");
					}

					if (tangents.Length == 0)
					{
						mesh.RecalculateTangents();
						tangents = mesh.tangents;
						Debug.Log($"OutlineNormalSmoother: ({ i + 1 } / { meshes.Count }) Recalculate Tangents: { mesh.name }");
					}
				}

				NativeArray<Vector3> nativeVertices = new(vertices, Allocator.TempJob);
				NativeArray<Vector3> nativeNormals = new(normals, Allocator.TempJob);
				NativeArray<Vector4> nativeTangents = new(tangents, Allocator.TempJob);
				NativeArray<Vector3> outSmoothedNormalTangentSpace = new(vertexCount, Allocator.TempJob);

				for (int j = 0; j < mesh.subMeshCount; j++)
				{
					var indices = mesh.GetIndices(j);
					var subMeshVertexCount = indices.Length;

					NativeArray<int> nativeIndices = new(indices, Allocator.TempJob);
					NativeArray<UnsafeParallelHashMap<Vector3, float3>> nativePositionNormalHashMapArray =
						new(MAX_COINCIDENT_VERTICES, Allocator.TempJob);
					NativeArray<UnsafeParallelHashMap<Vector3, float3>.ParallelWriter> nativePositionNormalHashMapWriterArray =
						new(MAX_COINCIDENT_VERTICES, Allocator.TempJob);
					for (int k = 0; k < MAX_COINCIDENT_VERTICES; k++)
					{
						UnsafeParallelHashMap<Vector3, float3> nativePositionNormalHashMap = new(subMeshVertexCount, Allocator.TempJob);
						nativePositionNormalHashMapArray[k] = nativePositionNormalHashMap;
						nativePositionNormalHashMapWriterArray[k] = nativePositionNormalHashMap.AsParallelWriter();
					}

					// Collect weighed normals
					JobHandle collectNormalJobHandle;
					{
						var collectSmoothedNormalJobData = new CollectWeightedNormalJob
						{
							indices = nativeIndices,
							vertices = nativeVertices,
							outPositionNormalHashMapArray = nativePositionNormalHashMapWriterArray
						};
						collectNormalJobHandle = collectSmoothedNormalJobData.Schedule(subMeshVertexCount, BATCH_COUNT);
					}

					// Bake smoothed normal TS to vertex color
					var bakeNormalJobData = new BakeNormalJob
					{
						indices = nativeIndices,
						vertices = nativeVertices,
						normals = nativeNormals,
						tangents = nativeTangents,
						positionNormalHashMapArray = nativePositionNormalHashMapArray,
						outSmoothedNormalTangentSpace = outSmoothedNormalTangentSpace
					};
					bakeNormalJobData.Schedule(subMeshVertexCount, BATCH_COUNT, collectNormalJobHandle).Complete();

					// Clear
					for (int k = 0; k < MAX_COINCIDENT_VERTICES; k++)
					{
						nativePositionNormalHashMapArray[k].Dispose();
					}
					nativeIndices.Dispose();
					nativePositionNormalHashMapArray.Dispose();
					nativePositionNormalHashMapWriterArray.Dispose();
				}

				// Save
				mesh.SetUVs(texcoordIndex, outSmoothedNormalTangentSpace.ToArray());
				mesh.MarkModified();
				
				Debug.Log($"OutlineNormalSmoother: ({ i + 1 } / { meshes.Count }) Saved to mesh: { mesh.name }");

				// Clear
				nativeVertices.Dispose();
				nativeNormals.Dispose();
				nativeTangents.Dispose();
				outSmoothedNormalTangentSpace.Dispose();
			}
		}

		// https://tajourney.games/7689/
		private static void CalculateWeightedAngle(float3 p1, float3 p2, float3 p3, int currentIndexInTriganle,
			out float3 outNormal, out float outAngle)
		{
			float3 d1 = 0;
			float3 d2 = 0;

			switch (currentIndexInTriganle)
			{
				case 0:
					d1 = p1 - p3;
					d2 = p2 - p1;
					break;
				case 1:
					d1 = p2 - p1;
					d2 = p3 - p2;
					break;
				case 2:
					d1 = p3 - p2;
					d2 = p1 - p3;
					break;
			}

			d1 = math.normalizesafe(d1);
			d2 = math.normalizesafe(d2);

			outNormal = math.normalizesafe(math.cross(p1 - p3, p2 - p1));
			outAngle = math.acos(math.clamp(math.dot(d1, -d2), -1, 1));
		}
	}
}