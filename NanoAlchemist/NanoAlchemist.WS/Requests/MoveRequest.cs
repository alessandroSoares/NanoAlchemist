namespace NanoAlchemist.WS.Requests
{
    public sealed class MoveRequest
    {
        public double Length { get; set; }
        public int Direction { get; set; }
        public double Speed { get; set; }
    }
}
