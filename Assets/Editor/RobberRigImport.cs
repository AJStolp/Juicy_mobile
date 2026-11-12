using UnityEditor;

public class RobberRigImport : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if (!assetPath.EndsWith("Robber_Rig.fbx")) return;
        ModelImporter importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.globalScale = 1f;
        importer.bakeAxisConversion = false;
    }
}
