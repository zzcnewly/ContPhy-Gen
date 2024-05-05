using UnityEngine;
using System.IO;

namespace PRUtils
{
    class PRUtils {
        public void ConvertPNGtoJPEG(string inputFilePath, string outputFilePath, int quality) {
            // Load the PNG image
            byte[] pngBytes = File.ReadAllBytes(inputFilePath);
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(pngBytes);

            // Encode the loaded texture as a JPEG
            byte[] jpegBytes = ImageConversion.EncodeToJPG(texture, quality);

            // Save the JPEG to the output file path
            File.WriteAllBytes(outputFilePath, jpegBytes);
        }   
    }
    
}
