using UnityEditor;
using UnityEngine;

// Import settings for the synthesized audio (ArtSource/Audio/build_audio.py),
// set for phones: the music loops stream compressed from storage instead of
// sitting decoded in memory, and the short effects load decoded so they play
// the instant they are called.
public class AudioImport : AssetPostprocessor
{
    const string Root = "Assets/Resources/Audio/";

    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith(Root)) return;
        var importer = (AudioImporter)assetImporter;
        bool music = assetPath.StartsWith(Root + "Music/");
        importer.forceToMono = true;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
        settings.quality = music ? 0.6f : 1f;
        importer.defaultSampleSettings = settings;
    }
}
