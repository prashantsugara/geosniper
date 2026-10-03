using UnityEngine;

namespace GeoSniper
{
    // Shared by lobby, enemy and first-person rigs. All distances are in world metres.
    public static class WeaponGripPose
    {
        public static Transform Bone(Transform root, params string[] names)
        {
            if(root==null) return null;
            foreach(string name in names)
                foreach(var child in root.GetComponentsInChildren<Transform>(true))
                {
                    string key=child.name.ToLowerInvariant().Replace(":", "").Replace("_", "").Replace(".", "");
                    if(key.EndsWith(name)) return child;
                }
            return null;
        }

        public static Vector3 PalmLocal(Transform hand)
        {
            if(hand==null) return Vector3.zero;
            foreach(var child in hand.GetComponentsInChildren<Transform>(true))
            {
                string name=child.name.ToLowerInvariant();
                if(child.parent==hand && (name.Contains("middle") || name.Contains("index") || name.Contains("finger01")))
                    return hand.InverseTransformPoint(Vector3.Lerp(hand.position,child.position,.65f));
            }
            // Wrist is a safe fallback; never invent a character-root offset.
            return Vector3.zero;
        }

        public static void Marker(Transform root,string name,Vector3 local)
        {
            var marker=root.Find(name);
            if(marker==null) {marker=new GameObject(name).transform;marker.SetParent(root,false);}
            marker.localPosition=local;
        }

        public static void Markers(Transform root,float support,float stock,Vector3 muzzle)
        {
            Marker(root,"WeaponGrip",Vector3.zero);
            Marker(root,"WeaponSupport",new Vector3(0,.025f,support));
            Marker(root,"WeaponStock",new Vector3(0,.065f,-stock));
            Marker(root,"WeaponMuzzle",muzzle);
        }

        // Analytic two-bone solve: preserves bone lengths and the authored elbow bend plane.
        public static void SolveArm(Transform hand,Vector3 palmLocal,Vector3 target,Quaternion rotation,Vector3 bendHint)
        {
            if(hand==null || hand.parent==null || hand.parent.parent==null) return;
            Transform elbow=hand.parent, shoulder=elbow.parent;
            Vector3 wristTarget=target-rotation*Vector3.Scale(palmLocal,hand.lossyScale);
            Vector3 origin=shoulder.position;
            float upper=Vector3.Distance(origin,elbow.position),lower=Vector3.Distance(elbow.position,hand.position);
            if(upper<.03f || lower<.03f) return;
            Vector3 delta=wristTarget-origin;
            float length=Mathf.Clamp(delta.magnitude,Mathf.Abs(upper-lower)+.0001f,upper+lower-.0001f);
            if(delta.sqrMagnitude<.000001f) return;
            Vector3 direction=delta.normalized;
            Vector3 bend=Vector3.ProjectOnPlane(elbow.position-origin,direction);
            if(bend.sqrMagnitude<.00001f) bend=Vector3.ProjectOnPlane(bendHint,direction);
            if(bend.sqrMagnitude<.00001f) bend=Vector3.Cross(direction,Vector3.right);
            bend.Normalize();
            float along=(upper*upper-lower*lower+length*length)/(2*length);
            float height=Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
            Vector3 elbowTarget=origin+direction*along+bend*height;
            shoulder.rotation=Quaternion.FromToRotation(elbow.position-origin,elbowTarget-origin)*shoulder.rotation;
            elbow.rotation=Quaternion.FromToRotation(hand.position-elbow.position,origin+direction*length-elbow.position)*elbow.rotation;
            hand.rotation=rotation;
        }
    }
}
