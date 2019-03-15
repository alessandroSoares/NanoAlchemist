using System;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.AppService;
using IoTUtilities;
using System.Threading.Tasks;
using Windows.Foundation.Collections;
using Newtonsoft.Json;
using NanoAlchemist.WS.Requests;
using System.Diagnostics;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace NanoAlchemist.WS
{
    public sealed class StartupTask : IBackgroundTask
    {
        private AppServiceConnection _connection;
        private BackgroundTaskDeferral _deferral;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();
            var server = new SimpleWebServer();

            server.Get("/", async (req, res) =>
            {
                await res.SendFileContentAsync("Server Running!!!!");
            });

            server.Post("/", async (req, res) =>
            {
                await res.SendFileContentAsync("Server Running!!!!");
            });

            server.Get("/connect", async (req, res) =>
            {
                _connection = new AppServiceConnection
                {
                    AppServiceName = "MovementService",
                    PackageFamilyName = "MovementService-uwp_53fac5cen2094"
                };

                var status = await _connection.OpenAsync();
                if (status == AppServiceConnectionStatus.Success)
                {
                    await res.SendStatusAsync(200);
                }
                else
                {
                    await res.SendStatusAsync(500);
                }
            });

            server.Post("/configure", async (req, res) =>
            {
                try
                {
                    var configureRequest = JsonConvert.DeserializeObject<ConfigureRequest>(req.GetValue(""));
                    var success = await Configure(configureRequest);
                    if (success)
                        await res.SendStatusAsync(200);
                    else
                        await res.SendStatusAsync(500);
                }
                catch (Exception ex)
                {
                    await res.SendFileContentAsync(ex.Message);
                }
            });

            server.Post("/moveToHome", async (req, res) =>
            {
                try
                {
                    var request = JsonConvert.DeserializeObject<MoveToHomeRequest>(req.GetValue(""));
                    var success = await MoveToHome(request);

                    if (success)
                        await res.SendStatusAsync(200);
                    else
                        await res.SendStatusAsync(500);
                }
                catch (Exception ex)
                {
                    await res.SendFileContentAsync(ex.Message);
                }
            });

            server.Post("/move", async (req, res) =>
            {
                try
                {
                    var request = JsonConvert.DeserializeObject<MoveRequest>(req.GetValue(""));
                    var success = await Move(request);

                    if (success)
                        await res.SendStatusAsync(200);
                    else
                        await res.SendStatusAsync(500);
                }
                catch (Exception ex)
                {
                    await res.SendFileContentAsync(ex.Message);
                }
            });

            server.Post("/sendFile", async (req, res) =>
            {
                try
                {
                    var request = JsonConvert.DeserializeObject<SendFileRequest>(req.GetValue(""));
                    var success = await SendFile(request);

                    if (success)
                        await res.SendStatusAsync(200);
                    else
                        await res.SendStatusAsync(500);
                }
                catch (Exception ex)
                {
                    await res.SendFileContentAsync(ex.Message);
                }
            });

            server.Post("/startPrint", async (req, res) =>
            {
                try
                {
                    var success = await StartPrint();

                    if (success)
                        await res.SendStatusAsync(200);
                    else
                        await res.SendStatusAsync(500);
                }
                catch (Exception ex)
                {
                    await res.SendFileContentAsync(ex.Message);
                }
            });

            try
            {
                server.Listen(8024);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        private async Task<bool> Configure(ConfigureRequest configureRequest)
        {
            var message = new ValueSet
            {
                ["method"] = "configure",
                ["mm_per_revolution"] = configureRequest.MmPerRevolution,
                ["motor_angle"] = configureRequest.MotorAngle,
                ["microsteps"] = configureRequest.MicroSteps
            };

            var response = await _connection.SendMessageAsync(message);
            return response.Status == AppServiceResponseStatus.Success;
        }

        private async Task<bool> MoveToHome(MoveToHomeRequest moveToHomeRequest)
        {
            var message = new ValueSet
            {
                ["method"] = "move_to_home",
                ["direction"] = moveToHomeRequest.Direction,
                ["speed"] = moveToHomeRequest.Speed
            };

            var response = await _connection.SendMessageAsync(message);
            return response.Status == AppServiceResponseStatus.Success;
        }

        private async Task<bool> Move(MoveRequest moveRequest)
        {
            var message = new ValueSet
            {
                ["method"] = "move",
                ["length"] = moveRequest.Length,
                ["direction"] = moveRequest.Direction,
                ["speed"] = moveRequest.Speed
            };

            var response = await _connection.SendMessageAsync(message);
            return response.Status == AppServiceResponseStatus.Success;
        }

        private async Task<bool> SendFile(SendFileRequest sendFileRequest)
        {
            var message = new ValueSet
            {
                ["method"] = "send_file",
                ["file"] = sendFileRequest.CompressedFile
            };

            var response = await _connection.SendMessageAsync(message);
            return response.Status == AppServiceResponseStatus.Success;
        }

        private async Task<bool> StartPrint()
        {
            var message = new ValueSet
            {
                ["method"] = "start_print"
            };

            var response = await _connection.SendMessageAsync(message);
            return response.Status == AppServiceResponseStatus.Success;
        }
    }
}
