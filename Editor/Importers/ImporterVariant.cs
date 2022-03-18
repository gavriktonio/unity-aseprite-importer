using AsepriteImporter.Editors;

namespace AsepriteImporter.Importers
{
    public class ImporterVariant
    {
        public string Name { get; }
        public SpriteImporter SpriteImporter { get; }
        public SpriteImporter TileSetImporter { get; }
        public SpriteImporter SliceImporter { get; }
        public SpriteImporterEditor Editor { get; }

        public ImporterVariant(string name, SpriteImporter spriteImporter, SpriteImporter tileSetImporter, SpriteImporter sliceImporter, SpriteImporterEditor editor)
        {
            Name = name;
            SpriteImporter = spriteImporter;
            TileSetImporter = tileSetImporter;
            SliceImporter = sliceImporter;
            Editor = editor;
        }
    }
}