using System;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using System.Diagnostics;
using MovementService.DTOs;
using MovementService.Shields;
using MovementService.Motors;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace MovementService
{
    public sealed class StartupTask : IBackgroundTask
    {
        private static NanoDlp _nanoDlp;

        private BackgroundTaskDeferral _deferral;        
        private AppServiceConnection _appServiceConnection;

        //To control the HDMI Display on Raspberry PI
        private static AppServiceConnection _appServiceConnectionDisplay;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();
            taskInstance.Canceled += OnMovementCanceled;

            Debug.WriteLine(Windows.ApplicationModel.Package.Current.Id.FamilyName);

            AppServiceTriggerDetails appServiceTrigger = taskInstance.TriggerDetails as AppServiceTriggerDetails;

            if (appServiceTrigger == null)
                return;

            if (appServiceTrigger.Name.Equals("MovementService"))
            {
                if (appServiceTrigger.CallerPackageFamilyName == "85c8e45f-76d7-480b-89fe-9d4bcb4f1df9_53fac5cen2094")
                    _appServiceConnectionDisplay = appServiceTrigger.AppServiceConnection;

                _appServiceConnection = appServiceTrigger.AppServiceConnection;
                _appServiceConnection.RequestReceived += OnConnectionRequestReceived;
                _appServiceConnection.ServiceClosed += OnConnectionClosed;


                if (_nanoDlp == null)
                {
                    _nanoDlp = new NanoDlp(new StepperMotor(1.8, Microsteps.Eighth), _appServiceConnection);
                    _nanoDlp.OnStatesChanged += OnControllerStateChanged;
                }
            }
            else
            {
                _deferral.Complete();
            }
        }

        private void OnConnectionClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            NotifyClient(sender, "OnConnectionClosed");
            Debug.WriteLine($"Service closed. Status={args.Status.ToString()}");
        }

        private void OnConnectionRequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            NotifyClient(sender, "OnConnectionRequestReceived");

            var messageDeferral = args.GetDeferral();
            try
            {
                var message = args.Request.Message;

                if (!message.ContainsKey("method"))
                    return;

                var method = message["method"].ToString();

                switch (method)
                {
                    case "configure":
                        var config = new ConfigDTO()
                        {
                            MmPerRevolution = Convert.ToDouble(message["mm_per_revolution"]),
                            Angle = Convert.ToDouble(message["motor_angle"]),
                            Microsteps = Convert.ToInt32(message["microsteps"])
                        };

                        _nanoDlp.Configure(config);
                        return;
                    case "move_to_home":
                    case "move":
                        Move(message);
                        return;
                    case "send_file":
                        SendFileToView(_appServiceConnectionDisplay, message["file"].ToString());
                        return;
                    case "start_print":
                        SendStartCommandToView(_appServiceConnectionDisplay);
                        return;
                }

                if (!message.ContainsKey("mms") && !message.ContainsKey("home"))
                {
                    return;
                }

                var distance = 1D;
                if (message.ContainsKey("distance"))
                    distance = Convert.ToDouble(message["distance"]);

                var milimeterPerSecond = 1D;
                if (message.ContainsKey("mms"))
                    milimeterPerSecond = Convert.ToDouble(message["mms"]);

                var direction = 1;
                if (message.ContainsKey("direction"))
                    direction = (int)message["direction"];

                if (message.ContainsKey("home"))
                    _nanoDlp.MoveToHome(milimeterPerSecond, direction);
                else
                    _nanoDlp.Move(distance, milimeterPerSecond, direction, false);

            }
            catch (Exception ex)
            {
                NotifyClient(sender, ex.StackTrace);
                Debug.WriteLine(ex.Message);
            }
            finally
            {
                messageDeferral.Complete();
            }
        }

        private void Move(ValueSet message)
        {
            var distance = 10D;
            if (message.ContainsKey("length"))
                distance = Convert.ToDouble(message["length"]);

            var milimeterPerSecond = 5D;
            if (message.ContainsKey("speed"))
                milimeterPerSecond = Convert.ToDouble(message["speed"]);

            var direction = 1;
            if (message.ContainsKey("direction"))
                direction = (int)message["direction"];

            if (message["method"].ToString().Trim() == "move_to_home")
                _nanoDlp.MoveToHome(milimeterPerSecond, direction);
            else
                _nanoDlp.Move(distance, milimeterPerSecond, direction, false);
        }

        private async void SendStartCommandToView(AppServiceConnection appServiceConnection)
        {
            try
            {
                await appServiceConnection?.SendMessageAsync(new ValueSet() { { "method", "start_print" } });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private async void SendFileToView(AppServiceConnection appServiceConnection, string compressedFile)
        {
            try
            {
                await appServiceConnection?.SendMessageAsync(new ValueSet() { { "method", "load_file" },{ "file", compressedFile } });
            }
            catch
            {
            }
        }

        private void OnMovementCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            NotifyClient(_appServiceConnection, "OnMovementCanceled");

            if (_deferral == null)
                return;

            _deferral.Complete();
            _deferral = null;
        }

        private void OnControllerStateChanged(object sender, string message, AppServiceConnection appServiceConnection)
        {
            NotifyClient(appServiceConnection, message);
        }

        private async void NotifyClient(AppServiceConnection appServiceConnection, string message)
        {
            try
            {
                await appServiceConnection?.SendMessageAsync(new ValueSet() { { "Method", message } });
            }
            catch
            {
            }
        }
    }
}
