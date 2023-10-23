using System;

namespace Ifak.Fast.Mediator.Util {

    public class AlarmManager {

        private Timestamp? timeOfFirstWarning;
        private Timestamp? timeOfLastWarning;
        private bool activated = false;

        private readonly Duration activationDuration;
        private readonly Duration deactivationDuration;

        public AlarmManager(Duration activationDuration, Duration deactivationDuration) {
            this.activationDuration = activationDuration;
            this.deactivationDuration = deactivationDuration;
        }

        public AlarmManager(Duration activationDuration) {
            this.activationDuration = activationDuration;
            this.deactivationDuration = Duration.FromMinutes(0);
        }

        public bool OnWarning(string msg) {
            Timestamp Now = Timestamp.Now;
            bool first = timeOfFirstWarning == null;
            timeOfLastWarning = Now;
            if (first) {
                timeOfFirstWarning = Now;
                Console.WriteLine(msg);
            }
            Duration diff = Now - timeOfFirstWarning!.Value;
            bool active = diff >= activationDuration;

            if (active && !activated) {
                activated = true;
                return true;
            }

            return false;
        }

        public bool IsActivated => activated;

        public bool ReturnToNormal() {
            return ReturnToNormal(out _);
        }

        public bool ReturnToNormal(out Timestamp timeOfLastWarn) {
            activated = false;
            timeOfLastWarn = timeOfLastWarning ?? Timestamp.Empty;
            if (!timeOfLastWarning.HasValue) return false;
            var t = Timestamp.Now - deactivationDuration;
            if (timeOfLastWarning.Value > t) return false;
            timeOfFirstWarning = null;
            timeOfLastWarning = null;
            return true;
        }
    }
}
