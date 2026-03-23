using System.Drawing.Imaging;
using System.IO;
using FlaUI.Core.Capturing;

namespace CashChangerSimulator.UI.Tests.Helpers;

public static class VisualVerificationHelper
{
    /// <summary>
    /// FlaUI の CaptureImage を PNG ストリームに変換し、Verify フレームワークへ渡せるようにします。
    /// </summary>
    public static Stream ToPngStream(this CaptureImage capture)
    {
        var stream = new MemoryStream();
        capture.Bitmap.Save(stream, ImageFormat.Png);
        stream.Position = 0;
        return stream;
    }
}
