namespace FreeSpotSensorPi3.StateClasses {
    class MsgBody {
        public string ID = "";    // Sensor ID / MAC
        public string Ip = "";
        public string Status = "";
        public string TS = "";
        public string Change = "";

        public MsgBody() {

        }

        public MsgBody(MsgBody msg) {
            setMsgBody(msg);
        }

        public void setMsgBody(MsgBody msg) {
            this.ID = msg.ID;
            this.Ip = msg.Ip;
            this.Status = msg.Status;
            this.TS = msg.TS;
            this.Change = msg.Change;
        }
    }
}
