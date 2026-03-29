using UnityEngine;
using System;
using System.IO;

public static class SaveWav
{
    private const int HEADER_SIZE = 44;

    public static byte[] GetWav(float[] samples, int channels, int hz)
    {
        using (MemoryStream stream = new MemoryStream())
        {
           // Scriviamo l'header WAV (44 byte) [cite: 121]
            byte[] header = CreateHeader(samples.Length, channels, hz);
            stream.Write(header, 0, HEADER_SIZE);

            // Convertiamo i campioni float in Int16 (PCM) [cite: 97, 130]
            short[] intData = new short[samples.Length];
            byte[] bytesData = new byte[samples.Length * 2];

            for (int i = 0; i < samples.Length; i++)
            {
                intData[i] = (short)(samples[i] * 32767);
                byte[] byteArr = BitConverter.GetBytes(intData[i]);
                byteArr.CopyTo(bytesData, i * 2);
            }

            stream.Write(bytesData, 0, bytesData.Length);
            return stream.ToArray();
        }
    }

    private static byte[] CreateHeader(int sampleCount, int channels, int hz)
    {
        byte[] header = new byte[HEADER_SIZE];
        int byteRate = hz * channels * 2;

        System.Buffer.BlockCopy(System.Text.Encoding.UTF8.GetBytes("RIFF"), 0, header, 0, 4);
        System.Buffer.BlockCopy(BitConverter.GetBytes(sampleCount * 2 + 36), 0, header, 4, 4);
        System.Buffer.BlockCopy(System.Text.Encoding.UTF8.GetBytes("WAVE"), 0, header, 8, 4);
        System.Buffer.BlockCopy(System.Text.Encoding.UTF8.GetBytes("fmt "), 0, header, 12, 4);
        System.Buffer.BlockCopy(BitConverter.GetBytes(16), 0, header, 16, 4);
        System.Buffer.BlockCopy(BitConverter.GetBytes((short)1), 0, header, 20, 2); // PCM
        System.Buffer.BlockCopy(BitConverter.GetBytes((short)channels), 0, header, 22, 2);
        System.Buffer.BlockCopy(BitConverter.GetBytes(hz), 0, header, 24, 4);
        System.Buffer.BlockCopy(BitConverter.GetBytes(byteRate), 0, header, 28, 4);
        System.Buffer.BlockCopy(BitConverter.GetBytes((short)(channels * 2)), 0, header, 32, 2);
        System.Buffer.BlockCopy(BitConverter.GetBytes((short)16), 0, header, 34, 2);
        System.Buffer.BlockCopy(System.Text.Encoding.UTF8.GetBytes("data"), 0, header, 36, 4);
        System.Buffer.BlockCopy(BitConverter.GetBytes(sampleCount * 2), 0, header, 40, 4);

        return header;
    }
}