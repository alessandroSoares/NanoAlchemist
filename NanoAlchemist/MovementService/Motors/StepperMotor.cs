namespace MovementService.Motors
{
    public sealed class StepperMotor
    {
        public double Angle { get; set; }
        public Microsteps Microstep { get; set; }
        public double MinPulseDuration { get; private set; } = 0.4;
        public double StepsPerRevolution
        {
            get { return GetStepsPerRevolution(); }
        }

        public StepperMotor(double angle, Microsteps microStep)
        {
            Angle = angle;
            Microstep = microStep;
        }

        public double GetStepsPerRevolution()
        {
            double stepsPerRevolution = 360 / Angle;

            switch (Microstep)
            {
                case Microsteps.Half:
                    stepsPerRevolution *= 2;
                    break;
                case Microsteps.Fourth:
                    stepsPerRevolution *= 4;
                    break;
                case Microsteps.Eighth:
                    stepsPerRevolution *= 8;
                    break;
                case Microsteps.Sixteenth:
                    stepsPerRevolution *= 16;
                    break;
            }

            return stepsPerRevolution;
        }
    }
}
