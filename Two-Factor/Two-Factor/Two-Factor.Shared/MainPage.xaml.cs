using AeroGear.OTP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using ZXing;

namespace Two_Factor
{
    /// <summary>
    /// Scan QRCode with the secret then genereate OTP
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private MediaCapture _mediaCapture;
        private Totp _totp;
        private Timer _timer;

        public MainPage()
        {
            InitializeComponent();
        }

        private async Task InitializeQrCode()
        {
            // Find all available webcams
            DeviceInformationCollection webcamList = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

            // Get the proper webcam (default one)
            DeviceInformation backWebcam = (from webcam in webcamList
                                            where webcam.IsEnabled
                                            select webcam).FirstOrDefault();

            _mediaCapture = new MediaCapture();
            await _mediaCapture.InitializeAsync(new MediaCaptureInitializationSettings
            {
                VideoDeviceId = backWebcam.Id,
                AudioDeviceId = "",
                StreamingCaptureMode = StreamingCaptureMode.Video,
                PhotoCaptureSource = PhotoCaptureSource.VideoPreview
            });

            startButton.Visibility = Visibility.Collapsed;
            capturePreview.Visibility = Visibility.Visible;
            capturePreview.Source = _mediaCapture;

            await _mediaCapture.StartPreviewAsync();
        }

        private async void StartScan(object sender, RoutedEventArgs e)
        {
            await InitializeQrCode();

            string secret = null;
            do
            {
                using (var stream = new InMemoryRandomAccessStream())
                {
                    var imgProp = new ImageEncodingProperties { Subtype = "BMP", Width = 600, Height = 800 };
                    await _mediaCapture.CapturePhotoToStreamAsync(imgProp, stream);

                    stream.Seek(0);

                    var writeableBitmap = new WriteableBitmap(600, 800);
                    await writeableBitmap.SetSourceAsync(stream);

                    var barcodeReader = new BarcodeReader();
                    var result = barcodeReader.Decode(writeableBitmap);

                    if (result != null)
                    {
                        secret = result.Text;
                    }
                }
            } while (secret == null);

            await _mediaCapture.StopPreviewAsync();
            CreateOTPCode(secret);
        }

        private void CreateOTPCode(string secret)
        {
            var param = ParseQueryString(secret);
            result.Visibility = Visibility.Visible;
            capturePreview.Visibility = Visibility.Collapsed;

            _totp = new Totp(param["secret"]);
            qrcode.Text = _totp.now();

            _timer = new Timer(tick, timeValid, 0, 300);
        }

        private async void tick(object state)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                timeValid.Value = timeValid.Value + 1;
                if (timeValid.Value >= 100)
                {
                    timeValid.Value = 0;
                    qrcode.Text = _totp.now();
                }
            });
        }

        internal IDictionary<string, string> ParseQueryString(string query)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();

            query = query.Substring(query.IndexOf('?') + 1);

            foreach (string valuePair in Regex.Split(query, "&"))
            {
                string[] pair = Regex.Split(valuePair, "=");
                result.Add(WebUtility.UrlDecode(pair[0]), WebUtility.UrlDecode(pair[1]));
            }

            return result;
        }
    }
}
