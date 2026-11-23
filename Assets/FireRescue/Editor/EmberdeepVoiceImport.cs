using UnityEditor;
using UnityEngine;

namespace FireRescue.Editor
{
    // Camp tutorial voice-over (Resources/EmberdeepVoice, EmberdeepSpiritLines): mono Vorbis kept compressed in memory, so
    // the 15 lines (~110 s) cost ~1.3 MB on Quest and decode one at a time. The source WAVs are already mono 48 kHz at
    // -18 LUFS (.agents/local/tools/vo-clean), so the rate is kept.
    public sealed class EmberdeepVoiceImport : AssetPostprocessor
    {
        public const string Folder="/Resources/EmberdeepVoice/";
        public const float Quality=.6f;
        void OnPreprocessAudio()
        {
            if(!assetPath.Replace('\\','/').Contains(Folder))return;
            var importer=(AudioImporter)assetImporter;
            importer.forceToMono=true;importer.loadInBackground=false;
            var settings=importer.defaultSampleSettings;
            settings.loadType=AudioClipLoadType.CompressedInMemory;settings.compressionFormat=AudioCompressionFormat.Vorbis;
            settings.quality=Quality;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;settings.preloadAudioData=true;
            importer.defaultSampleSettings=settings;
        }
    }
}
