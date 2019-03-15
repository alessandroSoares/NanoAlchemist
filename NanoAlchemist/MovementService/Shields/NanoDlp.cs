using MovementService.DTOs;
using MovementService.Events;
using MovementService.Motors;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Devices.Gpio;

namespace MovementService.Shields
{
    public sealed class NanoDlp
    {
        #region [ Fixed Pins ]
        private const int STEP_PIN = 25;
        private const int ENABLE_PIN = 22;
        private const int DIRECTION_PIN = 23;
        private const int FAULT_PIN = 6;
        private const int TOP_LIMIT_PIN = 18;
        private const int BOTTOM_LIMIT_PIN = 24;
        #endregion

        #region [ Config ]
        private double MM_PER_REVOLUTION { get; set; } = 2;
        #endregion

        #region [ Variables ]         
        private bool _inMoving = false;
        private bool _pinHigh = false;
        private bool _initialized = false;
        private bool _canMoveClockwise = false;
        private bool _canMoveCounterclockwise = false;
        private int _direction = 1; // 1 - Clockwise, -1 - Counterclockwise
        private GpioPin _stepPin;
        private GpioPin _enablepPin;
        private GpioPin _directionPin;
        private GpioPin _faultPin;
        private GpioPin _topLimitPin;
        private GpioPin _bottomLimitPin;
        private readonly StepperMotor _motor;
        private readonly AppServiceConnection _serviceConnection;
        #endregion

        #region [ Events ]
        public event OnValueChanged OnStatesChanged;
        #endregion

        public NanoDlp(StepperMotor motor, AppServiceConnection serviceConnection)
        {
            ConfigurePins();
            SetInitialPinState();

            _motor = motor;
            _serviceConnection = serviceConnection;
        }

        private void ConfigurePins()
        {
            var gpio = GpioController.GetDefault();

            if (gpio == null)
            {
                _stepPin = null;
                _enablepPin = null;
                _directionPin = null;
                return;
            }

            try
            {
                _stepPin = gpio.OpenPin(STEP_PIN);
                _enablepPin = gpio.OpenPin(ENABLE_PIN);
                _directionPin = gpio.OpenPin(DIRECTION_PIN);
                _faultPin = gpio.OpenPin(FAULT_PIN);
                _topLimitPin = gpio.OpenPin(TOP_LIMIT_PIN);
                _bottomLimitPin = gpio.OpenPin(BOTTOM_LIMIT_PIN);

                _stepPin.Write(GpioPinValue.Low);
                _enablepPin.Write(GpioPinValue.Low);
                _directionPin.Write(GpioPinValue.Low);

                _stepPin.SetDriveMode(GpioPinDriveMode.Output);
                _enablepPin.SetDriveMode(GpioPinDriveMode.Output);
                _directionPin.SetDriveMode(GpioPinDriveMode.Output);

                _faultPin.SetDriveMode(GpioPinDriveMode.Input);
                _topLimitPin.SetDriveMode(GpioPinDriveMode.Input);
                _bottomLimitPin.SetDriveMode(GpioPinDriveMode.Input);

                _faultPin.DebounceTimeout = TimeSpan.FromMilliseconds(50);
                _topLimitPin.DebounceTimeout = TimeSpan.FromMilliseconds(50);
                _bottomLimitPin.DebounceTimeout = TimeSpan.FromMilliseconds(50);
                _faultPin.ValueChanged += OnFaultChanged;
                _topLimitPin.ValueChanged += OnTopLimitChanged;
                _bottomLimitPin.ValueChanged += OnBottomLimitChanged;

                _initialized = true;
                _canMoveClockwise = true;
                _canMoveCounterclockwise = true;
            }
            catch
            {
            }
        }

        private void SetInitialPinState()
        {
            if (!_initialized)
                return;

            var topLimitValue = _topLimitPin.Read();
            var bottomLimitValue = _bottomLimitPin.Read();
            var directionValue = _directionPin.Read();

            _canMoveClockwise = topLimitValue == GpioPinValue.High;
            _canMoveCounterclockwise = bottomLimitValue == GpioPinValue.High;
            _direction = (directionValue == GpioPinValue.High) ? 1 : -1;
        }

