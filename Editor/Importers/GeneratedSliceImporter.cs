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
        private Texture2D atlas;

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

            Texture2D frame = AsepriteFile.GetFrames()[0];

            atlas = frame;
                
            try {
                File.WriteAllBytes(filePath, frame.EncodeToPNG());
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            } catch (Exception e) {
                Debug.LogError(e.Message);
            }
        }

        protected override bool OnUpdate()
        {
            return GenerateSprites(filePath, size);
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

        private bool GenerateSprites(string path, Vector2Int size) {
            this.size = size;

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
            var metaList = CreateMetaData(fileName);
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

        private List<SpriteMetaData> CreateMetaData(string fileName) {
            var res = new List<SpriteMetaData>();
            
            var sliceChunks = AsepriteFile.GetChunks<SliceChunk>();
            foreach (var sliceChunk in sliceChunks)
            {
                RectInt rect = sliceChunk.SliceKeys[0].GetSliceKeyRect();
                rect.y = size.y - rect.y - rect.height;
                if (Settings.tileEmpty == EmptyTileBehaviour.Remove && IsTileEmpty(rect, atlas)) 
                {
                    continue;
                }
                var meta = new SpriteMetaData();
                meta.name = fileName + "_" + sliceChunk.Name;
                meta.rect = new Rect(rect.min, rect.size);
                meta.alignment = Settings.spriteAlignment;
                meta.pivot = Settings.spritePivot;
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

        private bool IsTileEmpty(RectInt tileRect, Texture2D atlas) {
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