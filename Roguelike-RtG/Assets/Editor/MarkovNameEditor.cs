using UnityEngine;
using UnityEditor;

[System.Obsolete]
[CustomEditor(typeof(MarkovChainNames))]
public class MarkovNameEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        GUILayout.Space(10);

        MarkovChainNames markov = (MarkovChainNames)target;

        if (GUILayout.Button("Generate Models"))
        {
            // markov.LoadModels();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("Get Name"))
        {
            //var s = markov.getn
        }
        if (GUILayout.Button("Get Names"))
        {
            //markov.PrintNames();
        }
    }
}
