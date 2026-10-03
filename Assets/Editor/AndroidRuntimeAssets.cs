using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Runtime-created materials have no scene reference for the build dependency collector.
public sealed class AndroidRuntimeAssets : IPreprocessBuildWithReport
{
    public int callbackOrder => -100;
    static readonly string[] RuntimeShaders = { "Standard", "Unlit/Color" };
    public void OnPreprocessBuild(BuildReport report)
    {
        var settings=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset").FirstOrDefault();
        if(settings==null) throw new BuildFailedException("Cannot access Graphics Settings.");
        var serialized=new SerializedObject(settings);
        var included=serialized.FindProperty("m_AlwaysIncludedShaders");
        if(included==null) throw new BuildFailedException("Cannot preserve the runtime shader.");
        foreach(var shaderName in RuntimeShaders)
        {
            var shader=Shader.Find(shaderName);
            if(shader==null) continue;
            bool found=false;
            for(int i=0;i<included.arraySize;i++)
                if(included.GetArrayElementAtIndex(i).objectReferenceValue==shader) { found=true; break; }
            if(!found)
            {
                int index=included.arraySize;
                included.InsertArrayElementAtIndex(index);
                included.GetArrayElementAtIndex(index).objectReferenceValue=shader;
            }
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }
}
