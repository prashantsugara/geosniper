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
        float reloadProgress=1,boltBlend,boltPull;
        public void SetHandling(float reload,float bolt,float pull=0){reloadProgress=reload;boltBlend=bolt;boltPull=pull;}

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
            var rightTarget=weapon.TransformPoint(new Vector3(.055f,.065f,-.035f)*boltBlend-Vector3.forward*(.075f*boltPull));
            WeaponGripPose.SolveArm(right,rightPalm,rightTarget,weapon.rotation*Quaternion.Euler(0,0,-18f*boltBlend)*rightRotation,weapon.right-weapon.up);
            if(support!=null)
            {
                float reach=WeaponHandlingMotion.ReloadHand(reloadProgress);
                float withdraw=WeaponHandlingMotion.Ease(.28f,.44f,reloadProgress)*(1-WeaponHandlingMotion.Ease(.48f,.65f,reloadProgress));
                var magazine=weapon.TransformPoint(new Vector3(-.025f,-.07f-withdraw*.16f,.075f));
                var target=Vector3.Lerp(support.position,magazine,reach);
                WeaponGripPose.SolveArm(left,leftPalm,target,weapon.rotation*Quaternion.Euler(10f*reach,0,24f*reach)*leftRotation,-weapon.right-weapon.up);
            }
        }
    }
}
