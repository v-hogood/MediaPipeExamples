using Android.Content;
using Android.Media;
using Java.IO;
using Java.Lang;
using Java.Nio;
using MediaPipe.Tasks.Components.Containers;
using Uri = Android.Net.Uri;

namespace AudioClassification;

public static class Extensions
{
    public static AudioData CreateAudioData(this Uri uri, Context context)
    {
        var inputStream = context.ContentResolver.OpenInputStream(uri);
        var dataInputStream = new DataInputStream(inputStream);
        var targetArray = new byte[dataInputStream.Available()];
        dataInputStream.Read(targetArray);
        var audioShortArrayData = targetArray.ToShortArray();

        // get audio's duration
        var mmr = new MediaMetadataRetriever();
        mmr.SetDataSource(context, uri);
        var durationStr =
            mmr.ExtractMetadata(MetadataKey.Duration);
        var audioDuration = Integer.ParseInt(durationStr);

        // calculate the sample rate
        var expectedSampleRate =
            audioShortArrayData.Length / (audioDuration / 1000F / AudioClassifierHelper.ExpectedInputLength);

        // create audio data
        var audioData = AudioData.Create(
            AudioData.AudioDataFormat.InvokeBuilder().SetNumOfChannels(
                (int)ChannelIn.Default
            ).SetSampleRate(expectedSampleRate).Build(), audioShortArrayData.Length
        );
        audioData.Load(audioShortArrayData);
        return audioData;
    }

    public static short[] ToShortArray(this byte[] byteArray)
    {
        var result = new short[byteArray.Length / 2];
        ByteBuffer.Wrap(byteArray).Order(ByteOrder.LittleEndian).AsShortBuffer()
            .Get(result);
        return result;
    }
}
