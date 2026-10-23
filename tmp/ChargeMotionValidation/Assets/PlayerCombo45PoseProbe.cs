#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public class PlayerCombo45PoseProbe : MonoBehaviour
{
    private void Start()
    {
        PlayerMove player = FindFirstObjectByType<PlayerMove>();
        player.enabled = false;
        player.GetComponent<PlayerAttack>().enabled = false;
        Animator animator = player.GetComponent<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        HumanBodyBones[] bones = { HumanBodyBones.Hips, HumanBodyBones.Chest,
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };
        StringBuilder csv = new StringBuilder("clip,frame,time,bone,x,y,z,qx,qy,qz,qw\n");
        foreach (int number in new[] { 4, 5 })
        {
            string name = "AttackCombo" + number.ToString("00");
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath("Assets/ModularAnimalKnightsPolyart/Animation/" + name + ".fbx")
                .OfType<AnimationClip>().First(c => c.name == name);
            int frames = Mathf.RoundToInt(clip.length * clip.frameRate);
            for (int frame = 0; frame <= frames; frame++)
            {
                float time = frame / clip.frameRate;
                animator.Play(Animator.StringToHash("Base Layer." + name), 0, time / clip.length);
                animator.Update(0);
                foreach (HumanBodyBones bone in bones)
                {
                    Transform joint = animator.GetBoneTransform(bone);
                    Vector3 p = player.transform.InverseTransformPoint(joint.position);
                    Quaternion q = Quaternion.Inverse(player.transform.rotation) * joint.rotation;
                    csv.AppendLine($"{name},{frame},{time:F6},{bone},{p.x:F6},{p.y:F6},{p.z:F6},{q.x:F6},{q.y:F6},{q.z:F6},{q.w:F6}");
                }
            }
        }
        File.WriteAllText(Path.Combine(Application.dataPath, "../../player-combo45-poses.csv"), csv.ToString());
        EditorApplication.ExitPlaymode();
    }
}
#endif
