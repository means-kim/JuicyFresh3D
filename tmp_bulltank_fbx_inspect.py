import struct,zlib,pathlib,numpy as np,math
P=pathlib.Path('Assets/Zectorlab/BullTank/Art/Animations')
def read(path):
 b=path.read_bytes();pos=27;wide=struct.unpack_from('<I',b,23)[0]>=7500
 def node():
  nonlocal pos
  end,num,plen=struct.unpack_from('<QQQ' if wide else '<III',b,pos);pos+=24 if wide else 12;n=b[pos];pos+=1
  if not end:return None
  name=b[pos:pos+n].decode();pos+=n;props=[]
  for _ in range(num):
   t=chr(b[pos]);pos+=1
   if t in 'YCIFDL':
    fmt={'Y':'h','C':'?','I':'i','F':'f','D':'d','L':'q'}[t];props.append(struct.unpack_from('<'+fmt,b,pos)[0]);pos+=struct.calcsize(fmt)
   elif t in 'SR':
    size=struct.unpack_from('<I',b,pos)[0];pos+=4;v=b[pos:pos+size];pos+=size;props.append(v.decode(errors='replace') if t=='S' else v)
   else:
    count,enc,size=struct.unpack_from('<III',b,pos);pos+=12;v=b[pos:pos+size];pos+=size
    if enc:v=zlib.decompress(v)
    fmt={'f':'f','d':'d','l':'q','i':'i','b':'?','c':'b'}[t];props.append(struct.unpack('<'+str(count)+fmt,v))
  children=[]
  while pos<end-(25 if wide else 13):
   c=node()
   if c:children.append(c)
  pos=end;return name,props,children
 ns=[]
 while pos<len(b)-(25 if wide else 13):
  n=node()
  if n is None:break
  ns.append(n)
 return ns
def rotation(v):
 x,y,z=np.radians(v);cx,sx=math.cos(x),math.sin(x);cy,sy=math.cos(y),math.sin(y);cz,sz=math.cos(z),math.sin(z)
 return np.array([[cz,-sz,0],[sz,cz,0],[0,0,1]])@np.array([[cy,0,sy],[0,1,0],[-sy,0,cy]])@np.array([[1,0,0],[0,cx,-sx],[0,sx,cx]])
def analyze(path):
 ns=read(path);raw=next(c for n,p,c in ns if n=='Objects');objs={p[0]:(n,p,c) for n,p,c in raw if p};links=[p for n,p,c in next(c for n,p,c in ns if n=='Connections') if n=='C'];models={i:o for i,o in objs.items() if o[0]=='Model'}
 parents={p[1]:p[2] for p in links if p[0]=='OO' and p[1] in models and p[2] in models};props={i:{v[0]:v[4:] for a,pp,cc in c if a=='Properties70' for nn,v,ccc in cc if nn=='P'} for i,(n,p,c) in models.items()};curves={};times=[]
 for l in links:
  if l[0]=='OP' and l[1] in objs and objs[l[1]][0]=='AnimationCurveNode' and l[2] in models:
   axes={}
   for q in links:
    if q[0]=='OP' and q[2]==l[1] and q[1] in objs and objs[q[1]][0]=='AnimationCurve':
     data={n:p[0] for n,p,c in objs[q[1]][2] if p};tt=np.array(data['KeyTime'])/46186158000;vv=np.array(data['KeyValueFloat']);axes[q[3][-1]]=(tt,vv);times.extend(tt)
   curves[l[2],l[3]]=axes
 duration=max(times);samples=np.arange(round(duration*30)+1)/30;names={p[1].split('\x00')[0]:i for i,(n,p,c) in models.items()};out=[]
 def transform(i,t,cache):
  if i in cache:return cache[i]
  ps=props[i]
  def value(k,default):
   v=np.array(ps.get(k,default),dtype=float)
   for ax,(tt,vv) in curves.get((i,k),{}).items():v['XYZ'.index(ax)]=np.interp(t,tt,vv)
   return v
  m=np.eye(4);m[:3,:3]=rotation(ps.get('PreRotation',[0,0,0]))@rotation(value('Lcl Rotation',[0,0,0]))@rotation(ps.get('PostRotation',[0,0,0])).T@np.diag(value('Lcl Scaling',[1,1,1]));m[:3,3]=value('Lcl Translation',[0,0,0])
  if i in parents:m=transform(parents[i],t,cache)@m
  cache[i]=m;return m
 for t in samples:
  cache={};out.append({n:transform(i,t,cache)[:3,3] for n,i in names.items()})
 print(path.stem,'duration',round(duration,3))
 for bone in ['Weapon_R','Weapon_L']:
  z=np.array([x[bone][2] for x in out]);hi=np.argmax(z);low=np.argmin(z[hi:])+hi
  print(bone,'peak f',hi,'z',round(z[hi],1),'then low f',low,'z',round(z[low],1),'head z at peak',round(out[hi]['Head_M'][2],1),'start/end',round(z[0],1),round(z[-1],1))
 return samples,out
if __name__=='__main__':
 import matplotlib;matplotlib.use('Agg')
 import matplotlib.pyplot as plt
 paths=[P/f'AS_BullTank_{s}.FBX' for s in ['Attack01','Attack03_Start','Attack03_Loop','Attack03_End','Attack03_SpawnAxe']];fig,axs=plt.subplots(len(paths),6,figsize=(15,13))
 for row,path in enumerate(paths):
  tt,poses=analyze(path);edges=[('Root_M','Spine1_M'),('Spine1_M','Spine2_M'),('Spine2_M','Chest_M'),('Chest_M','Neck_M'),('Neck_M','Head_M')]
  for side in ['R','L']:edges += [('Chest_M','Scapula_'+side),('Scapula_'+side,'Shoulder_'+side),('Shoulder_'+side,'Elbow_'+side),('Elbow_'+side,'Wrist_'+side),('Wrist_'+side,'Weapon_'+side),('Root_M','Hip_'+side),('Hip_'+side,'Knee_'+side),('Knee_'+side,'Ankle_'+side)]
  for col,k in enumerate(np.linspace(0,len(poses)-1,6).astype(int)):
   pose=poses[k];ax=axs[row,col]
   for a,b in edges:
    p,q=pose[a],pose[b];ax.plot([p[1],q[1]],[p[2],q[2]],color='orange' if '_R' in b else '#8899aa',lw=2)
   p=pose['Weapon_R'];ax.scatter(p[1],p[2],c='red',s=40);ax.set_aspect('equal');ax.set_xlim(-160,160);ax.set_ylim(-10,300);ax.set_title(f'{path.stem.replace("AS_BullTank_","")} f{k}',fontsize=9);ax.grid(alpha=.2)
 fig.tight_layout();fig.savefig('tmp_bulltank_motion_analysis.png',dpi=120)
