namespace Moow {
    public interface IEvent {
        string eventName { get; }
    };

    public class Event<TEventData> : IEvent {
        private string _eventName;
        private TEventData _data;
        private bool _stopPropagate;

        public Event(string eventName, TEventData data) {
            _eventName = eventName;
            _data = data;
        }

        public string eventName {
            get => _eventName;
            set => _eventName = value;
        }

        public TEventData data {
            get => _data;
            set => _data = value;
        }

        public bool stopPropagate {
            get => _stopPropagate;
            set => _stopPropagate = value;
        }

        public override string ToString() {
            return "Event with type of data " + typeof(TEventData).Name;
        }
    }

    public class Event<TEventData1, TEventData2> : IEvent {
        private string _eventName;
        private TEventData1 _data1;
        private TEventData2 _data2;
        private bool _stopPropagate;

        public Event(string eventName, TEventData1 data1, TEventData2 data2) {
            _eventName = eventName;
            _data1 = data1;
            _data2 = data2;
        }

        public string eventName {
            get => _eventName;
            set => _eventName = value;
        }

        public TEventData1 data1 {
            get => _data1;
            set => _data1 = value;
        }

        public TEventData2 data2 {
            get => _data2;
            set => _data2 = value;
        }

        public bool stopPropagate {
            get => _stopPropagate;
            set => _stopPropagate = value;
        }

        public override string ToString() {
            return "Event with type of data " + typeof(TEventData1).Name;
        }
    }
}