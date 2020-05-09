using UnityEditor;
using UnityEngine;

namespace LeoLuz.PlugAndPlayJoystick
{
    [CustomEditor(typeof(AnalogicKnob))]
    class AnalogicKnobEditor : Editor
    {
        void OnSceneGUI()
        {
            if (Application.isPlaying)
                return;

            AnalogicKnob analogicKnob = target as AnalogicKnob;
            if (analogicKnob == null)
                return;

            Canvas canvas = analogicKnob.GetComponentInParent<Canvas>();
            RectTransform CanvasRect = canvas.GetComponent<RectTransform>();
            RectTransform KnobRect = analogicKnob.GetComponent<RectTransform>();

            analogicKnob.AnchoredAreaBounds = new Bounds(new Vector2(analogicKnob.NormalizedAnchoredAreaBounds.center.x * CanvasRect.sizeDelta.x, analogicKnob.NormalizedAnchoredAreaBounds.center.y * CanvasRect.sizeDelta.y)
                , new Vector2(analogicKnob.NormalizedAnchoredAreaBounds.size.x * CanvasRect.sizeDelta.x, analogicKnob.NormalizedAnchoredAreaBounds.size.y * CanvasRect.sizeDelta.y));

            EditorGUI.BeginChangeCheck();


            if (analogicKnob.AnchoredAreaBounds.size == Vector3.zero)
            {
                analogicKnob.AnchoredAreaBounds.size = new Vector3(200f, 200f, 0f);
                analogicKnob.AnchoredAreaBounds.center = new Vector3(50f, 80f, 0f);
            }

            Handles.color = Color.magenta;
            if (analogicKnob.clampMode == AnalogicKnob.ClampMode.Box)
            {
                //BOX CLAMPER

                Bounds convertedBounds = new Bounds(analogicKnob.AnchoredAreaBounds.center * CanvasRect.lossyScale.x, analogicKnob.AnchoredAreaBounds.size * CanvasRect.lossyScale.x); //Converte
                TriggerHandler.Box(ref convertedBounds, KnobRect.transform.position, "Knob Area");
                analogicKnob.AnchoredAreaBounds = new Bounds(convertedBounds.center / CanvasRect.lossyScale.x, convertedBounds.size / CanvasRect.lossyScale.x);
            }
            else
            {
                //CIRCLE CLAMPER
                Vector3 CircleCenter = analogicKnob.AnchoredAreaBounds.center * CanvasRect.lossyScale.x;
                float CircleRadius = analogicKnob.AnchoredAreaBounds.extents.x * CanvasRect.lossyScale.x;
                TriggerHandler.Circle(ref CircleCenter, ref CircleRadius, KnobRect.transform.position, "Knob Area");
                analogicKnob.AnchoredAreaBounds.center = CircleCenter / CanvasRect.lossyScale.x;
                analogicKnob.AnchoredAreaBounds.extents = new Vector3(CircleRadius / CanvasRect.lossyScale.x, CircleRadius / CanvasRect.lossyScale.x);
            }
            analogicKnob.NormalizedAnchoredAreaBounds = new Bounds(new Vector2(analogicKnob.AnchoredAreaBounds.center.x / CanvasRect.sizeDelta.x, analogicKnob.AnchoredAreaBounds.center.y / CanvasRect.sizeDelta.y)
                , new Vector2(analogicKnob.AnchoredAreaBounds.size.x / CanvasRect.sizeDelta.x, analogicKnob.AnchoredAreaBounds.size.y / CanvasRect.sizeDelta.y));


            //MAX DISTANCE
            Handles.color = Color.cyan;
            var distance = analogicKnob.TurnLimit * CanvasRect.lossyScale.x;
            var pos = Vector3.zero;
            TriggerHandler.Circle(ref pos, ref distance, KnobRect.transform.position, "Turn Limit");
            analogicKnob.TurnLimit = distance / CanvasRect.lossyScale.x;




            Undo.RecordObject(target, "Knob Handle");



        }


    }
}