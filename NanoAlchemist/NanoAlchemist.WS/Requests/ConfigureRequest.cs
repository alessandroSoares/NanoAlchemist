namespace NanoAlchemist.WS.Requests
{
    public sealed class ConfigureRequest
    {
        public double MmPerRevolution { get; set; }
        public double MotorAngle { get; set; }
        public int MicroSteps { get; set; }
    }
}
