using System;
using System.Collections.Generic;
using System.IO;
using Aseprite;
using Aseprite.Chunks;
using Aseprite.Utils;
using UnityEditor;
using UnityEngine;

namespace AsepriteImporter {
    public class GeneratedSliceImporter : SpriteImporter {
        private int padding = 1;
        private Vector2Int size;
        private string fileName;
        private string filePath;
        private int updateLimit;
        
        private Texture2D mainImage;
        protected Dictionary<string, Texture2D> separatedImages = new Dictionary<string, Texture2D>();


        public GeneratedSliceImporter(AseFileImporter importer) : base(importer)
        {
        }

        public override void OnImport()
        {
            fileName= Path.GetFileNameWithoutExtension(AssetPath);
            var directoryName = Path.GetDirectoryName(AssetPath) + "/" + fileName;
            if (!AssetDatabase.IsValidFolder(directoryName)) {
                AssetDatabase.CreateFolder(Path.GetDirectoryName(AssetPath), fileName);
            }

            filePath = directoryName + "/" + fileName + ".png";
            
            size = new Vector2Int(AsepriteFile.Header.Width, AsepriteFile.Header.Height);

            if (Settings.SeparateLayers.Length == 0)
            {
                Texture2D frame = AsepriteFile.GetFrames()[0];
                mainImage = frame;
            }
            else
            {
                Texture2D frame = AsepriteFile.GetFrames(Settings.SeparateLayers)[0];
                mainImage = frame;
                foreach (var separateLayerName in Settings.SeparateLayers)
                {
                    separatedImages.Add(separateLayerName, AsepriteFile.GetFrames(null, new []{separateLayerName})[0]);
                }
            }
                
            try {
                File.WriteAllBytes(filePath, mainImage.EncodeToPNG());
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            } catch (Exception e) {
                Debug.LogError(e.Message);
            }

            foreach (var separatedImage in separatedImages)
            {
                try {
                    File.WriteAllBytes(directoryName + "/" + fileName + '_' + separatedImage.Key +".png", separatedImage.Value.EncodeToPNG());
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                } catch (Exception e) {
                    Debug.LogError(e.Message);
                }
            }
        }

        protected override bool OnUpdate()
        {
            bool success = GenerateSprites(filePath, mainImage);
            if (success)
                foreach (var separatedImage in separatedImages)
                {
                    string filePathWithoutExtension = filePath.Replace(Path.GetExtension(filePath), "");
                    string separatedFilePath = filePathWithoutExtension + "_" + separatedImage.Key + Path.GetExtension(filePath);
                    if (!GenerateSprites(separatedFilePath, separatedImage.Value, "_" + separatedImage.Key))
                        return false;
                }
            return success;
        }

        private Color[] GetPixels(Texture2D sprite, RectInt from)
        {
            //Clamp because slices can go out of the image
            from.height = Math.Min(from.height, size.y - from.y);
            from.width = Math.Min(from.width, size.x - from.x);
            
            var res = sprite.GetPixels(from.x, from.y, from.width, from.height);
            if (Settings.transparencyMode == TransparencyMode.Mask) {
                for (int index = 0; index < res.Length; index++) {
                    var color = res[index];
                    if (color == Settings.transparentColor) {
                        color.r = color.g = color.b = color.a = 0;
                        res[index] = color;
                    }
                }
            }

            return res;
        }

        private Color GetPixel(Texture2D sprite, int x, int y) {
            var color = sprite.GetPixel(x, y);
            if (Settings.transparencyMode == TransparencyMode.Mask) {
                if (color == Settings.transparentColor) {
                    color.r = color.g = color.b = color.a = 0;
                }
            }

            return color;
        }

        private bool GenerateSprites(string path, Texture2D texture, string separatedString = null) {

            var fileName = Path.GetFileNameWithoutExtension(path);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) {
                return false;
            }

            //TextureImporterSettings textSetting = new TextureImporterSettings();
            //importer.ReadTextureSettings(textSetting);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = Settings.pixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            var metaList = CreateMetaData(fileName, texture, separatedString);
            var oldProperties = AseSpritePostProcess.GetPhysicsShapeProperties(importer, metaList);

            importer.spritesheet = metaList.ToArray();
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spriteImportMode = SpriteImportMode.Multiple;

            EditorUtility.SetDirty(importer);
            try {
                //textSetting.spriteMeshType = SpriteMeshType.FullRect;
                //importer.SetTextureSettings(textSetting);

                importer.SaveAndReimport();
            } catch (Exception e) {
                Debug.LogWarning("There was a problem with generating sprite file: " + e);
            }

            var newProperties = AseSpritePostProcess.GetPhysicsShapeProperties(importer, metaList);

            AseSpritePostProcess.RecoverPhysicsShapeProperty(newProperties, oldProperties);
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
            return true;
        }

        private List<SpriteMetaData> CreateMetaData(string fileName, Texture2D texture, string separatedString = null) {
            var res = new List<SpriteMetaData>();
            
            var sliceChunks = AsepriteFile.GetChunks<SliceChunk>();
            foreach (var sliceChunk in sliceChunks)
            {
                RectInt rect = sliceChunk.SliceKeys[0].GetSliceKeyRect(size);
                if (Settings.tileEmpty == EmptyTileBehaviour.Remove && IsTileEmpty(rect, texture)) 
                {
                    continue;
                }
                var meta = new SpriteMetaData();
                string name = fileName + "_" + sliceChunk.Name;
                if (separatedString != null)
                {
                    name = name.Replace(separatedString, "");
                    name += separatedString;
                }

                meta.name = name;
                meta.rect = new Rect(rect.min, rect.size);
                meta.alignment = Settings.spriteAlignment;

                if (sliceChunk.HasPivotInfo)
                {
                    Vector2Int pivotInfo = new Vector2Int(sliceChunk.SliceKeys[0].PivotXOrigin,
                        sliceChunk.SliceKeys[0].PivotYOrigin);
                    Vector2 pivot = new Vector2((float)pivotInfo.x / rect.width, 1f - (float)pivotInfo.y / rect.height);
                    meta.pivot = pivot;
                }
                else
                {
                    meta.pivot = Settings.spritePivot;
                }
                
                res.Add(meta);
            }
            
            return res;
        }

        private SerializedProperty GetPhysicsShapeProperty(TextureImporter importer, string spriteName) {
            SerializedObject serializedImporter = new SerializedObject(importer);

            if (importer.spriteImportMode == SpriteImportMode.Multiple) {
                var spriteSheetSP = serializedImporter.FindProperty("m_SpriteSheet.m_Sprites");

                for (int i = 0; i < spriteSheetSP.arraySize; i++) {
                    if (importer.spritesheet[i].name == spriteName) {
                        var element = spriteSheetSP.GetArrayElementAtIndex(i);
                        return element.FindPropertyRelative("m_PhysicsShape");
                    }
                }

            }

            return serializedImporter.FindProperty("m_SpriteSheet.m_PhysicsShape");
        }

        private bool IsTileEmpty(RectInt tileRect, Texture2D atlas)
        {
            if (atlas == null)
                return true;
            Color[] tilePixels = GetPixels(atlas, tileRect);
            for (int i = 0; i < tilePixels.Length; i++) {
                if (tilePixels[i].a != 0) {
                    return false;
                }
            }
            return true;
        }


    }
}