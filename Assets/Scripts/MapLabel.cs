using UnityEngine;

namespace GeoSniper
{
    public sealed class MapLabel : MonoBehaviour
    {
        Camera view;
        void Start(){ view=Camera.main; }
        void LateUpdate(){ if(view!=null) { transform.LookAt(view.transform); transform.Rotate(0,180,0,Space.Self); } }
    }
}
