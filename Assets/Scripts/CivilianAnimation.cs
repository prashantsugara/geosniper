using UnityEngine;
namespace GeoSniper
{
    [DefaultExecutionOrder(55)]
    public sealed class CivilianAnimation : MonoBehaviour
    {
        CivilianBot bot;Animation driver;string current;float previousHealth,reactionUntil;
        Transform idleSpine,idleHead;Vector3 spineAxis,headAxis;float idlePhase,idleOffset;bool idle;
        public bool Ready{get;private set;}
        public bool Initialize(CivilianBot owner,Transform visual)
        {
            bot=owner;if(visual==null)return false;
            var root=ImportedVisual.CharacterAnimationRoot(visual);string model=root.name.Replace("(Clone)","");
            var walk=Resources.Load<AnimationClip>("Models/Animations/Mixamo/"+model+"_walk");
            var fear=Resources.Load<AnimationClip>("Models/Animations/Mixamo/"+model+"_fear");
            if(walk==null || fear==null)return false;
            driver=root.GetComponent<Animation>();if(driver==null)driver=root.AddComponent<Animation>();driver.playAutomatically=false;driver.cullingType=AnimationCullingType.AlwaysAnimate;
            walk.legacy=true;fear.legacy=true;driver.AddClip(walk,"walk");driver.AddClip(fear,"fear");driver["walk"].wrapMode=WrapMode.Loop;driver["fear"].wrapMode=WrapMode.Loop;
            var run=Resources.Load<AnimationClip>("Models/Animations/Mixamo/"+model+"_run");if(run!=null){run.legacy=true;driver.AddClip(run,"run");driver["run"].wrapMode=WrapMode.Loop;}
            driver.Play("walk");driver["walk"].time=0;driver["walk"].speed=0;driver.Sample();current="walk";
            previousHealth=owner.Actor!=null?owner.Actor.Health:owner.health;
            idleOffset=owner.transform.position.x*.37f+owner.transform.position.z*.53f;Ready=true;return true;
        }
        void Update()
        {
            if(!Ready || bot==null)return;
            if(bot.Suspended || (bot.Actor!=null && bot.Actor.IsDead)){driver.Stop();current=null;return;}
            float health=bot.Actor!=null?bot.Actor.Health:previousHealth;if(health<previousHealth)reactionUntil=Time.time+1.3f;previousHealth=health;
            string next=bot.currentState==CivilianState.Cower || Time.time<reactionUntil?"fear":bot.CurrentSpeed>3 && driver.GetClip("run")!=null?"run":"walk";
            if(next!=current){current=next;driver.CrossFade(next,.15f);if(next=="fear")driver[next].time=4f;}
            driver[next].speed=next=="fear"?1:bot.CurrentSpeed<.1f?0:Mathf.Clamp(bot.CurrentSpeed/(next=="run"?5.5f:2),.65f,1.5f);
            idle=next=="walk" && bot.CurrentSpeed<.1f;
            if(idle)
            {
                // Restore the frozen base pose before adding breathing/look movement; never accumulate rotations.
                driver["walk"].time=0;driver.Sample();
                if(idleSpine==null || idleHead==null)
                    foreach(var bone in driver.GetComponentsInChildren<Transform>(true))
                    {
                        string name=bone.name.ToLowerInvariant().Replace("_","");
                        if(idleSpine==null && (name.Contains("spine2") || name.Contains("chest")))
                        {idleSpine=bone;spineAxis=bone.InverseTransformDirection(bot.transform.right).normalized;}
                        if(idleHead==null && name.EndsWith("head"))
                        {idleHead=bone;headAxis=bone.InverseTransformDirection(Vector3.up).normalized;}
                    }
            }
        }
        void LateUpdate()
        {
            if(!idle || !Ready || bot==null || bot.Suspended || (bot.Actor!=null && bot.Actor.IsDead))return;
            idlePhase+=Time.deltaTime;
            float offset=idleOffset;
            if(idleSpine!=null)idleSpine.localRotation*=Quaternion.AngleAxis(Mathf.Sin(idlePhase*1.7f+offset)*.7f,spineAxis);
            if(idleHead!=null)idleHead.localRotation*=Quaternion.AngleAxis(Mathf.Sin(idlePhase*.65f+offset)*5f,headAxis);
        }
    }
}
