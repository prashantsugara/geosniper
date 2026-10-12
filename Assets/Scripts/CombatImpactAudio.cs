using UnityEngine;
namespace GeoSniper
{
    // Separate source keeps impact pitch variation from altering the rifle report or dialogue.
    public sealed class CombatImpactAudio : MonoBehaviour
    {
        AudioSource source;AudioClip body,head;
        public static void Play(bool headshot)
        {
            var camera=Camera.main;if(camera==null)return;
            var feedback=camera.GetComponent<CombatImpactAudio>() ?? camera.gameObject.AddComponent<CombatImpactAudio>();
            if(feedback.source==null)
            {
                feedback.source=feedback.gameObject.AddComponent<AudioSource>();feedback.source.playOnAwake=false;feedback.source.spatialBlend=0;
                feedback.body=ProceduralAudio.CreateBodyHitThud();feedback.head=ProceduralAudio.CreateHeadshotPing();
            }
            feedback.source.pitch=Random.Range(.97f,1.03f);
            feedback.source.PlayOneShot(headshot?feedback.head:feedback.body,headshot?.85f:.7f);
        }
        void OnDestroy()
        {
            if(body!=null){if(Application.isPlaying)Destroy(body);else DestroyImmediate(body);}
            if(head!=null){if(Application.isPlaying)Destroy(head);else DestroyImmediate(head);}
            if(source!=null){if(Application.isPlaying)Destroy(source);else DestroyImmediate(source);}
        }
    }
}
