using Windows.ApplicationModel.AppService;

namespace MovementService.Events
{
    public delegate void OnValueChanged(object sender, string message, AppServiceConnection appServiceConnection);
}
