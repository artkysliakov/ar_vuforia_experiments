using UnityEngine;
using System.Collections;
using UnityEditor;

namespace unity4dv
{

    [CustomEditor(typeof(Plugin4DS))]
    public class Plugin4DSEditor : Editor
    {
        bool showPath = false;

        public override void OnInspectorGUI()
        {
            Plugin4DS myTarget = (Plugin4DS)target;

            Undo.RecordObject(myTarget, "Inspector");

            //GUILayout.Space(7);
            //myTarget._sourceType = (SOURCE_TYPE)GUILayout.Toolbar((int)myTarget._sourceType, new string[] { "Files", "Network" });
            GUILayout.Space(10);

            switch (myTarget._sourceType)
            {
                case SOURCE_TYPE.Files:
                    BuildFilesInspector(myTarget);
                    break;
                case SOURCE_TYPE.Network:
                    BuildNetworkInspector(myTarget);
                    break;
            }
            if (GUI.changed)
                EditorUtility.SetDirty(target);
        }



        private void BuildFilesInspector(Plugin4DS myTarget)
        {

            myTarget.AutoPlay = EditorGUILayout.Toggle("Auto Play", myTarget.AutoPlay);

            bool val = EditorGUILayout.Toggle("Compute Normals", myTarget.ComputeNormals);
            if (val != myTarget.ComputeNormals)
            {
                myTarget.ComputeNormals = val;
                myTarget.Preview();
            }

            Rect rect = EditorGUILayout.BeginVertical();
            myTarget.SequenceName = EditorGUILayout.TextField("Sequence Name", myTarget.SequenceName);
            EditorGUILayout.EndVertical();

            showPath = EditorGUILayout.Foldout(showPath, "Data Path");
            if (showPath)
            {
                myTarget._dataInStreamingAssets = EditorGUILayout.Toggle("In Streaming Assets", myTarget._dataInStreamingAssets);
                if (!myTarget._dataInStreamingAssets)
                {
                    myTarget.SequenceDataPath = EditorGUILayout.TextField("Data Path", myTarget.SequenceDataPath);
                }
            }

            GUIContent previewframe = new GUIContent("Preview Frame");
            Color color = GUI.color;
            if ((myTarget.LastActiveFrame != -1) && (myTarget.PreviewFrame < (int)myTarget.FirstActiveFrame || myTarget.PreviewFrame > (int)myTarget.LastActiveFrame))
                GUI.color = new Color(1, 0.6f, 0.6f);

            int frameVal = EditorGUILayout.IntSlider(previewframe, myTarget.PreviewFrame, 0, myTarget.SequenceNbOfFrames - 1);
            if (frameVal != myTarget.PreviewFrame)
            {
                myTarget.PreviewFrame = (int)frameVal;
                myTarget.Preview();
                myTarget.last_preview_time = System.DateTime.Now;
            }
            else
                myTarget.ConvertPreviewTexture();
            GUI.color = color;

            GUIContent activerange = new GUIContent("Active Range");
            float rangeMax = myTarget.LastActiveFrame == -1 ? myTarget.SequenceNbOfFrames - 1 : myTarget.LastActiveFrame;
            if (myTarget.LastActiveFrame == -1)
                GUI.color = new Color(0.5f, 0.7f, 2.0f);
            float firstActiveFrame = myTarget.FirstActiveFrame;
            EditorGUILayout.MinMaxSlider(activerange, ref firstActiveFrame, ref rangeMax, 0.0f, myTarget.SequenceNbOfFrames - 1);
            myTarget.FirstActiveFrame = (int)firstActiveFrame;
            if (rangeMax == myTarget.SequenceNbOfFrames - 1 && myTarget.FirstActiveFrame == 0)
                myTarget.LastActiveFrame = -1;
            else
                myTarget.LastActiveFrame = (int)rangeMax;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.Space();

            if (myTarget.LastActiveFrame == -1)
            {
                EditorGUILayout.Space();
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Full Range", GUILayout.Width(80));
                GUI.color = color;
                EditorGUILayout.Space();
                myTarget.LastActiveFrame = -1;
            }
            else
            {
                myTarget.FirstActiveFrame = EditorGUILayout.IntField((int)myTarget.FirstActiveFrame, GUILayout.Width(50));
                EditorGUILayout.Space();
                myTarget.LastActiveFrame = EditorGUILayout.IntField((int)myTarget.LastActiveFrame, GUILayout.Width(50));
            }


            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            myTarget.OutOfRangeMode = (OUT_RANGE_MODE)EditorGUILayout.EnumPopup("Out of Range Mode", myTarget.OutOfRangeMode);

            myTarget._debugInfo = EditorGUILayout.Toggle("Debug Info", myTarget._debugInfo);



            Event evt = Event.current;
            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (rect.Contains(evt.mousePosition))
                        DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                    else
                    {
                        //EditorGUILayout.EndVertical();
                        return;
                    }

                    if (evt.type == EventType.DragPerform)
                    {
                        foreach (string path in DragAndDrop.paths)
                        {
                            string seqName = path.Substring(path.LastIndexOf("/") + 1);
                            string dataPath = path.Substring(0, path.LastIndexOf("/") + 1);

                            if (dataPath.Contains("StreamingAssets"))
                            {
                                myTarget._dataInStreamingAssets = true;
                                dataPath = dataPath.Substring(dataPath.LastIndexOf("StreamingAssets") + 16);
                                myTarget.SequenceDataPath = dataPath;
                            }
                            else
                            {
                                if (dataPath.Contains("Assets"))
                                {
                                    string message = "The sequence should be in \"Streaming Assets\" for a good application deployment";
                                    EditorUtility.DisplayDialog("Warning", message, "Close");
                                }
                                myTarget._dataInStreamingAssets = false;
                                myTarget.SequenceDataPath = dataPath;
                            }
                            myTarget.SequenceName = seqName;

                            EditorUtility.SetDirty(target);

                            myTarget.Preview();
                        }
                    }
                    break;
            }
        }



        private void BuildNetworkInspector(Plugin4DS myTarget)
        {

            myTarget.AutoPlay = EditorGUILayout.Toggle("Auto Play", myTarget.AutoPlay);
            EditorGUILayout.BeginVertical();
            myTarget.ServerAddress = EditorGUILayout.TextField("Server address", myTarget.ServerAddress);
            myTarget.ServerPort = EditorGUILayout.IntField("Server port", myTarget.ServerPort);

            EditorGUILayout.EndVertical();
            myTarget._debugInfo = EditorGUILayout.Toggle("Debug Info", myTarget._debugInfo);
        }
    }

}