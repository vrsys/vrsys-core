using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRSYS.Recording;
using VRSYS.Recording.Testing;

public static class RecordingTestGenericRecorderChecks
{
    [MenuItem("VRSYS/Recording/Validate Generic Recorder Samples")]
    public static void Validate()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run this check outside Play mode.");
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var renderer = Instantiate("RecordingTestRenderer").GetComponentInChildren<MeshRenderer>();
            var driver = renderer.GetComponent<RecordingTestRenderer>();
            Call(driver,"Awake"); Call(driver,"Tick",2f);
            var recorder = renderer.gameObject.AddComponent<RecordingTestRendererRecorder>();
            recorder.OnReplayStart();
            Check((bool)Call(recorder,"FillGenericData"),"Renderer capture failed.");
            CopySlots(recorder);
            var expected = new MaterialPropertyBlock(); renderer.GetPropertyBlock(expected);
            Call(driver,"Tick",5.6f);
            Check(!renderer.enabled,"Renderer toggle was not exercised.");
            Call(recorder,"ProcessReplayData",2f);
            var actual = new MaterialPropertyBlock(); renderer.GetPropertyBlock(actual);
            Check(renderer.enabled,"Renderer enabled state was not replayed.");
            Check(actual.GetColor("_BaseColor")==expected.GetColor("_BaseColor"),"Renderer color did not round-trip.");
            Check(actual.GetColor("_EmissionColor")==expected.GetColor("_EmissionColor"),"Renderer emission did not round-trip.");
            recorder.OnReplayEnd(); renderer.GetPropertyBlock(actual);
            Check(actual.GetColor("_BaseColor")==expected.GetColor("_BaseColor"),"Renderer state was not restored.");

