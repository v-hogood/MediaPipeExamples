using Android.Content;
using Android.Graphics;
using Android.Util;
using Exception = Java.Lang.Exception;
using Uri = Android.Net.Uri;

namespace ImageGeneration;

public static class ImageUtils
{
    public static Bitmap DecodeBitmapFromUri(Context context, Uri uri)
    {
        try
        {
            var parcelFileDescriptor =
                context.ContentResolver.OpenFileDescriptor(uri, "r");
            var fileDescriptor =
                parcelFileDescriptor?.FileDescriptor;
            var image =
                BitmapFactory.DecodeFileDescriptor(fileDescriptor);
            parcelFileDescriptor?.Close();
            return image;
        }
        catch (Exception e)
        {
            Log.Error("ImageUtils", "Image decoding failed: " + e.Message);
            return null;
        }
    }
}
