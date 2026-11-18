"""One-page English update, matching the previous 0.2.6 report's visual format."""
from pathlib import Path
import json
from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import letter
from reportlab.lib.styles import ParagraphStyle
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import BaseDocTemplate, Frame, PageTemplate, Paragraph
from pypdf import PdfReader

ROOT=Path(__file__).resolve().parents[1]
OUTPUT=ROOT/'output/pdf/Emberdeep-Weekly-Update-0.2.24.pdf'
REPO='https://github.com/biggermelon3/emberdeep'
ICON=ROOT/'Assets/FireRescue/Art/AppIcon/Emberdeep-Hydrant-Icon.png'
pdfmetrics.registerFont(TTFont('Segoe','C:/Windows/Fonts/segoeui.ttf'))
pdfmetrics.registerFont(TTFont('SegoeBold','C:/Windows/Fonts/segoeuib.ttf'))
pdfmetrics.registerFontFamily('Segoe',normal='Segoe',bold='SegoeBold')
INK=colors.HexColor('#152B2B');TEAL=colors.HexColor('#15666B');GRAY=colors.HexColor('#536568')
BODY=ParagraphStyle('Body',fontName='Segoe',fontSize=10.5,leading=14.7,textColor=INK,alignment=TA_LEFT,spaceAfter=7)
HEADING=ParagraphStyle('Heading',parent=BODY,fontName='SegoeBold',fontSize=11.5,leading=15,spaceBefore=9,spaceAfter=5,keepWithNext=True)
ITEM=ParagraphStyle('Item',parent=BODY,spaceAfter=8)

def page(c,doc):
 c.saveState();c.setFillColor(colors.HexColor('#D65B35'));c.rect(0,786,612,6,fill=1,stroke=0)
 c.drawImage(str(ICON),492,690,width=72,height=72,mask='auto')
 c.setFillColor(INK);c.setFont('SegoeBold',28);c.drawString(48,735,'EMBERDEEP')
 c.setFont('Segoe',13);c.drawString(48,713,'Game overview and weekly development update')
 c.setFont('Segoe',9);c.setFillColor(GRAY);c.drawString(48,692,'16 November 2026  |  Version 0.2.24  |  Android code 27')
 c.setStrokeColor(colors.HexColor('#D6E2E0'));c.setLineWidth(.6);c.line(48,677,564,677)
 c.setFillColor(colors.HexColor('#EDF4F2'));c.roundRect(48,29,516,42,5,fill=1,stroke=0)
 c.setFillColor(GRAY);c.setFont('Segoe',8);c.drawString(60,56,'PROJECT REPOSITORY')
 c.setFillColor(TEAL);c.setFont('SegoeBold',10);c.drawString(60,40,REPO)
 c.linkURL(REPO,(59,36,410,52),relative=0,thickness=0)
 c.setFillColor(GRAY);c.setFont('Segoe',8);c.drawRightString(552,40,'Weekly update  /  1');c.restoreState()

installed_path=ROOT/'Logs/Emberdeep024/InstalledApp.json'
installed=False
if installed_path.exists():
 record=json.loads(installed_path.read_text(encoding='utf-8-sig'))
 installed=record.get('version_name')=='0.2.24' and str(record.get('version_code'))=='27'
delivery='Quest 3 build installed and version verified.' if installed else 'Quest 3 package is being built for device testing.'

story=[
 Paragraph('About the game',HEADING),
 Paragraph('Emberdeep is a <b>hand-tracked VR/MR firefighting game</b> targeting Quest 3 and PICO 4. '
           'As a giant guardian of a miniature forest, players use magical palm water or upgradable firefighting tools '
           'to contain flames, protect cabins and rescue sheep. Rescue rewards fund stronger magic and equipment; '
           'physical water stations replenish tool tanks.',BODY),
 Paragraph('Since the previous report: 0.2.6 (8 November)',HEADING),
 Paragraph('<b>Magical hands and an eight-level expedition.</b> A left-hand crystal displays mana. Open the left palm to aim; '
           'touch its back with any right fingertip to spray. The first three levels teach palm water, followed by the '
           'original five forest levels. A guided first upgrade increases capacity; base and upgraded recharge take '
           'about 5 and 10 seconds. The starter extinguisher gains flow and range of effect.',ITEM),
 Paragraph('<b>Animal rescue is now playable.</b> Cute sheep wander, panic and burn near fires. Water protects them; '
           'survivors escape, celebrate and turn into coins. Smaller models, fire effects, bleats and happy reactions '
           'add character. Players can now pick sheep up, release them gently, or pet them for hearts and cheerful feedback.',ITEM),
 Paragraph('<b>More flexible physical interactions.</b> Refined hand-specific grip poses, contact hints, nozzle aiming and '
           'hand-to-hand transfers. Tools align smoothly on pickup, float when released, wobble after 3 seconds and return '
           'to the rack after 5. Lever strokes deliver water proportionally. The refill station stays left; equipment stays right.',ITEM),
 Paragraph('<b>Clearer pacing, teaching and replay.</b> Every level opens with a safe preparation preview and an explicit '
           'start button, then a 3-second countdown. Successful actions dismiss their tutorials. Replaying a level now '
           'keeps earned coins and upgrades, allowing fresh rescues to earn new rewards. Animated teaching uses the new fingertip gesture.',ITEM),
 Paragraph('<b>A more cohesive forest presentation.</b> Added cartoon clouds and forest silhouettes behind the miniature '
           'scene while retaining transparent surroundings, improved river water and softened visual aliasing. Moving clouds, '
           'broader wind coverage, dust and fire updrafts animate the setting. Menus use forest-green panels and warm wood edges.',ITEM),
 Paragraph('<b>Language and menu polish.</b> English and Simplified Chinese follow the system language, with offline fonts '
           'and 302 text entries. Quitting requires confirmation.',ITEM),
 Paragraph('Validation and delivery',HEADING),
 Paragraph('<b>479 automated tests passed</b> (245 EditMode / 234 PlayMode). '+delivery+' '
           'Fingertip comfort, occlusion and sheep handling still need worn-headset checks. '
           'No PICO 0.2.24 package has been validated; persistent saves remain future work.',BODY)
]
OUTPUT.parent.mkdir(parents=True,exist_ok=True)
doc=BaseDocTemplate(str(OUTPUT),pagesize=letter,title='Emberdeep - Weekly Development Update - 0.2.24',
                   author='Emberdeep Project',subject='Changes since the 8 November 2026 report (0.2.6)',
                   leftMargin=48,rightMargin=48,topMargin=123,bottomMargin=84)
doc.addPageTemplates(PageTemplate(id='OnePage',frames=[Frame(48,84,516,586,leftPadding=0,rightPadding=0,topPadding=0,bottomPadding=0)],onPage=page))
doc.build(story)
r=PdfReader(OUTPUT);assert len(r.pages)==1,f'Expected one page, got {len(r.pages)}'
text=r.pages[0].extract_text()
for value in ['EMBERDEEP','0.2.24','0.2.6','479','245 EditMode','234 PlayMode',REPO,'persistent saves']:
 assert value in text,value
uris=[a.get_object().get('/A',{}).get('/URI') for a in r.pages[0].get('/Annots',[])]
assert REPO in uris
print(json.dumps({'path':str(OUTPUT),'pages':1,'words':len(text.split()),'clickable_url':REPO,'installed_status':installed}))
