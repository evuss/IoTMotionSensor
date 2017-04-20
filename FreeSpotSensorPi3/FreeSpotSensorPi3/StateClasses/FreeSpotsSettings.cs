namespace FreeSpotSensorPi3.StateClasses {
    class FreeSpotsSettings {
        public enum mode {
            RUNTIME1 = 0,
            RUNTIME2 = 1,
            DEBUG1 = 2,
            DEBUG2 = 3

        }
        public int timerCheckIntervall;        // How foten the PIR Sensor is read.
        public int timerKeepAliveIntervall;  // Maximum Seconds until a "keep alive" message is sent.
        public int timerStateOSmoothing;       // Minimum time with ONLY new PIR state Occupied, before CurrentState is changed to Occupied. 
        public int timerStateFSmoothing;       // Minimum time with ONLY new PIR state Free, before CurrentState is changed to Free.

        public FreeSpotsSettings(mode settings) {
            if(settings == mode.RUNTIME1) { // Primary runtime configuration
                timerCheckIntervall = 30;           // How often the PIR Sensor is read.
                timerKeepAliveIntervall = 1800;     // Maximum Seconds until a "keep alive" message is sent.
                timerStateOSmoothing = 25;         // Minimum time with ONLY new PIR state Occupied, before CurrentState is changed to Occupied. 
                timerStateFSmoothing = 300;       // Minimum time with ONLY new PIR state Free, before CurrentState is changed to Free.
            }
            else if(settings == mode.RUNTIME2) { // Primary runtime configuration
                timerCheckIntervall = 30;           // How often the PIR Sensor is read.
                timerKeepAliveIntervall = 1800;     // Maximum Seconds until a "keep alive" message is sent.
                timerStateOSmoothing = 0;         // Minimum time with ONLY new PIR state Occupied, before CurrentState is changed to Occupied. 
                timerStateFSmoothing = 300;       // Minimum time with ONLY new PIR state Free, before CurrentState is changed to Free.
            }
            else if (settings == mode.DEBUG1) { // Debug: Imediately show sensor detection (Ie good for identify PIRs own timer setting
                timerCheckIntervall = 5;           // How often the PIR Sensor is read.
                timerKeepAliveIntervall = 600;     // Maximum Seconds until a "keep alive" message is sent.
                timerStateOSmoothing = 0;         // Minimum time with ONLY new PIR state Occupied, before CurrentState is changed to Occupied. 
                timerStateFSmoothing = 0;       // Minimum time with ONLY new PIR state Free, before CurrentState is changed to Free.
            }
            else if (settings == mode.DEBUG2) { // Debug: Settings for quicker feedback.
                timerCheckIntervall = 30;           // How often the PIR Sensor is read.
                timerKeepAliveIntervall = 900;     // Maximum Seconds until a "keep alive" message is sent.
                timerStateOSmoothing = 25;         // Minimum time with ONLY new PIR state Occupied, before CurrentState is changed to Occupied.  
                timerStateFSmoothing = 300;       // Minimum time with ONLY new PIR state Free, before CurrentState is changed to Free.
            }
        }
    }
}