        internal void Configure(ConfigDTO config)
        {
            _motor.Angle = config.Angle;
            _motor.Microstep = IntMicrostepToEnum(config.Microsteps);
            MM_PER_REVOLUTION = config.MmPerRevolution;
        }

        private Microsteps IntMicrostepToEnum(int microSteps)
        {
            switch (microSteps)
            {
                case 1: return Microsteps.Full;
                case 2: return Microsteps.Half;
                case 4: return Microsteps.Fourth;
                case 8: return Microsteps.Eighth;
                case 16: return Microsteps.Sixteenth;
                default: return Microsteps.Full;
            }
        }

        private async void OnBottomLimitChanged(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            await Task.Run(() =>
            {
                _canMoveCounterclockwise = args.Edge == GpioPinEdge.RisingEdge;
                NotifyChanges(string.Format("Bottom Limit:{0}", !_canMoveCounterclockwise));
            });
        }

        private async void OnTopLimitChanged(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            await Task.Run(() =>
            {
                _canMoveClockwise = args.Edge == GpioPinEdge.RisingEdge;
                NotifyChanges(string.Format("Top Limit:{0}", !_canMoveClockwise));
            });
        }

        private async void OnFaultChanged(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            await Task.Run(() =>
            {
                _canMoveCounterclockwise = args.Edge == GpioPinEdge.RisingEdge;
                _canMoveClockwise = args.Edge == GpioPinEdge.RisingEdge;
                NotifyChanges(string.Format("Fault Detect:{0}", args.Edge == GpioPinEdge.FallingEdge));
            });
        }

        public void MoveToHome(double speed, int direction)
        {
            Move(500, speed, direction, false);
            Move(10, speed, (-1 * direction), true);
            Move(10, speed, direction, false);
        }

        /// <summary>
        /// Move a motor
        /// </summary>
        /// <param name="length">Length in mm</param>
        /// <param name="speed">Speed in Milimeter per seconds</param>
        /// <param name="direction">Direction of rotation (1-Clockwise, 1-Counterclockwise)</param>
        /// <returns></returns>
        public void Move(double length, double speed, int direction, bool endStopTouched)
        {

            if (_inMoving && !endStopTouched)
                return;

            if (!_initialized || speed <= 0)
                return;

            SetDirection(direction);

            if (!CanMove())            
                return;            

            double milimeterPerStep = (MM_PER_REVOLUTION / _motor.StepsPerRevolution);
            double steps = length / milimeterPerStep;
            double revolutionsPerSecond = speed / MM_PER_REVOLUTION;
            double stepsPerMilisecond = (revolutionsPerSecond * _motor.StepsPerRevolution) / 1000;

            double delayInMilliseconds = 1 / stepsPerMilisecond;

            double stepDelay = TimeSpan.TicksPerMillisecond * delayInMilliseconds;

            long lastStepTime = 0;

            try
            {
                var pulses = steps * 2;
                _inMoving = true;
                var timer = Stopwatch.StartNew();
                lock (timer)
                {
                    while (pulses > 0)
                    {

                        if (timer.ElapsedTicks - stepDelay <= lastStepTime)
                            continue;

                        if (!CanMove())
                            break;

                        MoveOneStep();
                        pulses--;
                        lastStepTime = timer.ElapsedTicks;
                    }
                }
            }
            finally
            {
                _inMoving = false;
            }
        }

        private void MoveOneStep()
        {
            _stepPin.Write(_pinHigh ? GpioPinValue.Low : GpioPinValue.High);
            _pinHigh = !_pinHigh;
        }

        private void SetDirection(int direction)
        {
            if (direction == _direction)
                return;

            _directionPin.Write(direction == -1 ? GpioPinValue.Low : GpioPinValue.High);

            _direction = direction;
        }

        private bool CanMove()
        {
            if (_direction == -1 &&
                !_canMoveClockwise)
                return false;

            if (_direction == 1 &&
                !_canMoveCounterclockwise)
                return false;

            return true;
        }

        private async void NotifyChanges(string message)
        {
            await Task.Run(() => { OnStatesChanged?.Invoke(this, message, _serviceConnection); });
        }
    }
}
