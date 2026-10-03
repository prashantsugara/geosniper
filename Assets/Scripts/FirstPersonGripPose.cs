using UnityEngine;

namespace GeoSniper
{
    [DefaultExecutionOrder(200)]
    public sealed class FirstPersonGripPose : MonoBehaviour
    {
        Transform weapon, left, right;
        Vector3 leftPalm, rightPalm;
        Quaternion leftRotation, rightRotation;
        Transform[] bones;
        Quaternion[] rest;

        public void Initialize(Transform rifle,Transform rig)
        {
            weapon=rifle;
            left=WeaponGripPose.Bone(rig,"lwrist03","lefthand");
            right=WeaponGripPose.Bone(rig,"rwrist027","rwrist03","righthand");
            leftPalm=WeaponGripPose.PalmLocal(left);rightPalm=WeaponGripPose.PalmLocal(right);
            // Preserve the authored arm proportions while registering its master grip with this rifle.
            if(right!=null) rig.position += weapon.position-right.TransformPoint(rightPalm);
            if(left!=null) leftRotation=Quaternion.Inverse(weapon.rotation)*left.rotation;
            if(right!=null) rightRotation=Quaternion.Inverse(weapon.rotation)*right.rotation;
            bones=new[]{left?.parent?.parent,left?.parent,left,right?.parent?.parent,right?.parent,right};
            rest=new Quaternion[bones.Length];
            for(int i=0;i<bones.Length;i++) if(bones[i]!=null) rest[i]=bones[i].localRotation;
            Apply();
        }

        void LateUpdate() => Apply();
        public void Apply()
        {
            if(weapon==null || bones==null) return;
            // The imported arms are a static bind pose; always solve from that pose, not last frame's IK.
            for(int i=0;i<bones.Length;i++) if(bones[i]!=null) bones[i].localRotation=rest[i];
            var support=weapon.Find("WeaponSupport");
            WeaponGripPose.SolveArm(right,rightPalm,weapon.position,weapon.rotation*rightRotation,weapon.right-weapon.up);
            if(support!=null) WeaponGripPose.SolveArm(left,leftPalm,support.position,weapon.rotation*leftRotation,-weapon.right-weapon.up);
        }
    }
}
