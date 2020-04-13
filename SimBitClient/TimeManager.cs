using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimBitClient
{
    class TimeManager
    {
        private DateTime StartTime;

        public TimeManager()
        {
            Reset();
        }

        public double GetElapsedSeconds()
        {
           return DateTime.Now.Subtract(StartTime).TotalMilliseconds / 1000.0;
        }

        public void Reset()
        {
            StartTime = DateTime.Now;
        }
    }
}
