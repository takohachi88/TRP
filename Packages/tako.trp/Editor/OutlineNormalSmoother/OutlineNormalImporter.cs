// Copyright (c) Jason Ma

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OutlineNormalSmoother
{
	public class OutlineNormalImporter : AssetPostprocessor
    {
	    public delegate bool OutlineNormalImporterCustomRuleEvent(string assetPath, [MaybeNull] AssetPostprocessor assetPostprocessor);
        
	    public static OutlineNormalImporterCustomRuleEvent shouldBakeOutlineNormal = 
            (assetPath, assetPostprocessor) => assetPath.ToLower().Contains("_outline.");

        private void OnPostprocessModel(GameObject go)
        {
            if (shouldBakeOutlineNormal(assetPath, this))
            {
	            var meshes = GetSharedMeshesFromGameObject(go);
                
	            Debug.Log($"OutlineNormalSmoother: Find { meshes.Count } meshes in file: { assetPath }");

                OutlineNormalBacker.BakeSmoothedNormalTangentSpaceToMesh(meshes);
            }
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            // movedAssets will not call OnPostprocessModel()
            foreach (var movedAsset in movedAssets)
            {
                if (shouldBakeOutlineNormal(movedAsset, null))
                {
                    var movedGO = AssetDatabase.LoadAssetAtPath<GameObject>(movedAsset);
                    var meshes = GetSharedMeshesFromGameObject(movedGO);
                    
                    Debug.Log($"OutlineNormalSmoother: Find { meshes.Count } meshes in file: { movedGO }");
                    
                    OutlineNormalBacker.BakeSmoothedNormalTangentSpaceToMesh(meshes);
                }
            }
        }

        internal static List<Mesh> GetSharedMeshesFromGameObject(GameObject go)
        {
            List<Mesh> meshes = new ();
            
            if (go == null)
	            return meshes;
            
            foreach (var meshFilter in go.GetComponentsInChildren<MeshFilter>())
            {
                meshes.Add(meshFilter.sharedMesh);
            }

            foreach (var skinnedMeshRenderer in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                meshes.Add(skinnedMeshRenderer.sharedMesh);
            }

            return meshes;
        }

        public static string NormalizeDirectorySeparatorChar(string path) => path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}