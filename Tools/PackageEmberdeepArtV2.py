"""Package the separate Art V2 sources and Unity assets for this existing project."""
import hashlib
import json
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED

ROOT = Path(__file__).resolve().parents[1]
VERSION = json.loads((ROOT/'Docs/Emberdeep/ImplementationStatus.json').read_text(encoding='utf-8-sig'))['candidate']['version']
OUTPUT = ROOT / f'Deliverables/Emberdeep/Emberdeep-ArtV2-{VERSION}.zip'
FOLDERS = ['ArtSource/EmberdeepV2', 'Assets/FireRescue/ArtV2', 'Assets/FireRescue/Art/AppIcon',
           'Assets/FireRescue/Resources/EmberdeepArtV2']
FILES = ['Tools/BuildEmberdeepArtV2.py', 'Tools/PackageEmberdeepArtV2.py',
         'Tools/RenderEmberdeepHydrantIcon.py',
         'Assets/FireRescue/Runtime/EmberdeepArtLibrary.cs',
         'Assets/FireRescue/Editor/EmberdeepArtBuilder.cs',
         'Assets/FireRescue/Resources/EmberdeepV2Surface.shader',
         'Assets/FireRescue/Resources/EmberdeepV2Creek.shader',
         'Assets/FireRescue/Resources/EmberdeepV2Particles.shader',
         'Assets/FireRescue/Resources/EmberdeepTrackedHand.shader',
         'Assets/FireRescue/Runtime/EmberdeepHandVisuals.cs',
         'Assets/FireRescue/Runtime/FireRescueVfx.cs',
         'Docs/Emberdeep/ArtV2Iteration.json',
         'Docs/Emberdeep/InteractionPolish.json',
         'Logs/Emberdeep-FirstResponse.png',
         'Logs/EmberdeepPolish/Editor-synthetic-Camp-hover.png',
         'Logs/EmberdeepFlameFeedback/Editor-Flame-OriginalBaseline.png',
         'Logs/EmberdeepFlameFeedback/Editor-Flame-Refined.png']

def main():
    paths = set()
    for folder in FOLDERS:
        paths.update(p for p in (ROOT/folder).rglob('*') if p.is_file() and p.suffix != '.blend1')
    for item in FILES:
        path = ROOT/item
        if not path.exists():
            raise FileNotFoundError(path)
        paths.add(path)
        meta = Path(str(path)+'.meta')
        if meta.exists():
            paths.add(meta)
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    with ZipFile(OUTPUT, 'w', ZIP_DEFLATED) as archive:
        for path in sorted(paths):
            archive.write(path, path.relative_to(ROOT).as_posix())
    manifest = {'file': str(OUTPUT), 'files': len(paths), 'bytes': OUTPUT.stat().st_size,
                'sha256': hashlib.sha256(OUTPUT.read_bytes()).hexdigest(),
                'scope': 'Separate art source and Unity assets for the existing Emberdeep project; not a standalone game project.',
                'document_visual_qa': 'Authoritative JSON included. Generated DOCX is excluded while its current visual review is unavailable.',
                'capture_origin': 'Concept is generated, preview is Blender; all Editor-named captures and Emberdeep-FirstResponse are Unity Editor evidence, not headset captures.'}
    OUTPUT.with_suffix('.json').write_text(json.dumps(manifest, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(manifest))

if __name__ == '__main__':
    main()
