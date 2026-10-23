using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public static class PlayerCombo45Analysis
{
    public static void Run()
    {
        PlayerMove player = Object.FindFirstObjectByType<PlayerMove>();
        Animator animator = player.GetComponent<Animator>();
        HumanBodyBones[] bones = { HumanBodyBones.Hips, HumanBodyBones.Chest,
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };
        StringBuilder csv = new StringBuilder("clip,frame,time,bone,x,y,z,qx,qy,qz,qw\n");
        foreach (int number in new[] { 4, 5 })
        {
            string name = "AttackCombo" + number.ToString("00");
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath("Assets/ModularAnimalKnightsPolyart/Animation/" + name + ".fbx")
                .OfType<AnimationClip>().First(c => c.name == name);
            PlayableGraph graph = PlayableGraph.Create("Pair45Sample");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false);
            playable.SetSpeed(0);
            var output = AnimationPlayableOutput.Create(graph, "Pose", animator);
            output.SetSourcePlayable(playable);
            graph.Play();
            int frames = Mathf.RoundToInt(clip.length * clip.frameRate);
            for (int frame = 0; frame <= frames; frame++)
            {
                float time = frame / clip.frameRate;
                playable.SetTime(time);
                graph.Evaluate(0);
                foreach (HumanBodyBones bone in bones)
                {
                    Transform joint = animator.GetBoneTransform(bone);
                    Vector3 p = player.transform.InverseTransformPoint(joint.position);
                    Quaternion q = Quaternion.Inverse(player.transform.rotation) * joint.rotation;
                    csv.AppendLine($"{name},{frame},{time:F6},{bone},{p.x:F6},{p.y:F6},{p.z:F6},{q.x:F6},{q.y:F6},{q.z:F6},{q.w:F6}");
                }
            }
            graph.Destroy();
        }
        File.WriteAllText(Path.Combine(Application.dataPath, "../../player-combo45-poses.csv"), csv.ToString());
    }
}
