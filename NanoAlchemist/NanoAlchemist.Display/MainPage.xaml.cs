using NanoAlchemist.Display.Files;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace NanoAlchemist.Display
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private CreationalWorkstationFile _slicedFile = new CreationalWorkstationFile();
        private AppServiceConnection _connection;
        private bool _debug = true;

        public MainPage()
        {
            this.InitializeComponent();
            Debug.WriteLine(Windows.ApplicationModel.Package.Current.Id.FamilyName);
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ConnectToMovementService();
        }

        private async void ConnectToMovementService()
        {
            _connection = new AppServiceConnection
            {
                AppServiceName = "MovementService",
                PackageFamilyName = "MovementService-uwp_53fac5cen2094"
            };            

            AppServiceConnectionStatus status = await _connection.OpenAsync();
            if (status == AppServiceConnectionStatus.Success)
            {
                _connection.RequestReceived += OnConnectionReceived;
                _connection.ServiceClosed += OnServiceClosed;

                LogMessage("Connected");
            }
            else
            {
                LogMessage("Not Connected!!");
            }
        }

        private void OnConnectionReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            var deferral = args.GetDeferral();
            try
            {
                var message = args.Request.Message;

                if (!message.ContainsKey("method"))
                    return;

                var method = message["method"].ToString();

                switch (method)
                {
                    case "load_file":
                        LoadFile(message["file"].ToString());
                        return;

                    case "start_print":
                        StartPrint();
                        return;
                }
            }
            catch (Exception ex)
            {
                LogMessage(ex.Message);
            }
            finally
            {
                deferral?.Complete();
            }
        }

        private async void StartPrint()
        {
            await MoveToHome();

            for (int i = 1; i <= _slicedFile.LayerCount; i++)
            {
                 var imageStream = await _slicedFile.GetLayerStream(i);

                if (imageStream == null)
                    return;

                await Camadas.Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
                {
                    var image = new BitmapImage();
                    image.SetSource(imageStream.AsRandomAccessStream());
                    Camadas.Source = image;
                    Camadas.Height = image.PixelHeight;
                    Camadas.Width = image.PixelWidth;
                });

                Task.Delay(200).Wait();

                await Camadas.Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
                {
                    Camadas.Source = null;
                });

                await Move();
            }
        }

        private async Task<bool> Move()
        {
            var message = new ValueSet
            {
                ["length"] = 10,
                ["direction"] = -1,
                ["speed"] = 10
            };

            var response = await _connection.SendMessageAsync(message);

            message = new ValueSet
            {
                ["length"] = 9.5,
                ["direction"] = 1,
                ["speed"] = 10
            };

            response = await _connection.SendMessageAsync(message);
            return true;
        }

        private async Task<bool> MoveToHome()
        {
            var message = new ValueSet
            {
                ["method"] = "move_to_home",
                ["direction"] = 1,
                ["speed"] = 10
            };

            var response = await _connection.SendMessageAsync(message);

            return response.Status == AppServiceResponseStatus.Success;
        }

        private void LoadFile(string compressedFile)
        {
            try
            {
                var file = Convert.FromBase64String(compressedFile.Replace(" ", "+"));
                var stream = new MemoryStream(file);
                _slicedFile.LoadSliceFile(stream);

                LogMessage("File Loaded");
            }
            catch (Exception ex)
            {
                LogMessage(ex.Message);
            }
        }

        private void OnServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            LogMessage("Connection Closed");
        }

        private async void LogMessage(string message)
        {
            if (_debug)
                await Descricao.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Descricao.Text = message;
                });

            Debug.WriteLine(message);
        }
    }
}
