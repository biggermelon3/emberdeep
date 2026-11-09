"""Build the one-page English Emberdeep overview and development update."""
from pathlib import Path
import json

from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import letter
from reportlab.lib.styles import ParagraphStyle
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import BaseDocTemplate, Frame, PageTemplate, Paragraph, Spacer
from pypdf import PdfReader

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / 'output/pdf/Emberdeep-Development-Update-0.2.6.pdf'
REPO = 'https://github.com/biggermelon3/emberdeep'
ICON = ROOT / 'Assets/FireRescue/Art/AppIcon/Emberdeep-Hydrant-Icon.png'
pdfmetrics.registerFont(TTFont('Segoe', 'C:/Windows/Fonts/segoeui.ttf'))
pdfmetrics.registerFont(TTFont('SegoeBold', 'C:/Windows/Fonts/segoeuib.ttf'))
pdfmetrics.registerFontFamily('Segoe', normal='Segoe', bold='SegoeBold')
INK = colors.HexColor('#152B2B')
TEAL = colors.HexColor('#15666B')
GRAY = colors.HexColor('#536568')
ORANGE = colors.HexColor('#D65B35')
BODY = ParagraphStyle('Body', fontName='Segoe', fontSize=10.5, leading=14.7,
                      textColor=INK, alignment=TA_LEFT, spaceAfter=7)
HEADING = ParagraphStyle('Heading', parent=BODY, fontName='SegoeBold', fontSize=11.5,
                         leading=15, spaceBefore=9, spaceAfter=5, keepWithNext=True)
ITEM = ParagraphStyle('Item', parent=BODY, spaceAfter=9)

def page(canvas, doc):
    canvas.saveState()
    canvas.setFillColor(ORANGE)
    canvas.rect(0, 786, 612, 6, fill=1, stroke=0)
    canvas.drawImage(str(ICON), 492, 690, width=72, height=72, mask='auto')
    canvas.setFillColor(INK)
    canvas.setFont('SegoeBold', 28)
    canvas.drawString(48, 735, 'EMBERDEEP')
    canvas.setFont('Segoe', 13)
    canvas.drawString(48, 713, 'Game overview and development update')
    canvas.setFont('Segoe', 9)
    canvas.setFillColor(GRAY)
    canvas.drawString(48, 692, '8 November 2026  |  Version 0.2.6  |  Android code 9')
    canvas.setStrokeColor(colors.HexColor('#D6E2E0'))
    canvas.setLineWidth(.6)
    canvas.line(48, 677, 564, 677)
    canvas.setFillColor(colors.HexColor('#EDF4F2'))
    canvas.roundRect(48, 29, 516, 42, 5, fill=1, stroke=0)
    canvas.setFillColor(GRAY)
    canvas.setFont('Segoe', 8)
    canvas.drawString(60, 56, 'PROJECT REPOSITORY  /  PRIVATE - ACCESS REQUIRED')
    canvas.setFillColor(TEAL)
    canvas.setFont('SegoeBold', 10)
    canvas.drawString(60, 40, REPO)
    canvas.linkURL(REPO, (59, 36, 400, 52), relative=0, thickness=0)
    canvas.setFillColor(GRAY)
    canvas.setFont('Segoe', 8)
    canvas.drawRightString(552, 40, 'Development log  /  1')
    canvas.restoreState()

story = [
    Paragraph('About the game', HEADING),
    Paragraph('Emberdeep is a hand-tracked VR firefighting game for <b>Quest 3 and PICO 4</b>. '
              'Players act as guardians of a miniature forest, collecting water and using distinct tools '
              'to contain fires in reachable tabletop areas. The planned expedition loop expands into '
              'searching recovered ground, rescuing wildlife and improving equipment to reach deeper fire zones.', BODY),
    Paragraph('Recent updates', HEADING),
    Paragraph('<b>More expressive hands.</b> Fixed hand models freezing after entering gameplay. '
              'Hands retain their last valid pose during tracking interruptions while related actions stop safely. '
              'Added equipment-specific grip poses, staged finger closure, squeeze and release. '
              'Palm contact now stays anchored relative to the handle during wrist rotation; tool aiming still follows the real hand.', ITEM),
    Paragraph('<b>Natural well-pump interaction.</b> The starter lever now recognizes a whole-hand grasp instead of an index-thumb pinch. '
              'Open near the handle, grip, lower roughly 8 cm and return. An undocked stroke produces a brief splash from the socket; '
              'a docked stroke adds 12 water units, capped by tank capacity.', ITEM),
    Paragraph('<b>Clearer water collection and controls.</b> Added guidance for docking, lowering, returning and full tanks. '
              'Water stations progress from a lever well to a rotary hydrant and then a one-press filling station. '
              'Interrupted strokes are discarded. Contextual hand rays support menu selection without remaining visible everywhere.', ITEM),
    Paragraph('<b>Visual feedback and art workflow.</b> Retained curved water streams, cannon charging and two-hand pumping, '
              'and refined the flame presentation. Organized separate concept art, editable Blender assets and Unity materials. '
              'The hydrant application icon is retained; some tools and water stations remain interaction grayboxes.', ITEM),
    Paragraph('<b>Independent project and delivery.</b> Established a private GitHub repository with Git LFS and a pinned PICO SDK dependency. '
              'Quest 3 and PICO 4 APKs for 0.2.6 are available. Standalone builds contain only the camp and fireground scenes '
              'and restore temporary build settings.', ITEM),
    Paragraph('Validation and next steps', HEADING),
    Paragraph('<b>167 EditMode and 84 PlayMode tests passed</b>, including a fresh regression after repository migration. '
              'Both APKs passed version, signature and scene-whitelist checks. New grasp recognition, contact fit, comfort and '
              'device performance still require headset validation. Next: refine hand feel, then develop safe searching, '
              'rewards and optional animal rescue. These exploration features and durable expedition saves are not yet delivered.', BODY),
]

OUTPUT.parent.mkdir(parents=True, exist_ok=True)
document = BaseDocTemplate(str(OUTPUT), pagesize=letter, title='Emberdeep Game Overview and Development Update',
                           author='Emberdeep Project', subject='Version 0.2.6 development update',
                           leftMargin=48, rightMargin=48, topMargin=123, bottomMargin=84)
frame = Frame(48, 84, 516, 586, leftPadding=0, rightPadding=0, topPadding=0, bottomPadding=0)
document.addPageTemplates(PageTemplate(id='OnePage', frames=[frame], onPage=page))
document.build(story)
reader = PdfReader(OUTPUT)
assert len(reader.pages) == 1, f'Expected one page, got {len(reader.pages)}'
text = reader.pages[0].extract_text()
assert all(value in text for value in ['EMBERDEEP', '167 EditMode', '84 PlayMode', REPO, 'not yet delivered'])
uris = [a.get_object().get('/A', {}).get('/URI') for a in reader.pages[0].get('/Annots', [])]
assert REPO in uris, 'Missing clickable GitHub link'
print(json.dumps({'path':str(OUTPUT), 'pages':len(reader.pages), 'clickable_url':REPO, 'words':len(text.split())}))
