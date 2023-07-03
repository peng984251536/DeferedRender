using UnityEditor;

public class ShaderTemplateEditor : Editor
{



    [MenuItem("Assets/Create/Shader/My Unlit URP Shader")]
    static void UnlitURPShader()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);//返回点击的路径
        string templatepath = AssetDatabase.GUIDToAssetPath("bfb6109b5b2d4221971f754285c9baef");//文件自己的GUID
        string newPath = string.Format("{0}/My New Unlit URP Shader.shader", path);

        AssetDatabase.CopyAsset(templatepath, newPath);
        AssetDatabase.ImportAsset(newPath);
    }
    

}