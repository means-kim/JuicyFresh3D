from pathlib import Path
import csv, math, json

root = Path(__file__).resolve().parent
rows = list(csv.DictReader((root / 'player-combo45-poses.csv').open()))
poses = {}
for row in rows:
    poses[(int(row['clip'][-2:]), int(row['frame']), row['bone'])] = ([float(row[k]) for k in ('x','y','z')], [float(row[k]) for k in ('qx','qy','qz','qw')])

def difference(a, b):
    distance = math.dist(a[0], b[0])
    qa, qb = a[1], b[1]
    dot = abs(sum(x*y for x,y in zip(qa,qb))) / math.sqrt(sum(x*x for x in qa)*sum(y*y for y in qb))
    angle = math.degrees(2*math.acos(min(1,max(-1,dot))))
    return round(distance,4), round(angle,2)

for number, frames in ((4, range(12,26)), (5, range(0,16))):
    print('Clip',number,'frame: right hand displacement/angle, chest displacement/angle since previous frame')
    for frame in frames:
        if frame:
            print(frame, *(difference(poses[(number,frame-1,bone)],poses[(number,frame,bone)]) for bone in ('RightHand','Chest')))
print('04 last pose -> 05 initial poses: hand displacement/angle, chest displacement/angle')
for frame in range(0,9):
    print(frame, *(difference(poses[(4,25,bone)],poses[(5,frame,bone)]) for bone in ('RightHand','Chest')))

(root/'player-combo45-pose-analysis.txt').write_text(json.dumps({
    '04_last_frame_vs_05_start': {bone: difference(poses[(4,25,bone)],poses[(5,0,bone)]) for bone in ('RightHand','Chest')},
    '04_last_step': {bone: difference(poses[(4,24,bone)],poses[(4,25,bone)]) for bone in ('RightHand','Chest')},
    '05_first_step': {bone: difference(poses[(5,0,bone)],poses[(5,1,bone)]) for bone in ('RightHand','Chest')}
},indent=2))
