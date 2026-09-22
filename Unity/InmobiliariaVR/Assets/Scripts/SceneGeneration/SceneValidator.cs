using UnityEngine;
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

/// <summary>
/// Validates the JSON structure received from the Python YOLO server
/// to ensure all required fields are present and properly formatted.
/// Provides detailed error reporting for invalid scenes.
/// </summary>
public class SceneValidator : MonoBehaviour
{
    [System.Serializable]
    public class ValidationResult
    {
        public bool isValid;
        public List<string> errors = new List<string>();
        public List<string> warnings = new List<string>();
        public DateTime validatedAt;
    }

    public static ValidationResult ValidateSceneJSON(string jsonString)
    {
        var result = new ValidationResult { isValid = true, validatedAt = DateTime.Now };

        if (string.IsNullOrWhiteSpace(jsonString))
        {
            result.isValid = false;
            result.errors.Add("JSON string is null or empty");
            return result;
        }

        try
        {
            var json = JObject.Parse(jsonString);
            ValidateRootStructure(json, result);

            if (result.isValid && json.ContainsKey("scene_elements"))
            {
                ValidateSceneElements(json["scene_elements"] as JArray, result);
            }
        }
        catch (Newtonsoft.Json.JsonException ex)
        {
            result.isValid = false;
            result.errors.Add($"Invalid JSON format: {ex.Message}");
        }
        catch (Exception ex)
        {
            result.isValid = false;
            result.errors.Add($"Unexpected validation error: {ex.Message}");
        }

        return result;
    }

    private static void ValidateRootStructure(JObject json, ValidationResult result)
    {
        // Check for required top-level fields
        if (!json.ContainsKey("metadata"))
            result.errors.Add("Missing 'metadata' field");

        if (!json.ContainsKey("room_info"))
            result.errors.Add("Missing 'room_info' field");

        if (!json.ContainsKey("scene_elements"))
            result.errors.Add("Missing 'scene_elements' field");

        // Validate metadata
        if (json.ContainsKey("metadata"))
        {
            var metadata = json["metadata"] as JObject;
            if (metadata == null)
            {
                result.errors.Add("'metadata' must be an object");
            }
            else
            {
                if (!metadata.ContainsKey("name"))
                    result.warnings.Add("'metadata.name' is missing");
            }
        }

        // Validate room_info
        if (json.ContainsKey("room_info"))
        {
            var roomInfo = json["room_info"] as JObject;
            if (roomInfo == null)
            {
                result.errors.Add("'room_info' must be an object");
                result.isValid = false;
                return;
            }

            if (!roomInfo.ContainsKey("dimensions"))
            {
                result.errors.Add("Missing 'room_info.dimensions' field");
                result.isValid = false;
                return;
            }

            var dims = roomInfo["dimensions"] as JObject;
            if (dims == null)
            {
                result.errors.Add("'room_info.dimensions' must be an object");
                result.isValid = false;
                return;
            }

            ValidateDimensions(dims, "room_info.dimensions", result);
        }
    }

    private static void ValidateSceneElements(JArray elements, ValidationResult result)
    {
        if (elements == null || elements.Count == 0)
        {
            result.warnings.Add("No scene elements found");
            return;
        }

        for (int i = 0; i < elements.Count; i++)
        {
            var element = elements[i] as JObject;
            if (element == null)
            {
                result.errors.Add($"scene_elements[{i}] is not a valid object");
                result.isValid = false;
                continue;
            }

            ValidateElement(element, i, result);
        }
    }

    private static void ValidateElement(JObject element, int index, ValidationResult result)
    {
        // Check required fields
        string elementId = element.ContainsKey("id") ? element["id"].ToString() : $"Element[{index}]";

        if (!element.ContainsKey("id"))
            result.errors.Add($"Element {index}: Missing 'id' field");

        if (!element.ContainsKey("type"))
            result.errors.Add($"Element {elementId}: Missing 'type' field");

        if (!element.ContainsKey("position"))
            result.errors.Add($"Element {elementId}: Missing 'position' field");

        if (!element.ContainsKey("rotation"))
            result.errors.Add($"Element {elementId}: Missing 'rotation' field");

        if (!element.ContainsKey("scale"))
            result.errors.Add($"Element {elementId}: Missing 'scale' field");

        // Validate transform fields
        if (element.ContainsKey("position"))
            ValidateDimensions(element["position"] as JObject, $"{elementId}.position", result);

        if (element.ContainsKey("rotation"))
            ValidateDimensions(element["rotation"] as JObject, $"{elementId}.rotation", result);

        if (element.ContainsKey("scale"))
            ValidateDimensions(element["scale"] as JObject, $"{elementId}.scale", result);

        // Validate element type
        if (element.ContainsKey("type"))
        {
            string type = element["type"].ToString();
            ValidateElementType(type, elementId, result);
        }
    }

    private static void ValidateDimensions(JObject dims, string fieldName, ValidationResult result)
    {
        if (dims == null)
        {
            result.errors.Add($"{fieldName} must be an object with x, y, z coordinates");
            result.isValid = false;
            return;
        }

        bool hasX = dims.ContainsKey("x") || dims.ContainsKey("X");
        bool hasY = dims.ContainsKey("y") || dims.ContainsKey("Y");
        bool hasZ = dims.ContainsKey("z") || dims.ContainsKey("Z");

        if (!hasX || !hasY || !hasZ)
        {
            result.errors.Add($"{fieldName} must have x, y, and z coordinates");
            result.isValid = false;
        }

        // Validate numerical values
        try
        {
            var xVal = dims.ContainsKey("x") ? dims["x"].Value<float>() : dims["X"].Value<float>();
            var yVal = dims.ContainsKey("y") ? dims["y"].Value<float>() : dims["Y"].Value<float>();
            var zVal = dims.ContainsKey("z") ? dims["z"].Value<float>() : dims["Z"].Value<float>();
        }
        catch
        {
            result.errors.Add($"{fieldName} coordinates must be valid numbers");
            result.isValid = false;
        }
    }

    private static void ValidateElementType(string type, string elementId, ValidationResult result)
    {
        string[] validTypes = { "muro", "piso", "puerta", "ventana", "sofa", "mesa", "cama",
                               "silla", "estanteria", "armario", "tv", "bano_completo",
                               "escritorio", "cajon", "cojin", "refrigerador", "horno",
                               "microondas", "estufa", "bañera", "inodoro", "lavabo" };

        bool isValid = false;
        foreach (var validType in validTypes)
        {
            if (type.Equals(validType, StringComparison.OrdinalIgnoreCase))
            {
                isValid = true;
                break;
            }
        }

        if (!isValid)
            result.warnings.Add($"Element {elementId}: Unknown type '{type}'");
    }

    public static void LogValidationResult(ValidationResult result)
    {
        Debug.Log($"=== Scene Validation Report ===");
        Debug.Log($"Valid: {result.isValid}");
        Debug.Log($"Validated at: {result.validatedAt}");

        if (result.errors.Count > 0)
        {
            Debug.LogError($"Errors ({result.errors.Count}):");
            foreach (var error in result.errors)
            {
                Debug.LogError($"  - {error}");
            }
        }

        if (result.warnings.Count > 0)
        {
            Debug.LogWarning($"Warnings ({result.warnings.Count}):");
            foreach (var warning in result.warnings)
            {
                Debug.LogWarning($"  - {warning}");
            }
        }

        if (result.isValid && result.errors.Count == 0)
        {
            Debug.Log("✓ Scene JSON is valid and ready for generation");
        }
    }
}
