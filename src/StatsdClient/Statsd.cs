using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace StatsdClient
{
    public interface IAllowsSampleRate { }

    public interface IAllowsDouble { }
    public interface IAllowsInteger { }
    public interface IAllowsString { }

    public interface IAllowsDelta { }
    public class Statsd : IStatsd
    {
        private readonly object _commandCollectionLock = new object();

        private IStopWatchFactory StopwatchFactory { get; set; }
        private IMetricsSender Udp { get; set; }
        private IRandomGenerator RandomGenerator { get; set; }

        private readonly string _prefix;

        public List<string> Commands { get; private set; }

        public class Counting : IAllowsSampleRate, IAllowsInteger { }
        public class Timing : IAllowsSampleRate, IAllowsInteger { }
        public class Gauge : IAllowsDouble, IAllowsDelta { }
        public class Histogram : IAllowsInteger { }
        public class Meter : IAllowsInteger { }
        public class Set : IAllowsString { }

        private readonly Dictionary<Type, string> _commandToUnit = new Dictionary<Type, string>
                                                                       {
                                                                           {typeof (Counting), "c"},
                                                                           {typeof (Timing), "ms"},
                                                                           {typeof (Gauge), "g"},
                                                                           {typeof (Histogram), "h"},
                                                                           {typeof (Meter), "m"},
                                                                           {typeof (Set), "s"}
                                                                       };

        public Statsd(IMetricsSender udp, IRandomGenerator randomGenerator, IStopWatchFactory stopwatchFactory, string prefix)
        {
            Commands = new List<string>();
            StopwatchFactory = stopwatchFactory;
            Udp = udp;
            RandomGenerator = randomGenerator;
            _prefix = prefix;
        }

        public Statsd(IMetricsSender udp, IRandomGenerator randomGenerator, IStopWatchFactory stopwatchFactory)
            : this(udp, randomGenerator, stopwatchFactory, string.Empty) { }

        public Statsd(IMetricsSender udp, string prefix)
            : this(udp, new RandomGenerator(), new StopWatchFactory(), prefix) { }

        public Statsd(IMetricsSender udp)
            : this(udp, "") { }


        public void Send<TCommandType>(string name, int value) where TCommandType : IAllowsInteger
        {
            Commands = new List<string>
            {
                GetCommand(name, value.ToString(CultureInfo.InvariantCulture), _commandToUnit[typeof(TCommandType)], 1)
            };
            Send();
        }
        public void Send<TCommandType>(string name, double value) where TCommandType : IAllowsDouble
        {
            Commands = new List<string>
            {
                GetCommand(name, string.Format(CultureInfo.InvariantCulture,"{0:F15}", value), 
                _commandToUnit[typeof(TCommandType)], 1)
            };
            Send();
        }
        public void Send<TCommandType>(string name, string value) where TCommandType : IAllowsString
        {
            Commands = new List<string>
            {
                GetCommand(name, value.ToString(CultureInfo.InvariantCulture), _commandToUnit[typeof(TCommandType)], 1)
            };
            Send();
        }
        public void Add<TCommandType>(string name, string value) where TCommandType : IAllowsString
        {
            ThreadSafeAddCommand(GetCommand(name, value.ToString(CultureInfo.InvariantCulture),
                _commandToUnit[typeof(TCommandType)], 1));
        }
        public void Add<TCommandType>(string name, int value) where TCommandType : IAllowsInteger
        {
            ThreadSafeAddCommand(GetCommand(name, value.ToString(CultureInfo.InvariantCulture), 
                _commandToUnit[typeof (TCommandType)], 1));
        }

        public void Add<TCommandType>(string name, double value) where TCommandType : IAllowsDouble
        {
            ThreadSafeAddCommand(GetCommand(name, string.Format(CultureInfo.InvariantCulture,"{0:F15}", value), 
                _commandToUnit[typeof(TCommandType)], 1));
        }
        public void Add<TCommandType>(string name, double value, bool isDelta) 
            where TCommandType : IAllowsDouble, IAllowsDelta
        {
            var prefix = GetDeltaPrefix(value, isDelta);
            ThreadSafeAddCommand(GetCommand(name, string.Format(CultureInfo.InvariantCulture, 
                "{0}{1:F15}", prefix, value), _commandToUnit[typeof(TCommandType)], 1));
        }
        public void Send<TCommandType>(string name, int value, double sampleRate) 
            where TCommandType : IAllowsInteger, IAllowsSampleRate
        {
            if (RandomGenerator.ShouldSend(sampleRate))
            {
                Commands = new List<string>
                {
                    GetCommand(name, value.ToString(CultureInfo.InvariantCulture), 
                    _commandToUnit[typeof(TCommandType)], sampleRate)
                };
                Send();
            }
        }
        public void Send<TCommandType>(string name, double value, bool isDelta)
            where TCommandType : IAllowsDouble, IAllowsDelta
        {
            var prefix = GetDeltaPrefix(value, isDelta);
            Commands = new List<string>
            {
                GetCommand(name, prefix + value.ToString(CultureInfo.InvariantCulture),
                    _commandToUnit[typeof(TCommandType)], 1)
            };
            Send();
        }

        public void Add<TCommandType>(string name, int value, double sampleRate) 
            where TCommandType : IAllowsInteger, IAllowsSampleRate
        {
            if (RandomGenerator.ShouldSend(sampleRate))
            {
                Commands.Add(GetCommand(name, value.ToString(CultureInfo.InvariantCulture), 
                    _commandToUnit[typeof(TCommandType)], sampleRate));
            }
        }

        private void ThreadSafeAddCommand(string command)
        {
            lock (_commandCollectionLock)
            {
                Commands.Add(command);
            }
        }
        private string GetDeltaPrefix(double value, bool isDelta)
        {
            return isDelta ? (value >= 0 ? "+" : "-") : string.Empty;
        }
        public void Send()
        {
            try
            {
                Udp.Send(string.Join("\n", Commands.ToArray()));
                Commands = new List<string>();
            }
            catch(Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }

        private string GetCommand(string name, string value, string unit, double sampleRate)
        {
            var format = sampleRate == 1 ? "{0}:{1}|{2}" : "{0}:{1}|{2}|@{3}";
            return string.Format(CultureInfo.InvariantCulture, format, _prefix + name, value, unit, sampleRate);
        }

        public void Add(Action actionToTime, string statName, double sampleRate=1)
        {
            var stopwatch = StopwatchFactory.Get();

            try
            {
                stopwatch.Start();
                actionToTime();
            }
            finally
            {
                stopwatch.Stop();
                if (RandomGenerator.ShouldSend(sampleRate))
                {
                    Add<Timing>(statName, stopwatch.ElapsedMilliseconds());
                }
            }
        }

        public void Send(Action actionToTime, string statName, double sampleRate=1)
        {
            var stopwatch = StopwatchFactory.Get();

            try
            {
                stopwatch.Start();
                actionToTime();
            }
            finally
            {
                stopwatch.Stop();
                if (RandomGenerator.ShouldSend(sampleRate))
                {
                    Send<Timing>(statName, stopwatch.ElapsedMilliseconds());
                }
            }
        }
    }
}
