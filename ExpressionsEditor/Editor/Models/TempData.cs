namespace ExpressionsEditor.Models
{
    using UnityEngine;

    public class TempData
    {
        public AnimationClip SelectedAnimationClip { get; set; }
        public AudioClip SelectedAudioClip { get; set; }
        public GameObject SelectedGameObject { get; set; }
        public bool isEnabledByDefault { get; set; }
        public bool includeAllChildren { get; set; }
        public string ParameterName { get; set; }
    }
}