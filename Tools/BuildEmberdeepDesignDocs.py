"""Build the English Emberdeep specifications from reviewable JSON sources.

Run with python-docx installed. Rendering and page inspection remain a separate
delivery gate; this script does not claim visual QA or headset verification.
"""
import json
import re
from pathlib import Path
from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_CELL_VERTICAL_ALIGNMENT
from docx.oxml import OxmlElement
from docx.oxml.ns import qn

ROOT = Path(__file__).resolve().parents[1]
SOURCES = ROOT / 'Docs' / 'Emberdeep'
OUTPUT = ROOT / 'Deliverables' / 'Emberdeep'

def clean_heading(text):
    return re.sub(r'[^A-Za-z0-9 ]+', ' ', text).strip()

def field(paragraph, name):
    run = paragraph.add_run()
    item = OxmlElement('w:fldSimple')
    item.set(qn('w:instr'), name)
    run._r.addnext(item)

def table(doc, block):
    columns, rows = block['columns'], block['rows']
    tab = doc.add_table(rows=1, cols=len(columns))
    tab.alignment = WD_TABLE_ALIGNMENT.CENTER
    tab.autofit = False
    # Compact identity columns; narrative requirements receive the remaining width.
    widths = block.get('widths')
    if widths is None:
        scores = [max(10, min(60, sum(len(str(row[i])) for row in rows) / max(1,len(rows)))) for i in range(len(columns))]
        weights = [v ** 0.6 for v in scores]
        widths = [6.95 * v / sum(weights) for v in weights]
    for col, width in zip(tab.columns, widths): col.width = Inches(width)
    for cell, text in zip(tab.rows[0].cells, columns): cell.text = text
    repeat = OxmlElement('w:tblHeader'); tab.rows[0]._tr.get_or_add_trPr().append(repeat)
    for row in rows:
        assert len(row) == len(columns), row
        for cell, text in zip(tab.add_row().cells, row): cell.text = str(text)
    for r, row in enumerate(tab.rows):
        prevent = OxmlElement('w:cantSplit'); row._tr.get_or_add_trPr().append(prevent)
        for i, cell in enumerate(row.cells):
            cell.width = Inches(widths[i])
            cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
            pr = cell._tc.get_or_add_tcPr()
            borders = OxmlElement('w:tcBorders')
            for edge in ['top','left','bottom','right']:
                el = OxmlElement('w:'+edge); el.set(qn('w:val'),'single'); el.set(qn('w:sz'),'4'); el.set(qn('w:color'),'D9D9D9'); borders.append(el)
            pr.append(borders)
            margins = OxmlElement('w:tcMar')
            for edge, value in [('top','65'),('bottom','65'),('left','90'),('right','90')]:
                el=OxmlElement('w:'+edge); el.set(qn('w:w'),value); el.set(qn('w:type'),'dxa'); margins.append(el)
            pr.append(margins)
            shade=OxmlElement('w:shd'); shade.set(qn('w:fill'),'DCE8EA' if r==0 else ('F4F6F6' if r%2==0 else 'FFFFFF')); pr.append(shade)
            for p in cell.paragraphs:
                p.paragraph_format.space_after=Pt(0)
                p.paragraph_format.line_spacing=1.02
                if len(p.text)<16: p.alignment=WD_ALIGN_PARAGRAPH.CENTER
                for run in p.runs:
                    run.font.size=Pt(10)
                    run.font.bold=r==0
                    run.font.color.rgb=RGBColor(0,0,0)
    p=doc.add_paragraph(); p.paragraph_format.space_after=Pt(0); p.paragraph_format.space_before=Pt(0); p.paragraph_format.line_spacing=Pt(3); p.add_run().font.size=Pt(3)

def build(spec, manifest):
    doc=Document()
    # Strip inherited template rules beneath titles; keep all headings plain.
    for element in list(doc.styles.element.iter(qn('w:pBdr'))):
        element.getparent().remove(element)
    section=doc.sections[0]
    section.page_width=Inches(8.5); section.page_height=Inches(11)
    section.left_margin=Inches(.775); section.right_margin=Inches(.775)
    section.top_margin=Inches(.70); section.bottom_margin=Inches(.65)
    section.header_distance=Inches(.25); section.footer_distance=Inches(.25)
    for name in ['Normal','Title','Subtitle','Heading 1','Heading 2','List Bullet']:
        style=doc.styles[name]
        style.font.name='Calibri'; style.font.color.rgb=RGBColor(0,0,0)
    normal=doc.styles['Normal']; normal.font.size=Pt(11)
    normal.paragraph_format.space_after=Pt(6); normal.paragraph_format.line_spacing=1.04
    normal.paragraph_format.widow_control=True
    for name,size in [('Title',25),('Heading 1',17),('Heading 2',12)]:
        s=doc.styles[name]; s.font.size=Pt(size); s.font.bold=True
        s.paragraph_format.space_before=Pt(10 if name=='Heading 2' else 0)
        s.paragraph_format.space_after=Pt(6)
        s.paragraph_format.keep_with_next=True
    doc.styles['List Bullet'].paragraph_format.space_after=Pt(4)
    doc.styles['List Bullet'].paragraph_format.line_spacing=1.03
    header=section.header.paragraphs[0]
    header.add_run('EMBERDEEP  /  '+spec['short_title'].upper()).font.size=Pt(8)
    footer=section.footer.paragraphs[0]
    footer.add_run(f"Design revision {manifest['revision']}  |  {manifest['date']}").font.size=Pt(8)
    footer.add_run(' '*8+'Page ').font.size=Pt(8); field(footer,'PAGE')
    pages=[]
    for source in spec['sources']:
        pages.extend(json.loads((SOURCES/source).read_text(encoding='utf-8-sig'))['pages'])
    for idx,page in enumerate(pages):
        heading=doc.add_paragraph(clean_heading(page['title']), 'Title' if idx==0 else 'Heading 1')
        if idx: heading.paragraph_format.page_break_before=True
        if page.get('intro'): doc.add_paragraph(page['intro'])
        for block in page['blocks']:
            kind=block['kind']
            if kind=='h2': doc.add_paragraph(clean_heading(block['text']),'Heading 2')
            elif kind=='p': doc.add_paragraph(block['text'])
            elif kind=='bullets':
                for item in block['items']: doc.add_paragraph(item,'List Bullet')
            elif kind=='table': table(doc,block)
            else: raise ValueError(kind)
    doc.core_properties.title=spec['title']
    doc.core_properties.subject='Implementation specification; current baseline and proposed design are explicitly separated'
    doc.core_properties.author='Emberdeep Project'
    doc.core_properties.version=manifest['revision']
    OUTPUT.mkdir(parents=True,exist_ok=True)
    path=OUTPUT/spec['file']; doc.save(path)
    print(json.dumps({'file':str(path),'logical_sections':len(pages)},ensure_ascii=False))

if __name__=='__main__':
    manifest=json.loads((SOURCES/'DocumentManifest.json').read_text(encoding='utf-8-sig'))
    for spec in manifest['documents']: build(spec, manifest)
