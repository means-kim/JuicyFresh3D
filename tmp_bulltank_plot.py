import runpy,numpy as np
from PIL import Image,ImageDraw
D=runpy.run_path('tmp_bulltank_fbx_inspect.py');img=Image.new('RGB',(1440,1050),'white');draw=ImageDraw.Draw(img)
for row,s in enumerate(['Attack01','Attack03_Start','Attack03_Loop','Attack03_End','Attack03_SpawnAxe']):
 tt,poses=D['analyze'](D['P']/('AS_BullTank_'+s+'.FBX'));edges=[('Root_M','Spine1_M'),('Spine1_M','Spine2_M'),('Spine2_M','Chest_M'),('Chest_M','Neck_M'),('Neck_M','Head_M')]
 for side in ['R','L']:edges += [('Chest_M','Scapula_'+side),('Scapula_'+side,'Shoulder_'+side),('Shoulder_'+side,'Elbow_'+side),('Elbow_'+side,'Wrist_'+side),('Wrist_'+side,'Weapon_'+side),('Root_M','Hip_'+side),('Hip_'+side,'Knee_'+side),('Knee_'+side,'Ankle_'+side)]
 for col,k in enumerate(np.linspace(0,len(poses)-1,6).astype(int)):
  pose=poses[k]; ox=col*240;oy=row*210
  def xy(n):p=pose[n];return (ox+120+p[1]*.6,oy+195-p[2]*.6)
  draw.text((ox+5,oy+4),s+' '+str(round(tt[k],2))+'s',fill='black');draw.line((ox,oy+195,ox+239,oy+195),fill='#dddddd')
  for a,b in edges:draw.line([xy(a),xy(b)],fill='#ed8b23' if '_R' in b else '#91a5b8',width=3)
  x,y=xy('Weapon_R');draw.ellipse((x-4,y-4,x+4,y+4),fill='red')
img.save('tmp_bulltank_motion_analysis.png')
