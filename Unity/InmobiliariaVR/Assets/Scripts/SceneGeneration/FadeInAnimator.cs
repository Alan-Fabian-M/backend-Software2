using UnityEngine;
using System.Collections;

/// <summary>
/// Handles fade-in animations for objects during progressive scene generation.
/// Makes objects progressively visible with smooth alpha transitions.
/// </summary>
public class FadeInAnimator : MonoBehaviour
{
    /// <summary>
    /// Fades in an object (and its children) over the specified duration.
    /// </summary>
    public static IEnumerator FadeIn(GameObject obj, float duration)
    {
        float elapsedTime = 0f;

        // Get all renderers in the object and its children
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
            yield break;

        // Store original colors and set initial alpha to 0
        Color[] originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            originalColors[i] = renderers[i].material.color;
            Color startColor = originalColors[i];
            startColor.a = 0;
            renderers[i].material.color = startColor;
        }

        // Fade in
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsedTime / duration);

            for (int i = 0; i < renderers.Length; i++)
            {
                Color newColor = originalColors[i];
                newColor.a = alpha;
                renderers[i].material.color = newColor;
            }

            yield return null;
        }

        // Ensure final color is set correctly
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].material.color = originalColors[i];
        }
    }

    /// <summary>
    /// Scales an object from zero to full size during instantiation.
    /// Creates a "pop" effect for visual feedback.
    /// </summary>
    public static IEnumerator ScaleIn(GameObject obj, float duration)
    {
        float elapsedTime = 0f;
        Vector3 targetScale = obj.transform.localScale;
        obj.transform.localScale = Vector3.zero;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float scale = Mathf.Clamp01(elapsedTime / duration);
            obj.transform.localScale = targetScale * scale;
            yield return null;
        }

        obj.transform.localScale = targetScale;
    }

    /// <summary>
    /// Combined fade and scale animation for smooth object appearance.
    /// </summary>
    public static IEnumerator FadeAndScaleIn(GameObject obj, float duration)
    {
        float elapsedTime = 0f;
        Vector3 targetScale = obj.transform.localScale;
        obj.transform.localScale = Vector3.zero;

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        Color[] originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            originalColors[i] = renderers[i].material.color;
            Color startColor = originalColors[i];
            startColor.a = 0;
            renderers[i].material.color = startColor;
        }

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);

            // Ease-out curve for more natural feel
            t = 1f - Mathf.Pow(1f - t, 3f);

            // Update scale
            obj.transform.localScale = targetScale * t;

            // Update alpha
            for (int i = 0; i < renderers.Length; i++)
            {
                Color newColor = originalColors[i];
                newColor.a = t;
                renderers[i].material.color = newColor;
            }

            yield return null;
        }

        obj.transform.localScale = targetScale;
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].material.color = originalColors[i];
        }
    }

    /// <summary>
    /// Animates object position from start to target over duration.
    /// Useful for objects sliding into place during generation.
    /// </summary>
    public static IEnumerator MoveIntoPlace(GameObject obj, Vector3 targetPosition, float duration)
    {
        float elapsedTime = 0f;
        Vector3 startPosition = obj.transform.position;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);

            // Ease-out cubic for smooth deceleration
            t = 1f - Mathf.Pow(1f - t, 3f);

            obj.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        obj.transform.position = targetPosition;
    }

    /// <summary>
    /// Rotates object into place during generation.
    /// </summary>
    public static IEnumerator RotateIntoPlace(GameObject obj, Quaternion targetRotation, float duration)
    {
        float elapsedTime = 0f;
        Quaternion startRotation = obj.transform.rotation;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);

            obj.transform.rotation = Quaternion.Lerp(startRotation, targetRotation, t);
            yield return null;
        }

        obj.transform.rotation = targetRotation;
    }
}
