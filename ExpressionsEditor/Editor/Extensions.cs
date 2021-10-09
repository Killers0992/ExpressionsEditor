namespace ExpressionsEditor
{
    using UnityEditor.Animations;
    using UnityEngine;

    public static class Extensions
    {
        public static string GetPath(this Transform current)
        {
            if (current.parent == null)
                return "/" + current.name;
            return current.parent.GetPath() + "/" + current.name;
        }

        public static string GetPath(this Component component)
        {
            return component.transform.GetPath() + "/" + component.GetType().ToString();
        }

        public static void RemoveLayerIfExists(this AnimatorController animator, string name)
        {
            for (int x = 0; x < animator.layers.Length; x++)
            {
                if (animator.layers[x].name == name)
                {
                    animator.RemoveLayer(x);
                }
            }
        }
    }
}