            var particles = Instantiate("RecordingTestParticles").GetComponentInChildren<ParticleSystem>();
            var particleDriver=particles.GetComponent<RecordingTestParticles>();
            Call(particleDriver,"Awake"); Call(particleDriver,"Tick",2.1f);
            var particleRecorder=particles.gameObject.AddComponent<RecordingTestParticlesRecorder>(); particleRecorder.OnReplayStart();
            Check((bool)Call(particleRecorder,"FillGenericData"),"Particle capture failed."); CopySlots(particleRecorder);
            var before = new ParticleSystem.Particle[particles.main.maxParticles]; int count = particles.GetParticles(before);
            Check(count>0,"Fountain did not emit any particles.");
            var replayFloats=(float[])typeof(GenericRecorder).GetField("_replayFloatDTO",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(particleRecorder);
            var recordedFloats=(float[])typeof(GenericRecorder).GetField("_recFloatDTO",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(particleRecorder);
            var replayInts=(int[])typeof(GenericRecorder).GetField("_replayIntDTO",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(particleRecorder);
            var recordedColor=particles.main.startColor.color;
            Call(particleDriver,"Tick",4.4f);
            Check(Mathf.Abs(particles.main.startSpeed.constant-recordedFloats[2])>0.01f,"Particle test did not animate its settings.");
            var disabledEmission=particles.emission; disabledEmission.enabled=false;
            var disabledShape=particles.shape; disabledShape.enabled=false;
            particles.GetComponent<ParticleSystemRenderer>().enabled=false;
            foreach(float time in new[]{6.5f,0.4f,10.1f,2.1f}) { replayFloats[0]=time; Call(particleRecorder,"ProcessReplayData",time); }
            Check(Mathf.Abs(particles.emission.rateOverTime.constant-recordedFloats[1])<0.0001f,"Emission rate did not round-trip.");
            Check(Mathf.Abs(particles.main.startSpeed.constant-recordedFloats[2])<0.0001f,"Start speed did not round-trip.");
            Check(Mathf.Abs(particles.main.startLifetime.constant-recordedFloats[3])<0.0001f,"Lifetime did not round-trip.");
            Check(Mathf.Abs(particles.main.startSize.constant-recordedFloats[4])<0.0001f,"Size did not round-trip.");
            Check(Mathf.Abs(particles.main.gravityModifier.constant-recordedFloats[5])<0.0001f,"Gravity did not round-trip.");
            Check(Mathf.Abs(particles.shape.angle-recordedFloats[6])<0.0001f,"Cone angle did not round-trip.");
            Check(Mathf.Abs(particles.shape.radius-recordedFloats[7])<0.0001f,"Cone radius did not round-trip.");
            Check(Mathf.Abs(particles.main.startRotation.constant-recordedFloats[8])<0.0001f,"Rotation did not round-trip.");
            Color replayColor=particles.main.startColor.color;
            for(int channel=0;channel<4;channel++) Check(Mathf.Abs(recordedColor[channel]-replayColor[channel])<=1f/255f,"Start color did not round-trip.");
            Check(particles.emission.enabled && particles.shape.enabled && particles.GetComponent<ParticleSystemRenderer>().enabled,"Particle module flags did not round-trip.");
            var after = new ParticleSystem.Particle[before.Length]; Check(particles.GetParticles(after)==count,"Particle count changed after backward seeking.");
            for(int i=0;i<count;i++)
            {
                Check(Vector3.Distance(before[i].position,after[i].position)<0.0001f,"Particle position changed after backward seeking.");
                Check(Mathf.Abs(before[i].remainingLifetime-after[i].remainingLifetime)<0.0001f,"Particle lifetime changed after backward seeking.");
                Check(before[i].randomSeed==after[i].randomSeed,"Particle seed changed after backward seeking.");
            }
            Call(particleRecorder,"ProcessReplayData",2.1f);
            Check(particles.GetParticles(after)==count,"Paused playback changed particle count.");
            replayInts[2]=0; replayInts[3]=0; replayInts[4]=0;
            Call(particleRecorder,"ProcessReplayData",2.1f);
            Check(!particles.emission.enabled && !particles.shape.enabled && !particles.GetComponent<ParticleSystemRenderer>().enabled,"Same-time module changes were skipped.");
            replayInts[2]=1; replayInts[3]=1; replayInts[4]=1;
            Call(particleRecorder,"ProcessReplayData",2.1f);
            Check(particles.GetParticles(after)==count,"Same-time re-enabling did not reconstruct the fountain.");
            replayInts[1]=0;
            var legacyMain=particles.main; legacyMain.startSpeed=1.4f;
            Call(particleRecorder,"ProcessReplayData",2.1f);
            Check(Mathf.Abs(particles.main.startSpeed.constant-1.4f)<0.0001f,"Legacy time/seed samples overwrote settings.");
            particleRecorder.OnReplayEnd();
            Check(particles.main.startColor.color==recordedColor,"Particle color was not restored after replay.");
            Check(Mathf.Abs(particles.main.startSpeed.constant-recordedFloats[2])<0.0001f,"Particle settings were not restored after replay.");
            var testRoot=new GameObject("Disabled Test Registration",typeof(RecordingTestObjectsController));
            var rootController=testRoot.GetComponent<RecordingTestObjectsController>(); rootController.runTestBehaviours=false;
            renderer.transform.root.SetParent(testRoot.transform,false);
            particles.transform.root.SetParent(testRoot.transform,false);
            driver.enabled=false; particleDriver.enabled=false;
            var setup=new GameObject("Registration Check",typeof(RecorderController));
            var controller=setup.GetComponent<RecorderController>(); controller.recorderState=setup.GetComponent<RecorderState>();
            var providers=(System.Collections.IList)typeof(RecorderController).GetField("_genericRecorderProviders",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controller);
            typeof(RecordingTestBehaviour).GetField("_controller",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(driver,controller);
            Call(driver,"OnEnable");
            Check(providers.Contains(driver),"A cached controller prevented provider registration after reload.");
            providers.Clear();
            typeof(RecordingTestObjectsController).GetField("_recorder",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(rootController,controller);
            Call(rootController,"OnEnable"); Call(rootController,"OnEnable");
            Check(providers.Count==1,"Root provider registration was not idempotent.");
            Call(controller,"AttachGenericRecorder");
            Check(controller.GetGenericRecorderDict().ContainsValue(recorder),"Disabled renderer test did not attach its recorder.");
            Check(controller.GetGenericRecorderDict().ContainsValue(particleRecorder),"Disabled particle test did not attach its recorder.");
            typeof(RecordingTestRenderer).GetField("_properties",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(driver,null);
            controller.onReplayIsPreparing.Invoke();
            Check(!rootController.IsRunning,"Recorder attachment enabled live tests.");
            Debug.Log("Generic recorder samples: renderer state round-trip and restoration; animated particle settings, forward/backward seeking, same-time changes, legacy data, restoration and disabled/reloaded provider registration passed ("+count+" particles).");
        }
        finally { EditorSceneManager.CloseScene(scene,true); SceneManager.SetActiveScene(previous); }
    }
    static GameObject Instantiate(string name)
    {
        string[] guids=AssetDatabase.FindAssets(name+" t:Prefab");
        foreach(string guid in guids)
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            if(Path.GetFileNameWithoutExtension(path)==name)
                return (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        }
        throw new InvalidOperationException("Import the Recording Tests sample first: missing "+name);
    }
    static object Call(object target,string method,params object[] args)
    {
        return target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(target,args);
    }
    static void CopySlots(GenericRecorder recorder)
    {
        foreach(string type in new[]{"Int","Float","Char"})
        {
            var recorded=(Array)typeof(GenericRecorder).GetField("_rec"+type+"DTO",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(recorder);
            var replay=(Array)typeof(GenericRecorder).GetField("_replay"+type+"DTO",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(recorder);
            Array.Copy(recorded,replay,recorded.Length);
        }
    }
    static void Check(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
}
