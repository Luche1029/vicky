using System.IO;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEngine;

namespace SimpleOfflineSTT
{
    public class ModelTools
    {
        [MenuItem("Tools/SimpleOfflineSTT/Quantize Model to fp16...")]
        public static void QuantizeFP16WithUI()
        {
            QuantizeWithUI(QuantizationType.Float16, "fp16");
        }

        [MenuItem("Tools/SimpleOfflineSTT/Quantize Model to uint8...")]
        public static void QuantizeUINT8WithUI()
        {
            QuantizeWithUI(QuantizationType.Uint8, "uint8");
        }

        static void QuantizeWithUI(QuantizationType quantizationType, string filenameSuffix)
        {
            // 1. Select the file
            string inputPath = EditorUtility.OpenFilePanel("Select Model", Application.dataPath, "onnx,sentis");
            if (string.IsNullOrEmpty(inputPath)) return;

            // 2. Convert absolute path to Project Relative path (Required for AssetDatabase)
            string relativePath = "Assets" + inputPath.Replace(Application.dataPath, "");

            // 3. Load as a ModelAsset first
            ModelAsset modelAsset = AssetDatabase.LoadAssetAtPath<ModelAsset>(relativePath);

            if (modelAsset == null)
            {
                EditorUtility.DisplayDialog("Error", "Could not load the file as a ModelAsset. Make sure it is inside your Assets folder and Unity has finished importing it.", "OK");
                return;
            }

            // 4. Select Output
            string defaultName = Path.GetFileNameWithoutExtension(inputPath) + "_" + filenameSuffix;
            string outputPath = EditorUtility.SaveFilePanel("Save Quantized Model", Path.GetDirectoryName(inputPath), defaultName, "sentis");
            if (string.IsNullOrEmpty(outputPath)) return;

            try
            {
                // 5. Convert ModelAsset to Model object
                Model model = ModelLoader.Load(modelAsset);

                // 6. Quantize
                ModelQuantizer.QuantizeWeights(quantizationType, ref model);

                // 7. Save
                ModelWriter.Save(outputPath, model);

                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Success", "Model quantized successfully!", "OK");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Quantization failed: {e}");
                EditorUtility.DisplayDialog("Error", e.Message, "OK");
            }
        }
    }
}