using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SimpleOfflineSTT
{
    public class WhisperDecoder : MonoBehaviour
    {
        private static Dictionary<char, byte> _decodingMap;

        void Awake()
        {
            // Ensure map is built when game starts
            if (_decodingMap == null)
            {
                BuildDecodingMap();
            }
        }

        /// <summary>
        /// Converts the Whisper output (GPT-2 Byte-Level text) into readable UTF-8 text.
        /// </summary>
        public static string DecodeText(string gpt2Text)
        {
            if (_decodingMap == null)
            {
                BuildDecodingMap();
            }

            List<byte> bytes = new List<byte>();

            foreach (char c in gpt2Text)
            {
                if (_decodingMap.TryGetValue(c, out byte b))
                {
                    bytes.Add(b);
                }
                else
                {
                    // Fallback: If it's a regular char not involved in the encoding, use it as-is
                    // (Though usually, all chars in vocab.json adhere to the map)
                    bytes.Add((byte)c);
                }
            }

            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        private static void BuildDecodingMap()
        {
            _decodingMap = new Dictionary<char, byte>();

            // 1. Define the ranges of "Good" bytes (Printable ASCII + Latin-1)
            // These map 1-to-1 (Byte 33 is Char 33)
            List<int> bs = new List<int>();
            bs.AddRange(Enumerable.Range('!', '~' - '!' + 1)); // 33-126
            bs.AddRange(Enumerable.Range('¡', '¬' - '¡' + 1)); // 161-172
            bs.AddRange(Enumerable.Range('®', 'ÿ' - '®' + 1)); // 174-255

            // 2. Create the mapping
            // 'cs' will hold the unicode chars we use to represent the bytes
            List<char> cs = bs.Select(x => (char)x).ToList();

            // 3. Handle the "Bad" bytes (Control characters, spaces, etc.)
            // These map to 256+ (e.g., Byte 0 might map to Char 256)
            int n = 0;
            for (int b = 0; b < 256; b++)
            {
                if (!bs.Contains(b))
                {
                    bs.Add(b);
                    cs.Add((char)(256 + n));
                    n++;
                }
            }

            // 4. Populate the static Dictionary (Char -> Byte)
            for (int i = 0; i < bs.Count; i++)
            {
                _decodingMap[cs[i]] = (byte)bs[i];
            }
        }
    }
}
