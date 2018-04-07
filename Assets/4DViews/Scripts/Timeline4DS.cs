using UnityEngine;
using System.Collections;

//------------------------------
// FDV extra tool which adds a slider 
// allowing to go through the sequence timeline
// Also displays the current frame and file index
//------------------------------

namespace unity4dv
{

    public class Timeline4DS : MonoBehaviour
    {

        Plugin4DS fdv;
        bool fullRange = false;
        int newFrameId;

        void Awake()
        {
            fdv = this.GetComponent<Plugin4DS>();
            //When this component is active, the playback is stopped to be controlled
            //by the slider
            if (this.isActiveAndEnabled) fdv.AutoPlay = false;
        }

        void Start()
        {
            fdv.Play(false);
            newFrameId = fdv.FirstActiveFrame;
        }


        void Update()
        {
            if (newFrameId != fdv.CurrentFrame) //Need to be fixed in plugin 
                fdv.GotoFrame(newFrameId);
        }

        void OnGUI()
        {

            bool newfullRange = GUI.Toggle(new Rect(25, Screen.height - 65, 200, 20), fullRange, "Full sequence range");

            if (!newfullRange && fullRange)
            {
                int lastActiveFrame = fdv.LastActiveFrame > 0 ? fdv.LastActiveFrame : fdv.ActiveNbOfFrames - 1;
                if (newFrameId < fdv.FirstActiveFrame)
                    newFrameId = fdv.FirstActiveFrame;
                else if (newFrameId > lastActiveFrame)
                    newFrameId = lastActiveFrame;
            }

            fullRange = newfullRange;

            {
                int firstFrame, lastFrame;
                int sliderMargin;

                if (fullRange)
                {
                    firstFrame = 0;
                    lastFrame = fdv.SequenceNbOfFrames - 1;
                    sliderMargin = 25;
                }
                else
                {
                    firstFrame = fdv.FirstActiveFrame;
                    lastFrame = fdv.LastActiveFrame;
                    sliderMargin = 75;
                }
                string message = fdv.CurrentFrame.ToString() ;
                GUI.Label(new Rect(10, 10, 200, 20), message);

                newFrameId = (int)GUI.HorizontalSlider(new Rect(sliderMargin, Screen.height - 35, Screen.width - (sliderMargin * 2), 20), newFrameId, (float)firstFrame, (float)lastFrame);
            }
        }

    }

}