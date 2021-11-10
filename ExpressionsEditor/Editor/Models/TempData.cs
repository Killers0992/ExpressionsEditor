namespace ExpressionsEditor.Models
{
    using UnityEngine;

    public class TempData
    {
        public AnimationClip SelectedAnimationClip { get; set; }
        public AudioClip SelectedAudioClip { get; set; }
        public GameObject SelectedGameObject { get; set; }
        public GameObject[] MultipleGameObjects { get; set; } = new GameObject[0];
        public bool isEnabledByDefault { get; set; }
        public bool includeAllChildren { get; set; }
        public string ParameterName { get; set; }
    }
}