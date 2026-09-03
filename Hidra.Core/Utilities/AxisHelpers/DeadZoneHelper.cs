using System;

namespace Hidra.Core.Utilities.AxisHelpers
{
    public class DeadZoneHelper
    {
        private double _scaleFactor;
        private double _deadzoneCutoff;
        
        public int Percentage
        {
            get => _percentage;
            set
            {
                if (value < 0)
                {
                    _percentage = 0;
                }
                else if (value > 100)
                {
                    _percentage = 100;
                }
                else
                {
                    _percentage = value;
                }
                
                PrecalculateValues();
            }
        }
        private int _percentage;

        public DeadZoneHelper()
        {
            PrecalculateValues();
        }

        private void PrecalculateValues()
        {
            if (_percentage == 0)
            {
                _deadzoneCutoff = 0;
                _scaleFactor = 1.0;
            }
            else if (_percentage == 100)
            {
                // At the maximum cutoff, everything is inside the dead zone; computing this the
                // same way as the general case below would divide by zero (AxisMaxValue -
                // AxisMaxValue) and turn scaleFactor into Infinity, which then produces NaN/wrong
                // output for max-magnitude input instead of the 0 that 100% dead zone should mean.
                _deadzoneCutoff = Constants.AxisMaxValue;
                _scaleFactor = 0;
            }
            else
            {
                _deadzoneCutoff = Constants.AxisMaxValue * (_percentage * 0.01);
                _scaleFactor = Constants.AxisMaxValue / (Constants.AxisMaxValue - _deadzoneCutoff);
            }
        }

        public short ApplyRangeDeadZone(short value)
        {
            var wideVal = Functions.WideAbs(value);
            if (wideVal < Math.Round(_deadzoneCutoff))
            {
                return 0;
            }

            var sign = Math.Sign(value);
            var adjustedValue = (wideVal - _deadzoneCutoff) * _scaleFactor;
            var newValue = (int) Math.Round(adjustedValue * sign);
            if (newValue < -32768) newValue = -32768;
            return (short) newValue;
        }
    }
}
