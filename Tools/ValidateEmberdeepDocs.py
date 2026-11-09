"""Compare generated Emberdeep DOCX content with authoritative JSON in order.

This is structural QA only. It never labels an unrendered document visually accepted.
Run with the bundled workspace Python after BuildEmberdeepDesignDocs.py.
"""
import hashlib
import argparse
import json
from pathlib import Path

from docx import Document
from docx.oxml.ns import qn
from docx.text.paragraph import Paragraph
from BuildEmberdeepDesignDocs import clean_heading

ROOT = Path(__file__).resolve().parents[1]
SOURCES = ROOT / 'Docs' / 'Emberdeep'
DEST = ROOT / 'Logs' / 'EmberdeepV2' / 'Docs' / 'qa.json'

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def expected_records(spec):
    records, hashes, page_count = [], {}, 0
    for name in spec['sources']:
        source = SOURCES / name
        hashes[str(source.relative_to(ROOT)).replace('\\', '/')] = digest(source)
        pages = json.loads(source.read_text(encoding='utf-8-sig'))['pages']
        page_count += len(pages)
        for page in pages:
            records.append(clean_heading(page['title']))
            if page.get('intro'):
                records.append(page['intro'])
            for block in page['blocks']:
                kind = block['kind']
                if kind == 'h2': records.append(clean_heading(block['text']))
                elif kind == 'p': records.append(block['text'])
                elif kind == 'bullets': records.extend(block['items'])
                elif kind == 'table':
                    records.extend(str(value) for value in block['columns'])
                    for row in block['rows']: records.extend(str(value) for value in row)
                else: raise ValueError(kind)
    return [value for value in records if value], hashes, page_count

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, default=DEST)
    args = parser.parse_args()
    manifest_path = SOURCES / 'DocumentManifest.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8-sig'))
    report = {
        'date': manifest['date'], 'revision': manifest['revision'],
        'candidate': json.loads((SOURCES / 'ImplementationStatus.json').read_text(encoding='utf-8-sig'))['candidate'],
        'structural_review': 'passed', 'visual_review': 'NOT_ASSESSED',
        'reason': 'This command checks exact source content only. Consult the current iteration render log for visual QA. Content checks cannot detect pagination, clipping, overlap or font rendering.',
        'manifest_sha256': digest(manifest_path), 'documents': [],
    }
    for spec in manifest['documents']:
        path = ROOT / 'Deliverables' / 'Emberdeep' / spec['file']
        doc = Document(path)
        actual = [Paragraph(p, doc).text for p in doc.element.body.iter(qn('w:p'))]
        actual = [value for value in actual if value]
        expected, hashes, pages = expected_records(spec)
        mismatches = [{'index': i, 'expected': a, 'actual': b}
                      for i, (a, b) in enumerate(zip(expected, actual)) if a != b]
        passed = expected == actual
        if not passed: report['structural_review'] = 'failed'
        assert doc.core_properties.title == spec['title']
        assert doc.core_properties.version == manifest['revision']
        record = {
            'file': str(path), 'sha256': digest(path),
            'structural_review': 'passed' if passed else 'failed',
            'expected_text_records': len(expected), 'actual_text_records': len(actual),
            'matched_text_records': sum(a == b for a, b in zip(expected, actual)),
            'content_compared_in_document_order': True,
            'logical_sections': pages, 'paragraphs': len(doc.paragraphs), 'tables': len(doc.tables),
            'source_sha256': hashes, 'mismatches': mismatches[:5],
            'visual_review': 'not performed on current version',
        }
        report['documents'].append(record)
        print(json.dumps({'file': spec['file'], 'status': record['structural_review'],
                          'records': len(expected), 'sha256': record['sha256']}))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2, ensure_ascii=False)+'\n', encoding='utf-8')
    if report['structural_review'] != 'passed': raise SystemExit(1)

if __name__ == '__main__':
    main()
